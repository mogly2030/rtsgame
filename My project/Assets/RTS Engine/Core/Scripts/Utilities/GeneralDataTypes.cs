using RTSEngine.Controls;
using UnityEngine;

namespace RTSEngine.Utilities.Controls
{
    [System.Serializable]
    public struct ControlTypeModifier
    {
        public ControlType controlType;
        public float factor;
    }
}

namespace RTSEngine.Utilities
{
    [System.Serializable]
    public struct SmoothSpeedRange
    {
        public FloatRange valueRange;
        public bool invert;
        public float GetValue(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            return valueRange.min + (invert ? 1.0f - ratio : ratio) * (valueRange.max - valueRange.min);
        }

        [Range(float.Epsilon, 1.0f)]
        public float smoothValue;
    }

    [System.Serializable]
    public struct SmoothSpeed
    {
        public float value;

        public float acceleration;
        public float deceleration;
    }

    [System.Serializable]
    public struct MouseButtonSmoothInput
    {
        public MouseButtonType buttonType;
        public bool invert;
        public float smoothFactor;

        public float InversionAwareSmoothFactor => (invert ? -1 : 1) * smoothFactor;
    }
}
