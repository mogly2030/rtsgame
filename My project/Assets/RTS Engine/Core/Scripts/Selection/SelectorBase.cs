using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;

using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.BuildingExtension;
using RTSEngine.Cameras;
using RTSEngine.Terrain;
using RTSEngine.Task;
using RTSEngine.EntityComponent;
using RTSEngine.UI;
using RTSEngine.Controls;
using RTSEngine.Logging;
using RTSEngine.Search;
using RTSEngine.Utilities;
using RTSEngine.Event;

namespace RTSEngine.Selection
{
    public abstract class SelectorBase : MonoBehaviour, ISelector
    {
        #region Attributes
        [SerializeField, Tooltip("Enable to allow the player to double click on an entity to select all entities of the same type within a defined range.")]
        private bool enableDoubleClickSelect = true;
        [SerializeField, Tooltip("When double click selection is enabled, you can enable this field to select only the entities of the same type that are visible currently in the screen instead of using a defined range. When enabled, the 'DOuble Click Select Range' becomes irrelevant.")]
        private bool doubleClickSelectVisibleEntities = true;
        [SerializeField, Tooltip("Entities of the same type within this range of the original double click will be selected."), Min(0.0f)]
        private float doubleClickSelectRange = 10.0f;

        private bool lockDoubleClick = false;
        private float doubleClickTimer;

        public abstract bool MultipleSelectionModeEnabled { get; }

        [Header("Layers"), SerializeField, Tooltip("Input the layer's name to be used for entity selection objects.")]
        private string entitySelectionLayer = "EntitySelection";
        public string EntitySelectionLayer => entitySelectionLayer;
        public LayerMask EntitySelectionLayerMask { private set; get; }

        [SerializeField, Tooltip("Input the terrain areas that are clickable for the player.")]
        private TerrainAreaType[] clickableTerrainAreas = new TerrainAreaType[0];

        // This would incldue the layers defined in the clickableTerrainAreas and entitySelectionLayer
        public LayerMask ClickableLayerMask { private set; get; }

        protected RaycastHitter raycast;

        [Header("Selection Flash")]
        [SerializeField, Tooltip("Duration of the selection marker flash.")]
        private float flashTime = 1.0f;
        [SerializeField, Tooltip("How often does the selection marker flash?")]
        private float flashRepeatTime = 0.2f;

        [SerializeField, Tooltip("Color used when the selection marker of a friendly entity is flashing.")]
        private Color friendlyFlashColor = Color.green;
        [SerializeField, Tooltip("Color used when the selection marker of an enemy entity is flashing.")]
        private Color enemyFlashColor = Color.red;

        // Locking mouse selector
        // A game service is allowed to lock the mouse selector and it must be the same game service that unlocks it.
        // When the mouse selector is unlocked, it is only unlocked the next frame after the Unlock call has been successfully made
        private IGameService lockedBy = null;
        private bool isAwaitingUnlock = false;
        public bool IsLocked => lockedBy.IsValid() || isAwaitingUnlock;

        // Game services
        protected IGameManager gameMgr { private set; get; }
        protected IGameUIManager gameUIMgr { private set; get; }
        protected ISelectionManager selectionMgr { private set; get; }
        protected IBuildingPlacement placementMgr { private set; get; }
        protected ITerrainManager terrainMgr { private set; get; }
        protected ITaskManager taskMgr { private set; get; }
        protected IMainCameraController mainCameraController { private set; get; }
        protected IGameControlsManager controls { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        protected IGridSearchHandler gridSearch { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;

            this.gameUIMgr = gameMgr.GetService<IGameUIManager>();
            this.placementMgr = gameMgr.GetService<IBuildingPlacement>();
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.terrainMgr = gameMgr.GetService<ITerrainManager>();
            this.taskMgr = gameMgr.GetService<ITaskManager>();
            this.mainCameraController = gameMgr.GetService<IMainCameraController>();
            this.controls = gameMgr.GetService<IGameControlsManager>();
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.gridSearch = gameMgr.GetService<IGridSearchHandler>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();

            ClickableLayerMask = new LayerMask();
            EntitySelectionLayerMask = new LayerMask();

            EntitySelectionLayerMask |= (1 << LayerMask.NameToLayer(entitySelectionLayer));
            ClickableLayerMask |= EntitySelectionLayerMask;

            if (!logger.RequireValid(clickableTerrainAreas,
              $"[{GetType().Name}] 'Clickable Terrain Areas' field has some invalid elements!"))
                return;

            globalEvent.EntitySelectedGlobal += HandleEntitySelectedGlobal;

            foreach (TerrainAreaType area in clickableTerrainAreas)
                ClickableLayerMask |= area.Layers;

            raycast = new RaycastHitter(ClickableLayerMask);

            doubleClickTimer = 0.0f;

            lockedBy = null;
            isAwaitingUnlock = false;

            OnInit();
        }

        protected virtual void OnInit() { }

        private void OnDestroy()
        {
            globalEvent.EntitySelectedGlobal -= HandleEntitySelectedGlobal;
        }
        #endregion

        #region Updating
        private void Update()
        {
            if (doubleClickTimer > 0)
                doubleClickTimer -= Time.deltaTime;

            if (gameMgr.State != GameStateType.running
                || placementMgr.IsLocalPlayerPlacingBuilding
                || !gameUIMgr.HasPriority(this)
                || EventSystem.current.IsPointerOverGameObject())
            {
                OnInactiveUpdate();
                return;
            }

            if (IsLocked)
            {
                if (isAwaitingUnlock)
                {
                    isAwaitingUnlock = false;
                }
                return;
            }

            OnActiveUpdate();
        }

        protected virtual void OnInactiveUpdate() { }

        protected virtual void OnActiveUpdate() { }
        #endregion

        #region Handling Double Click Selection
        private void HandleEntitySelectedGlobal(IEntity entity, EntitySelectionEventArgs args)
        {
            if (lockDoubleClick || !args.IsLocalPlayerClickSelection)
                return;

            if (doubleClickTimer > 0.0f)
            {
                doubleClickTimer = 0.0f;
                lockDoubleClick = true;

                if (!MultipleSelectionModeEnabled)
                    selectionMgr.RemoveAll();

                //if this is the second click (double click), select all entities of the same type within a certain range
                SelectEntitisInRange(entity, playerCommand: true);
                lockDoubleClick = false;
                return;
            }

            doubleClickTimer = 0.5f;
        }

        private IEntity nextRangeSelectionSource = null;
        private ErrorMessage IsTargetValidForRangeSelection(SetTargetInputData data)
        {
            if (!nextRangeSelectionSource.IsValid() || !data.target.instance.IsValid() || !data.target.instance.Selection.IsValid())
                return ErrorMessage.invalid;
            else if (data.target.instance.Code != nextRangeSelectionSource.Code)
                return ErrorMessage.entityCodeMismatch;
            else if (!nextRangeSelectionSource.IsSameFaction(data.target.instance))
                return ErrorMessage.factionMismatch;
            else if (doubleClickSelectVisibleEntities)
            {
                Vector3 entityScreenPosition = mainCameraController.MainCamera.WorldToScreenPoint(data.target.instance.Selection.transform.position);

                if (entityScreenPosition.x < 0.0f || entityScreenPosition.x > Screen.width
                    || entityScreenPosition.y < 0.0f || entityScreenPosition.y > Screen.height)
                    return ErrorMessage.positionOutOfSelectionBounds;
            }

            return ErrorMessage.none;
        }

        public void SelectEntitisInRange(IEntity source, bool playerCommand)
        {
            if (!enableDoubleClickSelect 
                || !source.IsValid()
                || source.IsFree)
                return;

            nextRangeSelectionSource = source;

            IReadOnlyList<IEntity> entitiesInRange;
            if (doubleClickSelectVisibleEntities)
            {
                gridSearch.SearchVisible(
                    IsTargetValidForRangeSelection,
                    playerCommand,
                    out entitiesInRange);
            }
            else
            {
                gridSearch.Search(
                    source.transform.position,
                    doubleClickSelectRange,
                    -1,
                    IsTargetValidForRangeSelection,
                    playerCommand,
                    out entitiesInRange);
            }
            selectionMgr.Add(entitiesInRange);
        }
        #endregion

        #region Handling Selection Flash
        public void FlashSelection(IEntity entity, bool isFriendly)
        {
            if (!entity.IsValid()
                || !entity.SelectionMarker.IsValid())
                return;

            entity.SelectionMarker.StartFlash(
                flashTime,
                flashRepeatTime,
                (isFriendly == true) ? friendlyFlashColor : enemyFlashColor);
        }
        #endregion

        #region Locking Mouse Selector
        public bool Lock(IGameService service)
        {
            if (lockedBy.IsValid())
                return false;

            lockedBy = service;
            isAwaitingUnlock = false;

            return true;
        }

        public bool Unlock(IGameService service)
        {
            if (lockedBy != service)
                return false;

            isAwaitingUnlock = true;
            lockedBy = null;
            return true;
        }
        #endregion

    }
}