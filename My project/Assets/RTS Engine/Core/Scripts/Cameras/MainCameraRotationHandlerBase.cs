
using RTSEngine.BuildingExtension;
using RTSEngine.Controls;
using RTSEngine.Game;
using RTSEngine.Terrain;
using RTSEngine.Utilities;
using UnityEngine;

namespace RTSEngine.Cameras
{
    public enum CameraRotationType { free = 0, rotateAround = 1 }

    public abstract class MainCameraRotationHandlerBase : MonoBehaviour, IMainCameraRotationHandler
    {
        #region Attributes
        public bool IsActive { set; get; }

        [SerializeField, Tooltip("Defines the initial rotation of the main camera.")]
        private Vector3 initialEulerAngles = new Vector3(45.0f, 45.0f, 0.0f);
        public Vector3 InitialEulerAngles => initialEulerAngles;
        private Quaternion initialRotation;
        /// <summary>
        /// True when the current rotation is different than the initially assigned rotation.
        /// </summary>
        public bool HasInitialRotation => cameraController.MainCamera.transform.eulerAngles - initialEulerAngles == Vector3.zero;

        [Space(), SerializeField, Tooltip("Have a fixed rotation when the camera is panning? When enabled, the camera rotation will be reset when the camera pans.")]
        private bool fixPanRotation = true;
        [SerializeField, Min(0), Tooltip("How far can the camera move before reverting to the initial rotation (if above field is enabled).")]
        private float allowedRotationPanSize = 0.2f;

        [Space(), SerializeField, Tooltip("How fast can the camera rotate?")]
        private SmoothSpeedRange rotationSpeed = new SmoothSpeedRange { valueRange = new FloatRange(30.0f, 50.0f), smoothValue = 0.1f };
        protected float CurrModifier { set; get; }
        public float CurrRotationSpeed => rotationSpeed.GetValue(cameraController.ZoomHandler.ZoomRatio) * CurrModifier;

        [SerializeField, Tooltip("Minimum rotation input value for an actual rotation to be executed. This allows to avoid small mouse movements of the right mouse button or the mouse wheel to trigger rotation. Values must be in [0,1) for both axis.")]
        private Vector2 minRotationTriggerValues = new Vector2(0.8f, 0.8f);

        [System.Serializable]
        public struct RotationLimit
        {
            public bool lockXRotation;
            public bool enableXLimit;
            public FloatRange xLimit;

            [Space()]
            public bool lockYRotation;
            public bool enableYLimit;
            public FloatRange yLimit;
        }
        [Space(), SerializeField, Tooltip("Limit the rotation of the main camera.")]
        private RotationLimit rotationLimit = new RotationLimit { enableYLimit = false, yLimit = new FloatRange(-360f, 360f), enableXLimit = true, xLimit = new FloatRange(25.0f, 90.0f) };

        [Space(), SerializeField, Tooltip("Reset camera rotation to initial values when placing a building? When enabled, player will not be allowed to rotate the camera as long as they are placing a building")]
        private bool resetRotationOnPlacement = true;
        [SerializeField, Tooltip("When resetting the rotation smoothly, this field defines how smooth the reset is.")]
        private float resetRotationSmoothFactor = 0.2f;

        [Space(), SerializeField, Tooltip("Pick the default rotation mode. Free: rotate in any direction. Rotate Around: rotating the main camera will always occur by orbiting/rotating around the position that the camera is looking at.")]
        private CameraRotationType defaultRotationType = CameraRotationType.rotateAround;
        [SerializeField, Tooltip("When the control type is enabled, it allows to use the alternative rotation type that is not set as the default one.")]
        private ControlType altRotationControlType = null;
        private bool isRotatingAround;
        private Vector3 rotateAroundCenter;

        // The current and last rotation value that is determined using the different rotation inputs.
        protected Vector2 currRotationValue;
        protected Vector2 LastRotationValue { private set; get; }

        protected bool triggerPointInputInactive;
        public bool IsPointerInputActive { protected set; get; }
        public bool IsRotating => IsPointerInputActive || currRotationValue != Vector2.zero;

        // When set to true, the main camera ignores all input until the rotation is reset back to the initial euler angles
        private bool isResettingRotation;

        protected IGameManager gameMgr { private set; get; }
        protected IGameControlsManager controls { private set; get; }
        protected IMainCameraController cameraController { private set; get; }
        protected ITerrainManager terrainMgr { private set; get; }
        protected IBuildingPlacement placementMgr { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;
            this.controls = gameMgr.GetService<IGameControlsManager>();
            this.cameraController = gameMgr.GetService<IMainCameraController>();
            this.terrainMgr = gameMgr.GetService<ITerrainManager>();
            this.placementMgr = gameMgr.GetService<IBuildingPlacement>();

            initialRotation = Quaternion.Euler(initialEulerAngles);
            CurrModifier = 1.0f;

            ResetRotation(smooth: false);

            minRotationTriggerValues.x = Mathf.Clamp(minRotationTriggerValues.x, 0.0f, 0.99f);
            minRotationTriggerValues.y = Mathf.Clamp(minRotationTriggerValues.y, 0.0f, 0.99f);

            IsActive = true;

            OnInit();
        }

        protected virtual void OnInit() { }
        #endregion

        #region Updating/Applying Input 
        public void PreUpdateInput()
        {
            if(triggerPointInputInactive)
            {
                triggerPointInputInactive = false;
                IsPointerInputActive = false;
                LastRotationValue = Vector2.zero;
            }
        }

        public abstract void UpdateInput();

        public void Apply()
        {
            if (rotationLimit.lockXRotation || Mathf.Abs(currRotationValue.x) < minRotationTriggerValues.x)
                currRotationValue.x = 0.0f;
            if (rotationLimit.lockYRotation || Mathf.Abs(currRotationValue.y) < minRotationTriggerValues.y)
                currRotationValue.y = 0.0f;

            if (resetRotationOnPlacement && placementMgr.IsLocalPlayerPlacingBuilding)
            {
                if (!HasInitialRotation && !isResettingRotation)
                {
                    Vector3 lookAtCenter = cameraController.ScreenToWorldPoint(RTSHelper.MiddleScreenPoint, applyOffset: false);
                    ResetRotation(smooth: false);
                    cameraController.PanningHandler.LookAt(lookAtCenter, smooth: false);
                }
                currRotationValue = Vector2.zero;
            }

            if (currRotationValue != Vector2.zero && cameraController.PanningHandler.IsFollowingTarget)
                cameraController.PanningHandler.SetFollowTarget(null);

            // If the player is moving the camera and the camera's rotation must be fixed during movement...
            //... or if the camera is following a target, lock camera rotation to default value
            if (isResettingRotation
                || ((fixPanRotation && cameraController.PanningHandler.LastPanDirection.magnitude > allowedRotationPanSize) || cameraController.PanningHandler.IsFollowingTarget))
            {
                cameraController.MainCamera.transform.rotation = Quaternion.Lerp(
                    cameraController.MainCamera.transform.rotation,
                    initialRotation,
                    resetRotationSmoothFactor);

                if (HasInitialRotation)
                    isResettingRotation = false;
                return;
            }

            if (!IsActive)
                return;

            // Smoothly update the last rotation value towards the current one
            LastRotationValue = Vector2.Lerp(
                LastRotationValue,
                currRotationValue,
                rotationSpeed.smoothValue);

            Vector3 nextEulerAngles = cameraController.MainCamera.transform.rotation.eulerAngles;

            bool altRotationTypeEnabled = controls.IsControlTypeEnabled(altRotationControlType);
            isRotatingAround = (defaultRotationType == CameraRotationType.rotateAround && !altRotationTypeEnabled) || altRotationTypeEnabled;

            if (isRotatingAround)
            {
                // The position that the camera will be rotating around is the world position of the middle of the screen.
                // Only update it when the player is not actively rotating the camera..
                if (currRotationValue == Vector2.zero)
                    rotateAroundCenter = cameraController.ScreenToWorldPoint(RTSHelper.MiddleScreenPoint, applyOffset: false);

                // orbit horizontally
                cameraController.MainCamera.transform.RotateAround(rotateAroundCenter,
                    Vector3.up,
                    LastRotationValue.x * CurrRotationSpeed * Time.deltaTime);
                // orbit vertically
                cameraController.MainCamera.transform.RotateAround(rotateAroundCenter,
                    cameraController.MainCamera.transform.TransformDirection(Vector3.right),
                    LastRotationValue.y * CurrRotationSpeed * Time.deltaTime);

                nextEulerAngles = cameraController.MainCamera.transform.eulerAngles;
            }
            else
            {
                nextEulerAngles.y -= CurrRotationSpeed * Time.deltaTime * LastRotationValue.x;
                nextEulerAngles.x -= CurrRotationSpeed * Time.deltaTime * LastRotationValue.y;
            }

            // Limit the y/x euler angless if that's enabled
            if (rotationLimit.enableXLimit)
                nextEulerAngles.x = Mathf.Clamp(nextEulerAngles.x, rotationLimit.xLimit.min, rotationLimit.xLimit.max);
            if (rotationLimit.enableYLimit)
                nextEulerAngles.y = Mathf.Clamp(nextEulerAngles.y, rotationLimit.yLimit.min, rotationLimit.yLimit.max);

            cameraController.MainCamera.transform.rotation = Quaternion.Euler(nextEulerAngles);

            if (LastRotationValue != Vector2.zero)
                cameraController.RaiseCameraTransformUpdated();
        }

        public void ResetRotation(bool smooth)
        {
            if (HasInitialRotation)
                return;

            if (smooth)
            {
                // This will allow the Apply() method to smoothly move the rotation back to the initial values.
                isResettingRotation = true;
            }
            else
            {
                cameraController.MainCamera.transform.rotation = initialRotation;
                isResettingRotation = false;
            }
        }
        #endregion

    }
}
 