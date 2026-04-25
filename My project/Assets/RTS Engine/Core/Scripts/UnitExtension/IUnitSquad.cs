using RTSEngine.Entities;
using RTSEngine.Event;
using System;
using System.Collections.Generic;

namespace RTSEngine.UnitExtension
{
    public interface IUnitSquad
    {
        string UnitCode { get; }
        int ID { get; }
        int FactionID { get; }

        int SpawnCount { get; }
        int CurrentCount { get; }
        IReadOnlyList<IUnit> Units { get; }

        bool IsSelected { get; }
        bool IsSelectedOnly { get; }

        int CurrHealth { get; }
        int MaxHealth { get; }
        bool IsDead { get; }

        event CustomEventHandler<IUnitSquad, HealthUpdateArgs> SquadHealthUpdated;
        event CustomEventHandler<IUnitSquad, HealthUpdateArgs> SquadMaxHealthUpdated;
        event CustomEventHandler<IUnitSquad, EventArgs> SquadUpdated;
        event CustomEventHandler<IUnitSquad, EventArgs> SquadDead;

        bool Contains(IUnit unit);

        void Deselect();
        void Select();
    }
}