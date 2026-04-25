using RTSEngine.Lobby;

namespace RTSEngine.SinglePlayer.Lobby
{ 
    public interface ILocalLobbyManager : ILobbyManager<ILocalLobbyFactionSlot>
    {
        void UpdateLobbyGameData(LobbyGameData lobbyGameData);

        void RemoveFactionSlot(ILocalLobbyFactionSlot slot);
    }
}
