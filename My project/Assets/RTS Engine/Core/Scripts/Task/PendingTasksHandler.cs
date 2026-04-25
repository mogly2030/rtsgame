using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.UI;
using RTSEngine.EntityComponent;
using RTSEngine.Determinism;
using RTSEngine.Logging;

namespace RTSEngine.Task
{
    public enum PendingTaskState { none, added, started, cancelled, preCompleted, completed }

    public class PendingTasksHandler : MonoBehaviour, IEntityPreInitializable, IPendingTasksHandler
    {
        #region Attributes
        // When the pending task handler is locked, it means the first task in the queue can not be completed due to missing requirements
        // This allows to avoid refreshing the UI elements every time the requirements are checked and they are still not met.
        private bool isLocked = false;

        public IEntity Entity { private set; get; }

        [SerializeField, Tooltip("Total amount of allowed simultaneous pending tasks at any given time for the entity."), Min(0)]
        private int maxCount = 4;

        private List<PendingTask> queue = new List<PendingTask>();
        public IReadOnlyList<PendingTask> Queue => queue.AsReadOnly();
        public int QueueCount => queue.Count;
        public PendingTask First => QueueCount > 0 ? queue[0] : default;

        // Timer of the first task in the queue, when it is through, the first task in queue is completed and the next one is loaded.
        public TimeModifiedTimer QueueTimer { private set; get; }
        public float QueueTimerValue => QueueTimer.CurrValue;

        // UI
        private List<EntityComponentPendingTaskUIAttributes> taskUIAttributesCache;

        // Game services
        protected IGlobalEventPublisher globalEvent { private set; get; } 
        protected IPlayerMessageHandler playerMsgHandler { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IPendingTasksHandler, PendingTaskEventArgs> PendingTaskStateUpdated;
        private void RaisePendingTaskStateUpdated(PendingTaskEventArgs args)
        {
            var handler = PendingTaskStateUpdated;
            handler?.Invoke(this, args);
        }
        #endregion

        #region Initializing/Terminating
        public void OnEntityPreInit(IGameManager gameMgr, IEntity entity)
        {
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>(); 
            this.playerMsgHandler = gameMgr.GetService<IPlayerMessageHandler>();

            this.Entity = entity;

            taskUIAttributesCache = new List<EntityComponentPendingTaskUIAttributes>(maxCount);

            isLocked = false;
            QueueTimer = new TimeModifiedTimer();
        }

        public void Disable()
        {
            CancelAll();
        }
        #endregion

        #region Adding Pending Tasks
        public bool Add(PendingTask newPendingTask, bool useCustomQueueTime = false, float customQueueTime = 0.0f)
        {
            if (queue.Count >= maxCount)
                return false;

            queue.Add(newPendingTask);
            newPendingTask.sourceTaskInput.OnStart();

            RaisePendingTaskStateUpdated(new PendingTaskEventArgs(
                data: newPendingTask,
                pendingQueueID: queue.Count - 1,
                state: PendingTaskState.added
            ));
            globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);

            if (queue.Count == 1)
                StartNext(useCustomQueueTime, customQueueTime);

            return true;
        }
        #endregion

        #region Handling Pending Tasks Progress
        private void StartNext(bool useCustomQueueTime = false, float customQueueTime = 0.0f)
        {
            isLocked = false;

            if (queue.Count == 0)
                return;

            QueueTimer.Reload(useCustomQueueTime ? customQueueTime : queue[0].sourceTaskInput.Data.reloadTime);

            RaisePendingTaskStateUpdated(new PendingTaskEventArgs(
                data: queue[0],
                pendingQueueID: 0,
                state: PendingTaskState.started
            ));
        }

        private void Update()
        {
            if (!Entity.CanLaunchTask
                || QueueCount == 0)
                return;

            if (QueueTimer.ModifiedDecrease())
                TryCompleteCurrent();
        }

        private bool TryCompleteCurrent()
        {
            RaisePendingTaskStateUpdated(new PendingTaskEventArgs(
                data: queue[0],
                pendingQueueID: 0,
                state: PendingTaskState.preCompleted
            ));
            queue[0].sourceComponent.OnPendingTaskPreComplete(queue[0]);

            ErrorMessage launchError = queue[0].sourceTaskInput.CanComplete();
            if(launchError == ErrorMessage.none)
            {
                CompleteCurrent();
                return true;
            }

            // Cancel task is it belongs to a NPC faction
            if(RTSHelper.IsNPCFaction(Entity))
                CancelByQueueID(0);

            else if (!isLocked)
            {
                if (Entity.IsLocalPlayerFaction())
                    playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                    {
                        message = launchError,

                        source = Entity
                    });

                globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);

                isLocked = true;
            }

            return false;
        }

        public void CompleteCurrent()
        {
            if (queue.Count == 0)
                return;

            PendingTask completedTask = queue[0];
            queue.RemoveAt(0);

            completedTask.sourceTaskInput.OnComplete();
            completedTask.sourceComponent.OnPendingTaskCompleted(completedTask);

            RaisePendingTaskStateUpdated(new PendingTaskEventArgs(
                data: completedTask,
                pendingQueueID: -1,
                state: PendingTaskState.completed
            ));

            StartNext();

            globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);
        }
        #endregion

        #region Cancelling Pending Tasks
        public void CancelAll()
        {
            while (queue.Count > 0)
                CancelByQueueID(0);
        }

        public void CancelByQueueID(int pendingTaskIndex)
        {
            if (pendingTaskIndex < 0 || pendingTaskIndex >= QueueCount)
                return;

            CancelInternal(queue[pendingTaskIndex]);
            queue.RemoveAt(pendingTaskIndex);

            globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);

            if (pendingTaskIndex == 0)
                StartNext();
        }

        public void CancelBySourceComponent(IPendingTaskEntityComponent sourceComponnet)
        {
            //in case the RemoveAll removes the first pending task in the queue, the queue must start the next one in case there is one.
            PendingTask lastFirst = First;

            queue.RemoveAll(pendingTask => 
            {
                if (pendingTask.sourceComponent == sourceComponnet)
                {
                    CancelInternal(pendingTask);
                    return true;
                }
                return false; 
            });

            globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);

            PendingTask newFirst = First;
            if (!lastFirst.Equals(newFirst))
                StartNext();
        }

        public void CancelBySourceID(IPendingTaskEntityComponent sourceComponnet, int sourceID)
        {
            //in case the RemoveAll removes the first pending task in the queue, the queue must start the next one in case there is one.
            PendingTask lastFirst = First;

            queue.RemoveAll(pendingTask => 
            {
                if (pendingTask.sourceComponent == sourceComponnet && pendingTask.sourceTaskInput.ID == sourceID)
                {
                    CancelInternal(pendingTask);
                    return true;
                }
                return false; 
            });

            globalEvent.RaiseEntityComponentPendingTaskUIReloadRequestGlobal(Entity);

            PendingTask newFirst = First;
            if (!lastFirst.Equals(newFirst))
                StartNext();
        }

        private void CancelInternal(PendingTask task)
        {
            task.sourceTaskInput.OnCancel();
            task.sourceComponent.OnPendingTaskCancelled(task);

            RaisePendingTaskStateUpdated(new PendingTaskEventArgs(
                data: task,
                pendingQueueID: -1,
                state: PendingTaskState.completed
            ));
        }
        #endregion

        #region Handling UI
        public bool OnPendingTaskUIRequest(out IReadOnlyList<EntityComponentPendingTaskUIAttributes> taskUIAttributes)
        {
            taskUIAttributes = null;

            if (!Entity.CanLaunchTask
                || !RTSHelper.IsLocalPlayerFaction(Entity))
                return false;

            taskUIAttributesCache.Clear();
            for (int i = 0; i < Queue.Count; i++)
            {
                PendingTask pendingTask = Queue[i];

                if (!pendingTask.sourceComponent.IsActive)
                    continue;

                taskUIAttributesCache.Add(
                   new EntityComponentPendingTaskUIAttributes
                   {
                       data = pendingTask.sourceTaskInput.Data,

                       pendingData = new EntityComponentPendingTaskUIData
                       {
                           queueIndex = i,
                           handler = this
                       },

                       locked = pendingTask.sourceTaskInput.CanComplete() != ErrorMessage.none,
                       lockedData = pendingTask.sourceTaskInput.MissingRequirementData,
                   });
            }

            taskUIAttributes = taskUIAttributesCache;

            return true;
        }
        #endregion
    }
}
