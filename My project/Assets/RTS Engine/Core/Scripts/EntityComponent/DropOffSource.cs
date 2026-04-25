using System.Collections.Generic;
using System.Linq;
using System;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.ResourceExtension;
using RTSEngine.Logging;
using RTSEngine.UnitExtension;
using RTSEngine.Model;
using RTSEngine.Faction;

namespace RTSEngine.EntityComponent
{
    public class DropOffSource : FactionEntityTargetComponent<IFactionEntity>, IDropOffSource
    {
        #region Attributes
        /*
         * Action types and their parameters:
         * startDropoff: no parameters.
         * */
        public enum ActionType : byte { startDropoff }

        public IUnit Unit { private set;  get; }

        // Always set 'HasTarget' to false because we do not want this component to affect the idle status of the unit.
        // If at least one IEntityTargetComponent of an IEntity has this property set to true then the unit is considered in idle status.
        // We still want to classify the unit as idle even in the case it has a drop off point assigned.
        public override bool IsIdle => true;
        public override bool RequireIdleEntity => false;

        [SerializeField, Tooltip("Define the faction entities that can be used as drop off points.")]
        private FactionEntityTargetPicker targetPicker = new FactionEntityTargetPicker();

        [SerializeField, Tooltip("Only allow the unit to drop off resources at points within a certain distance from the resource?")]
        private bool maxDropOffDistanceEnabled = false;
        [SerializeField, Tooltip("If the above option is enabled then this is the max drop off point distance")]
        private float maxDropOffDistance = 10.0f;

        // Holds the collected resources amount defined by each resource type.
        private IDictionary<ResourceTypeInfo, int> collectedResources;
        public IReadOnlyDictionary<ResourceTypeInfo, int> CollectedResources => collectedResources as IReadOnlyDictionary<ResourceTypeInfo, int>;
        public int CollectedResourcesSum { private set; get; }

        public ResourceTypeInfo LastCollectedResourceType { get; private set; }

        [SerializeField, Tooltip("What types of resources can be dropped off?")]
        private CollectableResourceData[] dropOffResources = new CollectableResourceData[0];

        private IReadOnlyDictionary<ResourceTypeInfo, CollectableResourceData> dropOffResourcesDic = null;
        public int GetMaxCapacity(ResourceTypeInfo resourceType)
        {
            if (dropOffResourcesDic.TryGetValue(resourceType, out CollectableResourceData data))
                return data.amount;
            return 0;
        }

        private GameObject dropOffObject;

        [SerializeField, Tooltip("The total maximum capacity of all resources that the collector can hold before they have to drop it off.")]
        private int totalMaxCapacity = 10;

        [SerializeField, Tooltip("When the maximum capacity is reached and the source unit is idle, allow the unit to automatically go to the drop off point as soon as it added?")]
        private bool dropOffOnTargetAvailable = true;

        public DropOffState State { private set; get; }
        #endregion

        #region Events
        public event CustomEventHandler<IDropOffSource, EventArgs> CollectedResourcesUpdated;
        private void RaiseCollectedResourcesUpdated()
        {
            var handler = CollectedResourcesUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event CustomEventHandler<IDropOffSource, EventArgs> DropOffStateUpdated;
        private void RaiseDropOffStateUpdated(DropOffState newState)
        {
            State = newState;

            var handler = DropOffStateUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event CustomEventHandler<IDropOffSource, EventArgs> DropOffUnloaded;
        private void RaiseDropOffUnloaded()
        {
            var handler = DropOffUnloaded;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        protected override void OnTargetInit()
        {
            this.Unit = Entity as IUnit;

            if (!Unit.CollectorComponent.IsValid())
            {
                logger.LogError($"[DropOffSource - {Entity.Code}] A component that extends {typeof(IResourceCollector).Name} interface must be attached to same object!", source: this);
                return;
            }

            ResetCollectedResources();

            // Just used to have constant access time to drop off data rather than having to go through the list each time
            dropOffResourcesDic = dropOffResources.ToDictionary(dr => dr.type, dr => dr);

            // DropOffSource does not search for targets but simply tracks DropOffTarget instances being added and removed to get a target
            if(targetFinder.IsValid())
                targetFinder.IsActive = false;

            Unit.FactionUpdateStart += HandleFactionUpdateStart;
            Unit.FactionUpdateComplete += HandleFactionUpdateComplete;

            if (Unit.IsFree)
            {
                logger.LogWarning($"[DropOffSource - {Entity.Code}] This component can not be attached to a free unit. The unit must belong to a faction slot!", this);
            }
            else
            {
                Unit.FactionMgr.OwnFactionEntityAdded += HandleOwnFactionEntityAdded;
                Unit.FactionMgr.OwnFactionEntityRemoved += HandleOwnFactionEntityRemoved;
            }

            LastCollectedResourceType = null;
            Unit.CollectorComponent.TargetUpdated += HandleResourceCollectorTargetUpdated;

            RaiseDropOffStateUpdated(DropOffState.inactive);
        }

        private void ResetCollectedResources()
        {
            collectedResources = resourceMgr.FactionResources[Unit.IsFree ? 0 : Unit.FactionID].ResourceHandlers.Values
                .ToDictionary(resourceHandler => resourceHandler.Type, mr => 0);

            CollectedResourcesSum = 0;

            RaiseCollectedResourcesUpdated();
        }

        protected override void OnTargetDisabled()
        {
            if (!Unit.IsFree)
            {
                Unit.FactionMgr.OwnFactionEntityAdded -= HandleOwnFactionEntityAdded;
                Unit.FactionMgr.OwnFactionEntityRemoved -= HandleOwnFactionEntityRemoved;
            }

            Unit.CollectorComponent.TargetUpdated -= HandleResourceCollectorTargetUpdated;
        }
        #endregion

        #region Handling Event: Faction Update, Resource Collector Target Update, Own Faction Entity Add/Remove
        private void HandleFactionUpdateStart(IEntity entity, FactionUpdateArgs args)
        {
            if (Unit.IsFree)
                return;

            Unit.FactionMgr.OwnFactionEntityAdded -= HandleOwnFactionEntityAdded;
            Unit.FactionMgr.OwnFactionEntityRemoved -= HandleOwnFactionEntityRemoved;
        }

        private void HandleFactionUpdateComplete(IEntity entity, FactionUpdateArgs args)
        {
            if (Unit.IsFree)
                return;

            Unit.FactionMgr.OwnFactionEntityAdded += HandleOwnFactionEntityAdded;
            Unit.FactionMgr.OwnFactionEntityRemoved += HandleOwnFactionEntityRemoved;
        }

        private void HandleResourceCollectorTargetUpdated(IEntityTargetComponent resourceCollector, TargetDataEventArgs args)
        {
            UpdateTarget();
        }

        private void HandleOwnFactionEntityAdded(IFactionManager sender, EntityEventArgs<IFactionEntity> args)
        {
            if (args.Entity.DropOffTarget.IsValid())
            {
                UpdateTarget();
            }
        }

        private void HandleOwnFactionEntityRemoved(IFactionManager sender, EntityEventArgs<IFactionEntity> args)
        {
            if (args.Entity.DropOffTarget.IsValid())
                UpdateTarget();
        }
        #endregion

        #region Handling Component Upgrade
        protected override void OnComponentUpgraded(FactionEntityTargetComponent<IFactionEntity> sourceFactionEntityTargetComponent)
        {
            foreach (KeyValuePair<ResourceTypeInfo, int> collectedResourcePair in (sourceFactionEntityTargetComponent as IDropOffSource).CollectedResources)
                UpdateCollectedResources(collectedResourcePair.Key, collectedResourcePair.Value);

            UpdateTarget();

            AttemptStartDropOff();
        }
        #endregion

        #region Stopping
        protected override bool CanStopOnNoTarget() => false;
        #endregion

        #region Searching/Updating Target
        public override bool IsTargetInRange(Vector3 sourcePosition, TargetData<IEntity> target)
        {
            return !maxDropOffDistanceEnabled
                || !Unit.CollectorComponent.Target.instance.IsValid()
                || Vector3.Distance(Unit.CollectorComponent.Target.instance.transform.position, target.instance.transform.position)
                    <= maxDropOffDistance + target.instance.Radius + Unit.CollectorComponent.Target.instance.Radius;
        }

        public override ErrorMessage IsTargetValid(SetTargetInputData data)
        {
            TargetData<IFactionEntity> potentialTarget = data.target;

            if (!potentialTarget.instance.IsValid() || !potentialTarget.instance.CanLaunchTask)
                return ErrorMessage.invalid;
            else if (!potentialTarget.instance.IsSameFaction(Unit))
                return ErrorMessage.factionMismatch;
            else if (!potentialTarget.instance.IsInteractable)
                return ErrorMessage.uninteractable;
            else if (potentialTarget.instance.Health.IsDead)
                return ErrorMessage.targetDead;
            else if (!potentialTarget.instance.DropOffTarget.IsValid())
                return ErrorMessage.dropOffTargetMissing;
            else if (!targetPicker.IsValidTarget(potentialTarget.instance))
                return ErrorMessage.targetPickerUndefined;
            else if (!IsTargetInRange(transform.position, potentialTarget))
                return ErrorMessage.targetOutOfRange;
            else if (!potentialTarget.instance.DropOffTarget.CanDropResourceType(LastCollectedResourceType))
                return ErrorMessage.resourceTypeMismatch;

            return potentialTarget.instance.DropOffTarget.CanMove(Unit);
        }

        protected override void OnTargetPostLocked(SetTargetInputData input, bool sameTarget)
        {
            // Force unit to drop its resources if this was a direct player command
            if(input.playerCommand)
                AttemptStartDropOff(force: true);
            else if (dropOffOnTargetAvailable && Unit.IsIdle)
                AttemptStartDropOff(force: false, LastCollectedResourceType);
        }

        // Called when a new drop off point is added to the faction or when the unit starts collecting a new resource
        private void UpdateTarget()
        {
            Vector3 searchSourcePosition = Unit.transform.position;
            if (Unit.CollectorComponent.HasTarget
                && dropOffResourcesDic.ContainsKey(Unit.CollectorComponent.Target.instance.ResourceType))
            {
                searchSourcePosition = Unit.CollectorComponent.Target.instance.transform.position;
                LastCollectedResourceType = Unit.CollectorComponent.Target.instance.ResourceType;
            }

            IFactionEntity closestDropOffTarget = RTSHelper.GetClosestEntity(
                searchSourcePosition,
                Unit.FactionMgr.DropOffTargets,
                IsDropOffTargetValid
                );

            if (closestDropOffTarget.IsValid())
                SetTarget(closestDropOffTarget.ToTargetData(), false);
        }
        private bool IsDropOffTargetValid(IFactionEntity dropOffTargetEntity) => IsTargetValid(dropOffTargetEntity.ToSetTargetInputData(playerCommand: false)) == ErrorMessage.none;
        #endregion

        #region Handling Actions
        public override ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input)
        {
            switch ((ActionType)actionID)
            {
                case ActionType.startDropoff:

                    return SendToTargetLocal(input.playerCommand);

                default:
                    return ErrorMessage.undefined;
            }
        }
        #endregion

        #region Handling Resource Drop Off
        public ErrorMessage SendToTarget(bool playerCommand)
        {
            if (!Target.instance.IsValid())
            {
                if (playerCommand && Unit.IsLocalPlayerFaction())
                    playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                    {
                        message = ErrorMessage.dropOffTargetMissing,

                        source = Entity,
                    });

                Unit.SetIdle();
                //unit.AnimatorController?.SetState(AnimatorState.idle);

                return ErrorMessage.dropOffTargetMissing;
            }

            return LaunchAction((byte)ActionType.startDropoff, new SetTargetInputData { playerCommand = playerCommand });
        }

        private ErrorMessage SendToTargetLocal(bool playerCommand)
        {
            if (!LastCollectedResourceType.IsValid())
                return ErrorMessage.invalid;

            RaiseDropOffStateUpdated(DropOffState.active);

            globalEvent.RaiseUnitResourceDropOffStartGlobal(Unit, new ResourceEventArgs(Unit.CollectorComponent.Target.instance, LastCollectedResourceType));

            dropOffObject = dropOffResourcesDic[LastCollectedResourceType].enableObject;
            if (dropOffObject.IsValid())
                dropOffObject.SetActive(true);

            Unit.AnimatorController.SetOverrideController(dropOffResourcesDic[LastCollectedResourceType].animatorOverrideController.Fetch());
            audioMgr.StopSFX(Unit.AudioSourceComponent);
            audioMgr.PlaySFX(Unit, dropOffResourcesDic[LastCollectedResourceType].enableAudio);

            Target.instance.DropOffTarget.Move(
                Unit,
                new AddableUnitData
                {
                    sourceTargetComponent = this,
                    playerCommand = playerCommand
                });

            return ErrorMessage.none;
        }

        public void UpdateCollectedResources(ResourceTypeInfo resourceType, int value)
        {
            collectedResources[resourceType] += value;
            CollectedResourcesSum += value;

            RaiseCollectedResourcesUpdated();
        }

        public bool HasReachedMaxCapacity(ResourceTypeInfo resourceType = null)
        {
            return collectedResources.Values.Sum() >= totalMaxCapacity
                ||
                (resourceType.IsValid()
                    && dropOffResourcesDic.ContainsKey(resourceType)
                    && collectedResources[resourceType] >= dropOffResourcesDic[resourceType].amount);
        }

        // Forcing drop off means that if the collector has at least one resource unit of any type, it will be dropped off.
        public bool AttemptStartDropOff (bool force = false, ResourceTypeInfo resourceType = null)
        {
            if (CollectedResourcesSum == 0
                || (!force
                    && !HasReachedMaxCapacity(resourceType)))
                return false;

            // When attempting to drop off a resource type that has already reached its maximum capacity limit
            if(resourceMgr.HasResourceTypeReachedLimitCapacity(resourceType, Unit.FactionID))
            {
                if (Unit.IsLocalPlayerFaction())
                {
                    playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                    {
                        message = ErrorMessage.resourceTypeLimitCapacityReached,
                        text = resourceType.DisplayName
                    });
                }

                Unit.CollectorComponent.Stop();

                return false;
            }

            RaiseDropOffStateUpdated(DropOffState.ready);

            SendToTarget(false);

            return true;
        }

        public void Unload()
        {
            RaiseDropOffStateUpdated(Unit.CollectorComponent.HasTarget ? DropOffState.goingBack : DropOffState.inactive);

            if (dropOffObject.IsValid())
                dropOffObject.SetActive(false);

            // Only units that belong to a faction can update their faction's resources.
            if(!Unit.IsFree) 
                foreach (ResourceTypeInfo resourceType in collectedResources.Keys.ToArray())
                {
                    if (collectedResources[resourceType] == 0)
                        continue;

                    ResourceInput nextInput = new ResourceInput
                    {
                        type = resourceType,
                        value = new ResourceTypeValue
                        {
                            amount = collectedResources[resourceType],
                            capacity = 0
                        }
                    };

                    resourceMgr.UpdateResource(
                        Unit.FactionID,
                        nextInput,
                        add: true,
                        out int restAmount);

                    int unloadedAmount = (nextInput.value.amount - restAmount);
                    collectedResources[nextInput.type] -= unloadedAmount;
                    CollectedResourcesSum -= unloadedAmount;

                    // In case not all of the amount could be unloaded due to a limited capacity type resource that reached its maximum capacity...
                    if(restAmount > 0 && Unit.IsLocalPlayerFaction())
                    {
                        playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                        {
                            message = ErrorMessage.resourceTypeLimitCapacityReached,
                            amount = restAmount,
                            text = nextInput.type.DisplayName
                        });
                    }

                    globalEvent.RaiseUnitResourceDropOffCompleteGlobal(Unit, new ResourceAmountEventArgs(nextInput));
                }

            Unit.AnimatorController?.ResetOverrideController();

            RaiseDropOffUnloaded();
            RaiseCollectedResourcesUpdated();

            // Back to collect the last resource
            if (State == DropOffState.goingBack)
            {
                Unit.CollectorComponent.SetTarget(Unit.CollectorComponent.Target, false);
            }
            // If the unit is idle (ResourceCollector is inactive) and we are allowed to drop off when the target available...
            // ...then this means we are resuming the resource collection of the same resource (LastTarget) that was being collected before...
            // ...the unit had to stop collecting due to not having a drop off point available
            else if (dropOffOnTargetAvailable && Unit.IsIdle)
            {
                Unit.CollectorComponent.SetTarget(Unit.CollectorComponent.LastTarget, false);
            }
        }

        public void Cancel()
        {
            RaiseDropOffStateUpdated(DropOffState.inactive);

            if (dropOffObject.IsValid())
                dropOffObject.SetActive(false);
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private bool showDropOffPosition = true;
        [HideInInspector]
        public bool editorFoldout;

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.yellow;
            if(HasTarget && showDropOffPosition)
                Gizmos.DrawWireSphere(Target.instance.DropOffTarget.GetAddablePosition(Unit), mvtMgr.StoppingDistance);
        }
#endif
        #endregion
    }
}
