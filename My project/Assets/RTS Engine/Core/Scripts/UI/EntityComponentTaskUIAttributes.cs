using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.ResourceExtension;
using System.Collections.Generic;

namespace RTSEngine.UI
{
    public struct EntityComponentTaskUIAttributes : ITaskUIAttributes
    {
        public EntityComponentTaskUIData data;

        public int factionID;

        public string title;
        public IReadOnlyList<ResourceInput> requiredResources;
        public IReadOnlyList<FactionEntityRequirement> factionEntityRequirements;

        public bool launchOnce;

        public IEntityComponentGroupDisplayer sourceTracker;

        public bool locked;
        public EntityComponentLockedTaskUIData lockedData;

        public string tooltipText;
    }
}
