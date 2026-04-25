
using UnityEngine;
using UnityEngine.EventSystems;
using RTSEngine.Controls;
using RTSEngine.Utilities;
using RTSEngine.Utilities.Controls;

namespace RTSEngine.Cameras
{

    [System.Serializable]
    public class MainCameraKeyboardPanningHandler : MainCameraPanningHandlerBase
    {
        #region Attributes
        // If the input axis panning is enabled, the defined axis can be used to move the camera
        [System.Serializable]
        public struct InputAxisPanning
        {
            public bool enabled;
            public string horizontal;
            public string vertical;
        }
        [Header("Keyboard Controls")]
        [SerializeField, Tooltip("Pan the camera using input axis.")]
        private InputAxisPanning inputAxisPanning = new InputAxisPanning { enabled = true, horizontal = "Horizontal", vertical = "Vertical" };

        // If the keyboard button panning is enabling, player will be able to use keyboard keys to move the camera
        [System.Serializable]
        public struct KeyPanning
        {
            public bool enabled;
            public ControlType up;
            public ControlType down;
            public ControlType right;
            public ControlType left;
        }
        [SerializeField, Tooltip("Pan the camera using keys.")]
        private KeyPanning keyPanning = new KeyPanning { enabled = false };

        [Space(), SerializeField, Tooltip("Pan the camera with a mouse button.")]
        private MouseButtonSmoothInput mouseButtonPanning = new MouseButtonSmoothInput { buttonType = MouseButtonType.wheel, smoothFactor = 0.1f };

        // When the player's mouse cursor is on the edge of the screen, should the camera move or not?
        [System.Serializable]
        public struct ScreenEdgePanning
        {
            public bool enabled;
            public float size;
            [Tooltip("When enabled, screen edge panning would be disabled when the player's mouse cursor is on a UI element even if it was on the defined screen edge.")]
            public bool ignoreUI;
        }
        [Space(), SerializeField, Tooltip("Pan the camera when the mouse is over the screen edge.")]
        private ScreenEdgePanning screenEdgePanning = new ScreenEdgePanning { enabled = true, size = 25.0f, ignoreUI = false };

        [Space(), SerializeField, Tooltip("When a control type is defined and held down, the panning speed is modified by the defined factor.")]
        private ControlTypeModifier panSpeedModifier = new ControlTypeModifier { factor = 2.0f };
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            controls.InitControlType(panSpeedModifier.controlType);
        }
        #endregion

        #region Update/Apply Input
        public override void UpdateInput()
        {
            currPanDirection = Vector3.zero;

            // If the pan on screen edge is enabled and we either are ignoring UI elements on the edge of the screen or the player's mouse is not over one
            if (screenEdgePanning.enabled && (screenEdgePanning.ignoreUI || !EventSystem.current.IsPointerOverGameObject()))
            {
                // If the mouse is in either one of the 4 edges of the screen then move it accordinly  
                if (Input.mousePosition.x <= screenEdgePanning.size && Input.mousePosition.x >= 0.0f)
                    currPanDirection.x = -1.0f;
                else if (Input.mousePosition.x >= Screen.width - screenEdgePanning.size && Input.mousePosition.x <= Screen.width)
                    currPanDirection.x = 1.0f;

                if (Input.mousePosition.y <= screenEdgePanning.size && Input.mousePosition.y >= 0.0f)
                    currPanDirection.z = -1.0f;
                else if (Input.mousePosition.y >= Screen.height - screenEdgePanning.size && Input.mousePosition.y <= Screen.height)
                    currPanDirection.z = 1.0f;
            }

            // Camera pan on key input (overwrites the screen edge pan if it has been enabled and had effect on this frame)
            if (keyPanning.enabled)
            {
                if(controls.Get(keyPanning.up))
                    currPanDirection.z = 1.0f;
                else if(controls.Get(keyPanning.down))
                    currPanDirection.z = -1.0f;

                if(controls.Get(keyPanning.right))
                    currPanDirection.x = 1.0f;
                else if(controls.Get(keyPanning.left))
                    currPanDirection.x = -1.0f;
            }

            // Camera pan on axis input (overwrites the screen edge pan/key input axis if it has been enabled and had effect on this frame)
            if (inputAxisPanning.enabled)
            {
                if (Mathf.Abs(Input.GetAxis(inputAxisPanning.horizontal)) > 0.25f)
                    currPanDirection.x = Mathf.Sign(Input.GetAxis(inputAxisPanning.horizontal)) * 1.0f;
                if (Mathf.Abs(Input.GetAxis(inputAxisPanning.vertical)) > 0.25f)
                    currPanDirection.z = Mathf.Sign(Input.GetAxis(inputAxisPanning.vertical)) * 1.0f;
            }

            if(controls.GetMouseButton(mouseButtonPanning.buttonType))
            {
                currPanDirection.x = cameraController.MousePositionDelta.x * mouseButtonPanning.InversionAwareSmoothFactor;
                currPanDirection.z = cameraController.MousePositionDelta.y * mouseButtonPanning.InversionAwareSmoothFactor;

                if(LastPanDirection != Vector3.zero)
                    IsPointerInputActive = true;
            }
            if(controls.GetMouseButtonUp(mouseButtonPanning.buttonType))
            {
                IsPointerInputActive = false;
            }

            CurrModifier = controls.IsControlTypeEnabled(panSpeedModifier.controlType)
                ? panSpeedModifier.factor
                : 1.0f;

            currPanDirection.y = 0.0f;
        }

        protected override void OnNonFollowTargetPanning()
        {
            SetPosition(cameraController.MainCamera.transform.position
                + Quaternion.Euler(new Vector3(0f, cameraController.MainCamera.transform.eulerAngles.y, 0f)) * LastPanDirection * CurrPanningSpeed * Time.deltaTime);
        }
        #endregion
    }
}
 