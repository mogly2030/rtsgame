using System.Collections.Generic;

using RTSEngine.Entities;

namespace RTSEngine.NPC.BuildingExtension
{
    public interface INPCBuildingConstructor : INPCComponent
    {
        IReadOnlyList<string> GetBuildingCreatorCodes(string buildingCode);
        int GetTargetBuildersAmount(IBuilding building);

        bool IsBuildingUnderConstruction(IBuilding building);

        void OnBuildingConstructionRequest(IBuilding building, int targetBuildersAmount, out int assignedBuilders, bool forceSwitch = false);
    }
}