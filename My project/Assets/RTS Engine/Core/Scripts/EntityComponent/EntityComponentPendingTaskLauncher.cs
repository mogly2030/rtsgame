using System.Collections.Generic;

using UnityEngine;

namespace RTSEngine.EntityComponent
{
    public class EntityComponentPendingTaskLauncher : PendingTaskEntityComponentBase
    {
        #region Attributes
        [SerializeField]
        private EntityComponentTaskInputBase[] tasks = new EntityComponentTaskInputBase[0];

        public override IReadOnlyList<IEntityComponentTaskInput> Tasks => tasks;
        #endregion

        #region Initialization/Terminating
        protected override void OnPendingInit()
        {
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

        #region Task UI
        protected override string GetTaskTooltipText(IEntityComponentTaskInput taskInput)
        {
            textDisplayer.EntityComponentTaskTooltipInputToText(
                taskInput,
                targetPrefab: null,
                out string tooltipText);

            return tooltipText;
        }
        #endregion

        #region Launching Tasks
        protected override ErrorMessage CompleteTaskActionLocal(int taskID, bool playerCommand)
        {
            LaunchTask(tasks[taskID], playerCommand);

            return ErrorMessage.none;
        }

        protected virtual void LaunchTask(EntityComponentTaskInputBase task, bool playerCommand)
        {
        }
        #endregion
    }
}