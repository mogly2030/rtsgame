using RTSEngine.Entities;
using System;

namespace RTSEngine.Event
{
    public struct HealthUpdateArgs
    {
        public int Value { get; }
        public IEntity Source { get; }

        // For max health update, the value is the last max health value
        public HealthUpdateArgs(int value, IEntity source)
        {
            this.Value = value;
            this.Source = source;
        }
    }

    public class DeadEventArgs : EventArgs
    {
        public bool IsUpgrade { get; }
        public IEntity Source { get; }
        public float DestroyObjectDelay { get; }

        public DeadEventArgs(bool isUpgrade, IEntity source, float destroyObjectDelay)
        {
            this.IsUpgrade = isUpgrade;
            this.Source = source;
            DestroyObjectDelay = destroyObjectDelay;
        }
    }
}
