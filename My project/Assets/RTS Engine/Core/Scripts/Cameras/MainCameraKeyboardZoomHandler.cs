
using UnityEngine;

using RTSEngine.Game;
using RTSEngine.BuildingExtension;
using RTSEngine.Controls;
using RTSEngine.Terrain;
using RTSEngine.Logging;
using RTSEngine.Utilities.Controls;

namespace RTSEngine.Cameras
{
    public class MainCameraKeyboardZoomHandler : MainCameraZoomHandlerBase
    {
        #region Zoom Attributes
        [System.Serializable]
        public struct MouseWheelZoom
        {
            public bool enabled;
            public bool invert;
            public string name;
            public float sensitivity;
        }
        [Header("Keyboard Controls")]
        [SerializeField, Tooltip("Use the mouse wheel to zoom.")]
        private MouseWheelZoom mouseWheelZoom = new MouseWheelZoom { enabled = true, invert = false, name = "Mouse ScrollWheel", sensitivity = 20.0f };

        [System.Serializable]
        public struct KeyZoom
        {
            public bool enabled;
            public ControlType inKey;
            public ControlType outKey;
        }
        [SerializeField, Tooltip("Zoom using keys.")]
        private KeyZoom keyZoom = new KeyZoom { enabled = false };

        [Space(), SerializeField, Tooltip("Enable this option to make the camera zoom always to/from mouse position.")]
        private bool toMouseAlways = false;
        [SerializeField, Tooltip("Assign a control type in this field that when held down, the zooming occurs towards the current mouse position.")]
        private ControlType toMouseControlType = null;

        [Space(), SerializeField, Tooltip("When a control type is defined and held down, the zooming speed is modified by the defined factor.")]
        private ControlTypeModifier zoomSpeedModifier = new ControlTypeModifier { factor = 2.0f };
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            controls.InitControlType(zoomSpeedModifier.controlType);
        }
        #endregion

        #region Handling Camera Zoom
        public override void UpdateInput()
        {
            zoomValue = 0.0f;

            if (placementMgr.IsLocalPlayerPlacingBuilding && !allowBuildingPlaceZoom)
                return;

            // Camera zoom on keys
            if (keyZoom.enabled)
            {
                if (controls.Get(keyZoom.inKey))
                    zoomValue = -1.0f;
                else if (controls.Get(keyZoom.outKey))
                    zoomValue = 1.0f;
            }

            // Camera zoom when the player is moving the mouse scroll wheel
            if (mouseWheelZoom.enabled)
                zoomValue += Input.GetAxis(mouseWheelZoom.name) * mouseWheelZoom.sensitivity 
                    * (mouseWheelZoom.invert ? -1.0f : 1.0f);

            // By default the zoom direction is the middle of the screen, i.e the forward direction of the main camera transform 
            // otherwise, zoom can also occur towards the current mouse position
            if (toMouseAlways || controls.IsControlTypeEnabled(toMouseControlType))
            {
                terrainMgr.ScreenPointToTerrainPoint(Input.mousePosition, null, out Vector3 mouseTerrainPosition);
                zoomDirection = (cameraController.MainCamera.transform.position - mouseTerrainPosition).normalized;
            }
            else
            {
                zoomDirection = cameraController.MainCamera.transform.forward;
            }

            CurrModifier = controls.IsControlTypeEnabled(zoomSpeedModifier.controlType)
                ? zoomSpeedModifier.factor
                : 1.0f;
        }
        #endregion
    }
}
 