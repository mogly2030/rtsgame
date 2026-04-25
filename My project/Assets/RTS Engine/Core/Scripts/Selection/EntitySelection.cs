using System;
using System.Linq;

using UnityEngine;

using RTSEngine.Audio;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.UI;
using RTSEngine.Selection;
using RTSEngine.EntityComponent;
using RTSEngine.Attack;
using System.Collections.Generic;

namespace RTSEngine.Event
{
    public class EntityDeselectionEventArgs : EventArgs
    {
        public DeselectedType DeselectedType { private set; get; }

        public EntityDeselectionEventArgs(DeselectedType deselectedType)
        {
            this.DeselectedType = deselectedType;
        }
    }

    public class EntitySelectionEventArgs : EventArgs
    {
        public SelectionType Type { private set; get; }
        public SelectedType SelectedType { private set; get; }
        public bool IsLocalPlayerClickSelection { private set; get; }

        public EntitySelectionEventArgs(SelectionType type, SelectedType selectedType, bool isLocalPlayerClickSelection)
        {
            this.Type = type;
            this.SelectedType = selectedType;
            this.IsLocalPlayerClickSelection = isLocalPlayerClickSelection;
        }
    }
}

namespace RTSEngine.Selection
{
    public abstract class EntitySelection : MonoBehaviour, IEntitySelection, IEntityPreInitializable
    {
        #region Class Attributes
        public IEntity Entity { private set; get; }

        [SerializeField, Tooltip("Colliders that define how the entity can be selected.")]
        private EntitySelectionCollider[] selectionColliders = new EntitySelectionCollider[0];

        [SerializeField, Tooltip("Can the player select this entity?")]
        private bool isActive = true;
        public bool IsActive { get { return isActive; } set { isActive = value; } }

        [SerializeField, Tooltip("Allow the player to select this entity only if it belongs to their faction?")]
        // If this is set to true then only the local player can select the entity associated to this.
        private bool selectOwnerOnly = false; 
        public bool SelectOwnerOnly { get { return selectOwnerOnly; } set { selectOwnerOnly = value; } }

        public bool CanSelect => isActive && !Entity.Health.IsDead && (!SelectOwnerOnly || RTSHelper.IsLocalPlayerFaction(Entity)) && extraSelectCondition;
        protected virtual bool extraSelectCondition => true;

        public bool IsSelected { private set; get; }

        [SerializeField, Tooltip("Audio clip to play when the entity is selected.")]
        protected AudioClipFetcher selectionAudio = new AudioClipFetcher();

#if RTSENGINE_FOW
        public HideInFogRTS HideInFog { private set; get; }
#endif

        // Game services
        protected ISelectionManager selectionMgr { private set; get; }
        protected IGameAudioManager audioMgr { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IAttackManager attackMgr { private set; get; } 
        #endregion

        #region Raising Events
        public event CustomEventHandler<IEntity, EntitySelectionEventArgs> Selected;
        public event CustomEventHandler<IEntity, EntityDeselectionEventArgs> Deselected;

        private void RaiseSelected (EntitySelectionEventArgs args)
        {
            var handler = Selected;
            handler?.Invoke(Entity, args);

            globalEvent.RaiseEntitySelectedGlobal(Entity, args);
        }
        private void RaiseDeselected (EntityDeselectionEventArgs args)
        {
            var handler = Deselected;
            handler?.Invoke(Entity, args);

            globalEvent.RaiseEntityDeselectedGlobal(Entity);
        }
        #endregion

        #region Initializing/Terminating
        public void OnEntityPreInit(IGameManager gameMgr, IEntity entity)
        {
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.audioMgr = gameMgr.GetService<IGameAudioManager>();
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();
            this.attackMgr = gameMgr.GetService<IAttackManager>(); 

            this.Entity = entity;

            if (!logger.RequireValid(selectionColliders,
                $"[{GetType().Name} - {entity.Code}] 'Selection Colliders' field has unassigned elements!"))
                return;

            foreach (EntitySelectionCollider collider in selectionColliders)
                collider.OnEntityPostInit(gameMgr, Entity);

#if RTSENGINE_FOW
            if (gameMgr.FoWMgr)
            {
                HideInFog = GetComponent<HideInFogRTS>();
                Assert.IsNotNull(HideInFog,
                    $"[EntitySelection - {entity.Code}] A component of type {typeof(HideInFogRTS).Name} must be attached to the entity!");
            }
#endif

            IsSelected = false;

            OnInit();
        }

        protected virtual void OnInit() { }

        public void Disable() 
        {
            OnDisabled();
        }

        protected virtual void OnDisabled() { }
        #endregion

        #region Selection Collider(s) Methods
        public bool IsSelectionCollider(Collider collider)
        {
            return selectionColliders.Contains(collider.GetComponent<EntitySelectionCollider>());
        }
        #endregion

        #region Selection State Update
        public void OnSelected(EntitySelectionEventArgs args)
        {
            audioMgr.PlaySFX(selectionAudio.Fetch(), Entity, loop:false);
            Entity.SelectionMarker?.Enable();

            IsSelected = true;
            RaiseSelected(args);
        }

        public void OnDeselected (EntityDeselectionEventArgs args)
        {
            Entity.SelectionMarker?.Disable();

            IsSelected = false;

            RaiseDeselected(args);
        }
        #endregion

        #region Launching Awaited Tasks
        public void OnAwaitingTaskAction(EntityComponentTaskUIAttributes taskData)
        {
            var entityTargetComps = taskData.sourceTracker.EntityTargetComponents;
            if (entityTargetComps[0] is IMovementComponent
                || entityTargetComps[0] is IAttackComponent)
            {
                taskData.sourceTracker.Entities
                    .SetTargetFirstMany(new SetTargetInputData
                    {
                        target = Entity.ToTargetData(),
                        playerCommand = true,
                        includeMovement = entityTargetComps[0] is IMovementComponent
                    });
            }
            else
            {
                foreach (var sourceComponent in entityTargetComps)
                    sourceComponent.OnAwaitingTaskTargetSet(taskData, Entity.ToTargetData());
            }

        }
        #endregion

        #region Launching Direct Action (Right Mouse Click)
        public void OnDirectAction()
        {
            // Only when the selection is active is the player allowed to launch a direct action on it
            if (!IsActive)
                return;

            RTSHelper.SetTargetFirstMany(
                selectionMgr.GetEntitiesList(EntityType.all, exclusiveType: false, localPlayerFaction: true),
                new SetTargetInputData
                {
                    target = RTSHelper.ToTargetData(Entity),
                    playerCommand = true,
                    includeMovement = false
                });
        }
        #endregion
    }
}
