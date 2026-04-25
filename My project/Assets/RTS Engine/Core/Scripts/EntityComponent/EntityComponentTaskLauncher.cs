using System.Collections.Generic;

using UnityEngine;
using RTSEngine.Entities;
using RTSEngine.UI;
using RTSEngine.Utilities;
using RTSEngine.Event;

namespace RTSEngine.EntityComponent
{
    public class EntityComponentTaskLauncher : EntityComponentBase
    {
        #region Attributes
        // EDITOR ONLY
        [HideInInspector]
        public Int2D tabID = new Int2D {x = 0, y = 0};

        private IFactionEntity factionEntity;
        [SerializeField]
        private EntityComponentTaskInputBase[] tasks = new EntityComponentTaskInputBase[0];

        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IGameUITextDisplayManager textDisplayer { private set; get; }
        #endregion

        #region Initialization/Terminating
        protected override void OnInit()
        {
            this.textDisplayer = gameMgr.GetService<IGameUITextDisplayManager>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();

            if(!Entity.IsFactionEntity())
            {
                logger.LogError($"[{GetType().Name} - {factionEntity.Code}] This component can only be attached to faction entities (units or buildings)!");
                return;
            }

            this.factionEntity = Entity as IFactionEntity;

            if (!tasks.IsValid())
            {
                logger.LogError($"[{GetType().Name} - {factionEntity.Code}] One or more elements in the 'Tasks' are invalid!");
                return;
            }

            for (int taskID = 0; taskID < tasks.Length; taskID++)
            {
                tasks[taskID].Init(this, taskID, gameMgr);
                tasks[taskID].Enable();
            }
        }
        #endregion

        #region Activating/Deactivating Component
        protected override void OnActiveStatusUpdated()
        {
            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);
            globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);
        }
        #endregion

        #region Task UI
        protected override bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
        {
            // For building creation tasks, we show building creation tasks that do not have required conditions to launch but make them locked.
            for (int i = 0; i < tasks.Length; i++)
            {
                var task = tasks[i];

                if (!task.IsFactionTypeAllowed(factionEntity.Slot.Data.type))
                    continue;

                taskUIAttributesCache.Add(new EntityComponentTaskUIAttributes
                {
                    data = task.Data,

                    factionID = factionEntity.Slot.ID,

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

        public override bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes)
        {
            // If it's not the launch task (task that makes the builder construct a building) then it is a building placement task.
            foreach (var task in tasks)
                if (task.Data.code == taskAttributes.data.code)
                {
                    task.OnComplete();

                    LaunchTask(task, playerCommand: true);

                    globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);
                    return true;
                }

            return false;
        }

        protected virtual string GetTaskTitleText(IEntityComponentTaskInput taskInput)
        {
            textDisplayer.EntityComponentTaskTitleToString(taskInput, out string text);
            return text;
        }

        protected virtual string GetTooltipText(IEntityComponentTaskInput nextTask)
        {
            textDisplayer.EntityComponentTaskTooltipInputToText(
                nextTask,
                targetPrefab: null,
                out string tooltipText);

            return tooltipText;
        }
        #endregion

        #region Launching Tasks
        protected virtual void LaunchTask(EntityComponentTaskInputBase task, bool playerCommand)
        {
        }
        #endregion
    }
}