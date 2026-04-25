using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Selection;

namespace RTSEngine.UI
{
    public class SelectionGroupUIHandler : BaseTaskPanelUIHandler<SelectionGroupUIAttributes>
    {
        #region Attributes
        [SerializeField, Tooltip("Parent game object of the active selection group UI elements.")]
        private GridLayoutGroup panel = null;

        [SerializeField, Tooltip("Selection group UI data parameters.")]
        private SelectionGroupUIData taskData = new SelectionGroupUIData { description = "Right click to deselect\nLeft click to select", tooltipEnabled = true };

        [SerializeField, Tooltip("Show selection group UI tasks only the group has at least one element?")]
        private bool hideOnEmpty = true;

        // Each created multiple selection task is registered in this list.
        private List<ITaskUI<SelectionGroupUIAttributes>> tasks = null;

        // Game services
        protected ISelectionGroupHandler selectionGroupHandler { private set; get; }
        protected IGameUITextDisplayManager gameUITextDisplayer { private set; get; } 
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            this.selectionGroupHandler = gameMgr.GetService<ISelectionGroupHandler>();
            this.gameUITextDisplayer = gameMgr.GetService<IGameUITextDisplayManager>(); 
            tasks = new List<ITaskUI<SelectionGroupUIAttributes>>();

            if (!selectionGroupHandler.IsValid())
            {
                logger.LogError($"[{GetType().Name}] No service of type '{typeof(ISelectionGroupHandler).Name}' has been found! It is required for this component");

            }
            else if (!panel.IsValid())
            {
                logger.LogError($"[{GetType().Name}] The 'Panel' field must be assigned!");
                return;
            }

            foreach(ISelectionGroup group in selectionGroupHandler.Groups)
            {
                group.GroupUpdated += HandleGroupUpdated;
            }

            gameUITextDisplayer.SelectionGroupToTooltip(taskData.description, out taskData.description);

            Show();

            if (hideOnEmpty)
                Hide();
        }

        private void HandleGroupUpdated(ISelectionGroup group, EventArgs args)
        {
            if (hideOnEmpty)
            {
                if (group.Entities.Count > 0)
                    Show(group);
                else
                    Hide(group);
            }
            else
                Show(group);
        }
        #endregion

        #region Disabling UI Panel
        public override void Disable()
        {
            Hide();
        }
        #endregion

        #region Adding Tasks
        private ITaskUI<SelectionGroupUIAttributes> Add()
        {
            // Find the first available (disabled) pending task slot to use next
            foreach (var task in tasks)
                if (!task.enabled)
                    return task;

            // None found? create one!
            return Create(tasks, panel.transform);
        }
        #endregion

        #region Hiding/Displaying Panel
        private void Hide(ISelectionGroup targetGroup)
        {
            foreach (var task in tasks)
            {
                if (task.Attributes.group != targetGroup)
                    continue;

                if (task.IsValid() && task.enabled)
                    task.Disable();
                return;
            }
        }

        private void Hide()
        {
            foreach (var task in tasks)
                if (task.IsValid() && task.enabled)
                    task.Disable();
        }

        private void Show()
        {
            Hide();

            foreach(ISelectionGroup group in selectionGroupHandler.Groups)
            {
                var newTask = Add();
                newTask.Reload(new SelectionGroupUIAttributes
                {
                    data = taskData,
                    group = group
                });

            }

            panel.gameObject.SetActive(true);
        }

        private void Show(ISelectionGroup targetGroup)
        {
            ITaskUI<SelectionGroupUIAttributes> showTask = null;
            foreach (ITaskUI<SelectionGroupUIAttributes> task in tasks)
            {
                if (task.Attributes.group != targetGroup)
                    continue;

                showTask = task;
                break;
            }

            // No task UI found that matches the group, add a new one
            if(!showTask.IsValid())
                showTask = Add();

            showTask.Reload(new SelectionGroupUIAttributes
            {
                data = taskData,
                group = targetGroup
            });
        }
        #endregion
    }
}
