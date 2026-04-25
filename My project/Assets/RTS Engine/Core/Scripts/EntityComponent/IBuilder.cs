using RTSEngine.Entities;
using System.Collections.Generic;

namespace RTSEngine.EntityComponent
{
    public interface IBuilder : IEntityTargetProgressComponent, IBuildingCreator
    {
        TargetData<IBuilding> Target { get; }

        float TimeMultiplier { get; }
    }
}
