using RTSEngine.EntityComponent;
using RTSEngine.Event;
using System;
using System.Collections.Generic;

namespace RTSEngine.ResourceExtension
{
    public interface IFactionResourceHandler
    {
        ResourceTypeInfo Type { get; }

        int Amount { get; }
        int ReservedAmount { get; }

        int Capacity { get; }
        int ReservedCapacity { get; }
        int FreeAmount { get; }
        IReadOnlyList<IResourceCollector> Collectors { get; }
        IReadOnlyList<IResourceGenerator> Generators { get; }
        int ProducerCount { get; }
        int FactionID { get; }

        event CustomEventHandler<IFactionResourceHandler, ResourceUpdateEventArgs> FactionResourceAmountUpdated;
        event CustomEventHandler<IFactionResourceHandler, EventArgs> FactionResourceProducersUpdated;

        void UpdateAmount(ResourceTypeValue updateValue, out int restAmount);
        void SetAmount(ResourceTypeValue setValue, out int restAmount);

        void ReserveAmount(ResourceTypeValue reserveValue);
        void SetReserveAmount(ResourceTypeValue setReserveValue);

        void ReleaseAmount(ResourceTypeValue reserveValue);
    }
}