using RTSEngine.Lobby;

namespace RTSEngine.SinglePlayer.Lobby
{
    public interface ILocalLobbyFactionSlot : ILobbyFactionSlot
    {
        void Init(ILobbyManager<ILocalLobbyFactionSlot> manager, bool isPlayerControlled);
    }
}
