using System;

using UnityEngine;

using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Controls;
using UnityEngine.UI;

namespace RTSEngine.UI
{
    public class GameMenuUIHandler : MonoBehaviour, IPreRunGameService
    {
        #region Attributes
        [SerializeField, Tooltip("Shown when the local player wins the game.")]
        private GameObject winMenu = null; 
        [SerializeField, Tooltip("Shown when the local player loses the game.")]
        private GameObject loseMenu = null; 

        [Space(), SerializeField, Tooltip("Shown when a multiplayer game is frozen.")]
        private GameObject freezeMenu = null;

        [Space(), SerializeField, Tooltip("Shown when the local player pauses the game.")]
        private GameObject pauseMenu = null;
        [SerializeField, Tooltip("Key used to toggle the pause menu during the game.")]
        private ControlType pauseKey = null;
        [SerializeField, Tooltip("Button in the pause menu that allows the player to leave the game to the main menu.")]
        private Button mainMenuButton;
        [SerializeField, Tooltip("A sub-panel in the pause menu that allows the player to confirm whether they are leaving the game or not. If assigned, enabled when the above button is clicked. Otherwise, the above button takes the player directly to the main menu.")]
        private GameObject mainMenuConfirmPanel;

        // Game services
        protected IGameManager gameMgr { private set; get; } 
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IGameControlsManager controls { private set; get; } 
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;

            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();
            this.controls = gameMgr.GetService<IGameControlsManager>(); 

            globalEvent.GameStateUpdatedGlobal += HandleGameStateUpdatedGlobal;
        }

        private void OnDestroy()
        {
            Disable();
        }
        #endregion

        #region Disabling Handler
        public void Disable()
        {
            globalEvent.GameStateUpdatedGlobal -= HandleGameStateUpdatedGlobal;
        }
        #endregion

        #region Handling Pause Menu
        public void TogglePauseMenu ()
        {
            if (gameMgr.State == GameStateType.running)
            {
                if (mainMenuConfirmPanel.IsValid())
                    mainMenuConfirmPanel.gameObject.SetActive(false);
                if (mainMenuButton)
                    mainMenuButton.gameObject.SetActive(true);

                gameMgr.SetState(GameStateType.pause);
            }
            else if (gameMgr.State == GameStateType.pause)
                gameMgr.SetState(GameStateType.running);
        }

        public void OnMainMenuButtonClick()
        {
            if (mainMenuConfirmPanel.IsValid())
            {
                mainMenuButton.gameObject.SetActive(false);
                mainMenuConfirmPanel.gameObject.SetActive(true);
            }
            else
                ConfirmMainMenu();
        }

        public void ConfirmMainMenu()
        {
            gameMgr.LeaveGame();
        }

        public void CancelMainMenu()
        {
            mainMenuConfirmPanel.gameObject.SetActive(false);
            mainMenuButton.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (controls.GetDown(pauseKey))
                TogglePauseMenu();
        }
        #endregion

        #region Handling Event: Game State Updated
        private void HandleGameStateUpdatedGlobal(IGameManager sender, EventArgs e)
        {
            UpdateMenu();
        }

        private void UpdateMenu ()
        {
            winMenu.SetActive(gameMgr.State == GameStateType.won);
            loseMenu.SetActive(gameMgr.State == GameStateType.lost);
            pauseMenu.SetActive(gameMgr.State == GameStateType.pause);
            freezeMenu.SetActive(gameMgr.State == GameStateType.frozen);
        }
        #endregion
    }
}
