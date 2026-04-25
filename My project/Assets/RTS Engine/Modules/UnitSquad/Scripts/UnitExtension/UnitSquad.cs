using System;
using System.Collections.Generic;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Selection;

namespace RTSEngine.UnitExtension
{
    public class UnitSquad : IUnitSquad
    {
        #region Attributes
        public int ID { get; }

        public string UnitCode { get; private set; }
        public int FactionID { get; private set; }

        public int SpawnCount { get; private set; }
        private List<IUnit> units;
        public IReadOnlyList<IUnit> Units => units;
        public int CurrentCount => units.Count;

        // Selection
        public bool IsSelected { private set; get; }
        public bool IsSelectedOnly { private set; get; }
        private bool lockSelection = false;

        // Health
        public int CurrHealth { private set; get; }
        public int MaxHealth { private set; get; }
        public bool IsDead => SpawnCount > 0 && CurrentCount == 0;

        protected IGameManager gameMgr { private set; get; }
        protected ISelectionManager selectionMgr { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IUnitSquad, EventArgs> SquadUpdated;
        private void RaiseSquadUpdated()
        {
            var handler = SquadUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event CustomEventHandler<IUnitSquad, EventArgs> SquadDead;
        private void RaiseSquadDead()
        {
            var handler = SquadDead;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event CustomEventHandler<IUnitSquad, HealthUpdateArgs> SquadHealthUpdated;
        public event CustomEventHandler<IUnitSquad, HealthUpdateArgs> SquadMaxHealthUpdated;
        private void RaiseSquadHealthUpdated(HealthUpdateArgs args)
        {
            var handler = SquadHealthUpdated;
            handler?.Invoke(this, args);
        }
        private void RaiseSquadMaxHealthUpdated(HealthUpdateArgs args)
        {
            var handler = SquadMaxHealthUpdated;
            handler?.Invoke(this, args);
        }
        #endregion

        #region Initializing/Terminating
        public UnitSquad(IGameManager gameMgr, int ID, string unitCode, int factionID, int spawnCount)
        {
            this.gameMgr = gameMgr;
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();

            this.ID = ID;
            this.UnitCode = unitCode;
            this.FactionID = factionID;
            this.SpawnCount = spawnCount;

            this.units = new List<IUnit>();

            IsSelected = false;
            IsSelectedOnly = false;
            lockSelection = false;

            CurrHealth = 0;
            MaxHealth = 0;
        }

        public void Destroy()
        {
            Deselect();

            while (units.Count > 0)
                Remove(units[0]);
        }
        #endregion

        #region Adding/Removing Units
        public bool Add(IUnit unit)
        {
            if (!unit.IsValid()
                || units.Contains(unit)
                || unit.Code != UnitCode
                || unit.FactionID != FactionID
                || unit.Squad.IsValid())
            {
                return false;
            }

            units.Add(unit);

            unit.Selection.Selected += HandleUnitSelected;
            unit.Selection.Deselected += HandleUnitDeselected;

            unit.Health.EntityHealthUpdated += HandleUnitHealthUpdated;
            unit.Health.EntityMaxHealthUpdated += HandleUnitMaxHealthUpdated;
            unit.Health.EntityDead += HandleUnitDead;

            CurrHealth += unit.Health.CurrHealth;
            MaxHealth += unit.Health.MaxHealth;

            RaiseSquadUpdated();

            return true;
        }

        private bool Remove(IUnit unit)
        {
            if (!unit.IsValid()
                || !units.Contains(unit))
            {
                return false;
            }

            units.Remove(unit);

            unit.Selection.Selected -= HandleUnitSelected;
            unit.Selection.Deselected -= HandleUnitDeselected;

            unit.Health.EntityHealthUpdated -= HandleUnitHealthUpdated;
            unit.Health.EntityMaxHealthUpdated -= HandleUnitMaxHealthUpdated;
            unit.Health.EntityDead -= HandleUnitDead;

            RaiseSquadUpdated();

            if (CurrentCount == 0)
            {
                if (IsSelected)
                    Deselect();

                RaiseSquadDead();
            }

            return true;
        }

        public bool Contains(IUnit unit)
        {
            return units.Contains(unit);
        }
        #endregion

        #region Handling Unit Events: Dead, Health
        private void HandleUnitHealthUpdated(IEntity entity, HealthUpdateArgs args)
        {
            CurrHealth += args.Value;
            RaiseSquadHealthUpdated(args);
        }

        private void HandleUnitMaxHealthUpdated(IEntity entity, HealthUpdateArgs args)
        {
            MaxHealth -= args.Value;
            MaxHealth += entity.Health.MaxHealth;
            RaiseSquadMaxHealthUpdated(args);
        }

        private void HandleUnitDead(IEntity entity, DeadEventArgs args)
        {
            Remove(entity as IUnit);
        }
        #endregion

        #region Handling Selection
        private void HandleUnitSelected(IEntity entity, EntitySelectionEventArgs args)
        {
            if (!lockSelection && args.SelectedType == SelectedType.newlySelected)
                Select();
        }

        private void HandleUnitDeselected(IEntity entity, EntityDeselectionEventArgs args)
        {
            if (args.DeselectedType == DeselectedType.all)
            {
                if(IsSelected)
                {
                    IsSelected = false;
                    IsSelectedOnly = false;

                    globalEvent.RaiseUnitSquadDeselectedGlobal(this, EventArgs.Empty);
                }
                return;
            }
            bool wasSelected = IsSelected;

            if (!lockSelection)
            {
                Deselect();
            }

            if (entity.Health.IsDead && wasSelected)
                Select();
        }

        public void Select()
        {
            lockSelection = true;
            selectionMgr.Add(units);
            lockSelection = false;

            IsSelected = true;
            IsSelectedOnly = selectionMgr.Count == CurrentCount;

            globalEvent.RaiseUnitSquadSelectedGlobal(this, EventArgs.Empty);
        }

        public void Deselect()
        {
            lockSelection = true;
            selectionMgr.Remove(units);
            lockSelection = false;

            IsSelected = false;
            IsSelectedOnly = false;

            globalEvent.RaiseUnitSquadDeselectedGlobal(this, EventArgs.Empty);
        }
        #endregion
    }
}
