using RTSEngine.Audio;
using RTSEngine.EntityComponent;
using RTSEngine.Game;
using RTSEngine.Terrain;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine.BuildingExtension
{
    public interface IBuildingPlacement : IPreRunGameService
    {
        bool IsLocalPlayerPlacingBuilding { get; }
        bool IsPlacingBuilding(int factionID);

        float BuildingPositionYOffset { get; }
        float TerrainMaxDistance { get; }

        AudioClipFetcher PlaceBuildingAudio { get; }

        IReadOnlyList<TerrainAreaType> PlacableTerrainAreas { get; }
        LayerMask PlacableLayerMask { get; }
        IReadOnlyList<TerrainAreaType> IgnoreTerrainAreas { get; }
        IGridPlacementHandler GridHandler { get; }
        IBuildingPlacementHandler LocalFactionHandler { get; }

        bool RegisterFactionPlacementHandler(int factionID, IBuildingPlacementHandler newHandler);

        bool Add(int factionID, IBuildingPlacementTask creationTask, BuildingPlacementOptions options = default);

        bool Stop(int factionID);

        ErrorMessage CanPlace(IBuildingPlacer buildingPlacer);
    }
}