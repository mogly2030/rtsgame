using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.ResourceExtension;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using RTSEngine.Selection;
using System.Linq;

namespace RTSEngine.UI
{

    public class ResourceTaskUI : BaseTaskUI<ResourceTaskUIAttributes>
    {
        protected override Sprite Icon => Attributes.resourceHandler.Type.Icon;

        protected override Color IconColor => Color.white;

        protected override bool IsTooltipEnabled => Attributes.tooltipEnabled;

        protected override string TooltipDescription => Attributes.tooltipText;

        [SerializeField, Tooltip("Child UI Text object used to display the resource's current amount (and capacity if applicable)")]
        private TextMeshProUGUI amountTextUI = null;

        [SerializeField, Tooltip("Child UI Text object used to display the resource's collectors count for the local player faction.")]
        private TextMeshProUGUI collectorsTextUI = null;

        protected IResourceManager resourceMgr { private set; get; }

        protected override void OnInit()
        {
            this.resourceMgr = gameMgr.GetService<IResourceManager>();
        }

        protected override void OnDisabled()
        {
            if (Attributes.resourceHandler.IsValid())
            {
                Attributes.resourceHandler.FactionResourceAmountUpdated -= HandleResourceAmountUpdated;

                if(Attributes.displayCollectorCount)
                    Attributes.resourceHandler.FactionResourceProducersUpdated -= HandleResourceProducersUpdated;
            }
        }

        protected override void OnPreReload()
        {
            if (Attributes.resourceHandler.IsValid())
            {
                Attributes.resourceHandler.FactionResourceAmountUpdated -= HandleResourceAmountUpdated;

                if(Attributes.displayCollectorCount)
                    Attributes.resourceHandler.FactionResourceProducersUpdated -= HandleResourceProducersUpdated;
            }
        }

        protected override void OnReload()
        {
            Attributes.resourceHandler.FactionResourceAmountUpdated += HandleResourceAmountUpdated;
            if(Attributes.displayCollectorCount)
                Attributes.resourceHandler.FactionResourceProducersUpdated += HandleResourceProducersUpdated;
            else if(collectorsTextUI.IsValid())
                collectorsTextUI.gameObject.SetActive(false);

            UpdateCollectorsText();
            UpdateAmountText();
        }

        private void HandleResourceProducersUpdated(IFactionResourceHandler sender, EventArgs args)
        {
            UpdateCollectorsText();
        }

        private void HandleResourceAmountUpdated(IFactionResourceHandler resourceHandler, ResourceUpdateEventArgs args)
        {
            UpdateAmountText();
        }

        private void UpdateCollectorsText()
        {
            if (collectorsTextUI.IsValid())
                collectorsTextUI.text = $"{Attributes.resourceHandler.ProducerCount}";
        }

        private void UpdateAmountText()
        {
            if (amountTextUI.IsValid())
                amountTextUI.text = Attributes.resourceHandler.Type.HasCapacity
                    ? $"{Attributes.resourceHandler.Amount}/{Attributes.resourceHandler.Capacity}"
                    : $"{Attributes.resourceHandler.Amount}";

            if (Attributes.resourceHandler.Type.HasCapacity)
                image.color = Attributes.resourceHandler.FreeAmount <= 0
                    ? Attributes.maxCapacityColor
                    : Color.white;
        }

        protected override void OnClick()
        {
            if (Attributes.resourceHandler.ProducerCount == 0)
                return;

            selectionMgr.RemoveAll();
            selectionMgr.Add(Attributes.resourceHandler.Collectors.Select(collector => collector.Entity));
            selectionMgr.Add(Attributes.resourceHandler.Generators.Select(generators => generators.Entity));
        }
    }
}
