using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Lobby;
using RTSEngine.Event;
using System;
using RTSEngine.Faction;
using RTSEngine.Lobby.Logging;
using RTSEngine.Lobby.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

namespace RTSEngine.SinglePlayer.Lobby
{

    public class LocalLobbyFactionSlot : MonoBehaviour, ILocalLobbyFactionSlot
    {
        #region Attributes
        public bool IsInitialized { private set; get; } = false;

        public FactionSlotData Data => new FactionSlotData
        {
            role = Role,

            name = inputData.name,
            color = lobbyMgr.FactionColorSelector.Get(inputData.colorID),

            type = lobbyMgr.CurrentMap.GetFactionType(inputData.factionTypeID),
            npcType = lobbyMgr.CurrentMap.GetNPCType(inputData.factionTypeID, inputData.npcTypeID),

            isLocalPlayer = Role == FactionSlotRole.host
        };
        private LobbyFactionSlotInputData inputData = new LobbyFactionSlotInputData();

        private FactionSlotRole role;
        public FactionSlotRole Role => role;

        public bool IsInteractable { private set; get; }

        [SerializeField, Tooltip("UI Image to display the faction's color.")]
        private Image factionColorImage = null; 

        [SerializeField, Tooltip("UI Input Field to display and change the faction's name.")]
        private TMP_InputField factionNameInput = null; 

        [SerializeField, Tooltip("UI Dropdown menu used to display the list of possible faction types that the slot can have.")]
        private TMP_Dropdown factionTypeMenu = null;
        protected virtual string RandomFactionTypeName => "Random";

        [SerializeField, Tooltip("UI Dropdown menu used to display the list of possible NPC faction types that the slot can have")]
        private TMP_Dropdown npcTypeMenu = null; 

        [SerializeField, Tooltip("Button used to remove the faction slot from the lobby.")]
        private Button removeButton = null;

        // Active game
        public IFactionSlot GameFactionSlot { private set; get; }

        // Lobby Services
        protected ILocalLobbyManager lobbyMgr { private set; get; }
        protected ILobbyLoggingService logger { private set; get; }
        protected ILobbyManagerUI lobbyUIMgr { private set; get; }
        protected ILobbyPlayerMessageUIHandler playerMessageUIHandler { private set; get; } 
        #endregion

        #region Raising Events
        public event CustomEventHandler<ILobbyFactionSlot, EventArgs> FactionSlotInitialized;
        private void RaiseFactionSlotInitialized()
        {
            IsInitialized = true;

            var handler = FactionSlotInitialized;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event CustomEventHandler<ILobbyFactionSlot, EventArgs> FactionRoleUpdated;
        private void RaiseRoleUpdated(FactionSlotRole role)
        {
            this.role = role;

            var handler = FactionRoleUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        public void Init(ILobbyManager<ILocalLobbyFactionSlot> lobbyMgr, bool playerControlled)
        {
            this.lobbyMgr = lobbyMgr as ILocalLobbyManager;

            this.logger = lobbyMgr.GetService<ILobbyLoggingService>();
            this.lobbyUIMgr = lobbyMgr.GetService<ILobbyManagerUI>();
            this.playerMessageUIHandler = lobbyMgr.GetService<ILobbyPlayerMessageUIHandler>();

            if (!logger.RequireValid(factionColorImage, $"[{GetType().Name}] The field 'Faction Color Image' is required!")
                || !logger.RequireValid(factionNameInput, $"[{GetType().Name}] The field 'Faction Name Input' is required!")
                || !logger.RequireValid(factionTypeMenu, $"[{GetType().Name}] The field 'Faction Type Menu' is required!")
                || !logger.RequireValid(npcTypeMenu, $"[{GetType().Name}] The field 'NPC Type Menu' is required!")
                || !logger.RequireValid(removeButton, $"[{GetType().Name}] The field 'Remove Button' is required!"))
                return;

            EventTrigger factionColorImageEventTrigger = factionColorImage.GetComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerClick;
            entry.callback.AddListener((eventData) => { OnFactionColorUpdated(); });
            factionColorImageEventTrigger.triggers.Add(entry);

            factionNameInput.onEndEdit.AddListener(OnFactionNameUpdated);
            factionTypeMenu.onValueChanged.AddListener(OnFactionTypeUpdated);
            npcTypeMenu.onValueChanged.AddListener(OnNPCTypeUpdated);
            removeButton.onClick.AddListener(OnRemove);

            this.lobbyMgr.LobbyGameDataUpdated += HandleLobbyGameDataUpdated;
            this.lobbyMgr.FactionSlotAdded += HandleFactionSlotAddedOrRemoved;
            this.lobbyMgr.FactionSlotRemoved += HandleFactionSlotAddedOrRemoved;

            ResetFactionType(prevMapID: -1);

            role = playerControlled ? FactionSlotRole.host : FactionSlotRole.npc;

            // Every faction slot starts with the same default input data
            ResetInputData();
            RefreshInputDataUI();

            // By default, the faction slot is not interactable until it is validated and initialized.
            SetInteractable(true);

            RaiseFactionSlotInitialized();
            RaiseRoleUpdated(Role);
        }

        private void OnDestroy()
        {
            this.lobbyMgr.LobbyGameDataUpdated -= HandleLobbyGameDataUpdated;
            this.lobbyMgr.FactionSlotAdded -= HandleFactionSlotAddedOrRemoved;
            this.lobbyMgr.FactionSlotRemoved -= HandleFactionSlotAddedOrRemoved;
        }

        private void HandleFactionSlotAddedOrRemoved(ILobbyFactionSlot senderSlot, EventArgs args)
        {
            if (this as ILobbyFactionSlot == senderSlot)
                return;

            // Refresh the interactability of the slot UI elements (such as displaying the remove button when there are enough factions to remove).
            SetInteractable(IsInteractable); 
        }
        #endregion

        #region Handling Faction Slot Input Data
        private void ResetInputData()
        {
            inputData = new LobbyFactionSlotInputData
            {
                name = Role == FactionSlotRole.host ? "player" : "npc",

                colorID = lobbyMgr.FactionSlotCount-1,

                factionTypeID = 0,
                npcTypeID = 0
            };
        }

        private void RefreshInputDataUI ()
        {
            factionNameInput.text = inputData.name;

            factionColorImage.color = lobbyMgr.FactionColorSelector.Get(inputData.colorID);

            factionTypeMenu.value = inputData.factionTypeID;
            npcTypeMenu.value = inputData.npcTypeID;
        }
        #endregion

        #region General UI Handling
        public void SetInteractable (bool interactable)
        {
            factionNameInput.interactable = interactable; 
            factionTypeMenu.interactable = interactable;

            npcTypeMenu.gameObject.SetActive(Role == FactionSlotRole.npc);
            npcTypeMenu.interactable = interactable;

            removeButton.gameObject.SetActive(Role == FactionSlotRole.npc);
            removeButton.interactable = Role == FactionSlotRole.npc && lobbyMgr.CanRemoveFactionSlot(this);

            IsInteractable = interactable;
        }

        #endregion

        #region Updating Lobby Game Data

        private void HandleLobbyGameDataUpdated (LobbyGameData prevLobbyGameData, EventArgs args)
        {
            ResetFactionType(prevMapID:prevLobbyGameData.mapID);
        }
        #endregion

        #region Updating Faction Name
        private void OnFactionNameUpdated (string newText)
        {
            if (!IsInteractable || factionNameInput.text.Trim() == "") 
            {
                factionNameInput.text = inputData.name;
                return;
            }

            inputData.name = factionNameInput.text.Trim();
        }
        #endregion

        #region Updating Faction Type
        private void ResetFactionType(int prevMapID)
        {
            inputData.prevFactionTypeID = inputData.factionTypeID;

            List<string> factionTypeOptions = lobbyMgr.CurrentMap.GetFactionTypes().Select(type => type.Name).ToList();
            // Last faction type option in the list is the random one
            factionTypeOptions.Add(RandomFactionTypeName);

            if (inputData.isFactionTypeRandom)
            {
                factionTypeMenu.value = factionTypeOptions.Count - 1;
            }
            else
            {
                RTSHelper.UpdateDropdownValue(ref factionTypeMenu,
                    lastOption: lobbyMgr.GetMap(prevMapID).GetFactionType(inputData.factionTypeID).Name,
                    newOptions: factionTypeOptions);
                    inputData.factionTypeID = factionTypeMenu.value;
            }

            ResetNPCType(prevMapID);
        }

        private void OnFactionTypeUpdated (int newOption)
        {
            inputData.prevFactionTypeID = inputData.factionTypeID;

            if (!IsInteractable)
            {
                factionTypeMenu.value = inputData.factionTypeID;
            }
            else
            {
                // Last faction type option in the list is the random one
                if (factionTypeMenu.value == factionTypeMenu.options.Count - 1)
                {
                    inputData.factionTypeID = UnityEngine.Random.Range(0, lobbyMgr.CurrentMap.GetFactionTypes().Count);
                    inputData.isFactionTypeRandom = true;
                }
                else
                {
                    inputData.factionTypeID = factionTypeMenu.value;
                    inputData.isFactionTypeRandom = false;
                }
            }

            if(!inputData.isPrevFactionTypeRandom || !inputData.isFactionTypeRandom)
                ResetNPCType(prevMapID: -1);

            inputData.isPrevFactionTypeRandom = factionTypeMenu.value == factionTypeMenu.options.Count - 1;
        }
        #endregion

        #region Updating Color
        private void OnFactionColorUpdated ()
        {
            if(!IsInteractable)
                return;

            inputData.colorID = lobbyMgr.FactionColorSelector.GetNextIndex(inputData.colorID);
            factionColorImage.color = lobbyMgr.FactionColorSelector.Get(inputData.colorID);
        }
        #endregion

        #region Updating NPC Type
        private void ResetNPCType(int prevMapID)
        {
            RTSHelper.UpdateDropdownValue(ref npcTypeMenu,
                lastOption: lobbyMgr.GetMap(prevMapID).GetNPCType(inputData.prevFactionTypeID, inputData.npcTypeID).Name,
                newOptions: (inputData.isFactionTypeRandom
                    ? lobbyMgr.CurrentMap.GetRandomFactionTypeNPCTypes()
                    : lobbyMgr.CurrentMap.GetNPCTypes(inputData.factionTypeID))
                    .Select(type => type.Name).ToList());

            inputData.npcTypeID = npcTypeMenu.value;
        }

        private void OnNPCTypeUpdated (int newOption)
        {
            if(!IsInteractable)
            {
                npcTypeMenu.value = inputData.npcTypeID;
                return;
            }

            if(!inputData.isFactionTypeRandom)
                inputData.prevFactionTypeID = inputData.factionTypeID;

            inputData.npcTypeID = npcTypeMenu.value;
        }
        #endregion

        #region Removing Faction Slot
        public void OnRemove()
        {
            lobbyMgr.RemoveFactionSlot(this);
        }
        #endregion

        #region Handling Active Game
        public void OnGameBuilt(IFactionSlot gameFactionSlot)
        {
            this.GameFactionSlot = gameFactionSlot;
        }
        #endregion
    }
}
