using UnityEngine;

using RTSEngine.Game;
using RTSEngine.BuildingExtension;
using RTSEngine.Controls;
using RTSEngine.Terrain;
using RTSEngine.Logging;
using RTSEngine.Utilities;

namespace RTSEngine.Cameras
{
    public abstract class MainCameraZoomHandlerBase : MonoBehaviour, IMainCameraZoomHandler
    {
        #region Attributes
        public bool IsActive { set; get; }

        [SerializeField, Tooltip("How fast can the main camera zoom? Max speed is reached when the camera is zoomed out the most (max height) and min speed is reached when the camera is when the camera is zoomed in the most (min height).")]
        private SmoothSpeedRange zoomSpeed = new SmoothSpeedRange { valueRange = new FloatRange(8f,15f), smoothValue = 0.1f };
        protected float CurrModifier { set; get; } 
        public float CurrZoomSpeed => zoomSpeed.GetValue(ZoomRatio) * CurrModifier;

        [Space(), SerializeField, Tooltip("Enable to zoom using the camera's field of view (FOV) in case of a perspective camera or the orthographic size in case of an orthographic projection mode instead of the height of the camera.")]
        private bool useCameraNativeZoom = false;
        public bool UseCameraNativeZoom => useCameraNativeZoom;

        // Gets either incremented or decremented depending on the zoom inputs.
        // Zoom value is treated / updated differently depending if FOV/size zoom or Transform based zoom is enabled.
        protected float zoomValue = 0.0f;
        protected float lastZoomValue = 0.0f;
        protected Vector3 zoomDirection = Vector3.zero;

        [Space(), SerializeField, Tooltip("The height that the main camera starts with.")]
        private float initialHeight = 15.0f;
        public float InitialHeight => initialHeight;
        [SerializeField, Tooltip("The minimum height the main camera is allowed to have.")]
        protected float minHeight = 5.0f;
        [SerializeField, Tooltip("The maximum height the main camera is allowed to have.")]
        protected float maxHeight = 18.0f;
        public float ZoomRatio
            => (maxHeight - (useCameraNativeZoom ? cameraController.MainCamera.fieldOfView : cameraController.MainCamera.transform.position.y)) / (maxHeight - minHeight);

        [Space(), SerializeField, Tooltip("Allow the player to zoom the camera when they are placing a building?")]
        protected bool allowBuildingPlaceZoom = true;

        public float LookAtTargetMinHeight => useCameraNativeZoom ? 0.0f : minHeight;

        protected bool triggerPointInputInactive;
        public bool IsPointerInputActive { protected set; get; }
        public bool IsZooming => IsPointerInputActive || zoomValue != 0.0f;

        protected IGameManager gameMgr { private set; get; }
        protected IGameControlsManager controls { private set; get; }
        protected IMainCameraController cameraController { private set; get; }
        protected IBuildingPlacement placementMgr { private set; get; }
        protected ITerrainManager terrainMgr { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;
            this.controls = gameMgr.GetService<IGameControlsManager>();
            this.placementMgr = gameMgr.GetService<IBuildingPlacement>();
            this.terrainMgr = gameMgr.GetService<ITerrainManager>();
            this.cameraController = gameMgr.GetService<IMainCameraController>();
            this.logger = gameMgr.GetService<IGameLoggingService>();

            if (!logger.RequireTrue(initialHeight >= minHeight && initialHeight <= maxHeight,
                $"[{GetType().Name}] The 'Initial Height' value must be between the minimum and maximum allowed height values."))
                return;

            CurrModifier = 1.0f;

            // Initial FOV/orthographic-size or camera position height configurations
            if (useCameraNativeZoom)
                SetCameraNativeZoom(initialHeight, forceUpdate: true);
            else
                cameraController.MainCamera.transform.position = new Vector3(cameraController.MainCamera.transform.position.x, initialHeight, cameraController.MainCamera.transform.position.y);

            cameraController.RaiseCameraTransformUpdated();

            IsActive = true;

            OnInit();
        }

        protected virtual void OnInit() { }
        #endregion

        #region Update/Apply Input
        public void PreUpdateInput()
        {
            if(triggerPointInputInactive)
            {
                triggerPointInputInactive = false;
                IsPointerInputActive = false;
            }
        }

        public abstract void UpdateInput();

        public void Apply()
        {
            if (!IsActive)
                return;

            if (useCameraNativeZoom)
            {
                ApplyCameraNativeZoom();
            }
            else
            {
                // Target direction used to zoom in / out the main camera
                ApplyTransformZoom();
            }
        }

        protected virtual void ApplyTransformZoom()
        {
            Vector3 targetDirection = Vector3.zero;
            // Hold the last camera position so that we restore it later if the height leaves the allowed boundaries
            Vector3 lastCamPos = cameraController.MainCamera.transform.position;

            // Handling actual zooming in/out
            lastZoomValue = Mathf.Lerp(
                lastZoomValue,
                zoomValue,
                zoomSpeed.smoothValue);

            //if ((lastCamPos.y > minHeight && lastZoomValue > 0.0f) || (lastCamPos.y < maxHeight && lastZoomValue < 0.0f))
            targetDirection += CurrZoomSpeed * Time.deltaTime * lastZoomValue * zoomDirection;

            // Updating the camera height by adding the target movement direction
            cameraController.MainCamera.transform.position += targetDirection;

            // Apply zooming limit
            if (cameraController.MainCamera.transform.position.y < minHeight || cameraController.MainCamera.transform.position.y > maxHeight)
            {
                lastCamPos.y = Mathf.Clamp(lastCamPos.y, minHeight, maxHeight);
                cameraController.MainCamera.transform.position = lastCamPos;
            }

            if (lastZoomValue != zoomValue)
                cameraController.RaiseCameraTransformUpdated();
        }

        private void ApplyCameraNativeZoom()
        {
            // Handling actual zooming in/out
            lastZoomValue = Mathf.Lerp(
                lastZoomValue,
                zoomValue,
                zoomSpeed.smoothValue);

            // Only if there is change in the zooming related inputs
            float targetHeight = Mathf.Clamp((cameraController.IsOrthographic ? cameraController.MainCamera.orthographicSize : cameraController.MainCamera.fieldOfView)
                + lastZoomValue * Time.deltaTime * CurrZoomSpeed,
                minHeight,
                maxHeight);

            SetCameraNativeZoom(targetHeight);
        }

        private void SetCameraNativeZoom(float value, bool forceUpdate = false)
        {
            if (cameraController.IsOrthographic)
                SetOrthographicSize(value, forceUpdate);
            else
                SetPerspectiveFOV(value, forceUpdate);

        }

        private void SetPerspectiveFOV(float value, bool forceUpdate = false)
        {
            if (!forceUpdate && cameraController.MainCamera.fieldOfView == value)
                return;

            cameraController.MainCamera.fieldOfView = value;
            if (cameraController.MainCameraUI.IsValid())
                cameraController.MainCameraUI.fieldOfView = value;

            cameraController.RaiseCameraTransformUpdated();
        }

        private void SetOrthographicSize(float value, bool forceUpdate = false)
        {
            if (cameraController.MainCamera.orthographicSize == value)
                return;

            cameraController.MainCamera.orthographicSize = value;
            if (cameraController.MainCameraUI.IsValid())
                cameraController.MainCameraUI.orthographicSize = value;

            cameraController.RaiseCameraTransformUpdated();
        }
        #endregion
    }
}
 