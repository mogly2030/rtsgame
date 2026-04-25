using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Selection;
using RTSEngine.UnitExtension;
using TMPro;

namespace RTSEngine.UI
{
    public class MultipleSelectionTaskUI : BaseTaskUI<MultipleSelectionTaskUIAttributes>
    {
        #region Attributes
        protected override Sprite Icon => Attributes.selectedElement.entities.First().Icon;

        protected override Color IconColor => Color.white;

        protected override bool IsTooltipEnabled => Attributes.data.tooltipEnabled;

        protected override string TooltipDescription => Attributes.data.description;

        // Amount of selected entities represented by this multiple selection task.
        private int count = 0;

        [SerializeField, Tooltip("To display the progress of the pending task.")]
        private ProgressBarUI progressBar = new ProgressBarUI();

        [SerializeField, Tooltip("UI Text to display the amount of the multiple selected entities.")]
        private TextMeshProUGUI label = null;

        IUnitSquad currSquad = null;
        IEntity currEntity = null;
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (!logger.RequireValid(label,
                $"[{GetType().Name}] The 'Label' field must be assigned!"))
                return;

            progressBar.Init(gameMgr);
        }
        #endregion

        #region Disabling Task UI
        protected override void OnDisabled()
        {
            if (currEntity.IsValid())
            {
                currEntity.Health.EntityHealthUpdated -= HandleSelectedEntityHealthUpdated;
                currEntity.Health.EntityMaxHealthUpdated -= HandleSelectedEntityHealthUpdated;
            }
            if(currSquad.IsValid())
            {
                currSquad.SquadHealthUpdated += HandleSelectedSquadHealthUpdated;
                currSquad.SquadMaxHealthUpdated += HandleSelectedSquadHealthUpdated;
            }

            progressBar.Toggle(false);
            label.enabled = false;

            currEntity = null;
            currSquad = null;

            count = 0;
        }
        #endregion

        #region Handling Event: Selected Entity Health Updated
        private void HandleSelectedEntityHealthUpdated(IEntity entity, HealthUpdateArgs e)
        {
            progressBar.Update(entity.Health.CurrHealth / (float)entity.Health.MaxHealth);
        }

        private void HandleSelectedSquadHealthUpdated(IUnitSquad currSquad, HealthUpdateArgs args)
        {
            progressBar.Update(currSquad.CurrHealth / (float)currSquad.MaxHealth);
        }
        #endregion

        #region Handling Attributes Reload
        protected override void OnReload()
        {
            if(Attributes.selectedElement.isSquad)
            {
                currSquad = (Attributes.selectedElement.entities.First() as IUnit).Squad;
            }
            else
            {
                currEntity = Attributes.selectedElement.entities.First();
            }

            count = Attributes.selectedElement.isSquad
                ? currSquad.CurrentCount
                : Attributes.selectedElement.entities.Count();

            // Only display health for individual selection tasks.
            if (Attributes.selectedElement.isSquad || count == 1)
            {
                progressBar.Toggle(true);

                if(Attributes.selectedElement.isSquad)
                {
                    currSquad.SquadHealthUpdated += HandleSelectedSquadHealthUpdated;
                    currSquad.SquadMaxHealthUpdated += HandleSelectedSquadHealthUpdated;
                    // Call to set the initial health bar valueentities:
                    HandleSelectedSquadHealthUpdated(currSquad, default);

                    label.enabled = true;
                    label.text = count.ToString();
                }
                else
                {
                    currEntity.Health.EntityHealthUpdated += HandleSelectedEntityHealthUpdated;
                    currEntity.Health.EntityMaxHealthUpdated += HandleSelectedEntityHealthUpdated;
                    // Call to set the initial health bar valueentities:
                    HandleSelectedEntityHealthUpdated(currEntity, default);

                    label.enabled = false;
                }
            }
            else
            {
                progressBar.Toggle(false);

                label.enabled = true;
                // Only if this a multiple selection task for multiple entities, then show their amount
                label.text = count.ToString();
            }

        }
        #endregion

        #region Handling Task UI Interaction
        protected override void OnClick()
        {
            // If the player is holding the multiple selection key then deselect the clicked entity
            if (selector.MultipleSelectionModeEnabled) 
                selectionMgr.Remove(Attributes.selectedElement.entities);
            else 
            {
                if (count == 1 || Attributes.selectedElement.isSquad)
                    selectionMgr.Add(
                        Attributes.selectedElement.entities.First(),
                        SelectionType.single,
                        isLocalPlayerClickSelection: false); 
                else
                    selectionMgr.Add(Attributes.selectedElement.entities);
            }

            HideTaskTooltip();
        }
        #endregion
    }
}
