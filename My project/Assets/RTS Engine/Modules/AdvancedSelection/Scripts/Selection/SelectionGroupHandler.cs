using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Controls;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using System;
using System.Linq;

namespace RTSEngine.Selection
{
    public enum SelectionGroupAction { select, assign, append, substract, reset }

    public interface ISelectionGroup
    {
        IReadOnlyList<IEntity> Entities { get; }
        bool HasKey { get; }

        event CustomEventHandler<ISelectionGroup, EventArgs> GroupUpdated;

        bool Update(SelectionGroupAction action, bool requireKey = true);

        void Add(IEnumerable<IEntity> entities);
        bool Add(IEntity entity);

        void Remove(IEnumerable<IEntity> entities);
        void Remove(IEntity entity);
        void RemoveAll();
    }

    [System.Serializable]
    public class SelectionGroup : ISelectionGroup
    {
        #region Attributes
        [SerializeField, Tooltip("Control type that defines the key used to use this selection group.")]
        private ControlType key = null;
        public bool HasKey => key.IsValid();

        [SerializeField, Tooltip("Define what entity types can be added to this selection group.")]
        private EntityType allowedEntityTypes = EntityType.unit & EntityType.building;
        
        [SerializeField, Tooltip("Enable an upper boundary on the amount of entities allowed in this selection group?")]
        private bool maxAmountEnabled = false;
        [SerializeField, Tooltip("The maximum amount of entities allowed in the selection group at the same time if the above field is enabled."), Min(1)]
        private int maxAmount = 20;

        [SerializeField, Tooltip("Force the group entities to be of the same type!")]
        private bool forceSameType = false;
        private string forcedEntityCode;

        private List<IEntity> current;
        public IReadOnlyList<IEntity> Entities => current;

        protected ISelectionManager selectionMgr { private set; get; }
        protected IGameControlsManager controls { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<ISelectionGroup, EventArgs> GroupUpdated;
        private void RaiseGroupUpdated()
        {
            var handler = GroupUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        public bool Init(int index, IGameManager gameMgr)
        {
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.controls = gameMgr.GetService<IGameControlsManager>(); 

            current = new List<IEntity>();

            if(!key.IsValid())
            {
                gameMgr.GetService<IGameLoggingService>().LogWarning($"[{GetType().Name} - index: {index}] 'Key' field must be assigned to use the selection group with keyboard keys!");
                return false;
            }

            return true;
        }
        #endregion

        #region Adding/Removing
        public bool Update(SelectionGroupAction action, bool requireKey = true)
        {
            if (requireKey && (!HasKey || !controls.Get(key)))
                return false;

            switch(action)
            {
                case SelectionGroupAction.assign:

                    current.Clear();

                    Add(selectionMgr.GetEntitiesList(allowedEntityTypes, exclusiveType: false, localPlayerFaction: true));
                    break;

                case SelectionGroupAction.append:

                    Add(selectionMgr.GetEntitiesList(allowedEntityTypes, exclusiveType: false, localPlayerFaction: true));
                    break;

                case SelectionGroupAction.substract:

                    Remove(selectionMgr.GetEntitiesList(allowedEntityTypes, exclusiveType: false, localPlayerFaction: true));
                    break;

                case SelectionGroupAction.reset:
                    RemoveAll();
                    break;

                case SelectionGroupAction.select:

                    if (current.Count > 0)
                    {
                        selectionMgr.RemoveAll();
                        selectionMgr.Add(current);
                    }
                    break;
            }

            return true;
        }

        public void Add(IEnumerable<IEntity> entities)
        {
            foreach (IEntity entity in entities)
                Add(entity);
        }

        public bool Add(IEntity entity)
        {
            if (current.Contains(entity)
                || (maxAmountEnabled && current.Count == maxAmount)
                || (forceSameType && current.Count > 0 && entity.Code != forcedEntityCode))
                return false;

            current.Add(entity);
            entity.Health.EntityDead += HandleEntityDead;
            entity.FactionUpdateComplete += HandleFactionUpdateComplete;

            if (forceSameType && current.Count == 1)
                forcedEntityCode = entity.Code;

            RaiseGroupUpdated();

            return true;
        }

        public void RemoveAll()
        {
            while (current.Count > 0)
                Remove(current[0]);
        }

        public void Remove(IEnumerable<IEntity> entities)
        {
            foreach (IEntity entity in entities)
                Remove(entity);
        }

        public void Remove(IEntity entity)
        {
            current.Remove(entity);
            entity.Health.EntityDead -= HandleEntityDead;
            entity.FactionUpdateComplete -= HandleFactionUpdateComplete;

            RaiseGroupUpdated();
        }
        #endregion

        #region Tracking Entities: FactionUpdateComplete, EntityDead
        private void HandleFactionUpdateComplete(IEntity entity, FactionUpdateArgs args)
        {
            if (!entity.IsLocalPlayerFaction())
                Remove(entity);
        }

        private void HandleEntityDead(IEntity entity, DeadEventArgs args)
        {
            Remove(entity);
        }
        #endregion
    }
    
    public interface ISelectionGroupHandler : IPreRunGameService
    {
        bool IsActive { get; }
        IReadOnlyList<ISelectionGroup> Groups { get; }
    }

    public class SelectionGroupHandler : MonoBehaviour, ISelectionGroupHandler
    {
        #region Attributes
        [SerializeField, Tooltip("Enable assigning, appending and substracting entity selection groups?")]
        private bool isActive = true;
        public bool IsActive => isActive && assignKey.IsValid();

        [SerializeField, Tooltip("Required control type that defines the key used to assign selected entities into a selection group.")]
        private ControlType assignKey = null;
        [SerializeField, Tooltip("Optional control type that defines the key used to append the currently selected entities into a selection group.")]
        private ControlType appendKey = null;
        [SerializeField, Tooltip("Optional control type that defines the key used to subtract the currently selected entities from a selection group.")]
        private ControlType substractKey = null;
        [SerializeField, Tooltip("Optional control type that defines the key used to reset the currently selected entities from a selection group.")]
        private ControlType resetKey = null;

        [SerializeField, Tooltip("For each selection group slot, define an element in this array field.")]
        private SelectionGroup[] groups = new SelectionGroup[0];
        public IReadOnlyList<ISelectionGroup> Groups => groups;

        protected IGameLoggingService logger { private set; get; }
        protected IGameControlsManager controls { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.logger = gameMgr.GetService<IGameLoggingService>(); 
            this.controls = gameMgr.GetService<IGameControlsManager>(); 

            for (int i = 0; i < groups.Length; i++)
                groups[i].Init(i, gameMgr);

            if (!isActive)
                return;

            if(!assignKey.IsValid())
            {
                logger.LogWarning($"[{GetType().Name}] The 'Assign Group Key' field must be assigned to use the selection group with keys!");
                return;
            }
        }
        #endregion

        #region Handling Selection Groups
        private void Update()
        {
            if (!IsActive)
                return;

            SelectionGroupAction nextAction = SelectionGroupAction.select;

            if(controls.Get(assignKey))
                nextAction = SelectionGroupAction.assign;
            else if (appendKey.IsValid() && controls.Get(appendKey))
                nextAction = SelectionGroupAction.append;
            else if (substractKey.IsValid() && controls.Get(substractKey))
                nextAction = SelectionGroupAction.substract;
            else if (resetKey.IsValid() && controls.Get(resetKey))
                nextAction = SelectionGroupAction.reset;

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Update(nextAction))
                    return;
            }
        }
        #endregion
    }
}
