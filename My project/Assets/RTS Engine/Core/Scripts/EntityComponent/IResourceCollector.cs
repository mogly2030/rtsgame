using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.ResourceExtension;

namespace RTSEngine.EntityComponent
{
    public interface IResourceCollector : IEntityTargetComponent
    {
        TargetData<IResource> Target { get; }
        TargetData<IResource> LastTarget { get; }

        bool InProgress { get; }

        event CustomEventHandler<IResourceCollector, SetTargetInputDataEventArgs> OnTargetMaxWorkerReached;

        bool IsResourceTypeCollectable(ResourceTypeInfo resourceType);
    }
}