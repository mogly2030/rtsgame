using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.ResourceExtension;
using System.Collections.Generic;

namespace RTSEngine.Faction
{
    public interface IFactionManager
    {
        int FactionID { get; }
        IFactionSlot Slot { get; }

        IEnumerable<IFactionEntity> FactionEntities { get; }
        IReadOnlyDictionary<string, int> FactionEntityToAmount { get; }
        IReadOnlyList<IFactionEntity> GetFactionEntitiesListByCode(string code);
        IReadOnlyDictionary<string, int> FactionEntityCategoryToAmount { get; }

        IEnumerable<IFactionEntity> MainEntities { get; }

        IEnumerable<IUnit> Units { get; }
        IEnumerable<IUnit> GetAttackUnits(float range = 1);

        IReadOnlyList<IBuilding> Buildings { get; }
        IReadOnlyList<IBuilding> BuildingCenters { get; }
        IReadOnlyList<IFactionEntity> DropOffTargets { get; }
        IReadOnlyList<IUnit> WorkerUnits { get; }

        event CustomEventHandler<IFactionManager, EntityEventArgs<IFactionEntity>> OwnFactionEntityAdded;
        event CustomEventHandler<IFactionManager, EntityEventArgs<IFactionEntity>> OwnFactionEntityRemoved;

        void Init(IGameManager gameMgr, IFactionSlot slot);

        bool AssignLimits(IEnumerable<FactionEntityAmountLimit> newLimits);
        bool HasReachedLimit(IEntity entity);
        bool HasReachedLimit(string code, string category);
        bool HasReachedLimit(string code, IEnumerable<string> category);
    }
}
