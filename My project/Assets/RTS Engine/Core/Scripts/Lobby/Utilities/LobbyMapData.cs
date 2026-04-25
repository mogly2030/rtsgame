using UnityEngine;

using RTSEngine.NPC;
using RTSEngine.Lobby.Logging;
using RTSEngine.Logging;
using RTSEngine.Faction;
using System.Collections.Generic;
using System.Linq;

namespace RTSEngine.Lobby.Utilities
{
    [System.Serializable]
    public struct FactionTypeLobbyData
    {
        public FactionTypeInfo factionType;
        [Tooltip("Define the NPC types that this faction type is allowed to have.")]
        public NPCType[] npcTypes;
    }

    [System.Serializable]
    public struct LobbyMapData
    {
        [Tooltip("The scene name that has the RTS Engine map to load when the game starts. Make sure the scene is added to the build settings.")]
        public string sceneName;

        [Tooltip("Name to display for the map in the UI menu.")]
        public string name;
        [Tooltip("Description to display for the map in the UI menu.")]
        public string description;

        [SerializeField, Tooltip("Minimum amount of factions allowed to play the map.")]
        public IntRange factionsAmount;

        [SerializeField, Tooltip("Types of factions available to select to play with in the map.")]
        private FactionTypeLobbyData[] factionTypes;
        public IReadOnlyList<FactionTypeInfo> GetFactionTypes() => factionTypes.Select(type => type.factionType).ToArray();
        public FactionTypeInfo GetFactionType(int factionTypeIndex) => factionTypes[factionTypeIndex].factionType;

        public IReadOnlyList<NPCType> GetNPCTypes(int factionTypeIndex) => factionTypeIndex.IsValidIndex(factionTypes) ? factionTypes[factionTypeIndex].npcTypes : factionTypes[0].npcTypes;
        public NPCType GetNPCType(int factionTypeIndex, int npcTypeIndex)
        {
            IReadOnlyList<NPCType> npcTypes = GetNPCTypes(factionTypeIndex);
            return npcTypeIndex.IsValidIndex(npcTypes) ? npcTypes[npcTypeIndex] : npcTypes[0];
        }

        [SerializeField, Tooltip("Input the NPC types to be picked when the faction type is set to random.")]
        private NPCType[] randomFactionTypeNPCTypes;
        public IReadOnlyList<NPCType> GetRandomFactionTypeNPCTypes() => randomFactionTypeNPCTypes;

        public void Init(ILobbyManagerBase lobbyMgr)
        {
            ILoggingService logger = lobbyMgr.GetService<ILobbyLoggingService>();

            if (factionsAmount.min < 1)
            {
                logger.LogError($"[{GetType().Name} - '{name}'] Minimum amount of factions must be at least 1.");
                return;
            }

            if (factionTypes.Length == 0)
            {
                logger.LogError($"[{GetType().Name} - '{name}'] At least one FactionTypeInfo asset must be assigned.");
                return;
            }

            foreach (FactionTypeLobbyData factionTypeData in factionTypes)
            {
                if (!factionTypeData.factionType.IsValid())
                {
                    logger.LogError($"[{GetType().Name} - '{name}'] Make sure all FactionTypeInfo assets are not null.");
                    return;
                }

                if (factionTypeData.npcTypes.Length == 0)
                {
                    logger.LogError($"[{GetType().Name} - '{name}'] At least one NPCTypeInfo asset for each faction type must be assigned.");
                    return;
                }
                if (!factionTypeData.npcTypes.IsValid())
                {
                    logger.LogError($"[{GetType().Name} - '{name}'] Make sure all NPCTypeInfo assets for each faction type are not null.");
                    return;
                }
            }

            if (!randomFactionTypeNPCTypes.IsValid() || randomFactionTypeNPCTypes.Length == 0 )
            {
                logger.LogError($"[{GetType().Name} - '{name}'] Make sure all NPCTypeInfo in the 'Random Faction Type NPC Types' field are not null and there is at least one valid element assigned.");
                return;
            }
        }
    }
}
