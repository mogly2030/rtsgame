using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Event;
using RTSEngine.Faction;
using RTSEngine.Game;
using RTSEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RTSEngine.ResourceExtension
{
    public class FactionResourceHandler : IFactionResourceHandler
    {
        #region Attributes
        public int FactionID { private set; get; }
        public ResourceTypeInfo Type { private set; get; }

        public int Amount { private set; get; }
        public int ReservedAmount { private set; get; }

        public int Capacity { private set; get; }
        public int ReservedCapacity { private set; get; }
        public int FreeAmount => Capacity - Amount;

        private List<IResourceCollector> collectors;
        public IReadOnlyList<IResourceCollector> Collectors => collectors;
        private List<IResourceGenerator> generators;
        public IReadOnlyList<IResourceGenerator> Generators => generators;
        public int ProducerCount => collectors.Count + generators.Count;

        // Game services
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IFactionResourceHandler, ResourceUpdateEventArgs> FactionResourceAmountUpdated;

        private void RaiseFactionResourceAmountUpdated(ResourceUpdateEventArgs args)
        {
            var handler = FactionResourceAmountUpdated;

            handler?.Invoke(this, args);
        }

        public event CustomEventHandler<IFactionResourceHandler, EventArgs> FactionResourceProducersUpdated;

        private void RaiseFactionResourceProducersUpdated()
        {
            var handler = FactionResourceProducersUpdated;

            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        public FactionResourceHandler(
            IFactionSlot factionSlot,
            IGameManager gameMgr,
            ResourceTypeInfo data,
            ResourceTypeValue startingAmount)
        {
            this.FactionID = factionSlot.ID;
            this.Type = data;

            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();
            this.logger = gameMgr.GetService<IGameLoggingService>(); 

            Amount = startingAmount.amount;
            Capacity = startingAmount.capacity;

            collectors = new List<IResourceCollector>();
            generators = new List<IResourceGenerator>();

            globalEvent.ResourceGeneratorInitGlobal += HandleResourceGeneratorInitGlobal;
            factionSlot.FactionMgr.OwnFactionEntityAdded += HandleOwnFactionEntityAdded;
        }
        #endregion

        #region Updating Amount
        public void UpdateAmount(ResourceTypeValue updateValue, out int restAmount)
        {
            restAmount = 0;

            if (Type.HasCapacity && Type.CapacityType == ResourceCapacityType.simpleLimit
                && Amount + updateValue.amount > Capacity + updateValue.capacity)
            {
                int allowedAmount = Capacity + updateValue.capacity - Amount;
                restAmount = updateValue.amount - allowedAmount;
                updateValue.amount = allowedAmount;
            }

            Capacity += updateValue.capacity;
            Amount += updateValue.amount;

            OnAmountUpdated();

            ResourceUpdateEventArgs eventArgs = new ResourceUpdateEventArgs(
                    Type,
                    updateValue);

            globalEvent.RaiseFactionSlotResourceAmountUpdatedGlobal(FactionID.ToFactionSlot(), eventArgs);
            RaiseFactionResourceAmountUpdated(eventArgs);
        }

        public void SetAmount(ResourceTypeValue setValue, out int restAmount)
        {
            UpdateAmount(
                new ResourceTypeValue 
                {
                    amount = -Amount + setValue.amount,
                    capacity = -Capacity + setValue.capacity
                }, out restAmount);
        }

        private void OnAmountUpdated()
        {
            if (Amount < 0)
            {
                //logger.LogError($"[FactionResourceHandler - Faction ID: {factionID} - Resource Type: {Type.Key}] Property 'Amount' has been updated to a negative value. This is not allowed. Follow error trace to see how we got here.");
                Amount = 0;
            }
            if (Capacity < 0)
            {
                //logger.LogError($"[FactionResourceHandler - Faction ID: {factionID} - Resource Type: {Type.Key}] Property 'Capacity' has been updated to a negative value. This is not allowed. Follow error trace to see how we got here.");
                Capacity = 0;
            }
        }
        #endregion

        #region Reserving Amount
        public void SetReserveAmount(ResourceTypeValue setReserveValue)
        {
            ReserveAmount(new ResourceTypeValue
            {
                amount = -ReservedAmount + setReserveValue.amount,
                capacity = -ReservedCapacity + setReserveValue.capacity
            });
        }

        public void ReserveAmount(ResourceTypeValue reserveValue)
        {
            ReservedCapacity += reserveValue.capacity;
            ReservedAmount += reserveValue.amount;

            OnReservedUpdated();
        }
        #endregion

        #region Releasing Amount
        public void ReleaseAmount (ResourceTypeValue reserveValue)
        {
            ReservedCapacity -= reserveValue.capacity;
            ReservedAmount -= reserveValue.amount;

            OnReservedUpdated();
        }

        private void OnReservedUpdated()
        {
            if (ReservedAmount < 0)
            {
                //logger.LogError($"[FactionResourceHandler - Faction ID: {factionID} - Resource Type: {Type.Key}] Property 'ReservedAmount' has been updated to a negative value. This is not allowed. Follow error trace to see how we got here.");
                ReservedAmount = 0;
            }
            if (ReservedCapacity < 0)
            {
                //logger.LogError($"[FactionResourceHandler - Faction ID: {factionID} - Resource Type: {Type.Key}] Property 'ReservedCapacity' has been updated to a negative value. This is not allowed. Follow error trace to see how we got here.");
                ReservedCapacity = 0;
            }
        }
        #endregion

        #region Tracking Resource Generators
        private void HandleResourceGeneratorInitGlobal(IResourceGenerator generator, EventArgs args)
        {
            if (!generator.Entity.IsSameFaction(FactionID))
                return;

            foreach(var resource in generator.Resources)
            {
                if(resource.type == Type)
                {
                    generators.Add(generator);

                    generator.Entity.Health.EntityDead += HandleResourceGeneratorDead;

                    RaiseFactionResourceProducersUpdated();
                    break;
                }
            }
        }

        private void HandleResourceGeneratorDead(IEntity entity, DeadEventArgs args)
        {
            foreach(var generator in (entity as IFactionEntity).ResourceGenerators)
            {
                foreach (var resource in generator.Resources)
                {
                    if (resource.type == Type)
                    {
                        generators.Remove(generator);

                        generator.Entity.Health.EntityDead -= HandleResourceGeneratorDead;

                        RaiseFactionResourceProducersUpdated();
                        break;
                    }
                }
            }
        }
        #endregion

        #region Tracking Resource Collectors
        private void HandleOwnFactionEntityAdded(IFactionManager factionMgr, EntityEventArgs<IFactionEntity> args)
        {
            if (!args.Entity.IsUnit())
                return;

            IUnit unit = args.Entity as IUnit;
            if (!unit.CollectorComponent.IsValid()
                || !unit.CollectorComponent.IsResourceTypeCollectable(Type))
                return;

            unit.CollectorComponent.TargetUpdated += HandleResourceCollectorTargetUpdated;
            unit.CollectorComponent.TargetStop += HandleResourceCollectorTargetStopped;

            unit.Health.EntityDead += HandleResourceCollectorDead;
        }

        private void HandleResourceCollectorDead(IEntity entity, DeadEventArgs args)
        {
            IUnit unit = entity as IUnit;
            unit.CollectorComponent.TargetUpdated -= HandleResourceCollectorTargetUpdated;
            unit.CollectorComponent.TargetStop -= HandleResourceCollectorTargetStopped;

            unit.Health.EntityDead -= HandleResourceCollectorDead;
        }

        private void HandleResourceCollectorTargetUpdated(IEntityTargetComponent collectorComp, TargetDataEventArgs args)
        {
            if (!(args.Data.instance as IResource).IsValid() || (args.Data.instance as IResource).ResourceType != Type)
                return;

            collectors.Add((collectorComp.Entity as IUnit).CollectorComponent);
            RaiseFactionResourceProducersUpdated();
        }

        private void HandleResourceCollectorTargetStopped(IEntityTargetComponent collectorComp, TargetDataEventArgs args)
        {
            if (!(args.Data.instance as IResource).IsValid() || (args.Data.instance as IResource).ResourceType != Type)
                return;

            collectors.Remove((collectorComp.Entity as IUnit).CollectorComponent);
            RaiseFactionResourceProducersUpdated();
        }
        #endregion
    }
}
