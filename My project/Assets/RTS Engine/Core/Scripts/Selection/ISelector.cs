using RTSEngine.Entities;
using RTSEngine.Game;
using UnityEngine;

namespace RTSEngine.Selection
{
    public interface ISelector : IPreRunGameService
    {
        bool MultipleSelectionModeEnabled { get; }
        string EntitySelectionLayer { get; }
        LayerMask ClickableLayerMask { get; }
        LayerMask EntitySelectionLayerMask { get; }

        void SelectEntitisInRange(IEntity source, bool playerCommand);

        void FlashSelection(IEntity entity, bool isFriendly);

        bool Unlock(IGameService service);
        bool Lock(IGameService service);
    }
}