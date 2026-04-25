using RTSEngine.BuildingExtension;
using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.NPC;
using RTSEngine.UnitExtension;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RTSEngine.Faction
{
    [System.Serializable]
    public struct InitialFactionEntityPrefabOption
    {
        [Tooltip("Pick the prefab to spawn for the faction slot."), EnforceType(typeof(IFactionEntity), prefabOnly: true)]
        public GameObject prefab;
        [Tooltip("Assign the transform whose position will be used to spawn the entity prefab."), EnforceType(sameScene: true)]
        public Transform spawnTransform;
    }

    [System.Serializable]
    public struct InitialFactionEntitiesOption
    {
        [Tooltip("What faction types must the slot faction have in order to consider using the initial faction entities here?")]
        public FactionTypeTargetPicker factionTypes;
        [Tooltip("What NPC types must the slot faction have in order to consider using the initial faction entities here?")]
        public NPCTypeTargetPicker npcTypes;

        [Tooltip("Parent object of the initial faction entities to use. You can leave unassigned and fill in the next field!"), EnforceType(sameScene: true)]
        public GameObject factionEntitiesParent;
        [Tooltip("If the parent object is left unassigned then you can drag and drop initial faction entities individually in this list."), EnforceType(typeof(IFactionEntity), sameScene: true)]
        public GameObject[] factionEntities;
        [Tooltip("Additionally, you can assign prefabs instead of pre-placed faction entities to spawn for the faction slot using this field. This field is always relevant (no relation to the above two fields).")]
        public InitialFactionEntityPrefabOption[] factionEntityPrefabs;

        public IReadOnlyList<IFactionEntity> GetFactionEntities()
        {
            if (factionEntitiesParent.IsValid())
                return factionEntitiesParent.GetComponentsInChildren<IFactionEntity>();
            return factionEntities.FromGameObject<IFactionEntity>().ToList();
        }

        public void Destroy()
        {
            if(factionEntitiesParent.IsValid())
                UnityEngine.Object.DestroyImmediate(factionEntitiesParent);
            foreach (var entity in factionEntities)
                UnityEngine.Object.DestroyImmediate(entity);

            factionEntities = new GameObject[0];
        }
    }

    [System.Serializable]
    public struct FactionSlotInitialEntitiesElement
    {
        [Tooltip("Define different options for initial faction entities for the faction slot of ID that matches the index of this element. The initial faction entities depend on the NPC and faction types of the faction slot.")]
        public InitialFactionEntitiesOption[] options;
    }

    public class InitialFactionEntitiesSpawner : MonoBehaviour, IPostRunGameService
    {
        protected IGameManager gameMgr { private set; get; }

        [SerializeField, Tooltip("For each faction slot you define in the Game Manager, you can add an element to this list and define the initial faction entities (meaning that the initial faction entities assigned in the Game Manager will not be considered). If no valid element is defined for a faction slot here then the faction slot will simply use the initial faction entities assigned in the Game Manager.")]
        private FactionSlotInitialEntitiesElement[] initialFactionEntities = new FactionSlotInitialEntitiesElement[0];

        protected IGameLoggingService logger { private set; get; }
        protected IUnitManager unitMgr { private set; get; }
        protected IBuildingManager buildingMgr { private set; get; } 

        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.unitMgr = gameMgr.GetService<IUnitManager>();
            this.buildingMgr = gameMgr.GetService<IBuildingManager>(); 

            for(int factionID = 0; factionID < gameMgr.FactionCount; factionID++)
            {
                if (!factionID.IsValidIndex(initialFactionEntities))
                {
                    logger.LogWarning($"[{GetType().Name}] No initial faction entities defined for faction ID: '{factionID}'!");
                    continue;
                }

                IFactionSlot factionSlot = gameMgr.GetFactionSlot(factionID);
                bool optionFound = false;

                foreach (InitialFactionEntitiesOption option in initialFactionEntities[factionID].options)
                {
                    if (optionFound
                        || (factionSlot.Data.role == FactionSlotRole.npc && !option.npcTypes.IsValidTarget(factionSlot.Data.npcType))
                        || (factionSlot.Data.type.IsValid() && !option.factionTypes.IsValidTarget(factionSlot.Data.type)))
                    {
                        option.Destroy();
                        continue;
                    }

                    optionFound = true;

                    if (option.factionEntitiesParent.IsValid())
                        RTSHelper.InitFactionEntities(option.GetFactionEntities(), factionID);

                    if (!RTSHelper.IsMasterInstance())
                        return;

                    foreach(var prefabOption in option.factionEntityPrefabs)
                    {
                        IFactionEntity prefab = prefabOption.prefab.GetComponent<IFactionEntity>();
                        if (prefab.IsUnit())
                            unitMgr.CreateUnit(prefab as IUnit, prefabOption.spawnTransform.position, prefab.transform.rotation,
                                new InitUnitParameters
                                {
                                    factionID = factionID,
                                    free = false,

                                    setInitialHealth = false,

                                    rallypoint = null,

                                    giveInitResources = true

                                });
                        else if (prefab.IsBuilding())
                            buildingMgr.CreatePlacedBuilding(prefab as IBuilding, prefabOption.spawnTransform.position, prefab.transform.rotation,
                                new InitBuildingParameters
                                {
                                    factionID = factionID,
                                    free = false,

                                    setInitialHealth = false,

                                    giveInitResources = true
                                });
                    }
                }
            }
        }
    }
}