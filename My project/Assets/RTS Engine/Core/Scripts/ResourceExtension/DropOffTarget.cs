using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Model;
using RTSEngine.Movement;
using RTSEngine.Terrain;
using RTSEngine.UnitExtension;
using System.Linq;
using UnityEngine;

namespace RTSEngine.ResourceExtension
{
    public class DropOffTarget : MonoBehaviour, IDropOffTarget, IEntityPostInitializable
    {
        #region Class Attributes
        public IEntity Entity { private set; get; }

        [SerializeField, Tooltip("Code to identify this component, unique within the entity")]
        private string code = "unique_code";
        public string Code => code;

        [Space(), SerializeField, Tooltip("Pick what types of resources that a collector is allowed to drop off.")]
        private ResourceTypeTargetPicker allowedResourceTypes = new ResourceTypeTargetPicker();
        public bool CanDropResourceType(ResourceTypeInfo resourceType) => !resourceType.IsValid() || allowedResourceTypes.IsValidTarget(resourceType);

        [Space(), SerializeField, Tooltip("Require the drop off point to have a pre-defined fixed position for collectors to drop their resources at?")]
        private bool requireDropOffPosition = true;
        [SerializeField, Tooltip("Code to identify this component, unique within the entity")]
        private Transform dropOffPosition = null;
        [SerializeField, Tooltip("If populated then this defines the types of terrain areas that the drop off target must use when a resource collector attempts to drop off their resources.")]
        private TerrainAreaType[] forcedTerrainAreas = new TerrainAreaType[0];

        // Game services
        protected ITerrainManager terrainMgr { private set; get; }
        protected IMovementManager mvtMgr { private set; get; }
        protected IGameLoggingService logger { private set; get; } 
        #endregion

        #region Initializing/Terminating
        public void OnEntityPostInit(IGameManager gameMgr, IEntity entity)
        {
            this.Entity = entity;

            this.terrainMgr = gameMgr.GetService<ITerrainManager>();
            this.mvtMgr = gameMgr.GetService<IMovementManager>();
            this.logger = gameMgr.GetService<IGameLoggingService>();

            if(dropOffPosition.IsValid())
            {
                if (!terrainMgr.GetTerrainAreaPosition(dropOffPosition.position, forcedTerrainAreas, out Vector3 addablePosition))
                {
                    logger.LogError($"[DropOffTarget - {Entity.Code}] Unable to find a suitable drop off position for the forced terrain areas of the drop off target");
                    return;

                }
                else if(forcedTerrainAreas.Length > 0 && forcedTerrainAreas.Any(terrainArea => !terrainArea.IsValid()))
                {
                    logger.LogError($"[DropOffTarget - {Entity.Code}] The 'Forced Terrain Areas' field must be either empty or populated with valid elements!");
                    return;
                }

                dropOffPosition.position = addablePosition;
            }
            else if(requireDropOffPosition)
            {
                logger.LogError($"[DropOffTarget - {Entity.Code}] The 'Drop Off Position' field is required to be assigned!");
                return;
            }
        }

        public void Disable() { }
        #endregion

        #region IAddableUnit/Dropping Off Resources
        public Vector3 GetAddablePosition(IUnit unit)
        {
            if (!requireDropOffPosition)
                return Entity.transform.position;

            // In case the entity can move, we need to check if the dropOffPosition has been updated.
            if (Entity.MovementComponent.IsValid())
            {
                terrainMgr.GetTerrainAreaPosition(dropOffPosition.position, forcedTerrainAreas, out Vector3 addablePosition);
                return addablePosition;
            }

            return dropOffPosition.position;
        }

        public ErrorMessage CanAdd(IUnit unit, AddableUnitData addableData = default) => ErrorMessage.undefined;

        public ErrorMessage Add(IUnit unit, AddableUnitData addableUnitData)
        {
            if (!unit.IsValid())
                return ErrorMessage.invalid;

            unit.DropOffSource.Unload();

            return ErrorMessage.none;
        }

        public ErrorMessage CanMove(IUnit unit, AddableUnitData addableData = default)
        {
            if (!unit.IsValid())
                return ErrorMessage.invalid;
            else if (!unit.IsInteractable)
                return ErrorMessage.uninteractable;
            else if (unit.Health.IsDead)
                return ErrorMessage.healthDead;

            else if (addableData.otherFactionBehaviour != CarriableUnitOtherFactionBehaviour.allowAll && !RTSHelper.IsSameFaction(unit, Entity))
                return ErrorMessage.factionMismatch;

            return ErrorMessage.none;
        }

        public ErrorMessage Move(IUnit unit, AddableUnitData addableData)
        {
            Vector3 addablePosition = GetAddablePosition(unit);

            mvtMgr.SetPathDestinationLocal(
                new SetPathDestinationData<IEntity>
                {
                    source = unit,
                    destination = addablePosition,
                    offsetRadius = requireDropOffPosition ? 0.0f : Entity.Radius,
                    target = Entity,
                    mvtSource = new MovementSource
                    {
                        playerCommand = addableData.playerCommand,

                        sourceTargetComponent = addableData.sourceTargetComponent,

                        targetAddableUnit = this,
                        targetAddableUnitPosition = addablePosition,

                        disableMarker = true
                    }
                });

            return ErrorMessage.none;
        }
        #endregion
    }
}
