using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.UI;
using RTSEngine.Event;
using System;

namespace RTSEngine.EntityComponent
{
    public abstract class EntityComponentBase : MonoBehaviour, IEntityComponent, IEntityPreInitializable
    {
        #region Attributes
        public bool IsInitialized { private set; get; } = false;
        public virtual bool AllowPreEntityInit => false;

        [SerializeField, Tooltip("Code that defines this component, uniquely within the entity.")]
        private string code = "CHANGE_ME";
        public string Code => code;

        public IEntity Entity { private set; get; }

        [SerializeField, Tooltip("Is the component enabled by default?")]
        private bool isActive = true;
        public bool IsActive => isActive;

        public EntityComponentData Data => new EntityComponentData
        {
            isActive = IsActive
        };

        // UI
        private List<EntityComponentTaskUIAttributes> taskUIAttributesCache;
        private List<string> disabledTaskCodesCache;

        protected IGameLoggingService logger { private set; get; }
        protected IPlayerMessageHandler playerMsgHandler { private set; get; }
        protected IGameManager gameMgr { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void OnEntityPreInit(IGameManager gameMgr, IEntity entity)
        {
            if (!AllowPreEntityInit || IsInitialized)
                return;

            Init(gameMgr, entity);
        }

        public void OnEntityPostInit (IGameManager gameMgr, IEntity entity)
        {
            if (IsInitialized)
                return;

            Init(gameMgr, entity);
        }

        private void Init(IGameManager gameMgr, IEntity entity)
        {
            this.gameMgr = gameMgr;
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.playerMsgHandler = gameMgr.GetService<IPlayerMessageHandler>();
            this.Entity = entity;

            if (IsInitialized)
            {
                logger.LogError($"[{GetType().Name} - {Entity.Code}] Component already initialized! It is not supposed to be initialized again! Please retrace and report!", source: this);
                return;
            }

            taskUIAttributesCache = new List<EntityComponentTaskUIAttributes>();
            disabledTaskCodesCache = new List<string>();

            Entity.FactionUpdateStart += HandleFactionUpdateStart;
            Entity.FactionUpdateComplete += HandleFactionUpdateComplete;

            OnInit();

            IsInitialized = true;
        }

        protected virtual void OnInit() { }

        public void Disable()
        {
            if (!IsInitialized)
                return;

            Entity.FactionUpdateStart -= HandleFactionUpdateStart;
            Entity.FactionUpdateComplete -= HandleFactionUpdateComplete;

            OnDisabled();
        }

        protected virtual void OnDisabled() { }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IEntityComponent, EventArgs> ActiveStatusUpdate;
        private void RaiseActiveStatusUpdate()
        {
            var handler = ActiveStatusUpdate;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Handling Faction Update
        private void HandleFactionUpdateStart(IEntity sender, FactionUpdateArgs args)
        {
            OnFactionUpdateStart();
        }

        protected virtual void OnFactionUpdateStart() { }

        private void HandleFactionUpdateComplete(IEntity sender, FactionUpdateArgs args)
        {
            OnFactionUpdateComplete();
        }

        protected virtual void OnFactionUpdateComplete() { }
        #endregion

        #region Handling Component Upgrade
        public virtual void HandleComponentUpgrade (IEntityComponent sourceEntityComponent) { }
        #endregion

        #region Activating/Deactivating Component
        public ErrorMessage SetActive(bool active, bool playerCommand) => RTSHelper.SetEntityComponentActive(this, active, playerCommand);

        public ErrorMessage SetActiveLocal(bool active, bool playerCommand)
        {
            isActive = active;

            OnActiveStatusUpdated();
            RaiseActiveStatusUpdate();

            return ErrorMessage.none;
        }

        protected virtual void OnActiveStatusUpdated() { }
        #endregion

        #region Handling Actions
        public ErrorMessage LaunchAction(byte actionID, SetTargetInputData input)
            => RTSHelper.LaunchEntityComponentAction(this, actionID, input);

        public virtual ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input) => ErrorMessage.undefined;
        #endregion

        #region Task UI
        public bool OnTaskUIRequest(
            out IReadOnlyList<EntityComponentTaskUIAttributes> taskUIAttributes,
            out IReadOnlyList<string> disabledTaskCodes)
        {
            taskUIAttributesCache.Clear();
            disabledTaskCodesCache.Clear();

            if (OnTaskUICacheUpdate(taskUIAttributesCache, disabledTaskCodesCache))
            {
                taskUIAttributes = taskUIAttributesCache.AsReadOnly();
                disabledTaskCodes = disabledTaskCodesCache.AsReadOnly();
                return true;
            }
            else
            {
                taskUIAttributes = null;
                disabledTaskCodes = null;

                return false;
            }
        }

        protected virtual bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
        {
            return false;
        }

        public virtual bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes) 
        {
            return false;
        }

        public virtual bool OnAwaitingTaskTargetSet(EntityComponentTaskUIAttributes taskAttributes, TargetData<IEntity> target)
        {
            return false;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        void Reset()
        {
            // Set a default code to the newly added entity component
            if (code != "CHANGE_ME")
                return;

            IEntity entity = gameObject.GetComponentInParent<IEntity>();

            if (!entity.IsValid())
                return;

            code = $"{entity.Code}_{GetType().Name}_{UnityEngine.Random.Range(0, 256)}";
        }
#endif
        #endregion
    }
}
