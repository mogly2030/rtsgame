using UnityEngine;

using RTSEngine.Game;
using RTSEngine.Utilities;

namespace RTSEngine.Cameras
{
    public interface IMainCameraControlHandler
    {
        bool IsActive { get; set; }

        void Init(IGameManager gameMgr);

        void PreUpdateInput();
        void UpdateInput();
        void Apply();
    }

    public interface IMainCameraPanningHandler : IMainCameraControlHandler
    {
        bool IsPanning { get; }
        Vector3 LastPanDirection { get; }
        float CurrPanningSpeed { get; }

        bool IsFollowingTarget { get; }

        float CurrOffsetX { get; }
        float CurrOffsetZ { get; }

        void LookAt(Vector3 targetPosition, bool smooth, float smoothFactor = 0.1F);
        void SetFollowTarget(Transform transform, bool lockMovementUntilCentered = true);

        void SetPosition(Vector3 position);
    }

    public interface IMainCameraZoomHandler : IMainCameraControlHandler
    {
        bool UseCameraNativeZoom { get; }

        float InitialHeight { get; }
        float LookAtTargetMinHeight { get; }

        float ZoomRatio { get;}
        float CurrZoomSpeed { get; }
        bool IsZooming { get; }
    }

    public interface IMainCameraRotationHandler : IMainCameraControlHandler
    {
        bool HasInitialRotation { get; }
        Vector3 InitialEulerAngles { get; }
        float CurrRotationSpeed { get; }
        bool IsRotating { get; }

        void ResetRotation(bool smooth);
    }
}
 