using RTSEngine.Determinism;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Faction;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace RTSEngine.UI
{
    public struct FactionEntityCountUIAttributes : ITaskUIAttributes
    {
        public string code;
        public Sprite icon;
        public int amount;
    }

    public class FactionEntityCountUIHandler : BaseTaskPanelUIHandler<FactionEntityCountUIAttributes>
    {
        [SerializeField, Tooltip("What types of faction entities to display in the counter UI panel?")]
        private FactionEntityTargetPicker targetPicker;

        [SerializeField, Tooltip("Parent transform of the active task UI elements that display unit type counts.")]
        private Transform panel = null;

        private List<ITaskUI<FactionEntityCountUIAttributes>> tasks;
        private Dictionary<string, ITaskUI<FactionEntityCountUIAttributes>> codeToTask;

        protected IInputManager inputMgr { private set; get; } 

        #region Initializing/Terminating
        protected override void OnInit()
        {
            // If there is no local faction slot then we have no resources to display.
            if (!gameMgr.LocalFactionSlot.IsValid())
                return;
            else if (!logger.RequireValid(panel,
              $"[{GetType().Name}] The 'Panel' field must be assigned!"))
                return;

            this.inputMgr = gameMgr.GetService<IInputManager>(); 

            tasks = new List<ITaskUI<FactionEntityCountUIAttributes>>();
            codeToTask = new Dictionary<string, ITaskUI<FactionEntityCountUIAttributes>>();

            if (!gameMgr.LocalFactionSlot.IsValid())
                return;

            Hide();

            foreach (IFactionEntity factionEntity in gameMgr.LocalFactionSlot.FactionMgr.FactionEntities)
            {
                if (codeToTask.ContainsKey(factionEntity.Code))
                    continue;

                Show(factionEntity);
            }

            gameMgr.LocalFactionSlot.FactionMgr.OwnFactionEntityAdded += HandleLocalFactionEntityAdded;
            gameMgr.LocalFactionSlot.FactionMgr.OwnFactionEntityRemoved += HandleLocalFactionEntityRemoved;
        }
        #endregion

        private void HandleLocalFactionEntityAdded(IFactionManager sender, EntityEventArgs<IFactionEntity> args)
        {
            Show(args.Entity);
        }

        private void HandleLocalFactionEntityRemoved(IFactionManager sender, EntityEventArgs<IFactionEntity> args)
        {
            Show(args.Entity);
        }

        #region Disabling UI Panel
        public override void Disable()
        {
            if (!gameMgr.LocalFactionSlot.IsValid())
                return;

            Hide();

            gameMgr.LocalFactionSlot.FactionMgr.OwnFactionEntityAdded -= HandleLocalFactionEntityAdded;
            gameMgr.LocalFactionSlot.FactionMgr.OwnFactionEntityRemoved -= HandleLocalFactionEntityRemoved;
        }
        #endregion

        #region Adding Tasks
        private ITaskUI<FactionEntityCountUIAttributes> Add(string factionEntityCode)
        {
            ITaskUI<FactionEntityCountUIAttributes> nextTask = null;
            // Find the first available (disabled) pending task slot to use next
            foreach (var task in tasks)
                if (!task.enabled)
                {
                    nextTask = task;
                    break;
                }

            // None found? create one!
            if(!nextTask.IsValid())
                nextTask = Create(tasks, panel.transform);

            codeToTask.Add(factionEntityCode, nextTask);
            return nextTask;
        }
        #endregion

        #region Hiding/Displaying Panel
        private void Hide(string factionEntityCode)
        {
            if (!codeToTask.TryGetValue(factionEntityCode, out var task))
                return;

            task.Disable();
            codeToTask.Remove(factionEntityCode);
        }

        private void Hide()
        {
            foreach (var task in tasks)
                if (task.IsValid() && task.enabled)
                    task.Disable();

            codeToTask.Clear();
        }

        private void Show(IFactionEntity factionEntity)
        {
            if (!targetPicker.IsValidTarget(factionEntity)
                || !gameMgr.LocalFactionSlot.FactionMgr.FactionEntityToAmount.ContainsKey(factionEntity.Code))
                return;

            string factionEntityCode = factionEntity.Code;
            int amount = gameMgr.LocalFactionSlot.FactionMgr.FactionEntityToAmount[factionEntityCode];
            if (amount == 0)
            {
                Hide(factionEntityCode);
                return;
            }

            if (!codeToTask.TryGetValue(factionEntityCode, out var showTask))
                showTask = Add(factionEntityCode);

            inputMgr.TryGetEntityPrefabWithCode(factionEntityCode, out IEntity prefab);

            showTask.Reload(new FactionEntityCountUIAttributes
            {
                code = factionEntityCode,
                icon = prefab.Icon,
                amount = amount
            });
        }
        #endregion
    }
}
