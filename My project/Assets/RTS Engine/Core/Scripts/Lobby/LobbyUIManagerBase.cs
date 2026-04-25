using System;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Logging;
using RTSEngine.Lobby.Logging;
using UnityEngine.Serialization;
using TMPro;

namespace RTSEngine.Lobby
{
    public abstract class LobbyUIManagerBase<T> : MonoBehaviour, ILobbyManagerUI where T : ILobbyFactionSlot
    {
        #region Attributes
        [SerializeField, Tooltip("Main canvas that is the parent object of all lobby UI elements.")]
        private Canvas canvas = null;

        [SerializeField, Tooltip("Parent object of all the faction lobby slots objects.")]
        private GridLayoutGroup lobbyFactionSlotParent = null; 

        [SerializeField, Tooltip("UI Dropdown Menu used to represent the maps that can be selected in the lobby.")]
        private TMP_Dropdown mapDropdown = null;
        protected LobbyGameData UIToLobbyGameData => new LobbyGameData
        {
            mapID = mapDropdown.value,

            defeatConditionID = lobbyMgr.DefeatConditionSelector.CurrentOptionID,
            timeModifierID = lobbyMgr.TimeModifierSelector.CurrentOptionID,
            initialResourcesID = lobbyMgr.InitialResourcesSelector.CurrentOptionID
        };

        [SerializeField, Tooltip("UI Text used to display the selected map's description.")]
        private TextMeshProUGUI mapDescriptionUIText = null; 
        [SerializeField, Tooltip("UI Text used to display the selected map's min and max allowed faction amount.")]
        private TextMeshProUGUI mapFactionAmountUIText = null;

        [SerializeField, Tooltip("UI Button used to add a faction slot to the lobby."), FormerlySerializedAs("addFactionButton")]
        protected Button addNPCFactionButton = null;

        [SerializeField, Tooltip("UI Button used to start the game.")]
        protected Button startGameButton = null;

        // Services
        protected ILoggingService logger { private set; get; }

        // Other components
        protected ILobbyManager<T> lobbyMgr { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(ILobbyManagerBase manager)
        {
            this.lobbyMgr = manager as ILobbyManager<T>;

            this.logger = lobbyMgr.GetService<ILobbyLoggingService>();

            if (!logger.RequireValid(canvas, $"[{GetType().Name}] The 'Canvas' field must be assigned")
                || !logger.RequireValid(lobbyFactionSlotParent, $"[{GetType().Name}] The 'Lobby Fation Slot Parent' field must be assigned")
                || !logger.RequireValid(mapDropdown, $"[{GetType().Name}] The 'Map Dropdown menu' field must be assigned"))
                return;

            mapDropdown.onValueChanged.AddListener(OnLobbyGameDataUIUpdated);
            mapDropdown.ClearOptions();
            mapDropdown.AddOptions(lobbyMgr.Maps.Select(map => map.name).ToList());

            HandleLobbyGameDataUpdated(lobbyMgr.CurrentLobbyGameData, EventArgs.Empty);

            lobbyMgr.FactionSlotAdded += HandleFactionSlotAdded;
            lobbyMgr.LobbyGameDataUpdated += HandleLobbyGameDataUpdated;

            OnInit();
        }

        protected virtual void OnInit() { }

        private void OnDestroy()
        {
            lobbyMgr.FactionSlotAdded -= HandleFactionSlotAdded;
            lobbyMgr.LobbyGameDataUpdated -= HandleLobbyGameDataUpdated;

            OnDestroyed();
        }

        protected virtual void OnDestroyed() { }

        public void Toggle(bool show)
        {
            canvas.gameObject.SetActive(show);
        }
        #endregion

        #region General UI Handling
        public void SetInteractable (bool interactable)
        {
            mapDropdown.interactable = interactable;

            lobbyMgr.DefeatConditionSelector.Interactable = interactable;
            lobbyMgr.TimeModifierSelector.Interactable = interactable;
            lobbyMgr.InitialResourcesSelector.Interactable = interactable;

            if (startGameButton.IsValid())
            {
                startGameButton.interactable = interactable;
                startGameButton.gameObject.SetActive(interactable);
            }

            if (addNPCFactionButton.IsValid())
            {
                addNPCFactionButton.interactable = interactable;
                addNPCFactionButton.gameObject.SetActive(interactable);
            }

            OnInteractableUpdate();
        }

        protected virtual void OnInteractableUpdate() { }
        #endregion

        #region Updating Lobby Game Data
        private void OnLobbyGameDataUIUpdated(int optionID) => OnLobbyGameDataUIUpdated();

        public abstract void OnLobbyGameDataUIUpdated();

        private void HandleLobbyGameDataUpdated(LobbyGameData prevLobbyGameData, EventArgs args)
        {
            if(!lobbyMgr.IsLobbyGameDataMaster())
                mapDropdown.value = lobbyMgr.CurrentLobbyGameData.mapID;

            if(mapDescriptionUIText)
                mapDescriptionUIText.text = lobbyMgr.CurrentMap.description;
            if(mapFactionAmountUIText)
                mapFactionAmountUIText.text = $"{lobbyMgr.CurrentMap.factionsAmount.min} - {lobbyMgr.CurrentMap.factionsAmount.max}";
        }
        #endregion

        #region Updating Faction Slots
        private void HandleFactionSlotAdded(T newSlot, EventArgs args)
        {
            newSlot.transform.SetParent(lobbyFactionSlotParent.transform, false);
            newSlot.transform.localScale = Vector3.one;
        }
        #endregion
    }
}
