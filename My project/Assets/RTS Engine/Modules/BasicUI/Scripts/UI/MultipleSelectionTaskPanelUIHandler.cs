using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Entities;
using RTSEngine.UnitExtension;

namespace RTSEngine.UI
{
    public class MultipleSelectionTaskPanelUIHandler : BaseTaskPanelUIHandler<MultipleSelectionTaskUIAttributes>
    {
        #region Attributes
        [SerializeField, EnforceType(sameScene: true), Tooltip("Parent game object of the active multiple selection tasks.")]
        private GridLayoutGroup panel = null;

        [SerializeField, Tooltip("Multiple selection UI data parameters.")]
        private MultipleSelectionTaskUIData taskData = new MultipleSelectionTaskUIData { description = "Deselect", tooltipEnabled = true };

        [SerializeField, Tooltip("If the multiple selected entities is over this threshold, each type of the selected entities will have one task with the selected amount displayed on the task.")]
        private int entityTypeSelectionTaskThreshold = 10;

        [SerializeField, Tooltip("Amount of task UI instances to pre-create at the start of the game to have ready to be instantly used instead of having to create them when needed."), Min(0)]
        private int preCreateAmount = 10;

        // Each created multiple selection task is registered in this list.
        private List<ITaskUI<MultipleSelectionTaskUIAttributes>> tasks = null;
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            tasks = new List<ITaskUI<MultipleSelectionTaskUIAttributes>>();

            if (!logger.RequireValid(panel,
                $"[{GetType().Name}] The 'Panel' field must be assigned!"))
                return;

            while(tasks.Count < preCreateAmount)
                Create(tasks, panel.transform);

            globalEvent.EntitySelectedGlobal += HandleEntitySelectionUpdate;
            globalEvent.EntityDeselectedGlobal += HandleEntitySelectionUpdate;

            globalEvent.UnitSquadSelectedGlobal += HandleUnitSquadSelectionUpdate;
            globalEvent.UnitSquadDeselectedGlobal += HandleUnitSquadSelectionUpdate;

            Hide();
        }
        #endregion

        #region Disabling UI Panel
        public override void Disable()
        {
            Hide();

            globalEvent.EntitySelectedGlobal -= HandleEntitySelectionUpdate;
            globalEvent.EntityDeselectedGlobal -= HandleEntitySelectionUpdate;

            globalEvent.UnitSquadSelectedGlobal -= HandleUnitSquadSelectionUpdate;
            globalEvent.UnitSquadDeselectedGlobal -= HandleUnitSquadSelectionUpdate;

        }
        #endregion

        #region Handling Event: Entity Selected/Deselected
        private void HandleEntitySelectionUpdate(IEntity entity, EventArgs e)
        {
            bool isIncomingSingleSquadSelection = false;

            if(entity.IsUnit())
            {
                IUnit unit = entity as IUnit;
                if (unit.Squad.IsValid())
                    isIncomingSingleSquadSelection = selectionMgr.Count < unit.Squad.CurrentCount;
            }

            if (!selectionMgr.IsUnitSquadSelectedOnly()
                && !isIncomingSingleSquadSelection
                && selectionMgr.Count > 1)
                Show();
            else
                Hide();
        }

        private void HandleUnitSquadSelectionUpdate(IUnitSquad unitSquad, EventArgs args)
        {
            if (selectionMgr.IsUnitSquadSelectedOnly())
                Hide();
            else
                Show();
        }
        #endregion

        #region Adding Tasks
        private ITaskUI<MultipleSelectionTaskUIAttributes> Add()
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
        private void Hide()
        {
            foreach (var task in tasks)
                if (task.IsValid() && task.enabled)
                    task.Disable();
        }

        private void Show()
        {
            Hide();

            var selectedEntities = selectionMgr
                    .GetEntitiesList(EntityType.all, true, false)
                    .ToList();

            List<MultipleSelectionUIElement> selectedElements = new List<MultipleSelectionUIElement>();
            while(selectedEntities.Count > 0)
            {
                if(selectedEntities[0].Health.IsDead)
                {
                    selectedEntities.RemoveAt(0);
                    continue;
                }

                if(selectedEntities[0].IsUnit())
                {
                    IUnit unit = selectedEntities[0] as IUnit;
                    if(unit.Squad.IsValid() && !unit.Squad.IsDead)
                    {
                        foreach (IUnit squadUnit in unit.Squad.Units)
                            selectedEntities.Remove(squadUnit);

                        selectedElements.Add(new MultipleSelectionUIElement
                        {
                            isSquad = true,
                            entities = unit.Squad.Units
                        });
                        continue;
                    }
                }

                selectedElements.Add(new MultipleSelectionUIElement

                {
                    isSquad = false,
                    entities = Enumerable.Repeat(selectedEntities[0], 1)
                });
                selectedEntities.RemoveAt(0);
            }

            // If the amount of selected units is higher than the maximum allowed multiple selection tasks that represent each entity individually:
            if (selectedElements.Count >= entityTypeSelectionTaskThreshold)
            {
                // Get the selected units in a form of a dictionary with each selected entity's code as key and the selected entities of each type in a list as the value.
                selectedElements = selectionMgr
                    .GetEntitiesDictionary(EntityType.all, localPlayerFaction: false)
                    .Values
                    .Select(set => new MultipleSelectionUIElement
                    {
                        isSquad = false,
                        entities = set
                    })
                    .ToList();
            }
            
            foreach(var element in selectedElements)
            {
                var newTask = Add();
                newTask.Reload(new MultipleSelectionTaskUIAttributes
                {
                    data = taskData,
                    selectedElement = element
                });
            }

            panel.gameObject.SetActive(true);
        }
        #endregion
    }
}
