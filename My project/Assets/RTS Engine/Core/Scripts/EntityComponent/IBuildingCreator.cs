using System.Collections.Generic;

namespace RTSEngine.EntityComponent
{
    public interface IBuildingCreator : IEntityComponent
    {
        IReadOnlyList<BuildingCreationTask> UpgradeTargetCreationTasks { get; }
        IReadOnlyList<BuildingCreationTask> CreationTasks { get; }
    }
}