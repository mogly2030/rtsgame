using RTSEngine.Game;
using UnityEngine;

namespace RTSEngine.Controls
{
    [CreateAssetMenu(fileName = "newControlType", menuName = "RTS Engine/Control Type", order = 200)]
    public class ControlType : RTSEngineScriptableObject
    {
        [SerializeField, Tooltip("Unique identifier for the control, this is used to identify the key to allow multiple components to use the same control key.")]
        private string key = "unique_key_identifier";
        public override string Key => key;

        [SerializeField, Tooltip("Default key code used to trigger the control.")]
        private KeyCode defaultKeyCode = KeyCode.None;
        public KeyCode DefaultKeyCode => defaultKeyCode;

        [SerializeField, Tooltip("If the key will be used for enabling a state then set the enable type to an option other than none, this way the controls manager will keep track of the enable state of the control type.")]
        private ControlEnableType enableType = ControlEnableType.hold;
        public ControlEnableType EnableTye => enableType;
    }
}
