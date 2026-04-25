using System.Collections;

using UnityEngine;
using UnityEngine.Events;

using RTSEngine.Lobby;
using RTSEngine.Determinism;
using RTSEngine.Game;
using RTSEngine.Scene;
using UnityEngine.SceneManagement;

namespace RTSEngine.SinglePlayer.Lobby
{
    public class LocalLobbyManager : LobbyManagerBase<ILocalLobbyFactionSlot>, ILocalLobbyManager
    {
        #region Attributes
        [SerializeField, Tooltip("Scene loaded when leaving this lobby menu.")]
        private string prevScene = "main_menu";

        [SerializeField, EnforceType(typeof(ILobbyFactionSlot)), Tooltip("Prefab used to represent each faction slot in the lobby.")]
        private GameObject lobbyFactionPrefab = null;

        [Space, SerializeField, Tooltip("Delay time after the host player requests to launch the game.")]
        private float startDelayTime = 2.0f;
        private Coroutine startLobbyDelayedCoroutine;
        public override bool IsStartingLobby => !ActiveGameMgr.IsValid() && startLobbyDelayedCoroutine.IsValid();

        [SerializeField, Tooltip("Triggered when the player clicks on the 'Start Game' button when all game start conditions are met.")]
        private UnityEvent startLobbyEvent = new UnityEvent();

        [SerializeField, Tooltip("Define properties for loading target scenes from this scene.")]
        private SceneLoader sceneLoader = new SceneLoader();
        #endregion

        #region IGameBuilder
        public override bool IsMaster => true;
        public override bool CanFreezeTimeOnPause => true;

        protected override void OnGameBuiltComplete(IGameManager gameMgr)
        {
            OnInputAdderReady(new DirectInputAdder(gameMgr));
        }
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (!logger.RequireValid(lobbyFactionPrefab,
              $"[{GetType().Name}] The field 'Lobby Faction Prefab' must be assigned!"))
                return; 

            // Add the minimum amount of factions for the default map, first faction slot is reserved for player.
            for(int i = 0; i < CurrentMap.factionsAmount.min; i++)
                AddFactionSlot();
        }

        protected override void OnDestroyed() { }
        #endregion

        #region Updating Lobby Game Data
        // Only one local player in a single player lobby so they are always the game master.
        public override bool IsLobbyGameDataMaster() => true;

        protected override void OnLobbyGameDataUpdated (LobbyGameData prevLobbyGameData)
        {
            // Remove excess factions
            while (FactionSlotCount > CurrentMap.factionsAmount.max)
                RemoveFactionSlot(GetFactionSlot(FactionSlotCount - 1));

            // Add necessary factions
            while (FactionSlotCount < CurrentMap.factionsAmount.min)
                AddFactionSlot();
        }
        #endregion

        #region Adding/Removing Faction Slots
        public void AddFactionSlot ()
        {
            if (IsStartingLobby)
                return;

            if(FactionSlotCount >= CurrentMap.factionsAmount.max) 
            {
                playerMessageUIHandler.Message.Display($"Maximum factions amount {CurrentMap.factionsAmount.max} for this map has been reached.");
                return;
            }

            ILocalLobbyFactionSlot newSlot = Instantiate(lobbyFactionPrefab.gameObject).GetComponent<ILocalLobbyFactionSlot>();

            // First faction slot is the player's one.
            if (FactionSlotCount == 0)
                LocalFactionSlot = newSlot;

            base.AddFactionSlot(newSlot);
            // Make sure one faction only is player controlled
            newSlot.Init(this, isPlayerControlled: FactionSlotCount <= 1);

            playerMessageUIHandler.Message.Display($"New faction slot added!");
        }

        public override bool CanRemoveFactionSlot(ILocalLobbyFactionSlot slot)
        {
            return slot.IsValid()
                && FactionSlotCount > CurrentMap.factionsAmount.min;
        }

        public new void RemoveFactionSlot(ILocalLobbyFactionSlot slot) => base.RemoveFactionSlot(slot);

        protected override void OnFactionSlotRemoved (ILocalLobbyFactionSlot slot)
        {
            Destroy(slot.gameObject);

            playerMessageUIHandler.Message.Display($"Faction slot removed!");
        }
        #endregion

        #region Starting/Leaving Lobby
        protected override void OnPreLobbyLeave()
        {
            SceneManager.LoadScene(prevScene);
        }

        protected override void OnStartLobby()
        {
            foreach(ILocalLobbyFactionSlot slot in FactionSlots)
                slot.SetInteractable(false);

            lobbyUIMgr.SetInteractable(false);
            playerMessageUIHandler.Message.Display("Starting game...");

            startLobbyDelayedCoroutine = StartCoroutine(StartLobbyDelayed(delayTime: startDelayTime));
            startLobbyEvent.Invoke();
        }

        private IEnumerator StartLobbyDelayed(float delayTime)
        {
            yield return new WaitForSeconds(delayTime);

            sceneLoader.LoadScene(CurrentMap.sceneName, source: this);
        }

        protected override void OnStartLobbyInterrupt ()
        {
            StopCoroutine(startLobbyDelayedCoroutine);
            startLobbyDelayedCoroutine = null;

            foreach(ILocalLobbyFactionSlot slot in FactionSlots)
                slot.SetInteractable(true);

            lobbyUIMgr.SetInteractable(true);

            playerMessageUIHandler.Message.Display("Game start interrupted!");
        }
        #endregion
    }
}
