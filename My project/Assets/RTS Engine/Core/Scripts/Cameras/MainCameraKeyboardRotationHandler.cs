using UnityEngine;

using RTSEngine.Game;
using RTSEngine.Controls;
using RTSEngine.Terrain;
using RTSEngine.Utilities;
using RTSEngine.BuildingExtension;
using RTSEngine.Event;
using RTSEngine.Entities;
using System;
using RTSEngine.Utilities.Controls;

namespace RTSEngine.Cameras
{
    public class MainCameraKeyboardRotationHandler : MainCameraRotationHandlerBase 
    {
        #region Attributes
        [System.Serializable]
        public struct KeyRotation
        {
            public bool enabled;
            public ControlType positive;
            public ControlType negative;
        }
        [Header("Keyboard Controls")]
        [SerializeField, Tooltip("Rotate the camera with keys")]
        protected KeyRotation keyRotation = new KeyRotation { enabled = false };

        [Space(), SerializeField, Tooltip("Rotate the camera with a mouse button.")]
        private MouseButtonSmoothInput mouseButtonRotation = new MouseButtonSmoothInput { buttonType = MouseButtonType.right, smoothFactor = 0.1f };

        [Space(), SerializeField, Tooltip("When a control type is defined and held down, the rotation speed is modified by the defined factor.")]
        private ControlTypeModifier rotationSpeedModifier = new ControlTypeModifier { factor = 2.0f };
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            controls.InitControlType(rotationSpeedModifier.controlType);
        }
        #endregion

        #region Updating/Applying Input 
        public override void UpdateInput()
        {
            PreUpdateInput();

            currRotationValue = Vector2.zero;

            // If the keyboard keys rotation is enabled, check for the positive and negative rotation keys and update the current rotation value accordinly
            if (keyRotation.enabled)
            {
                if (controls.Get(keyRotation.positive))
                    currRotationValue.x = 1.0f; 
                else if (controls.Get(keyRotation.negative))
                    currRotationValue.x = -1.0f;
            }

            if (currRotationValue == Vector2.zero)
            {
                // If the mouse wheel rotation is enabled and the player is holding the mouse wheel button, update the rotation value accordinly
                if (controls.GetMouseButton(mouseButtonRotation.buttonType))
                {
                    currRotationValue = cameraController.MousePositionDelta * mouseButtonRotation.InversionAwareSmoothFactor;

                    if (LastRotationValue != Vector2.zero)
                    {
                        IsPointerInputActive = true;
                    }
                }
                if (controls.GetMouseButtonUp(mouseButtonRotation.buttonType))
                {
                    triggerPointInputInactive = IsPointerInputActive;
                }
            }

            CurrModifier = controls.IsControlTypeEnabled(rotationSpeedModifier.controlType)
                ? rotationSpeedModifier.factor
                : 1.0f;
        }
        #endregion
    }
}
 