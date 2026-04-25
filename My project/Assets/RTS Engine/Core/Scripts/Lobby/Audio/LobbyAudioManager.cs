using RTSEngine.Audio;
using RTSEngine.Lobby.Logging;

namespace RTSEngine.Lobby.Audio
{
    public class LobbyAudioManager : AudioManagerBase, ILobbyAudioManager
    {
        #region Initializing/Terminating
        public void Init(ILobbyManagerBase lobbyMgr)
        {
            InitBase(lobbyMgr.GetService<ILobbyLoggingService>());
        }
        #endregion
    }
}
