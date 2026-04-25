using System.Collections.Generic;
using System.Linq;
using System;

using UnityEngine;

using RTSEngine.Game;
using RTSEngine.Logging;

namespace RTSEngine.Controls
{
    [System.Serializable]
    public struct ControlTypeRuntimeWrapper
    {
        public ControlType type;
        public KeyCode currentKeyCode;
    }

    public enum KeyBehaviour { get, getDown, getUp };

    public enum ControlEnableType { hold = 0, toggle = 1 }

    public enum MouseButtonType { none = -1, left = 0, right = 1, wheel = 2 }

    public class GameControlsManager : MonoBehaviour, IGameControlsManager 
    {
        public int ServicePriority => 1;

        private Dictionary<string, ControlTypeRuntimeWrapper> registeredControls = null;

        private Dictionary<string, bool> togglableControlsState = null;
        private List<string> togglablesControls = null;

        // Game Services
        protected IGameLoggingService logger { private set; get; }

        public void Init(IGameManager gameMgr)
        {
            this.logger = gameMgr.GetService<IGameLoggingService>();

            registeredControls = new Dictionary<string, ControlTypeRuntimeWrapper>();

            togglableControlsState = new Dictionary<string, bool>();
            togglablesControls = new List<string>();
        }

        #region Mouse Button
        public bool GetMouseButton(MouseButtonType type) => type != MouseButtonType.none && Input.GetMouseButton((int)type);
        public bool GetMouseButtonUp(MouseButtonType type) => type != MouseButtonType.none && Input.GetMouseButtonUp((int)type);
        public bool GetMouseButtonDown(MouseButtonType type) => type != MouseButtonType.none && Input.GetMouseButtonDown((int)type);
        #endregion

        public KeyCode GetCurrentKeyCode(ControlType controlType)
            => registeredControls.TryGetValue(controlType.Key, out var runtimeWrapper)
                ? runtimeWrapper.currentKeyCode
                : controlType.DefaultKeyCode;

        public bool Get(ControlType controlType, bool requireValid = false) => Get(controlType, KeyBehaviour.get, requireValid);
        public bool GetDown(ControlType controlType, bool requireValid = false) => Get(controlType, KeyBehaviour.getDown, requireValid);
        public bool GetUp(ControlType controlType, bool requireValid = false) => Get(controlType, KeyBehaviour.getUp, requireValid);
        public bool Get(ControlType controlType, KeyBehaviour behaviour, bool requireValid = false)
        {
            if(!controlType.IsValid())
            {
                if (requireValid)
                    logger.LogWarning($"[{GetType().Name}] The input control type is invalid! Please follow the trace to find the component providing the input control type and assign it!");
                return false;
            }

            if (!registeredControls.TryGetValue(controlType.Key, out ControlTypeRuntimeWrapper runtimeWrapper)
                && !InitControlType(controlType, out runtimeWrapper))
            {
                logger.LogWarning($"[{GetType().Name}] Unable to find runtime wrapper for control type!");
            }

            switch(behaviour)
            {
                case KeyBehaviour.get:
                    return Input.GetKey(runtimeWrapper.currentKeyCode);
                case KeyBehaviour.getUp:
                    return Input.GetKeyUp(runtimeWrapper.currentKeyCode);
                case KeyBehaviour.getDown:
                    return Input.GetKeyDown(runtimeWrapper.currentKeyCode);

                default:
                    return false;
            }
        }

        public bool InitControlType(ControlType controlType) => InitControlType(controlType, out _);
        public bool InitControlType(ControlType controlType, out ControlTypeRuntimeWrapper runtimeWrapper)
        {
            runtimeWrapper = new ControlTypeRuntimeWrapper();

            if (!controlType.IsValid() || registeredControls.ContainsKey(controlType.Key))
                return false;

            runtimeWrapper = new ControlTypeRuntimeWrapper
            {
                type = controlType,
                currentKeyCode = controlType.DefaultKeyCode
            };
            registeredControls.Add(controlType.Key, runtimeWrapper);

            if (controlType.EnableTye == ControlEnableType.toggle)
            {
                togglablesControls.Add(controlType.Key);
                togglableControlsState.Add(controlType.Key, false);
            }

            return true;
        }

        public bool IsControlTypeEnabled(ControlType controlType)
        {
            if (!controlType.IsValid())
                return false;

            if(!registeredControls.ContainsKey(controlType.Key))
            {
                logger.LogError($"[{GetType().Name}] To use this method, you need to call Init() on the control type first! Make sure to call it in the initialization process of the component the control type is a field of.");
                return false;
            }

            switch(controlType.EnableTye)
            {
                case ControlEnableType.hold:
                    return Get(controlType);
                case ControlEnableType.toggle:
                    return togglableControlsState[controlType.Key];
            }

            return true;
        }

        private void Update()
        {
            if (togglablesControls.Count == 0)
                return;

            foreach(var key in togglablesControls)
            {
                if (GetUp(registeredControls[key].type))
                    togglableControlsState[key] = !togglableControlsState[key];
            }
        }
    }
}
