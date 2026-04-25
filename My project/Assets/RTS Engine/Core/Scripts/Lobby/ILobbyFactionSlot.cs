using System;

using RTSEngine.Event;
using RTSEngine.Faction;

namespace RTSEngine.Lobby
{
    public interface ILobbyFactionSlot : IMonoBehaviour
    {
        bool IsInitialized { get; }

        FactionSlotRole Role { get; }
        FactionSlotData Data { get; }
        IFactionSlot GameFactionSlot { get; }

        event CustomEventHandler<ILobbyFactionSlot, EventArgs> FactionSlotInitialized;
        event CustomEventHandler<ILobbyFactionSlot, EventArgs> FactionRoleUpdated;
        void SetInteractable(bool interactable);


        void OnGameBuilt(IFactionSlot gameFactionSlot);
    }
}
