using RTSEngine.Lobby;
using RTSEngine.SinglePlayer.Lobby;

namespace RTSEngine.Singleplayer.Lobby.UI
{
    public class LocalLobbyUIManager : LobbyUIManagerBase<ILocalLobbyFactionSlot>
    {
        #region Attributes
        protected new ILocalLobbyManager lobbyMgr { private set; get; }
        #endregion

        #region Initializing/Terminating
        protected override void OnInit() 
        {
            this.lobbyMgr = base.lobbyMgr as ILocalLobbyManager;
        }
        #endregion

        #region Updating Lobby Game Data
        public override void OnLobbyGameDataUIUpdated()
        {
            lobbyMgr.UpdateLobbyGameData(UIToLobbyGameData);
        }
        #endregion
    }
}
