using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Faction;
using RTSEngine.Game;
using RTSEngine.NPC.Event;
using System;

namespace RTSEngine.NPC
{
    public abstract class NPCRegulator<T> : INPCRegulator where T : IFactionEntity
    {
        #region Attributes 
        public IFactionEntity Prefab { private set; get; }

        public int Count { private set; get; }
        /// <summary>
        /// Amount of the regulated faction entity type that the NPC faction aims to reach.
        /// </summary>
        public int TargetCount { private set; get; }
        public bool HasTargetCount => Count >= TargetCount;

        // Inferred from the provided regulator data
        public int MinTargetAmount { private set; get; }
        public int MaxTargetAmount { private set; get; }

        public bool HasReachedMaxAmount => Count >= MaxTargetAmount || factionMgr.HasReachedLimit(Prefab.Code, Prefab.Category) || CurrPendingAmount >= MaxPendingAmount;
        public bool HasReachedMinAmount => Count >= MinTargetAmount;

        // Current spawned instances of the regulated prefabs
        private List<T> instances;
        public IReadOnlyList<T> Instances => instances.AsReadOnly();
        private List<T> idleInstances;
        private List<T> nonIdleInstances;
        public IReadOnlyList<T> IdleInstances => idleInstances;
        public IReadOnlyList<T> NonIdleInstances => nonIdleInstances;
        public IReadOnlyList<T> InstancesIdleFirst {
            get
            {
                List<T> returnList = new List<T>(idleInstances);
                returnList.AddRange(nonIdleInstances);
                return returnList.AsReadOnly();
            }
        }
        // Inferred from the provided regulator data
        public int MaxPendingAmount { private set; get; }
        public int CurrPendingAmount { private set; get; }

        protected IFactionManager factionMgr { private set; get; }

        protected INPCManager npcMgr { private set; get; }

        protected IGameManager gameMgr { private set; get; }

        // Game services
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<NPCRegulator<T>, NPCRegulatorUpdateEventArgs> AmountUpdated;

        private void RaiseAmountUpdated(int count, int pendingAmount)
        {
            Count += count;
            CurrPendingAmount += pendingAmount;

            NPCRegulatorUpdateEventArgs args = new NPCRegulatorUpdateEventArgs(count, pendingAmount);

            var handler = AmountUpdated;
            handler?.Invoke(this, args);
        }
        #endregion

        #region Initializing/Terminating
        public NPCRegulator(NPCRegulatorData data, T prefab, IGameManager gameMgr, INPCManager npcMgr)
        {
            this.gameMgr = gameMgr;
            this.globalEvent = this.gameMgr.GetService<IGlobalEventPublisher>();

            this.npcMgr = npcMgr;
            this.factionMgr = npcMgr.FactionMgr;

            this.Prefab = prefab;

            instances = new List<T>();
            idleInstances = new List<T>();
            nonIdleInstances = new List<T>();

            MaxTargetAmount = data.MaxAmount;
            MinTargetAmount = data.MinAmount;
            MaxPendingAmount = data.MaxPendingAmount;

            Count = 0;
            TargetCount = MinTargetAmount;

            globalEvent.EntityFactionUpdateCompleteGlobal += HandleEntityFactionUpdateCompleteGlobal;
        }

        public void Disable()
        {
            globalEvent.EntityFactionUpdateCompleteGlobal -= HandleEntityFactionUpdateCompleteGlobal;

            OnDisabled();
        }

        protected virtual void OnDisabled() { }
        #endregion

        #region Handling Events: Faction Entity Death/Update
        private void HandleFactionEntityDead(IEntity factionEntity, DeadEventArgs e)
        {
            RemoveExisting((T)factionEntity);
        }

        private void HandleEntityFactionUpdateCompleteGlobal(IEntity updatedInstance, FactionUpdateArgs args)
        {
            if (!(updatedInstance is T))
                return;

            T factionEntity = (T)updatedInstance;

            // Remove the old instance and add the upgrade target one
            RemoveExisting(factionEntity);
            AddNewlyCreated(factionEntity);
        }
        #endregion

        #region Handling Events: Faction Entity Idle Enter/Exit
        private void HandleFactionEntityEnterIdle(IEntity entity, EventArgs args)
        {
            T factionEntity = (T)entity;
            idleInstances.Add(factionEntity);
            nonIdleInstances.Remove(factionEntity);
        }

        private void HandleFactionEntityExitIdle(IEntity entity, EventArgs args)
        {
            T factionEntity = (T)entity;
            idleInstances.Remove(factionEntity);
            nonIdleInstances.Add(factionEntity);
        }
        #endregion

        #region Adding/Removing Instances
        protected void AddExisting(T factionEntity)
        {
            if (!CanInstanceBeRegulated(factionEntity))
                return;

            instances.Add(factionEntity);
            RaiseAmountUpdated(count: 1, pendingAmount: 0);

            factionEntity.Health.EntityDead += HandleFactionEntityDead;
            factionEntity.EntityEnterIdle += HandleFactionEntityEnterIdle;
            factionEntity.EntityExitIdle += HandleFactionEntityExitIdle;

            if (!factionEntity.IsIdle)
                idleInstances.Add(factionEntity);
            else
                nonIdleInstances.Add(factionEntity);
        }

        protected void AddNewlyCreated(T factionEntity)
        {
            if (!CanInstanceBeRegulated(factionEntity))
                return;

            instances.Add(factionEntity);
            RaiseAmountUpdated(count: 1, pendingAmount: 0);

            factionEntity.Health.EntityDead += HandleFactionEntityDead;
            factionEntity.EntityEnterIdle += HandleFactionEntityEnterIdle;
            factionEntity.EntityExitIdle += HandleFactionEntityExitIdle;
        }

        protected void AddPending(T pendingFactionEntity)
        {
            if (!CanPendingInstanceBeRegulated(pendingFactionEntity))
                return;

            RaiseAmountUpdated(count: 0, pendingAmount: 1);
        }

        protected void RemoveExisting(T factionEntity)
        {
            if (!CanInstanceBeRegulated(factionEntity)
                || !instances.Remove(factionEntity))
                return;

            idleInstances.Remove(factionEntity);
            nonIdleInstances.Remove(factionEntity);

            factionEntity.Health.EntityDead -= HandleFactionEntityDead;
            factionEntity.EntityEnterIdle -= HandleFactionEntityEnterIdle;
            factionEntity.EntityExitIdle -= HandleFactionEntityExitIdle;

            RaiseAmountUpdated(count: -1, pendingAmount: 0);
        }

        protected void RemovePending(T pendingFactionEntity)
        {
            if (!CanPendingInstanceBeRegulated(pendingFactionEntity))
                return;

            RaiseAmountUpdated(count: 0, pendingAmount: -1);
        }
        #endregion

        #region Handling Target Count
        public void UpdateTargetCount(int newTargetCount)
        {
            TargetCount = Mathf.Clamp(newTargetCount, MinTargetAmount, MaxTargetAmount);

            RaiseAmountUpdated(count: 0, pendingAmount: 0);
        }
        #endregion

        #region Regulation Helper Methods
        public virtual bool CanPendingInstanceBeRegulated(T factionEntity)
        {
            return factionEntity.IsValid()
                && factionEntity.Code == Prefab.Code;
        }

        public virtual bool CanInstanceBeRegulated(T factionEntity)
        {
            return factionEntity.IsValid()
                && factionMgr.IsSameFaction(factionEntity)
                && factionEntity.Code == Prefab.Code;
        }
        #endregion
    }
}
