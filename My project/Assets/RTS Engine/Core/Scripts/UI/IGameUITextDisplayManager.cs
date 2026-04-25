using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Faction;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.ResourceExtension;
using System.Collections.Generic;

namespace RTSEngine.UI
{
    public interface IGameUITextDisplayManager : IPreRunGameService
    {
        bool UnitCreationTaskTooltipToString(UnitCreationTask creationTask, out string text);
        bool UpgradeTaskTooltipToString(UpgradeTask upgradeTask, out string text);
        bool BuildingCreationTaskTooltipToString(BuildingCreationTask creationTask, out string text);
        bool EntityComponentTaskTooltipInputToText(IEntityComponentTaskInput taskInput, IEntity targetPrefab, out string text);
        bool EntityComponentTaskTooltipToText(EntityComponentTaskUIData taskData, out string text);

        bool EntityDescriptionToText(IEntity entity, out string text);
        bool EntityNameToText(IEntity entity, out string text);

        bool ResourceInputToString(IEnumerable<ResourceInput> resourceInputSet, out string text);
        bool ResourceTooltipToString(ResourceTypeInfo resourceType, out string text);

        bool PlayerErrorMessageToString(PlayerErrorMessageWrapper msgWrapper, out string text);
        bool FactionSlotDefeatToText(IFactionSlot factionSlot, out string text);
        bool SelectionGroupToTooltip(string defaultTooltip, out string text);
        bool FactionSlotToStatsText(IFactionSlot factionSlot, out string text);
        bool EntityComponentTaskTitleToString(IEntityComponentTaskInput taskInput, out string text);
        bool FactionResourceHandlerToString(IFactionResourceHandler resourceHandler, out string text);
    }
}