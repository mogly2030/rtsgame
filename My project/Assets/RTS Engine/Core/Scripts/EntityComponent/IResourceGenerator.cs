using RTSEngine.Entities;
using RTSEngine.ResourceExtension;
using System.Collections.Generic;

namespace RTSEngine.EntityComponent
{
    public interface IResourceGenerator : IEntityComponent
    {
        IFactionEntity FactionEntity { get; }
        IReadOnlyList<ResourceInput> Resources { get; }
    }
}
