using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Movement;
using RTSEngine.Animation;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Determinism;
using RTSEngine.Audio;
using RTSEngine.Terrain;
using RTSEngine.UnitExtension;
using UnityEngine.Serialization;

namespace RTSEngine.EntityComponent
{
    public class UnitMovement : FactionEntityTargetComponent<IEntity>, IMovementComponent
    {
        #region Class Attributes
        /*
         * Action types and their parameters:
         * setPosition: target.position as Vector3 position to set the unit at
         * */
        public enum ActionType : byte { setPosition, toggleMovementRotation }

        public IUnit Unit { private set; get; }

        [SerializeField, Tooltip("Pick the terrain are types that the unit is able to move within. Leave empty to allow all terrain area types registered in the Terrain Manager.")]
        private TerrainAreaType[] movableTerrainAreas = new TerrainAreaType[0];
        public IReadOnlyList<TerrainAreaType> TerrainAreas => movableTerrainAreas;
        public TerrainAreaMask AreasMask { private set; get; }

        [SerializeField, Tooltip("Movement formation for this unit type.")]
        private MovementFormationSelector formation = new MovementFormationSelector { };
        /// <summary>
        /// Gets the MovementFormation struct that defines the movement formation for the unit.
        /// </summary>
        public MovementFormationSelector Formation => formation;

        [SerializeField, Tooltip("The lower the Movement Priority value is, the more units of this type will be prioritized when generating the path destinations.")]
        private int movementPriority = 0;
        /// <summary>
        /// The lower the Movement Priority value is, the more units of this type will be prioritized when generating the path destinations.
        /// This can be used to have units pick path destinations in front of other units with higher Movement Priority values in the case of row-based formation movement.
        /// </summary>
        public int MovementPriority => movementPriority;

        [SerializeField, HideInInspector]
        private bool isMoving;
        [SerializeField, HideInInspector]
        private bool isMovementPending;
        [SerializeField, HideInInspector]
        private Vector3 startPosition;
        public Vector3 StartPosition => HasTarget ? startPosition : Unit.transform.position;
        public override bool HasTarget => isMovementPending || isMoving;
        public override bool IsIdle => !isMoving;

        /// <summary>
        /// An instance that extends the IMovementController interface which is responsible for computing the navigation path and moving the unit.
        /// </summary>
        public IMovementController Controller { private set; get; }

        /// <summary>
        /// The current corner that the unit is moving towards in its current path.
        /// </summary>
        public Vector3 NextCorner { private set; get; }

        /// <summary>
        /// Has the unit reached its current's path destination?
        /// </summary>
        public bool DestinationReached { private set; get; }
        //public Vector3 Destination => Target.position;
        public Vector3 Destination => Controller.IsValid() ? Controller.Destination : Vector3.zero;

        [SerializeField, Tooltip("Default movement speed.")]
        private TimeModifiedFloat speed = new TimeModifiedFloat(10.0f);

        [SerializeField, Tooltip("How fast will the unit reach its movement speed?")]
        private TimeModifiedFloat acceleration = new TimeModifiedFloat(10.0f);

        [SerializeField, Tooltip("When disabled, the rotation speed will be set to 0 and rotatin will not be handled by this component.")]
        private bool movementRotationEnabled = true;

        [SerializeField, Tooltip("How fast does the unit rotate while moving?")]
        private TimeModifiedFloat mvtAngularSpeed = new TimeModifiedFloat(250.0f);

        [SerializeField, Tooltip("When disabled, the unit will have to rotate to face the next corner of the path before moving to it."), FormerlySerializedAs("canMoveRotate")]
        private bool canMoveAndRotate = true; //can the unit rotate and move at the same time? 

        [SerializeField, Tooltip("If 'Can Move Rotate' is disabled, this value represents the angle that the unit must face in each corner of the path before moving towards it.")]
        private float minMoveAngle = 40.0f; //the close this value to 0.0f, the closer must the unit face its next destination in its path to move.
        private bool facingNextCorner = false; //is the unit facing the next corner on the path regarding the min move angle value?.

        [SerializeField, Tooltip("Can the unit rotate while not moving?")]
        private bool canIdleRotate = true; //can the unit rotate when not moving?
        [SerializeField, Tooltip("Is the idle rotation smooth or instant?")]
        private bool smoothIdleRotation = true;
        [SerializeField, Tooltip("How fast does the unit rotate while attempting to face its next corner in the path or while idle? Only if the idle rotation is smooth.")]
        private TimeModifiedFloat idleAngularSpeed = new TimeModifiedFloat(2.0f);

        //rotation helper fields.
        public Quaternion NextRotationTarget { private set; get; }

        /// <summary>
        /// The IMovementTargetPositionMarker instance assigned to the unit movement that marks the position that the unit is moving towards.
        /// </summary>
        public IMovementTargetPositionMarker TargetPositionMarker { get; private set; }

        [SerializeField, Tooltip("What audio clip to loop when the unit is moving?")]
        private AudioClipFetcher mvtAudio = new AudioClipFetcher(); //Audio clip played when the unit is moving.
        [SerializeField, Tooltip("What audio clip to play when is unable to move?")]
        private AudioClipFetcher invalidMvtPathAudio = new AudioClipFetcher(); //When the movement path is invalid, this audio is played.

        // Game services
        protected ITimeModifier timeModifier { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IMovementComponent, MovementEventArgs> MovementStart;
        public event CustomEventHandler<IMovementComponent, EventArgs> MovementStop;
        public event CustomEventHandler<IMovementComponent, EventArgs> PositionSet;

        private void RaiseMovementStart(MovementEventArgs args)
        {
            var handler = MovementStart;
            handler?.Invoke(this, args);
        }
        private void RaiseMovementStop()
        {
            var handler = MovementStop;
            handler?.Invoke(this, EventArgs.Empty);
        }
        private void RaisePositionSet()
        {
            var handler = PositionSet;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        protected override void OnTargetInit()
        {
            this.Unit = Entity as IUnit;

            this.timeModifier = gameMgr.GetService<ITimeModifier>();

            this.timeModifier.ModifierUpdated += HandleTimeModifierUpdated;

            Controller = Unit.gameObject.GetComponentInChildren<IMovementController>();

            if (!logger.RequireValid(this.Unit,
              $"[{GetType().Name}] This component must be initialized with a valid instane of {typeof(IUnit).Name}!")

                || !logger.RequireValid(Controller,
                $"[{GetType().Name} - '{Unit.Code}'] A component that implements the '{typeof(IMovementController).Name}' interface must be attached to the object.")

                || !logger.RequireValid(formation.type,
                $"[{GetType().Name} - '{Unit.Code}'] The movement formation type must be assigned!")

                || !logger.RequireTrue(movableTerrainAreas.Length == 0 || movableTerrainAreas.All(area => area.IsValid()),
                $"[{GetType().Name} - '{Unit.Code}'] The field 'Movable Terrain Areas' must be either empty or have valid elements assigned!"))
                return;

            formation.Init();

            AreasMask = gameMgr.GetService<ITerrainManager>().TerrainAreasToMask(TerrainAreas);

            Controller.Init(gameMgr, this, TimeModifiedControllerData);
            TargetPositionMarker = new UnitTargetPositionMarker(gameMgr, this);

            // Movement component requires no auto-target search
            if (targetFinder.IsValid())
                targetFinder.IsActive = false;

            isMoving = false;
            isMovementPending = false;

            UpdateRotationTarget(Entity.transform.rotation);
        }

        protected override void OnTargetDisabled()
        {
            TargetPositionMarker.Toggle(enable: false);
            this.timeModifier.ModifierUpdated -= HandleTimeModifierUpdated;

            Controller.Disable();
        }
        #endregion

        #region Handling Component Upgrade
        protected override void OnComponentUpgraded(FactionEntityTargetComponent<IEntity> sourceFactionEntityTargetComponent)
        {
            // Reset animator state (to the same previous state) post upgrade
            Unit.AnimatorController.SetState(Unit.AnimatorController.CurrState);

            UnitMovement sourceMovementComp = sourceFactionEntityTargetComponent as UnitMovement;

            // Reset the rotation target as well
            UpdateRotationTarget(sourceMovementComp.NextRotationTarget);
        }
        #endregion

        #region Handling Event: Time Modifier Update
        public MovementControllerData TimeModifiedControllerData => new MovementControllerData
        {
            speed = speed.Value,
            acceleration = acceleration.Value,

            angularSpeed = movementRotationEnabled ? mvtAngularSpeed.Value : 0.0f,
            stoppingDistance = mvtMgr.StoppingDistance
        };

        private void HandleTimeModifierUpdated(ITimeModifier sender, EventArgs args)
        {
            // Update the movement time modified values to keep up with the time modifier
            Controller.Data = TimeModifiedControllerData;
        }
        #endregion

        #region Activating/Deactivating Component
        protected override void OnTargetActiveStatusUpdated()
        {
            Controller.Enabled = IsActive;
        }
        #endregion

        #region Updating Unit Movement State
        /// <summary>
        /// Handles updating the unit state whether it is in its idle or movement state.
        /// </summary>
        void FixedUpdate()
        {
            if (Unit.Health.IsDead) //if the unit is already dead
                return; //do not update movement

            if (isMoving == false)
            {
                UpdateIdleRotation();
                return;
            }

            //to sync the unit's movement with its animation state, only handle movement if the unit is in its mvt animator state.
            if (!Unit.AnimatorController.IsInMvtState == true)
                return;

            if(movementRotationEnabled)
                UpdateMovementRotation();

            if (Controller.LastSource.targetAddableUnit.IsValid() //we have an addable target
                                                                  //and it moved further away from the fetched addable position when the path was calculated and movement started.
                && !mvtMgr.IsPositionReached(Controller.LastSource.targetAddableUnitPosition, Controller.LastSource.targetAddableUnit.GetAddablePosition(Unit)))
            {
                OnHandleAddableUnitStop();
                return;
            }

            if (DestinationReached == false) //check if the unit has reached its target position or not
                if ((DestinationReached = mvtMgr.IsPositionReached(Unit.transform.position, Destination)))
                    OnHandleAddableUnitStop();
        }

        public void OnHandleAddableUnitStop()
        {
            MovementSource lastSource = Controller.LastSource;
            Stop(); //stop the unit mvt

            if (lastSource.targetAddableUnit.IsValid()) //unit is supposed to be added to this instance.
            {
                //so that the unit does not look at the IAddableUnit entity after it is added.
                Target = new TargetData<IEntity> { opPosition = Target.opPosition };
                AddableUnitData addableUnitData = lastSource.sourceTargetComponent.IsValid() && lastSource.sourceTargetComponent != Unit.CarriableUnit
                        ? new AddableUnitData(lastSource.sourceTargetComponent, playerCommand: false)
                        : Unit.CarriableUnit.GetAddableData(playerCommand: false);

                if (DestinationReached)
                    lastSource.targetAddableUnit.Add(
                        Unit,
                        addableUnitData);
                else
                    lastSource.targetAddableUnit.Move(
                        Unit,
                        addableUnitData);
            }
        }

        /// <summary>
        /// Handles updating the unit's rotation while in idle state.
        /// </summary>
        private void UpdateIdleRotation ()
        {
            if (!canIdleRotate) //can the unit rotate when idle + there's a valid rotation target
                return;

            if (Target.instance.IsValid()) //if there's a target object to look at.
                NextRotationTarget = RTSHelper.GetLookRotation(Unit.transform, Target.instance.transform.position); //keep updating the rotation target as the target object might keep changing position

            if (smoothIdleRotation)
                Unit.transform.rotation = Quaternion.Slerp(Unit.transform.rotation, NextRotationTarget, Time.deltaTime * idleAngularSpeed.Value);
            else
                Unit.transform.rotation = NextRotationTarget;
        }

        /// <summary>
        /// Deactivates the movement controller and sets the unit's rotation target to the next corner in the path.
        /// </summary>
        private void EnableMovementHaltingRotation ()
        {
            facingNextCorner = false; //to trigger checking for correct rotation properties
            Controller.IsActive = false; //stop handling rotation using the movement controller

            NextCorner = Controller.NextPathTarget; //assign new corner in path
            //set the rotation target to the next corner.
            NextRotationTarget = RTSHelper.GetLookRotation(Unit.transform, NextCorner);
        }

        /// <summary>
        /// Handles updating the unit's rotation while it is in its movement state.
        /// This mainly handles blocking the movement controller and rotating the unit if it is required to rotate toward its target before moving.
        /// </summary>
        private void UpdateMovementRotation()
        {
            if (canMoveAndRotate) //can move and rotate? do not proceed.
                return;

            if (NextCorner != Controller.NextPathTarget) //if the next corner/destination on path has been updated
                EnableMovementHaltingRotation();

            if (facingNextCorner) //facing next corner? we good
                return;

            if (Controller.IsActive) //stop movement it if it's not already stopped
                Controller.IsActive = false;

            //keep checking if the angle between the unit and its next destination
            Vector3 IdleLookAt = NextCorner - Unit.transform.position;
            IdleLookAt.y = 0.0f;

            //as long as the angle is still over the min allowed movement angle, then do not proceed to keep moving
            //allow the controller to retake control of the movement if we're correctly facing the next path corner.
            if(facingNextCorner = Vector3.Angle(Unit.transform.forward, IdleLookAt) <= minMoveAngle)
            { 
                Controller.IsActive = true;
                return;
            }

            //update the rotation as long as the unit is attempting to look at the next target in the path before it the Controller takes over movement (and rotation)
            Unit.transform.rotation = Quaternion.Slerp(
                Unit.transform.rotation,
                NextRotationTarget,
                Time.deltaTime * idleAngularSpeed.Value);
        }
        #endregion

        #region Updating Movement Target
        public override ErrorMessage SetTarget (SetTargetInputData input)
        {
            if(Entity.TasksQueue.IsValid() && Entity.TasksQueue.CanAdd(input))
            {
                return Entity.TasksQueue.Add(new SetTargetInputData 
                {
                    componentCode = Code,

                    target = input.target,
                    playerCommand = input.playerCommand,
                });
            }

            return mvtMgr.SetPathDestination(input.ToSetPathDestinationData<IEntity>(Unit));
        }

        public override ErrorMessage SetTargetLocal(SetTargetInputData input)
        {
            return mvtMgr.SetPathDestinationLocal(input.ToSetPathDestinationData<IEntity>(Unit));
        }

        public ErrorMessage SetTarget(TargetData<IEntity> newTarget, float stoppingDistance, MovementSource source)
        {
            return mvtMgr.SetPathDestination(
                new SetPathDestinationData<IEntity>
                {
                    source = Unit,
                    destination = newTarget.position,
                    offsetRadius = stoppingDistance,
                    target = newTarget.instance,
                    mvtSource = source
                });
        }

        public ErrorMessage SetTargetLocal(TargetData<IEntity> newTarget, float stoppingDistance, MovementSource source)
        {
            return mvtMgr.SetPathDestinationLocal(
                new SetPathDestinationData<IEntity>
                {
                    source = Unit,
                    destination = newTarget.position,
                    offsetRadius = stoppingDistance,
                    target = newTarget.instance,
                    mvtSource = source
                });
        }

        public ErrorMessage OnPathDestination(TargetData<IEntity> newTarget, MovementSource source)
        {
            Target = newTarget;
            TargetInputData = new SetTargetInputData { target = Target, fromTasksQueue = source.fromTasksQueue };

            //enable the target position marker and set the unit's current target destination to reserve it
            if(!source.disableMarker)
                TargetPositionMarker.Toggle(true, Target.position);

            if (Unit.CarriableUnit.IsValid() 
                && Unit.CarriableUnit.CurrCarrier.IsValid()
                && Unit.CarriableUnit.CurrCarrier.AllowMovementToExitCarrier)
                Controller.Enabled = true;

            isMovementPending = true;

            Controller.Prepare(newTarget.position, source);

            return ErrorMessage.none;
        }

        public void OnPathFailure()
        {
            Unit.SetIdle(); //stop all unit activities in case path was supposed to be for a certain activity

            TargetPositionMarker.Toggle(true, Unit.transform.position);

            if (Controller.LastSource.playerCommand && RTSHelper.IsLocalPlayerFaction(Unit)) //if the local player owns this unit and the player called this
                audioMgr.PlaySFX(invalidMvtPathAudio.Fetch(), Unit);
        }

        public void OnPathPrepared(MovementSource source)
        {
            isMoving = true; //player is now marked as moving
            isMovementPending = false;

            if (Unit.AnimatorController?.CurrState == AnimatorState.moving) //if the unit was already moving, then lock changing the animator state briefly
                Unit.AnimatorController.LockState = true;

            globalEvent.RaiseMovementStartGlobal(this);
            startPosition = Unit.transform.position;
            RaiseMovementStart(new MovementEventArgs(source));
            RaiseTargetUpdated();

            Unit.SetIdle(source.sourceTargetComponent, false);

            DestinationReached = false; //destination is not reached by default

            if (Unit.AnimatorController.IsValid())
            {
                Unit.AnimatorController.LockState = false; //unlock animation state and play the movement anim
                Unit.AnimatorController.SetState(AnimatorState.moving);
            }

            Controller.Launch();
            NextCorner = Controller.NextPathTarget; //set the current target destination corner

            if (!canMoveAndRotate) //can not move before facing the next corner in the path by a certain angle?
                EnableMovementHaltingRotation();

            if (Controller.LastSource.playerCommand && RTSHelper.IsLocalPlayerFaction(Unit))
            {
                audioMgr.PlaySFX(Unit.AudioSourceComponent, mvtAudio.Fetch(), true);
            }
        }

        protected override bool CanStopOnNoTarget() => false;
        /// <summary>
        /// Stops the current unit's movement.
        /// </summary>
        /// <param name="prepareNextMovement">When true, not all movement settings will be reset since a new movement command will be followed.</param>
        protected override void OnStop()
        {
            audioMgr.StopSFX(Unit.AudioSourceComponent); //stop the movement audio from playing

            isMoving = false; //marked as not moving
            isMovementPending = false;

            globalEvent.RaiseMovementStopGlobal(this);
            RaiseMovementStop();

            Controller.IsActive = false; 

            //update the next rotation target using the registered IdleLookAt position for the idle rotation.
            //only do this once the unit stops moving in case there's no IdleLookAt object.
            UpdateRotationTarget(
                LastTarget.instance,
                LastTarget.instance.IsValid() ? LastTarget.instance.transform.position : LastTarget.opPosition
            );

            TargetPositionMarker.Toggle(true, Unit.transform.position);

            if (!Unit.Health.IsDead) //if the unit is not dead
            {
                Unit.AnimatorController?.SetState(AnimatorState.idle); //get into idle state

                if (!Controller.LastSource.sourceTargetComponent.IsValid())
                    Unit.SetIdle(exception: this, includeMovement: false);
            }
        }

        public override ErrorMessage IsTargetValid(SetTargetInputData data) => ErrorMessage.none;
        public override bool IsTargetInRange(Vector3 sourcePosition, TargetData<IEntity> target) => true;
        public override bool CanSearch => false;

        public void UpdateRotationTarget(IEntity rotationTarget, Vector3 rotationPosition, bool lookAway = false, bool setImmediately = false)
        {
            Target = new TargetData<IEntity>
            {
                position = Target.position,

                instance = rotationTarget,
                opPosition = rotationPosition
            };

            UpdateRotationTarget(
                RTSHelper.GetLookRotation(Unit.transform, Target.opPosition, reversed: lookAway, fixYRotation: true),
                setImmediately);
        }

        // Updating idle rotation
        public void UpdateRotationTarget (Quaternion targetRotation, bool setImmediately = false)
        {
            NextRotationTarget = targetRotation;

            if (setImmediately)
                Unit.transform.rotation = NextRotationTarget;
        }
        #endregion

        #region Handling Actions
        public override ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input)
        {
            switch((ActionType)actionID)
            {
                case ActionType.setPosition:
                    return SetPositionLocal(input.target.position);
                default:
                    return base.LaunchActionLocal(actionID, input);
            }
        }
        #endregion

        #region ToggleRotation
        public void ToggleMovementRotation(bool enable)
        {
            movementRotationEnabled = enable;

            Controller.Data = TimeModifiedControllerData;
        }
        #endregion

        #region SetPosition
        public ErrorMessage SetPosition(Vector3 position)
        {
            ErrorMessage positionClearError = mvtMgr.IsPositionClear(ref position, this, playerCommand: false);
            if (positionClearError != ErrorMessage.none)
                return positionClearError;

            return LaunchAction((byte)ActionType.setPosition, new SetTargetInputData { target = position });
        }

        private ErrorMessage SetPositionLocal(Vector3 position)
        {
            bool wasActive = false;

            if (!IsIdle)
            {
                Stop();
            }

            if (IsActive)
            {
                wasActive = true;

                SetActiveLocal(false, playerCommand: false);
            }

            Unit.transform.position = position;
            TargetPositionMarker.Toggle(true, Unit.transform.position);

            if (wasActive)
            {
                SetActiveLocal(true, playerCommand: false);
            }

            RaisePositionSet();

            return ErrorMessage.none;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private bool showPathDestination = true;
        [SerializeField, HideInInspector]
        private bool showPathNextCorner = true;

        protected override void OnDrawGizmosSelected()
        {
            DrawTargetGizmo();
            DrawMovementGizmo();
        }

        protected void DrawMovementGizmo()
        {
            if (!isMoving)
                return;

            Gizmos.color = Color.yellow;

            if (showPathDestination)
            {
                Gizmos.DrawWireSphere(Destination, mvtMgr.StoppingDistance);
            }

            if (showPathNextCorner)
            {
                Gizmos.DrawLine(Entity.transform.position, NextCorner);
            }
        }
#endif
        #endregion
    }
}
