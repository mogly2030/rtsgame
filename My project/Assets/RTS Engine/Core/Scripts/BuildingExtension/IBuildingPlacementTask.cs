using System.Collections.Generic;

using RTSEngine.Entities;
using RTSEngine.ResourceExtension;

namespace RTSEngine.BuildingExtension
{
    public interface IBuildingPlacementTask
    {
        IBuilding TargetObject { get; }
        IReadOnlyList<ResourceInput> RequiredResources { get; }
        IReadOnlyList<FactionEntityRequirement> FactionEntityRequirements { get; }
        int FactionID { get; }

        ErrorMessage CanStart();
        void OnStart();

        ErrorMessage CanComplete();
        void OnComplete();

        void OnCancel();
    }
}
