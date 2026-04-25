using UnityEngine;

namespace RTSEngine
{
    public static class RTSOptimizations
    {
        // Poolable Objects
        public static Vector3 POOLABLE_OBJECT_INACTIVE_POSITION => new(1337.0f, 1337.0f, 1337.0f);

        // Time Modifier
        public const int INIT_TIME_MODIFIER_DATA_CAPACITY = 200;
        public const bool LIMIT_TIMER_REMOVALS_PER_FRAME = true;
        public const int MAX_TIMER_REMOVALS_PER_FRAME = 10;

        // NPC
        public const int INIT_SENDABLE_UNITS_CAPACITY = 50;
    }
}

