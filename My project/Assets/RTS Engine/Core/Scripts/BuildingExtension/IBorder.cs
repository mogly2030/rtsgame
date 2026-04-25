using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Game;

namespace RTSEngine.BuildingExtension
{
    public interface IBorder : IMonoBehaviour
    {
        IBuilding Building { get; }
        bool IsActive { get; }

        int SortingOrder { get; }
        float Size { get; }
        float Surface { get; }

        IReadOnlyList<IBuilding> BuildingsInRange { get; }
        IReadOnlyList<IResource> ResourcesInRange { get; }
        IReadOnlyList<IEntity> EntitiesInRange { get; }

        void Init(IGameManager gameMgr, IBuilding building);
        void Disable();

        bool IsInBorder(Vector3 testPosition);
        bool IsBuildingAllowedInBorder(IBuilding building);
    }
}
