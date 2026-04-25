using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Faction;
using RTSEngine.ResourceExtension;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace RTSEngine.UI
{

    public struct FactionSlotStatsUIAttributes : ITaskUIAttributes
    {
        public int factionID;
        public string text;
        public FactionSlotState state;
        public Color color;
    }

    public class FactionSlotStatsUIHandler : BaseTaskPanelUIHandler<FactionSlotStatsUIAttributes>
    {
        [SerializeField, Tooltip("Parent transform of the active task UI elements that display unit type counts.")]
        private Transform panel = null;

        [SerializeField, Tooltip("Input the main resoruce type whose count will be used to order the faction slot stats (the higher the count is, the higher the faction slot is in the rankings to win the current game)."), FormerlySerializedAs("resourceType")]
        private ResourceTypeInfo orderResourceType = null;

        [SerializeField, Tooltip("Input the resoruce types whose count will be displayed in the stats text for each faction slot.")]
        private List<ResourceTypeInfo> displayResourceTypes = new List<ResourceTypeInfo>();
        private Dictionary<ResourceTypeInfo, string>[] displayResourceCounts = null;

        private List<ITaskUI<FactionSlotStatsUIAttributes>> tasks;

        // Game services
        protected IResourceManager resourceMgr { private set; get; }
        protected IGameUITextDisplayManager textDisplayer { private set; get; }

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (!logger.RequireValid(panel,
              $"[{GetType().Name}] The 'Panel' field must be assigned!"))
                return;

            this.resourceMgr = gameMgr.GetService<IResourceManager>();
            this.textDisplayer = gameMgr.GetService<IGameUITextDisplayManager>();

            tasks = new List<ITaskUI<FactionSlotStatsUIAttributes>>();

            if (orderResourceType.IsValid() && !resourceMgr.IsResourceTypeValidInGame(orderResourceType))
            {
                logger.LogError($"[{GetType().Name}] 'Order Resource Type' field value is not a resource defined in this map scene!");
                orderResourceType = null;
            }

            displayResourceCounts = new Dictionary<ResourceTypeInfo, string>[gameMgr.FactionSlots.Count];

            for (int slotID = 0; slotID < gameMgr.FactionSlots.Count; slotID++)
            {
                IFactionSlot slot = gameMgr.FactionSlots[slotID];

                displayResourceCounts[slotID] = new Dictionary<ResourceTypeInfo, string>();

                ITaskUI<FactionSlotStatsUIAttributes> task = Create(tasks, panel);

                if (orderResourceType.IsValid())
                {
                    resourceMgr.FactionResources[slotID].ResourceHandlers[orderResourceType].FactionResourceAmountUpdated += HandleResourceAmountUpdated;
                }

                int i = 0;
                while(i < displayResourceTypes.Count)
                {
                    ResourceTypeInfo resourceType = displayResourceTypes[i];
                    if (resourceType.IsValid() && resourceMgr.IsResourceTypeValidInGame(resourceType))
                    {
                        if(resourceType != orderResourceType)
                            resourceMgr.FactionResources[slotID].ResourceHandlers[resourceType].FactionResourceAmountUpdated += HandleResourceAmountUpdated;

                        displayResourceCounts[slotID].Add(resourceType, String.Empty);
                        UpdateResourceCount(slotID, resourceType);
                        i++;
                    }
                    else
                    {
                        displayResourceTypes.RemoveAt(i);
                    }
                }

                slot.FactionSlotStateUpdated += HandleFactionStateUpdated;

                UpdateStats(slotID);
            }
        }

        private void HandleFactionStateUpdated(IFactionSlot slot, EventArgs args)
        {
            UpdateStats(slot.ID);
        }

        private void HandleResourceAmountUpdated(IFactionResourceHandler sender, ResourceUpdateEventArgs args)
        {
            UpdateResourceCount(sender.FactionID, args.ResourceType);
            UpdateStats(sender.FactionID);
        }

        public override void Disable()
        {
            foreach (IFactionSlot slot in gameMgr.FactionSlots)
            {
                if (orderResourceType.IsValid())
                {
                    resourceMgr.FactionResources[slot.ID].ResourceHandlers[orderResourceType].FactionResourceAmountUpdated -= HandleResourceAmountUpdated;
                }

                foreach (ResourceTypeInfo resourceType in displayResourceTypes)
                {
                    if (resourceType.IsValid() && resourceMgr.IsResourceTypeValidInGame(resourceType) && resourceType != orderResourceType)
                        resourceMgr.FactionResources[slot.ID].ResourceHandlers[resourceType].FactionResourceAmountUpdated -= HandleResourceAmountUpdated;
                }

                slot.FactionSlotStateUpdated -= HandleFactionStateUpdated;
            }
        }
        #endregion
        private void UpdateResourceCount(int factionID, ResourceTypeInfo resourceType)
        {
            if (!factionID.IsValidIndex(displayResourceCounts)
                || !displayResourceCounts[factionID].ContainsKey(resourceType))
                return;

            textDisplayer.FactionResourceHandlerToString(resourceMgr.FactionResources[factionID].ResourceHandlers[resourceType], out string amountText);
            displayResourceCounts[factionID][resourceType] = amountText;
        }

        public void UpdateStats(int factionID)
        {
            var task = tasks[factionID];
            var slot = gameMgr.GetFactionSlot(factionID);

            textDisplayer.FactionSlotToStatsText(slot, out string statsText);

            if(displayResourceTypes.Count > 0)
                statsText += ": ";
            for (int i = 0; i < displayResourceTypes.Count; i++)
            {
                ResourceTypeInfo resourceType = displayResourceTypes[i];
                if (displayResourceCounts[factionID].TryGetValue(resourceType, out string amountText))
                {
                    statsText += $"{amountText} - ";
                }
            }

            statsText = statsText.Remove(statsText.Length - 3, 3);

            task.Reload(new FactionSlotStatsUIAttributes
            {
                factionID = factionID,
                text = statsText,
                state = slot.State,
                color = slot.Data.color
            });
        }
    }
}
