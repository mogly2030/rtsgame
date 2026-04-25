
using RTSEngine.Game;
using UnityEngine;

namespace RTSEngine.Controls
{
    public interface IGameControlsManager : IPreRunGamePriorityService
    {
        bool Get(ControlType controlType, bool requireValid = false);
        bool GetDown(ControlType controlType, bool requireValid = false);
        bool GetUp(ControlType controlType, bool requireValid = false);

        bool Get(ControlType controlType, KeyBehaviour behaviour, bool requireValid = false);
        bool IsControlTypeEnabled(ControlType controlType);
        bool InitControlType(ControlType controlType);
        KeyCode GetCurrentKeyCode(ControlType controlType);
        bool GetMouseButton(MouseButtonType type);
        bool GetMouseButtonUp(MouseButtonType type);
        bool GetMouseButtonDown(MouseButtonType type);
    }
}
