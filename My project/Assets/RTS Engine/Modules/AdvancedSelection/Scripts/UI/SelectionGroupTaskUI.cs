using RTSEngine.Effect;
using RTSEngine.Game;
using RTSEngine.Selection;
using RTSEngine.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace RTSEngine.UI
{
    [System.Serializable]
    public struct SelectionGroupUIData
    {
        [Tooltip("Show a description of the task in the tooltip when the mouse hovers over the selection group task?")]
        public bool tooltipEnabled;
        [TextArea(), Tooltip("Description of the selection group task that will appear in the selection group UI panel.")]
        public string description;
    }

    public struct SelectionGroupUIAttributes : ITaskUIAttributes
    {
        public SelectionGroupUIData data;

        public ISelectionGroup group;
    }

    public class SelectionGroupTaskUI : BaseTaskUI<SelectionGroupUIAttributes>
    {
        #region Attributes
        protected override Sprite Icon => Attributes.group.Entities.Count > 0
            ? Attributes.group.Entities[0].Icon
            : null;

        protected override Color IconColor => Color.white;

        protected override bool IsTooltipEnabled => Attributes.data.tooltipEnabled;

        protected override string TooltipDescription => Attributes.data.description;

        protected override bool hideButtonOnDisable => true;

        [SerializeField, Tooltip("UI Text to display the amount of the entities in the selection group.")]
        private TextMeshProUGUI label = null;

        [SerializeField, Tooltip("Enable holding down this button to free up the selection group if assigned or to set selection to this group if free.")]
        private bool enableHoldDownInteraction = false;
        private const float holdDownTime = 1.0f;
        private float holdDownTimer;
        private bool isHoldingDown = false;
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (!logger.RequireValid(label,
                $"[{GetType().Name}] The 'Label' field must be assigned!"))
                return;

            isHoldingDown = false;
        }
        #endregion

        #region Disabling Task UI
        protected override void OnDisabled()
        {
            label.enabled = false;
        }
        #endregion

        #region Handling Attributes Reload
        protected override void OnReload()
        {
            if(Attributes.group.Entities.Count > 0)
            {
                image.enabled = true; 
                image.sprite = Icon;

                label.enabled = true;
                label.text = $"{Attributes.group.Entities.Count()}";
            }
            else
            {
                label.enabled = false;
                image.enabled = false;
            }
        }
        #endregion

        #region Handling Task UI Interaction
        private void Update()
        {
            if (!isHoldingDown)
                return;

            holdDownTimer -= Time.deltaTime;

            if(holdDownTimer <= 0.0f)
            {
                if (selectionMgr.Count == 0 && Attributes.group.Entities.Count > 0)
                    Attributes.group.Update(SelectionGroupAction.reset, requireKey: false);
                else
                    Attributes.group.Update(SelectionGroupAction.append, requireKey: false);

                isHoldingDown = false;
            }
        }

        protected override void OnPointerUp()
        {
            if (!isHoldingDown)
                return;

            isHoldingDown = false;
        }

        protected override void OnPointerDown()
        {
            if (!enableHoldDownInteraction || isHoldingDown)
                return;

            isHoldingDown = true;
            holdDownTimer = holdDownTime;
        }

        protected override void OnClick(PointerEventData.InputButton clickType)
        {
            switch(clickType)
            {
                case PointerEventData.InputButton.Left:
                    Attributes.group.Update(SelectionGroupAction.select, requireKey: false);
                    break;
                case PointerEventData.InputButton.Right:
                    if(Attributes.group.Entities.Count > 0)
                        Attributes.group.Update(SelectionGroupAction.reset, requireKey: false);
                    else 
                        Attributes.group.Update(SelectionGroupAction.assign, requireKey: false);
                    break;
            }
        }
        #endregion
    }
}
