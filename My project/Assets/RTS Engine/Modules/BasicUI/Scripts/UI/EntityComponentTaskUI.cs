using RTSEngine.Controls;
using RTSEngine.EntityComponent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace RTSEngine.UI
{
    [RequireComponent(typeof(Button))]
    public class EntityComponentTaskUI : BaseTaskUI<EntityComponentTaskUIAttributes>
    {
        protected override Sprite Icon => Attributes.locked && Attributes.lockedData.icon != null 
            ? Attributes.lockedData.icon
            : Attributes.data.icon; 

        protected override Color IconColor => Attributes.locked 
            ? Attributes.lockedData.color 
            : Color.white;

        protected override bool IsTooltipEnabled => Attributes.data.tooltipEnabled;

        protected override string TooltipDescription => String.IsNullOrEmpty(Attributes.tooltipText) ? Attributes.data.description : Attributes.tooltipText;

        [SerializeField, Tooltip("UI Text to display the control key assigned to the task, if there is any.")]
        private TextMeshProUGUI controlLabel = null;

        protected IGameControlsManager controlsMgr { private set; get; }

        #region Initializing/Terminating
        protected override void OnInit()
        {
            this.controlsMgr = gameMgr.GetService<IGameControlsManager>(); 
        }
        #endregion

        #region Disabling Task UI
        protected override void OnDisabled()
        {
            if(controlLabel.IsValid())
                controlLabel.enabled = false;
        }
        #endregion

        #region Handling Attributes Reload
        protected override void OnReload()
        {
            if (controlLabel.IsValid())
            {
                if (Attributes.data.controlType.IsValid())
                {
                    controlLabel.enabled = true;
                    controlLabel.text = $"{controlsMgr.GetCurrentKeyCode(Attributes.data.controlType)}";
                }
                else
                {
                    controlLabel.enabled = false;
                }
            }
        }
        #endregion

        private void Update()
        {
            if (!Attributes.data.controlType.IsValid())
                return;

            if (controlsMgr.GetUp(Attributes.data.controlType))
                OnClick();
        }

        protected override void OnClick()
        {
            if (Attributes.locked)
                return;

            if (Attributes.launchOnce)
                Attributes.sourceTracker.EntityComponents.FirstOrDefault()?.OnTaskUIClick(Attributes);
            else
            {
                // The ToArray() call is used to create a new Enumerable of the source IEntityComponent components because the original collection might be updated when the task is clicked.
                foreach (IEntityComponent component in Attributes.sourceTracker.EntityComponents.ToArray())
                    component.OnTaskUIClick(Attributes);
            }

            if (Attributes.data.hideTooltipOnClick)
                HideTaskTooltip();
        }
    }
}
