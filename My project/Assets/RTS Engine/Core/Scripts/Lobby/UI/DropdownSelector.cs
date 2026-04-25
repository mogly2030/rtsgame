using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Lobby.Logging;
using TMPro;
using System;

namespace RTSEngine.Lobby.UI
{
    [System.Serializable]
    public abstract class DropdownSelector<T>
    {
        protected Dictionary<int, T> elementsDic = new Dictionary<int, T>();
        public IEnumerable<string> OptionNames { private set; get; }

        // Serivces
        private ILobbyLoggingService logger;
        private ILobbyManagerUI lobbyUI;

        [SerializeField]
        private TMP_Dropdown dropdownMenu = null;
        public int a;
        public int b = 6;

        public bool Interactable
        {
            set
            {
                dropdownMenu.interactable = value;
            }

            get
            {
                return dropdownMenu.interactable;
            }
        }

        // The name of the value type that this drop down menu is handling
        private readonly string name;

        // The default value of the type to return if the drop down menu value isn't valid.
        public int CurrentOptionID => dropdownMenu.value;
        private readonly T defaultValue;
        public T CurrentValue => GetValue(dropdownMenu.value);
        public int CurrentValueIndex => dropdownMenu.value;
        public T GetValue(int index) => elementsDic.TryGetValue(index, out T returnValue) ? returnValue : defaultValue;

        public DropdownSelector(T defaultValue, string name)
        {
            this.defaultValue = defaultValue;
            this.name = name;
        }

        protected void Init(IEnumerable<string> optionNames, ILobbyManagerBase lobbyMgr)
        {
            this.OptionNames = optionNames;
            this.logger = lobbyMgr.GetService<ILobbyLoggingService>();
            this.lobbyUI = lobbyMgr.GetService<ILobbyManagerUI>();

            if (!logger.RequireValid(dropdownMenu, $"[{GetType().Name}] The drop down menu of the '{name}' hasn't been assigned."))
                return;

            dropdownMenu.onValueChanged.AddListener(OnOptionUpdated);
            dropdownMenu.ClearOptions();
            dropdownMenu.AddOptions(optionNames.ToList()); 
        }

        private void OnOptionUpdated(int optionID) => lobbyUI.OnLobbyGameDataUIUpdated();

        public void SetOption (int optionID)
        {
            dropdownMenu.value = optionID;
        }
    }
}
