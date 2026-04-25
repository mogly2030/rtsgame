using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Determinism;
using RTSEngine.Effect;
using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Terrain;
using RTSEngine.Search;
using RTSEngine.Attack;
using RTSEngine.Audio;
using RTSEngine.Utilities;

namespace RTSEngine.Movement
{
    public struct SetPathDestinationData<T>
    {
        public T source;

        public Vector3 destination;
        public float offsetRadius;
        public IEntity target;
        public MovementSource mvtSource;
    }

    public class MovementManager : MonoBehaviour, IMovementManager
    {
        #region Attributes
        [SerializeField, Tooltip("Determines the distance at which a unit stops before it reaches its movement target position."), Min(float.Epsilon)]
        private float stoppingDistance = 0.1f;
        public float StoppingDistance => stoppingDistance;

        [SerializeField, Tooltip("Enable this option to allow to have a different maximum stopping distance for moving units on Y-axis (height) of the destination. This can be useful due to the difference in the height of the path calculations and the navigation meshes generated when it comes to uneven surfaces.")]
        private bool enableHeightSpecificStoppingDistance = true;
        [SerializeField, Tooltip("When the above option is enabled, this is the allowed stopping distance on the Y-axis between the moving unit position and its destination to mark the destination as reached. The other stopping distance is used for the X and Z axis."), Min(float.Epsilon)]
        private float heightSpecificStoppingDistance = 0.2f;
        public float HeightSpecificStoppingDistance => heightSpecificStoppingDistance;

        public bool IsPositionReached(Vector3 inputPosition, Vector3 targetPosition)
        { 
            if(!enableHeightSpecificStoppingDistance)
                return Vector3.Distance(inputPosition, targetPosition) <= stoppingDistance;

            return Vector2.Distance(
                new Vector2(inputPosition.x, inputPosition.z),
                new Vector2(targetPosition.x, targetPosition.z)
            ) <= stoppingDistance
            && Mathf.Abs(inputPosition.y - targetPosition.y) <= heightSpecificStoppingDistance;
        }

        [SerializeField, EnforceType(typeof(IEffectObject), prefabOnly: true), Tooltip("Visible to the local player when they command unit(s) to move to a location.")]
        private GameObject movementTargetEffectPrefab = null;
        public IEffectObject MovementTargetEffect { get; private set; }

        // Collection used to store and pass path destination between methods in this script
        // Having it as a member variable makes more sense to avoid the garbage produced by creating and destroying this list every time we are generating path destinations
        private List<Vector3> pathDestinations;
        private const int PATH_DESTINATIONS_DEFAULT_CAPACITY = 50;

        // When generating path destinations, this hashset holds the formation types that this manager used to generate path destinations
        // In case two formation types are the fallback formation types of each other, without knowing that one was already tried before the other one...
        // ...it would generate an endless loop if both formation types are unable to generate enough valid path destinations
        private HashSet<MovementFormationType> attemptedFormationTypes;

        /// <summary>
        /// Handles connecting the pathfinding system and the RTS Engine movement system.
        /// </summary>
        public IMovementSystem MvtSystem { private set; get; }

        private IReadOnlyDictionary<MovementFormationType, IMovementFormationHandler> formationHandlers = null;

        // Game services
        protected IGameManager gameMgr { private set; get; }
        protected IInputManager inputMgr { private set; get; }
        protected ITerrainManager terrainMgr { private set; get; }
        protected IEffectObjectPool effectObjPool { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        protected IGridSearchHandler gridSearch { private set; get; }
        protected IAttackManager attackMgr { private set; get; }
        protected IGameAudioManager audioMgr { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;

            this.inputMgr = gameMgr.GetService<IInputManager>();
            this.terrainMgr = gameMgr.GetService<ITerrainManager>();
            this.effectObjPool = gameMgr.GetService<IEffectObjectPool>();
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.gridSearch = gameMgr.GetService<IGridSearchHandler>();
            this.attackMgr = gameMgr.GetService<IAttackManager>();
            this.audioMgr = gameMgr.GetService<IGameAudioManager>();

            MvtSystem = gameObject.GetComponent<IMovementSystem>();

            if (!MvtSystem.IsValid())
            {
                logger.LogError("[MovementManager] A component that implements the 'IMovementSystem' interface interface must be attached to the object.");
                return;
            }

            pathDestinations = new List<Vector3>(PATH_DESTINATIONS_DEFAULT_CAPACITY);
            attemptedFormationTypes = new HashSet<MovementFormationType>();

            if (movementTargetEffectPrefab.IsValid())
                this.MovementTargetEffect = movementTargetEffectPrefab.GetComponent<IEffectObject>();

            formationHandlers = gameObject
                .GetComponents<IMovementFormationHandler>()
                .ToDictionary(handler =>
                {
                    handler.Init(gameMgr);

                    return handler.FormationType;
                });
        }
        #endregion

        #region Setting Path Destination Helper Methods
        private void OnPathDestinationCalculationStart(IEntity entity)
        {
            // Disable the target position marker so it won't intefer in determining the target positions
            entity.MovementComponent.TargetPositionMarker.Toggle(false);
        }
        private void OnPathDestinationCalculationStart(IReadOnlyList<IEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
                // Disable the target position marker so it won't intefer in determining the target positions
                entities[i].MovementComponent.TargetPositionMarker.Toggle(false);
        }

        private void OnPathDestinationCalculationInterrupted(IEntity entity)
        {
            entity.MovementComponent.TargetPositionMarker.Toggle(true);
        }
        private void OnPathDestinationCalculationInterrupted(IReadOnlyList<IEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
                entities[i].MovementComponent.TargetPositionMarker.Toggle(true);
        }
        #endregion

        #region Setting Path Destination: Single Entity
        public ErrorMessage SetPathDestination(SetPathDestinationData<IEntity> data)
        {
            return inputMgr.SendInput(
                new CommandInput()
                {
                    sourceMode = (byte)InputMode.entity,
                    targetMode = (byte)InputMode.movement,

                    sourcePosition = data.source.transform.position,
                    targetPosition = data.destination,

                    floatValue = data.offsetRadius,

                    // MovementSource:
                    code = $"{data.mvtSource.sourceTargetComponent?.Code}" +
                    $"{RTSHelper.STR_SEPARATOR_L1}{data.mvtSource.targetAddableUnit?.Code}",
                    opPosition = data.mvtSource.targetAddableUnitPosition,
                    playerCommand = data.mvtSource.playerCommand,
                    intValues = inputMgr.ToIntValues((int)data.mvtSource.BooleansToMask())
                    //intValues = inputMgr.ToIntValues(data.mvtSource.isAttackMove ? 1 : 0, data.mvtSource.isOriginalAttackMove ? 1 : 0)
                },
                source: data.source,
                target: data.target);
        }

        public ErrorMessage SetPathDestinationLocal(SetPathDestinationData<IEntity> data)
        {
            if (!data.source.IsValid())
            {
                logger.LogError("[MovementManager] Can not move an invalid data.source!");
                return ErrorMessage.invalid;
            }
            else if (!data.source.CanMove(data.mvtSource.playerCommand))
                return ErrorMessage.mvtDisabled;

            OnPathDestinationCalculationStart(data.source);

            // Used for the movement target effect and rotation look at of the unit
            Vector3 originalDestination = data.destination;
            Vector3 destination = data.destination;
            TargetData<IEntity> targetData = new TargetData<IEntity>
            {
                instance = data.target,
                position = data.destination,
                opPosition = originalDestination
            };

            // First check if the actual destination is a valid target position, if it can't be then search for a valid one depending on the movement formation
            // If the offset radius is not zero, then the unit will be moving towards a target data.source and a calculation for a path destination around that target is required
            if (data.offsetRadius > 0.0f
                || !terrainMgr.SampleHeight(destination, data.source.MovementComponent, out destination.y)
                || IsPositionClear(ref destination, data.source.MovementComponent, data.mvtSource.playerCommand) != ErrorMessage.none)
            {
                targetData.position = destination;

                GeneratePathDestination(
                    data.source,
                    targetData,
                    data.source.MovementComponent.Formation,
                    data.offsetRadius,
                    data.mvtSource,
                    ref pathDestinations);

                if (pathDestinations.Count == 0)
                {
                    OnPathDestinationCalculationInterrupted(data.source);
                    //logger.LogError($"[Movement Manager] Unable to determine path destination! Please follow error trace to find the movement's data.mvtSource!");
                    return ErrorMessage.mvtTargetPositionNotFound;
                }

                // Get the closest target position
                //destination = pathDestinations.OrderBy(pos => (pos - data.source.transform.position).sqrMagnitude).First();
                pathDestinations.Sort((pos1, pos2) => (pos1 - data.source.transform.position).sqrMagnitude.CompareTo((pos2 - data.source.transform.position).sqrMagnitude));
                destination = pathDestinations[0];
            }

            if (data.mvtSource.playerCommand && !data.target.IsValid() && RTSHelper.IsLocalPlayerFaction(data.source))
            {
                SpawnMovementTargetEffect(data.source, originalDestination, data.mvtSource);

                audioMgr.PlaySFX(data.source.MovementComponent.OrderAudio, data.source, loop:false); //play the movement audio.
            }

            return data.source.MovementComponent.OnPathDestination(
                new TargetData<IEntity>
                {
                    instance = data.target,
                    position = destination,
                    opPosition = originalDestination
                },
                data.mvtSource);
        }
        #endregion

        #region Setting Path Destination: Multiple Entities
        public ErrorMessage SetPathDestination(SetPathDestinationData<IReadOnlyList<IEntity>> data)
        {
            // Only one data.source to move? use the dedicated method instead!
            if (data.source.Count == 1) 
                return SetPathDestination(new SetPathDestinationData<IEntity> 
                {
                    source = data.source[0],
                    destination = data.destination,
                    offsetRadius = data.offsetRadius,
                    target = data.target,
                    mvtSource = data.mvtSource
                });

            return inputMgr.SendInput(new CommandInput()
            {
                sourceMode = (byte)InputMode.entityGroup,
                targetMode = (byte)InputMode.movement,

                targetPosition = data.destination,
                floatValue = data.offsetRadius,

                playerCommand = data.mvtSource.playerCommand,
                intValues = inputMgr.ToIntValues((int)data.mvtSource.BooleansToMask())
            },
            source: data.source,
            target: data.target);
        }

        public ErrorMessage SetPathDestinationLocal(SetPathDestinationData<IReadOnlyList<IEntity>> data)
        {
            if (!data.source.IsValid())
            {
                logger.LogError("[MovementManager] Some or all entities that are attempting to move are invalid!!");
                return ErrorMessage.invalid;
            }

            // Sort the attack units based on their codes, we assume that units that share the same code (which is the defining property of an data.source in the RTS Engine) are identical.
            // Additionally, filter out any units that are not movable.

            // CHANGE ME: In case the below OrderBy call is creating too much garbage, it needs to change
            // Maybe using a pre existing list that gets cleared everytime but holds the same capacity would help? But how to handle the simple sorting?
            var sortedMvtSources = RTSHelper.SortEntitiesByCode(
                data.source,
                entity => entity.CanMove(data.mvtSource.playerCommand))
                .Values
                .OrderBy(mvtSourceSet => mvtSourceSet[0].MovementComponent.MovementPriority)
                .ToList();


            TargetData <IEntity> targetData = new TargetData<IEntity>
            {
                instance = data.target,
                position = data.destination,
            };

            for (int i = 0; i < sortedMvtSources.Count; i++) 
            {
                List<IEntity> mvtSourceSet = sortedMvtSources[i];

                OnPathDestinationCalculationStart(mvtSourceSet);

                GeneratePathDestination(
                    mvtSourceSet,
                    targetData,
                    mvtSourceSet[0].MovementComponent.Formation,
                    data.offsetRadius,
                    data.mvtSource,
                    ref pathDestinations);

                if (pathDestinations.Count == 0)
                {
                    OnPathDestinationCalculationInterrupted(mvtSourceSet);
                    //logger.LogError($"[Movement Manager] Unable to determine path destination! Please follow error trace to find the movement's data.mvtSource!");
                    return ErrorMessage.mvtTargetPositionNotFound;
                }

                // Compute the directions of the units we have so we know the direction they will face in regards to the target.
                Vector3 unitsDirection = RTSHelper.GetEntitiesDirection(data.source, data.destination);
                unitsDirection.y = 0;

                // Index counter for the generated path destinations.
                int destinationID = 0;
                // Index for the entities in the current set
                int j = 0;

                for (j = 0; j < mvtSourceSet.Count; j++) 
                {
                    IEntity mvtSource = mvtSourceSet[j];

                    // If this movement is towards a target, pick the closest position to the target for each unit
                    if (data.target.IsValid())
                    {
                        //pathDestinations.OrderBy(pos => (pos - mvtSource.transform.position).sqrMagnitude).ToList();
                        pathDestinations.Sort((pos1, pos2) => (pos1 - mvtSource.transform.position).sqrMagnitude.CompareTo((pos2 - mvtSource.transform.position).sqrMagnitude));
                    }

                    if (mvtSource.MovementComponent.OnPathDestination(
                        new TargetData<IEntity>
                        {
                            instance = data.target,
                            position = pathDestinations[destinationID],

                            opPosition = pathDestinations[destinationID] + unitsDirection // Rotation look at position
                        },
                        data.mvtSource) != ErrorMessage.none)
                    {
                        OnPathDestinationCalculationInterrupted(mvtSource);
                        continue;
                    }

                    // Only move to the next path destination if we're moving towards a non target, if not keep removing the first element of the list which was the closest to the last unit
                    if (data.target.IsValid())
                        pathDestinations.RemoveAt(0);
                    else
                        destinationID++;

                    if (destinationID >= pathDestinations.Count) // No more paths to test, stop moving units.
                        break;
                }

                // If no path destinations could be assigned to the rest of the units, interrupt their path calculation state
                if(j < mvtSourceSet.Count)
                    OnPathDestinationCalculationInterrupted(mvtSourceSet.GetRange(j + 1, mvtSourceSet.Count - (j + 1)));
            }


            if (data.mvtSource.playerCommand && !data.target.IsValid() && RTSHelper.IsLocalPlayerFaction(data.source.FirstOrDefault()))
            {
                IEntity refEntity = data.source.First();
                SpawnMovementTargetEffect(refEntity, data.destination, data.mvtSource);

                audioMgr.PlaySFX(refEntity.MovementComponent.OrderAudio, refEntity, loop:false); //play the movement audio.
            }

            return ErrorMessage.none;
        }
        #endregion

        #region Generating Path Destinations
        public ErrorMessage GeneratePathDestination(IEntity entity, TargetData<IEntity> target, MovementFormationSelector formationSelector, float offset, MovementSource source, ref List<Vector3> pathDestinations, System.Func<PathDestinationInputData, Vector3, ErrorMessage> condition = null)
            => GeneratePathDestination(
                entity,
                1,
                (target.position - entity.transform.position).normalized,
                target,
                formationSelector,
                offset,
                source,
                ref pathDestinations,
                condition
        );

        public ErrorMessage GeneratePathDestination(IReadOnlyList<IEntity> entities, TargetData<IEntity> target, MovementFormationSelector formationSelector, float offset, MovementSource source, ref List<Vector3> pathDestinations, System.Func<PathDestinationInputData, Vector3, ErrorMessage> condition = null)
            => GeneratePathDestination(
                entities[0],
                entities.Count(),
                RTSHelper.GetEntitiesDirection(entities, target.position),
                target,
                formationSelector,
                offset,
                source,
                ref pathDestinations,
                condition
        );


        // refMvtSource: The unit that will be used as a reference to the rest of the units of the same type.
        // amount: The amount of path destinations that we want to produce.
        // direction: the direction the units will face in regards to the target.
        public ErrorMessage GeneratePathDestination(IEntity refMvtSource, int amount, Vector3 direction, TargetData<IEntity> target, MovementFormationSelector formationSelector, float offset, MovementSource source, ref List<Vector3> pathDestinations, System.Func<PathDestinationInputData, Vector3, ErrorMessage> condition = null)
        {
            // ASSUMPTIONS: All entities are of the same type.

            // Ref list must be already initialized.
            pathDestinations.Clear();
            attemptedFormationTypes.Clear();

            if (!formationSelector.type.IsValid())
            {
                logger.LogError($"[MovementManager] Requesting path destinations for entity of code '{refMvtSource.Code}' with invalid formation type!");
                return ErrorMessage.invalid;
            }
            else if (!formationHandlers.ContainsKey(formationSelector.type))
            {
                logger.LogError($"[MovementManager] Requesting path destinations for formation of type: '{formationSelector.type.Key}' but no suitable component that implements '{typeof(IMovementFormationHandler).Name}' is found!");
                return ErrorMessage.invalid;
            }

            // Depending on the ref entity's movable terrain areas, adjust the target position
            terrainMgr.GetTerrainAreaPosition(target.position, refMvtSource.MovementComponent.TerrainAreas, out target.position);

            ErrorMessage errorMessage;

            // We want to handle setting the height by sampling the terrain to get the correct height since there's no way to know it directly.
            // There we keep this direction position value on the y axis to 0
            direction.y = 0;

            // Holds the amount of attempts made to generate path destinations but resulted in no generated positions.
            int emptyAttemptsCount = 0;
            // In case the attack formation is switched due to max empty attempts or an error then we want to reset the offset.
            float originalOffset = offset;

            while (amount > 0)
            {
                // In case the path destination generation methods result into a failure, return with the failure's error code.
                if ((errorMessage = formationHandlers[formationSelector.type].GeneratePathDestinations(
                    new PathDestinationInputData
                    {
                        refMvtComp = refMvtSource.MovementComponent,

                        target = target,
                        direction = direction,

                        source = source,
                        formationSelector = formationSelector,

                        condition = condition,
                        
                        playerCommand = source.playerCommand
                    },
                    ref amount,
                    ref offset,
                    ref pathDestinations,
                    out int generatedAmount)) != ErrorMessage.none || emptyAttemptsCount >= formationHandlers[formationSelector.type].MaxEmptyAttempts)
                {
                    attemptedFormationTypes.Add(formationSelector.type);

                    // Reset empty attemps count and offset for next fallback formation type
                    emptyAttemptsCount = 0;
                    offset = originalOffset;

                    // Current formation type could not compute all path destinations then generate path destinations with the fall back formation if there's one
                    if (formationHandlers[formationSelector.type].FallbackFormationType.IsValid()
                        // And make sure we have not attempted to generate path destinations with the fallback formation in this call
                        && !attemptedFormationTypes.Contains(formationHandlers[formationSelector.type].FallbackFormationType))
                    {
                        formationSelector = new MovementFormationSelector
                        {
                            type = formationHandlers[formationSelector.type].FallbackFormationType,
                            properties = formationSelector.properties
                        };

                        continue;
                    }

                    // No fallback formation? exit!
                    return errorMessage;
                }

                // Only if the last attempt resulted in no generated path destinations.
                if (generatedAmount == 0)
                    emptyAttemptsCount++;
            }

            // We have computed at least one path destination, the count of the list is either smaller or equal to the initial value of the "amount" argument.
            return ErrorMessage.none; 
        }

        // Generate single path destination around an origin position within a certain range
        public ErrorMessage GeneratePathDestination (Vector3 originPosition, float range, IMovementComponent refMvtComp, out Vector3 targetPosition)
        {
            targetPosition = originPosition + (Random.insideUnitSphere * range);

            int expectedPositionCount = Mathf.FloorToInt(2.0f * Mathf.PI * range / (refMvtComp.Entity.Radius * 2.0f));

            // If no expected positions are to be found and the radius offset is zero then set the expected position count to 1 to test the actual target position if it is valid
            if (expectedPositionCount == 0)
                expectedPositionCount = 1;

            // Represents increment value of the angle inside the current circle with the above perimeter
            float angleIncValue = 360f / expectedPositionCount;
            float currentAngle = 0.0f;

            Vector3 nextDestination = originPosition + Vector3.right * range;

            int counter = 0; 

            // As long as we haven't inspected all the expected free positions inside this cirlce
            while (counter < expectedPositionCount) 
            {
                // Always make sure that the next path destination has a correct height in regards to the height of the map.
                if (terrainMgr.SampleHeight(nextDestination, refMvtComp, out nextDestination.y))
                {
                    // Check if there is no obstacle and no other reserved target position on the currently computed potential path destination
                    if (IsPositionClear(ref nextDestination, refMvtComp, playerCommand: false) == ErrorMessage.none)
                    {
                        targetPosition = nextDestination;
                        return ErrorMessage.none;
                    }

                    // Increment the angle value to find the next position on the circle
                    currentAngle += angleIncValue;

                    // Rotate the nextDestination vector around the y axis by the current angle value
                    nextDestination = originPosition + range * new Vector3(Mathf.Cos(Mathf.Deg2Rad * currentAngle), 0.0f, Mathf.Sin(Mathf.Deg2Rad * currentAngle));
                }

                counter++;
            }

            return ErrorMessage.invalid;
        }
        #endregion

        #region Generating Path Destinations Helper Methods
        public ErrorMessage IsPositionClear(ref Vector3 targetPosition, IMovementComponent refMvtComp, bool playerCommand)
            => IsPositionClear(ref targetPosition, refMvtComp.Controller.Radius, refMvtComp.Controller.NavigationAreaMask, refMvtComp.AreasMask, playerCommand);

        public ErrorMessage IsPositionClear(ref Vector3 targetPosition, float agentRadius, LayerMask navAreaMask, TerrainAreaMask areasMask, bool playerCommand)
        {
            ErrorMessage errorMessage;
            if ((errorMessage = gridSearch.IsPositionReserved(targetPosition, agentRadius, areasMask, playerCommand)) != ErrorMessage.none)
                return errorMessage;

            else if (TryGetMovablePosition(targetPosition, agentRadius, navAreaMask, out targetPosition))
                return ErrorMessage.none;

            return ErrorMessage.mvtPositionNavigationOccupied;
        }

        public bool TryGetMovablePosition(Vector3 center, float radius, LayerMask areaMask, out Vector3 movablePosition)
            => MvtSystem.TryGetValidPosition(center, radius, areaMask, out movablePosition);

        public bool GetRandomMovablePosition(IEntity entity, Vector3 origin, float range, out Vector3 targetPosition, bool playerCommand)
        {
            targetPosition = entity.transform.position;
            if (!entity.IsValid() || !entity.CanMove())
                return false;

            // Pick a random direction to go to
            Vector3 randomDirection = Random.insideUnitSphere * range; 
            randomDirection += origin;

            // Get the closet movable point to the randomly chosen direction
            if (terrainMgr.SampleHeight(randomDirection, entity.MovementComponent, out randomDirection.y)
                && MvtSystem.TryGetValidPosition(randomDirection, range, entity.MovementComponent.Controller.NavigationAreaMask, out targetPosition)
                && IsPositionClear(ref targetPosition, entity.MovementComponent, playerCommand) == ErrorMessage.none)
                return true;

            return false;
        }
        #endregion

        #region Movement Helper Methods
        private void SpawnMovementTargetEffect(IEntity entity, Vector3 position, MovementSource source)
        {
            effectObjPool.Spawn(
                source.isMoveAttackRequest && entity.FirstActiveAttackComponent.IsValid() && entity.FirstActiveAttackComponent.IsAttackMoveEnabled
                ? attackMgr.AttackMoveTargetEffect
                : MovementTargetEffect,
                position);
        }
        #endregion
    }
}