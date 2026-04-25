using System;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Selection;
using System.Collections.Generic;
using RTSEngine.ResourceExtension;
using RTSEngine.UnitExtension;
using TMPro;

namespace RTSEngine.UI
{
    [Serializable]
    public struct SingleSelectionDropOffResourceUI
    {
        [Tooltip("Define the resource collectors and resource drop off target entities that can display the current resources they gathered to drop off and the resources that they allow to be dropped off respectively.")]
        public EntityTargetPicker entityPicker;
        [Tooltip("The UI panel that acts as the parent object of the UI elements that display drop off resources.")]
        public RectTransform panel;
        [Tooltip("Prefab used to show the icon and the amount of each drop off resource.")]
        public DropOffResourceTaskUI prefab;
        [Tooltip("Types of resources allowed to appear in the drop off resources panel.")]
        public ResourceTypeInfo[] allowedResourceTypes;
        [Tooltip("Color of the resource icon if collector reached maximum capacity and must drop off collected resources.")]
        public Color maxCapacityColor;
    }

    public class SingleSelectionPanelUIHandler : MonoBehaviour, IPreRunGameService
    {
        #region Attributes
        [SerializeField, Tooltip("The single selection menu, parent of the rest of the following UI objects.")]
        private GameObject panel = null; 
        [SerializeField, Tooltip("Displays the icon of the selected entity.")]
        private Image icon = null;
        [SerializeField, Tooltip("Displays the name of the selected entity.")]
        private TextMeshProUGUI nameUIText;
        [SerializeField, Tooltip("Displays the description of the selected entity.")]
        private TextMeshProUGUI descriptionUIText = null; 

        [Space(), SerializeField, Tooltip("Displays the active workers of the selected entity.")]
        private TextMeshProUGUI workersUIText = null;
        [SerializeField, Tooltip("Define the entities that are allowed to have their workers displayed when they are selected.")]
        private EntityTargetPicker showWorkersEntityPicker = new EntityTargetPicker();

        [Space(), SerializeField, Tooltip("Displays the amount of health of the selected entity.")]
        private TextMeshProUGUI healthUIText = null; 
        [SerializeField, Tooltip("Handles the health bar of the selected entity.")]
        private ProgressBarUI healthBar = new ProgressBarUI();

        [Space(), SerializeField, Tooltip("Configurations for displaying drop off resources for resource collectors and drop off target entities on the single selection UI panel.")]
        private SingleSelectionDropOffResourceUI dropOffResourcePanel = new SingleSelectionDropOffResourceUI();
        private bool dropOffResourcePanelEnabled = false;
        private Dictionary<ResourceTypeInfo, DropOffResourceTaskUI> dropOffResourceTasks = null;

        // Holds the entity currently displayed in the single selection panel
        private IEntity currEntity;
        private IEntityWorkerManager currWorkerMgr;
        private IUnitSquad currSquad;

        // Game services
        protected IGameLoggingService logger { private set; get; }
        protected ISelectionManager selectionMgr { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IGameUITextDisplayManager textDisplayer { private set; get; }
        protected IResourceManager resourceMgr { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.textDisplayer = gameMgr.GetService<IGameUITextDisplayManager>();
            this.resourceMgr = gameMgr.GetService<IResourceManager>(); 

            healthBar.Init(gameMgr);

            globalEvent.EntitySelectedGlobal += HandleEntitySelectionUpdate;
            globalEvent.EntityDeselectedGlobal += HandleEntitySelectionUpdate;

            globalEvent.UnitSquadSelectedGlobal += HandleUnitSquadSelectionUpdate;
            globalEvent.UnitSquadDeselectedGlobal += HandleUnitSquadSelectionUpdate;

            dropOffResourcePanelEnabled = dropOffResourcePanel.panel.IsValid() && dropOffResourcePanel.prefab.IsValid();
            if(dropOffResourcePanelEnabled)
            {
                dropOffResourceTasks = dropOffResourcePanel
                    .allowedResourceTypes
                    .ToDictionary(
                    resourceType => resourceType,
                    resourceType =>
                    {
                        var nextTask = Instantiate(dropOffResourcePanel.prefab)
                            .GetComponent<DropOffResourceTaskUI>();
                        nextTask.Init(gameMgr, this);

                        nextTask.transform.SetParent(dropOffResourcePanel.panel.transform, true);
                        nextTask.transform.localScale = Vector3.one;

                        return nextTask;
                    });
            }

            Hide();
        }

        public void Disable()
        {
            globalEvent.EntitySelectedGlobal -= HandleEntitySelectionUpdate;
            globalEvent.EntityDeselectedGlobal -= HandleEntitySelectionUpdate;

            globalEvent.UnitSquadSelectedGlobal -= HandleUnitSquadSelectionUpdate;
            globalEvent.UnitSquadDeselectedGlobal -= HandleUnitSquadSelectionUpdate;
        }
        #endregion

        #region Handling Single Unit Squad
        private void HandleUnitSquadSelectionUpdate(IUnitSquad squad, EventArgs args)
        {
            if(selectionMgr.IsUnitSquadSelectedOnly(squad))
            {
                ShowSquad(squad);
            }
            else
                Hide();
        }

        private void HandleSquadUpdated(IUnitSquad squad, EventArgs args)
        {
            if (selectionMgr.IsUnitSquadSelectedOnly(squad))
            {
                ShowSquad(squad);
            }
        }

        private void ShowSquad(IUnitSquad squad)
        {
            //either valid selected entity or one that is not currently being displayed in the panel
            if (!squad.IsValid()
                || squad.CurrentCount == 0)
                return;

            IEntity entity = squad.Units[0];
            panel.SetActive(true);

            if (nameUIText && textDisplayer.EntityNameToText(entity, out string entityName))
                nameUIText.text = $"{entityName} {squad.CurrentCount}/{squad.SpawnCount}"; //display that the entity belongs to a faction or free one?

            if(descriptionUIText && textDisplayer.EntityDescriptionToText(entity, out string entityDescription)) 
                descriptionUIText.text = entityDescription;

            if(icon)
                icon.sprite = entity.Icon;

            if(squad == currSquad)
                return;

            squad.SquadUpdated += HandleSquadUpdated;

            ShowSquadHealthUI(squad);
            squad.SquadHealthUpdated += HandleSquadHealthUpdated;
            squad.SquadMaxHealthUpdated += HandleSquadHealthUpdated;

            //ShowDropOffResources(entity);

            currEntity = entity;
            currSquad = squad;
        }

        #region Handling Unit Squad Health
        private void HandleSquadHealthUpdated(IUnitSquad squad, HealthUpdateArgs args)
        {
            ShowSquadHealthUI(squad);
        }

        public void ShowSquadHealthUI(IUnitSquad squad)
        {
            if(healthUIText) //show the faction entity health:
            {
                healthUIText.gameObject.SetActive(true);
                healthUIText.text = squad.CurrHealth.ToString() + "/" + squad.MaxHealth.ToString();
            }

            //health bar:
            healthBar.Toggle(true);

            //Update the health bar:
            healthBar.Update(squad.CurrHealth / (float)squad.MaxHealth);
        }
        #endregion
        #endregion

        #region Handling Single Entity
        private void HandleEntitySelectionUpdate(IEntity entity, EventArgs e)
        {
            if (selectionMgr.IsUnitSquadSelectedOnly())
            {
                if (currSquad != selectionMgr.SingleSelectedUnitSquad)
                    ShowSquad(selectionMgr.SingleSelectedUnitSquad);
                return;
            }

            if (selectionMgr.Count == 1)
                Show(selectionMgr.GetSingleSelectedEntity(EntityType.all));
            else
                Hide();
        }


        private void Show(IEntity entity)
        {
            //either valid selected entity or one that is not currently being displayed in the panel
            if (!entity.IsValid()
                || entity == currEntity)
                return;

            panel.SetActive(true);

            if (nameUIText && textDisplayer.EntityNameToText(entity, out string entityName))
                nameUIText.text = entityName; //display that the entity belongs to a faction or free one?

            if(descriptionUIText && textDisplayer.EntityDescriptionToText(entity, out string entityDescription)) 
                descriptionUIText.text = entityDescription;

            if(icon)
                icon.sprite = entity.Icon;

            currWorkerMgr = entity.GetWorkerManager();
            if (currWorkerMgr.IsValid())
            {
                ShowWorkerUI();

                currWorkerMgr.WorkerAdded += HandleWorkerAdded;
                currWorkerMgr.WorkerRemoved += HandleWorkerRemoved;
            }

            ShowHealthUI(entity);
            entity.Health.EntityHealthUpdated += HandleEntityHealthUpdated;
            entity.Health.EntityMaxHealthUpdated += HandleEntityHealthUpdated;

            ShowDropOffResources(entity);

            currEntity = entity;
        }

        #region Handling Single Entity WorkerManager
        private void HandleWorkerAdded(IEntity sender, EntityEventArgs<IUnit> e)
        {
            ShowWorkerUI();
        }

        private void HandleWorkerRemoved(IEntity sender, EntityEventArgs<IUnit> e)
        {
            ShowWorkerUI();
        }

        private void ShowWorkerUI()
        {
            if(workersUIText && showWorkersEntityPicker.IsValidTarget(currWorkerMgr.Entity))
            {
                workersUIText.gameObject.SetActive(true);
                workersUIText.text = $"{currWorkerMgr.Amount} / {currWorkerMgr.MaxAmount}";
            }
        }

        private void HideWorkerUI ()
        {
            workersUIText.gameObject.SetActive(false);
        }

        private void HandleEntityHealthUpdated(IEntity sender, HealthUpdateArgs e)
        {
            ShowHealthUI(sender);
        }
        #endregion

        #region Handling Single Unit Health
        public void ShowHealthUI(IEntity entity)
        {
            if(healthUIText) //show the faction entity health:
            {
                healthUIText.gameObject.SetActive(true);
                healthUIText.text = entity.Health.CurrHealth.ToString() + "/" + entity.Health.MaxHealth.ToString();
            }

            //health bar:
            healthBar.Toggle(true);

            //Update the health bar:
            healthBar.Update(entity.Health.CurrHealth / (float)entity.Health.MaxHealth);
        }

        //hides the health related UI elements:
        private void HideHealthUI ()
        {
            healthUIText?.gameObject.SetActive(false);
            healthBar.Toggle(false);
        }
        #endregion

        #region Handling Single Entity Drop Off Resources
        private void ShowDropOffResources(IEntity entity)
        {
            if (!dropOffResourcePanelEnabled
                || !dropOffResourcePanel.entityPicker.IsValidTarget(entity))
                return;

            // Resource collector handling
            if(entity.IsUnit())
            {
                IDropOffSource dropOffComp = (entity as IUnit).DropOffSource;
                if(dropOffComp.IsValid())
                {
                    foreach(var elem in dropOffComp.CollectedResources)
                    {
                        if(dropOffResourceTasks.TryGetValue(elem.Key, out DropOffResourceTaskUI resourceTaskUI))
                        {
                            resourceTaskUI.Reload(new DropOffResourceTaskUIAttributes
                            {
                                dropOffSource = dropOffComp,
                                resourceType = elem.Key,
                                maxCapacityColor = dropOffResourcePanel.maxCapacityColor
                            }) ;
                        }
                    }
                }
            }
        }

        private void HideDropOffResources()
        {
            if (!dropOffResourcePanelEnabled)
                return;

            foreach (DropOffResourceTaskUI dropOffResourceTask in dropOffResourceTasks.Values)
                dropOffResourceTask.Disable();
        }
        #endregion
        #endregion

        #region Hiding Single Selection Panel
        private void Hide () 
        {
            panel.SetActive(false);

            HideWorkerUI();
            HideHealthUI();
            HideDropOffResources();

            if (currEntity.IsValid())
            {

                if (currWorkerMgr.IsValid())
                {
                    currWorkerMgr.WorkerAdded -= HandleWorkerAdded;
                    currWorkerMgr.WorkerRemoved -= HandleWorkerRemoved;
                }

                currEntity.Health.EntityHealthUpdated -= HandleEntityHealthUpdated;
                currEntity.Health.EntityMaxHealthUpdated -= HandleEntityHealthUpdated;
            }
            
            if(currSquad.IsValid())
            {
                currSquad.SquadUpdated -= HandleSquadUpdated;

                currSquad.SquadHealthUpdated -= HandleSquadHealthUpdated;
                currSquad.SquadMaxHealthUpdated -= HandleSquadHealthUpdated;
            }

            currEntity = null;
            currSquad = null;
        }

        #endregion
    }
}
