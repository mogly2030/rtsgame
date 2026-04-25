using System.Collections.Generic;

using RTSEngine.BuildingExtension;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Faction;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.UI;
using RTSEngine.Upgrades;

namespace RTSEngine.EntityComponent
{
    [System.Serializable]
    public class BuildingCreationTaskHandler
    {
        #region Attributes
        private IEntityComponent entityComponent;
        private IFactionSlot factionSlot => entityComponent.Entity.Slot;

        private List<BuildingCreationTask> creationTasks;
        private BuildingCreationTask[] upgradeTargetCreationTasks;

        protected IGameLoggingService logger { private set; get; }
        protected IEntityUpgradeManager entityUpgradeMgr { private set; get; }
        protected IBuildingPlacement placementMgr { private set; get; }
        protected IBuildingManager buildingMgr { private set; get; }
        protected IGameUITextDisplayManager textDisplayer { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr, IEntityComponent entityComponent, List<BuildingCreationTask> creationTasks, BuildingCreationTask[] upgradeTargetCreationTasks)
        {
            this.entityComponent = entityComponent;
            this.creationTasks = creationTasks;
            this.upgradeTargetCreationTasks = upgradeTargetCreationTasks;

            this.logger = gameMgr.GetService<IGameLoggingService>();

            if(!entityComponent.IsValid())
            {
                logger.LogError($"[{GetType().Name}] The provided IEntityComponent instance is not valid!");
                return;
            }
            else if(!entityComponent.Entity.IsFactionEntity())
            {
                logger.LogError($"[{GetType().Name} - {entityComponent.Entity.Code}] This component must be attached to a faction entity (unit or building)!");
                return;
            }
            else if (!this.creationTasks.IsValid()
                || !this.upgradeTargetCreationTasks.IsValid())
            {
                logger.LogError($"[{GetType().Name} - {entityComponent.Entity.Code}] Some elements in the 'Creation Tasks' or 'Upgrade Creation Tasks' array have the 'Prefab Object' field unassigned!");
                return;
            }

            this.entityUpgradeMgr = gameMgr.GetService<IEntityUpgradeManager>();
            this.placementMgr = gameMgr.GetService<IBuildingPlacement>(); 
            this.buildingMgr = gameMgr.GetService<IBuildingManager>(); 
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();
            this.textDisplayer = gameMgr.GetService<IGameUITextDisplayManager>();

            // Initialize creation tasks
            int taskID = 0;
            for (taskID = 0; taskID < this.creationTasks.Count; taskID++)
            {
                this.creationTasks[taskID].Init(entityComponent, taskID, gameMgr);
                this.creationTasks[taskID].Enable();
            }
            for (int i = 0; i < this.upgradeTargetCreationTasks.Length; i++)
            {
                BuildingCreationTask nextTask = this.upgradeTargetCreationTasks[i];
                nextTask.Init(entityComponent, taskID, gameMgr);
                taskID++;
            }

            // Check for building upgrades:
            if (!entityComponent.Entity.IsFree
                && entityUpgradeMgr.TryGet (factionSlot.ID, out UpgradeElement<IEntity>[] upgradeElements))
            {
                foreach(var nextElement in upgradeElements)
                {
                    DisableTasksWithPrefabCode(nextElement.sourceCode);
                    EnableUpgradeTargetTasksWithPrefab(nextElement.target);
                }
            }

            globalEvent.BuildingUpgradedGlobal += HandleBuildingUpgradedGlobal;
        }

        public void Disable()
        {
            globalEvent.BuildingUpgradedGlobal -= HandleBuildingUpgradedGlobal;
        }

        private void DisableTasksWithPrefabCode (string prefabCode)
        {
            foreach (var task in creationTasks)
                if (task.TargetObject.Code == prefabCode)
                    task.Disable();
        }

        private void EnableUpgradeTargetTasksWithPrefab(IEntity prefab)
        {
            foreach (var upgradeTargetTask in upgradeTargetCreationTasks)
                if (upgradeTargetTask.TargetObject.Code == prefab.Code)
                {
                    creationTasks.Add(upgradeTargetTask);
                    upgradeTargetTask.Enable();
                }
        }
        #endregion

        #region Handling Events: Building Upgrade
        private void HandleBuildingUpgradedGlobal(IBuilding building, UpgradeEventArgs<IEntity> args)
        {
            if (!factionSlot.IsSameFaction(args.FactionID))
                return;

            // Disable the upgraded tasks
            DisableTasksWithPrefabCode(args.UpgradeElement.sourceCode);
            EnableUpgradeTargetTasksWithPrefab(args.UpgradeElement.target);

            // Remove the building creation task of the unit that has been upgraded

            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(
                entityComponent,
                new TaskUIReloadEventArgs(reloadAll: true));
        }
        #endregion

        #region Task UI
        public bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
        {
            // For building creation tasks, we show building creation tasks that do not have required conditions to launch but make them locked.
            for (int i = 0; i < creationTasks.Count; i++)
            {
                var task = creationTasks[i];

                if (!task.IsFactionTypeAllowed(factionSlot.Data.type))
                    continue;

                taskUIAttributesCache.Add(new EntityComponentTaskUIAttributes
                {
                    data = task.Data,

                    factionID = factionSlot.ID,

                    title = GetTaskTitleText(task),
                    requiredResources = task.RequiredResources,
                    factionEntityRequirements = task.FactionEntityRequirements,
                    tooltipText = GetTooltipText(task),

                    // Wa want the building placement process to start once, this avoids having each builder component instance launch a placement
                    launchOnce = true,

                    locked = task.CanStart() != ErrorMessage.none,
                    lockedData = task.MissingRequirementData
                });
            }

            return true;
        }

        public bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes)
        {
            // If it's not the launch task (task that makes the builder construct a building) then it is a building placement task.
            foreach (BuildingCreationTask creationTask in creationTasks)
                if (creationTask.Data.code == taskAttributes.data.code)
                {
                    placementMgr.Add(factionSlot.ID, creationTask);
                    return true;
                }

            return false;
        }

        protected virtual string GetTaskTitleText(BuildingCreationTask taskInput)
        {
            textDisplayer.EntityComponentTaskTitleToString(taskInput, out string text);
            return text;
        }

        protected virtual string GetTooltipText(BuildingCreationTask nextTask)
        {
            textDisplayer.BuildingCreationTaskTooltipToString(
                nextTask,
                out string tooltipText);

            return tooltipText;
        }
        #endregion
    }
}