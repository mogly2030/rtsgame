using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Selection;
using System;
using UnityEngine.EventSystems;
using RTSEngine.BuildingExtension;
using RTSEngine.Entities;

namespace RTSEngine.UI
{
    public enum TooltipDisplayType { pointerEnterExit, pointerUpDown }

    public abstract class BaseBareTaskUI<T> : MonoBehaviour, ITaskUI<T> where T : ITaskUIAttributes
    {
        #region Attributes
        [SerializeField, Tooltip("Enable this option to deactivateA the task's game object when the task is disabled. Disable this field to disable all UI components of the task but keep the game object active when disabled.")]
        private bool deactivateObjectOnDisable = false;

        public bool IsEnabled { private set; get; } = false;

        public T Attributes { private set; get; }

        protected IGameManager gameMgr { private set; get; } 
        protected IGameService handlerService { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Initializing/Terminating
        public virtual void Init(IGameManager gameMgr, IGameService handlerService)
        {
            this.gameMgr = gameMgr;

            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();

            this.handlerService = handlerService;

            OnBareInit();
        }

        protected virtual void OnBareInit() { }

        private void OnDestroy()
        {
            Disable();
            OnDestroyed();
        }

        protected virtual void OnDestroyed() { }
        #endregion

        #region Disabling Task UI
        public void Disable()
        {
            if(deactivateObjectOnDisable)
                gameObject.SetActive(false);

            this.enabled = false;

            IsEnabled = false;

            OnBareDisabled();
        }

        protected virtual void OnBareDisabled() { }
        #endregion

        #region Reloading Attributes
        public void Reload(T attributes)
        {
            OnPreReload();
            this.Attributes = attributes;

            if(deactivateObjectOnDisable)
                gameObject.SetActive(true);
            this.enabled = true;

            IsEnabled = true;

            OnBareReload();
        }

        protected virtual void OnPreReload() { }

        protected virtual void OnBareReload() { }
        #endregion

        #region Interacting with Task UI
        public void Click() => OnClick();
        protected virtual void OnClick() { }
        #endregion
    }

    public abstract class BaseTaskUI<T> : BaseBareTaskUI<T>, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ITaskUI<T> where T : ITaskUIAttributes
    {
        #region Attributes
        protected abstract Sprite Icon { get; }
        protected abstract Color IconColor { get; }

        protected abstract bool IsTooltipEnabled { get; }
        protected abstract string TooltipDescription { get; }

        // Is this TaskUI the soruce of the currently displayed tooltip?
        private bool isCurrentTooltipSource;

        [SerializeField, Tooltip("UI Image component to display the task's icon.")]
        protected Image image = null;

        protected virtual bool hideButtonOnDisable => false;
        protected Button button = null;

        [SerializeField, Tooltip("How to display the tooltip? Using pointer enter and exit or pointer up and down. First option is suitable for desktop while the second would be more suitable for mobile platforms.")]
        private TooltipDisplayType tooltipDisplayType = TooltipDisplayType.pointerEnterExit;

        // Game services
        protected ISelectionManager selectionMgr { private set; get; }
        protected ISelector selector { private set; get; } 
        #endregion

        #region Initializing/Terminating
        protected sealed override void OnBareInit()
        {
            button = GetComponent<Button>();

            if (!logger.RequireValid(image,
                $"[{GetType().Name}] The 'Image' field must be assigned!")
                || !logger.RequireValid(button,
                $"[{GetType().Name}] This component must be attached to a game object that has a '{typeof(Button).Name}' component attached to it!"))
                return;

            button.onClick.RemoveAllListeners();

            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.selector = gameMgr.GetService<ISelector>(); 

            isCurrentTooltipSource = false;

            globalEvent.BuildingPlacementStartGlobal += HandleBuildingPlacementStartGlobal;
            globalEvent.BuildingPlacementResetGlobal += HandleBuildingPlacementResetGlobal;

            OnInit();

            Disable();
        }

        protected virtual void OnInit() { }

        protected sealed override void OnDestroyed()
        {
            globalEvent.BuildingPlacementStartGlobal -= HandleBuildingPlacementStartGlobal;
            globalEvent.BuildingPlacementResetGlobal -= HandleBuildingPlacementResetGlobal;
        }
        #endregion

        #region Handling Events: Building Placement
        private void HandleBuildingPlacementStartGlobal(IBuilding building, EventArgs args)
        {
            if(building.IsLocalPlayerFaction())
                button.interactable = false;
        }

        private void HandleBuildingPlacementResetGlobal(IBuildingPlacementHandler placementHandler, EventArgs args)
        {
            if(placementHandler.FactionSlot.IsLocalPlayerFaction())
                button.interactable = IsEnabled;
        }
        #endregion

        #region Disabling Task UI
        protected sealed override void OnBareDisabled()
        {
            ToggleUI(false);

            OnDisabled();
        }

        protected virtual void OnDisabled() { }
        #endregion

        #region Reloading Attributes
        protected sealed override void OnBareReload()
        {
            image.sprite = Icon;
            image.color = IconColor;

            this.enabled = true;

            ToggleUI(true);

            // If the reload happens while this TaskUI is displaying its tooltip then update the tooltip
            if (isCurrentTooltipSource)
            {
                globalEvent.RaiseShowTooltipGlobal(
                    this,
                    new MessageEventArgs(MessageType.info, message: TooltipDescription));
            }

            OnReload();
        }

        private void ToggleUI(bool enable) 
        {
            image.enabled = enable;
            if (hideButtonOnDisable)
            {
                button.enabled = enable;
                if(button.image.IsValid())
                    button.image.enabled = enable;
            }

            button.interactable = enable;
        }

        protected virtual void OnReload() { }
        #endregion

        #region Handling Task UI Tooltip
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipDisplayType == TooltipDisplayType.pointerEnterExit)
                DisplayTaskTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipDisplayType == TooltipDisplayType.pointerEnterExit)
                HideTaskTooltip();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnPointerDown();

            if (tooltipDisplayType == TooltipDisplayType.pointerUpDown)
                DisplayTaskTooltip();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnPointerUp();

            if (tooltipDisplayType == TooltipDisplayType.pointerUpDown)
                HideTaskTooltip();
        }

        protected void DisplayTaskTooltip()
        {
            if (enabled
                && IsTooltipEnabled)
            {
                globalEvent.RaiseShowTooltipGlobal(
                    this,
                    new MessageEventArgs(MessageType.info, message: TooltipDescription));

                isCurrentTooltipSource = true;
            }
        }

        protected void HideTaskTooltip ()
        {
            globalEvent.RaiseHideTooltipGlobal(this);

            isCurrentTooltipSource = false;
        }
        #endregion

        #region Interacting with Task UI
        protected virtual void OnPointerDown() { }
        protected virtual void OnPointerUp() { }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick();
            OnClick(eventData.button);
        }

        protected virtual void OnClick(PointerEventData.InputButton clickType) { }
        #endregion
    }
}
