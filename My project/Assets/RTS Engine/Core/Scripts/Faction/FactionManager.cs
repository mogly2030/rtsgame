using System;
using System.Linq;
using System.Collections.Generic;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.BuildingExtension;
using RTSEngine.Game;
using RTSEngine.ResourceExtension;

namespace RTSEngine.Faction
{
    public class FactionManager : IFactionManager
    {
        #region Attributes
        public int FactionID { private set; get; }

        public IFactionSlot Slot {private set; get;}

        private List<IFactionEntity> factionEntities; 
        public IEnumerable<IFactionEntity> FactionEntities => factionEntities.ToArray();

        private Dictionary<string, int> factionEntityToAmount;
        public IReadOnlyDictionary<string, int> FactionEntityToAmount => factionEntityToAmount;
        private Dictionary<string, List<IFactionEntity>> factionEntityCodeDic;
        public IReadOnlyList<IFactionEntity> GetFactionEntitiesListByCode(string code)
            => factionEntityCodeDic.TryGetValue(code, out List<IFactionEntity> list)
            ? list
            : new IFactionEntity[0];

        private Dictionary<string, int> factionEntityCategoryToAmount;
        public IReadOnlyDictionary<string, int> FactionEntityCategoryToAmount => factionEntityCategoryToAmount;

        private List<IFactionEntity> dropOffTargets;
        public IReadOnlyList<IFactionEntity> DropOffTargets => dropOffTargets;

        private List<IFactionEntity> mainEntities;
        public IEnumerable<IFactionEntity> MainEntities => mainEntities.ToArray();

        private List<IUnit> units; 
        public IEnumerable<IUnit> Units => units.ToArray();
        private List<IUnit> workerUnits;
        public IReadOnlyList<IUnit> WorkerUnits => workerUnits;

        private List<IUnit> attackUnits;
        public IEnumerable<IUnit> GetAttackUnits(float range = 1.0f)
            => attackUnits.GetRange(0, (int)(attackUnits.Count * (range >= 0.0f && range <= 1.0f ? range : 1.0f)));

        private List<IBuilding> buildings;
        public IReadOnlyList<IBuilding> Buildings => buildings.AsReadOnly();

        private List<IBuilding> buildingCenters;
        public IReadOnlyList<IBuilding> BuildingCenters => buildingCenters.AsReadOnly();

        private List<FactionEntityAmountLimit> limits = new List<FactionEntityAmountLimit>();

        // Game services
        protected IGameManager gameMgr { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IFactionManager, EntityEventArgs<IFactionEntity>> OwnFactionEntityAdded;
        public event CustomEventHandler<IFactionManager, EntityEventArgs<IFactionEntity>> OwnFactionEntityRemoved;

        private void RaiseOwnFactionEntityAdded (EntityEventArgs<IFactionEntity> args)
        {
            var handler = OwnFactionEntityAdded;
            handler?.Invoke(this, args);
        }
        private void RaiseOwnFactionEntityRemoved (EntityEventArgs<IFactionEntity> args)
        {
            var handler = OwnFactionEntityRemoved;
            handler?.Invoke(this, args);
        }
        #endregion

        #region Initializing/Terminating
        public void Init (IGameManager gameMgr, IFactionSlot slot) 
        {
            this.gameMgr = gameMgr;
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();

            this.Slot = slot;
            this.Slot.FactionSlotStateUpdated += HandleFactionSlotStateUpdated;
            this.FactionID = slot.ID;

            this.limits = new List<FactionEntityAmountLimit>();
            if (slot.Data.type.IsValid() && slot.Data.type.Limits.IsValid())
                limits = slot.Data.type.Limits
                    .Select(limit => new FactionEntityAmountLimit(definer: limit.Definer, maxAmount: limit.MaxAmount))
                    .ToList();

            factionEntities = new List<IFactionEntity>();
            factionEntityToAmount = new Dictionary<string, int>();
            factionEntityCategoryToAmount = new Dictionary<string, int>();
            factionEntityCodeDic = new Dictionary<string, List<IFactionEntity>>();

            mainEntities = new List<IFactionEntity>();

            dropOffTargets = new List<IFactionEntity>();

            units = new List<IUnit>();
            workerUnits = new List<IUnit>();
            attackUnits = new List<IUnit>();

            buildings = new List<IBuilding>();
            buildingCenters = new List<IBuilding>();

            globalEvent.UnitInitiatedGlobal += HandleUnitInitiatedGlobal;

            globalEvent.BorderActivatedGlobal += HandleBorderActivatedGlobal;
            globalEvent.BorderDisabledGlobal += HandleBorderDisabledGlobal;
            globalEvent.BuildingPlacedGlobal += HandleBuildingPlacedGlobal;
            globalEvent.BuildingBuiltGlobal += HandleBuildingBuiltGlobal;

            globalEvent.FactionEntityDeadGlobal += HandleFactionEntityDeadGlobal;

            globalEvent.EntityFactionUpdateStartGlobal += HandleEntityFactionUpdateStartGlobal;
            globalEvent.EntityFactionUpdateCompleteGlobal += HandleEntityFactionUpdateCompleteGlobal;
		}

        private void HandleFactionSlotStateUpdated(IFactionSlot slot, EventArgs args)
        {
            // Disable this component when the faction is eliminated.
            if (slot.State == FactionSlotState.eliminated)
                Disable();
        }

        private void Disable()
        {
            globalEvent.UnitInitiatedGlobal -= HandleUnitInitiatedGlobal;

            globalEvent.BorderActivatedGlobal -= HandleBorderActivatedGlobal;
            globalEvent.BorderDisabledGlobal -= HandleBorderDisabledGlobal;
            globalEvent.BuildingPlacedGlobal -= HandleBuildingPlacedGlobal;
            globalEvent.BuildingBuiltGlobal -= HandleBuildingBuiltGlobal;

            globalEvent.FactionEntityDeadGlobal -= HandleFactionEntityDeadGlobal;

            globalEvent.EntityFactionUpdateStartGlobal -= HandleEntityFactionUpdateStartGlobal;
            globalEvent.EntityFactionUpdateCompleteGlobal -= HandleEntityFactionUpdateCompleteGlobal;
        }
        #endregion

        #region Handling Events
        private void HandleBorderActivatedGlobal(IBorder border, EventArgs args)
        {
            if (!RTSHelper.IsFactionEntity(border.Building, FactionID))
                return;

            buildingCenters.Add(border.Building);
        }
        private void HandleBorderDisabledGlobal(IBorder border, EventArgs args)
        {
            if (!RTSHelper.IsFactionEntity(border.Building, FactionID))
                return;

            buildingCenters.Remove(border.Building);
        }

        private void HandleUnitInitiatedGlobal(IUnit sender, EventArgs args) => AddUnit(sender);

        private void HandleBuildingPlacedGlobal(IBuilding sender, EventArgs args) => AddBuilding(sender);

        private void HandleBuildingBuiltGlobal(IBuilding building, EventArgs args)
        {
            if (!building.IsSameFaction(FactionID))
                return;

            AddFactionEntityAmount(building);

            RaiseOwnFactionEntityAdded(new EntityEventArgs<IFactionEntity>(building));
        }

        private void HandleFactionEntityDeadGlobal(IFactionEntity factionEntity, DeadEventArgs args)
        {
            if (factionEntity.IsUnit())
                RemoveUnit(factionEntity as IUnit);
            else if (factionEntity.IsBuilding())
            {
                IBuilding building = factionEntity as IBuilding;
                if(!building.IsPlacementInstance)
                    RemoveBuilding(building);
            }
        }

        private void HandleEntityFactionUpdateStartGlobal(IEntity updatedInstance, FactionUpdateArgs args)
        {
            if (updatedInstance.IsUnit())
                RemoveUnit(updatedInstance as IUnit);
            else if (updatedInstance.IsBuilding())
                RemoveBuilding(updatedInstance as IBuilding);
        }

        private void HandleEntityFactionUpdateCompleteGlobal (IEntity updatedInstance, FactionUpdateArgs args)
        {
            //when the conversion is complete and the faction entity is assigned their new faction, add them back to the faction lists:
            if (updatedInstance.IsUnit())
                AddUnit(updatedInstance as IUnit);
            else if (updatedInstance.IsBuilding())
            {
                IBuilding building = updatedInstance as IBuilding;
                AddBuilding(building);
                AddFactionEntityAmount(building);
            }

        }
        #endregion

        #region Adding/Removing Faction Entities
        private void AddFactionEntity(IFactionEntity factionEntity)
        {
            if (factionEntity.IsDummy)
                return;

            factionEntities.Add(factionEntity);
            if(!factionEntityCodeDic.TryGetValue(factionEntity.Code, out List<IFactionEntity> entityTypeList))
            {
                entityTypeList = new List<IFactionEntity>();
                factionEntityCodeDic.Add(factionEntity.Code, entityTypeList);
            }
            entityTypeList.Add(factionEntity);

            if (factionEntity.IsMainEntity)
                mainEntities.Add(factionEntity);

            if (factionEntity.DropOffTarget.IsValid())
                dropOffTargets.Add(factionEntity);

            UpdateLimit(factionEntity.Code, factionEntity.Category, increment: true);
        }

        private void RemoveFactionEntity (IFactionEntity factionEntity)
        {
            if (factionEntity.IsDummy)
                return;

            factionEntities.Remove(factionEntity);
            factionEntityCodeDic[factionEntity.Code].Remove(factionEntity);

            if (factionEntity.IsMainEntity)
                mainEntities.Remove(factionEntity);

            if (factionEntity.DropOffTarget.IsValid())
                dropOffTargets.Remove(factionEntity);

            UpdateLimit(factionEntity.Code, factionEntity.Category, increment:false);

            // Check if the faction doesn't have any buildings/units anymore and trigger the faction defeat in that case
            CheckFactionDefeat(); 
        }

        private void AddUnit (IUnit unit)
        {
            if(!RTSHelper.IsFactionEntity(unit, FactionID))
                return;

            AddFactionEntity(unit);
            AddFactionEntityAmount(unit);

			units.Add (unit);
            if (unit.AttackComponents.Count > 0)
                attackUnits.Add(unit);
            if (unit.BuilderComponent.IsValid() || unit.CollectorComponent.IsValid())
                workerUnits.Add(unit);

            RaiseOwnFactionEntityAdded(new EntityEventArgs<IFactionEntity>(unit));
        }

		private void RemoveUnit (IUnit unit)
		{
            if(!RTSHelper.IsFactionEntity(unit, FactionID))
                return;

            RemoveFactionEntity(unit);
            RemoveFactionEntityAmount(unit);

			units.Remove (unit);
            if (unit.AttackComponents.Count > 0)
                attackUnits.Remove(unit);
            if (unit.BuilderComponent.IsValid() || unit.CollectorComponent.IsValid())
                workerUnits.Remove(unit);

            RaiseOwnFactionEntityRemoved(new EntityEventArgs<IFactionEntity>(unit));
        }

        private void AddBuilding (IBuilding building)
		{
            if(!RTSHelper.IsFactionEntity(building, FactionID))
                return;

            AddFactionEntity(building);

			buildings.Add (building);
		}

		private void RemoveBuilding (IBuilding building)
		{
            if(!RTSHelper.IsFactionEntity(building, FactionID))
                return;

            RemoveFactionEntity(building);
            RemoveFactionEntityAmount(building);

			buildings.Remove (building);

            RaiseOwnFactionEntityRemoved(new EntityEventArgs<IFactionEntity>(building));
        }
        #endregion

        #region Faction Entity Code/Category Amount Updating
        private void AddFactionEntityAmount(IFactionEntity factionEntity)
        {
            if (factionEntity.IsDummy
                || !factionEntity.CanLaunchTask)
                return;

            foreach (string category in factionEntity.Category)
            {
                if (!factionEntityCategoryToAmount.ContainsKey(category))
                    factionEntityCategoryToAmount.Add(category, 0);

                factionEntityCategoryToAmount[category] += 1;
            }

            if (!factionEntityToAmount.ContainsKey(factionEntity.Code))
                factionEntityToAmount.Add(factionEntity.Code, 0);
            factionEntityToAmount[factionEntity.Code] += 1;
        }

        private void RemoveFactionEntityAmount(IFactionEntity factionEntity)
        {
            if (factionEntity.IsDummy
                || (!factionEntity.CanLaunchTask && !factionEntity.Health.IsDead))
                return;

            // check whether the category/code exist in the dictionaries
            // in case of a building that has not been constructed yet, it is not present in these two dictionaries
            foreach (string category in factionEntity.Category)
            {
                if(factionEntityCategoryToAmount.ContainsKey(category))
                    factionEntityCategoryToAmount[category] = factionEntityCategoryToAmount[category] - 1;
            }

            if(factionEntityToAmount.ContainsKey(factionEntity.Code))
                factionEntityToAmount[factionEntity.Code] = factionEntityToAmount[factionEntity.Code] - 1;
        }
        #endregion

        #region Handling Faction Defeat Conditions
        // A method that checks if the faction doesn't have any more units/buildings and trigger a faction defeat in that case.
        private void CheckFactionDefeat ()
        {
            if (mainEntities.Count == 0)
                globalEvent.RaiseFactionSlotDefeatConditionTriggeredGlobal(Slot, new DefeatConditionEventArgs(DefeatConditionType.eliminateMain));

            if (factionEntities.Count == 0)
                globalEvent.RaiseFactionSlotDefeatConditionTriggeredGlobal(Slot, new DefeatConditionEventArgs(DefeatConditionType.eliminateAll));
        }
        #endregion

        #region Handling Faction Limits
        public bool AssignLimits (IEnumerable<FactionEntityAmountLimit> newLimits)
        {
            if (!newLimits.IsValid())
                return false;

            limits = newLimits.ToList();

            return true;
        }

        public bool HasReachedLimit(IEntity entity)
            => HasReachedLimit(entity.Code, entity.Category);

        public bool HasReachedLimit(string code, IEnumerable<string> category) 
            => limits
                .Any(limit => limit.IsMaxAmountReached(code, category));
        public bool HasReachedLimit(string code, string category) 
            => limits
                .Any(limit => limit.IsMaxAmountReached(code, Enumerable.Repeat(category, 1)));

        public void UpdateLimit(IEntity entity, bool increment)
            => UpdateLimit(entity.Code, entity.Category, increment);

        private void UpdateLimit(string code, IEnumerable<string> category, bool increment)
        {
            foreach(FactionEntityAmountLimit limit in limits)
                if (limit.Contains(code, category))
                {
                    limit.Update(increment ? 1 : -1);
                    return;
                }
        }
        #endregion
    }
}
