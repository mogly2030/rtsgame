using RTSEngine.Cameras;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Selection;
using System.Linq;
using UnityEngine;

namespace RTSEngine.UI
{
    public class UtilityButtonsUIHandler : MonoBehaviour, IPostRunGameService
    {
        public enum UtilityButtonType { selectAttackUnits, selectIdleWorkers, followSelected, lookAtSpawn }

        [SerializeField, Tooltip("Tooltip used for the button to select attack units.")]
        protected string selectAttackUnitsTooltip = "Select all attack units.";
        [SerializeField, Tooltip("Tooltip used for the button to select attack units.")]
        protected string selectIdleWorkersTooltip = "Select all idle workers.";

        [SerializeField, Tooltip("Tooltip used for the button that follows selected units.")]
        protected string followSelectedTooltip = "Follow selected entities.";

        [SerializeField, Tooltip("Tooltip used for the button that looks at the local player's initial faction spawn position.")]
        protected string lookAtFactionSpawnTooltip = "Look at faction spawn position.";

        // Game services
        protected IGameManager gameMgr { private set; get; } 
        protected IGameLoggingService logger { private set; get; }
        protected ISelectionManager selectionMgr { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IMainCameraController mainCamCtrl { private set; get; }

        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;

            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>(); 
            this.mainCamCtrl = gameMgr.GetService<IMainCameraController>(); 
        }

        public void SelectAttackUnits()
        {
            if (!gameMgr.LocalFactionSlot.IsValid())
                return;

            var attackUnits = gameMgr.LocalFactionSlot.FactionMgr.GetAttackUnits();
            if (!attackUnits.Any())
                return;

            selectionMgr.RemoveAll();
            selectionMgr.Add(attackUnits);
        }

        public void DisplaySelectAttackUnitsTooltip() => ShowTooltip(UtilityButtonType.selectAttackUnits);

        public void DisplayFollowSelectedTooltip() => ShowTooltip(UtilityButtonType.followSelected);

        public void LookAtFactionSpawn()
        {
            if (!gameMgr.LocalFactionSlot.IsValid())
                return;

            mainCamCtrl.PanningHandler.LookAt(gameMgr.LocalFactionSlot.FactionSpawnPosition, smooth: false);
        }

        public void DisplayLookAtFactionSpawnTooltip() => ShowTooltip(UtilityButtonType.lookAtSpawn);

        public void SelectIdleWorkerUnits()
        {
            if (!gameMgr.LocalFactionSlot.IsValid())
                return;

            var idleWorkerUnits = gameMgr.LocalFactionSlot.FactionMgr.WorkerUnits
                .Where(unit => unit.IsIdle);

            if (!idleWorkerUnits.Any())
                return;

            selectionMgr.RemoveAll();
            selectionMgr.Add(idleWorkerUnits);
        }

        public void DisplaySelectIdleWorkersTooltip() => ShowTooltip(UtilityButtonType.selectIdleWorkers);

        private void ShowTooltip(UtilityButtonType buttonType)
        {
            string tooltipText = string.Empty;
            switch(buttonType)
            {
                case UtilityButtonType.followSelected:
                    tooltipText = followSelectedTooltip;
                    break;
                case UtilityButtonType.selectAttackUnits:
                    tooltipText = selectAttackUnitsTooltip;
                    break;
                case UtilityButtonType.selectIdleWorkers:
                    tooltipText = selectIdleWorkersTooltip;
                    break;
                case UtilityButtonType.lookAtSpawn:
                    tooltipText = lookAtFactionSpawnTooltip;
                    break;
            }

            if (!string.IsNullOrEmpty(tooltipText))
            {
                globalEvent.RaiseShowTooltipGlobal(
                    this,
                    new MessageEventArgs(MessageType.info, message: tooltipText));
            }

        }

        public void HideTooltip ()
        {
            globalEvent.RaiseHideTooltipGlobal(this);
        }
    }
}
