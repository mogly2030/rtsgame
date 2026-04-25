using RTSEngine.Entities;
using System.Collections.Generic;

namespace RTSEngine.EntityComponent
{
    public interface IEntityComponentGroupDisplayer
    {
        IReadOnlyList<IEntity> Entities { get; }

        IReadOnlyList<IEntityComponent> EntityComponents { get; }

        IReadOnlyList<IEntityTargetComponent> EntityTargetComponents { get; }
    }
}
