using System.Collections.Generic;

using UnityEngine;
using RTSEngine.Entities;
using RTSEngine.UI;
using RTSEngine.Utilities;

namespace RTSEngine.EntityComponent
{
    public class BuildingCreator : EntityComponentBase, IBuildingCreator
    {
        #region Attributes
        // EDITOR ONLY
        [HideInInspector]
        public Int2D tabID = new Int2D {x = 0, y = 0};

        private IFactionEntity factionEntity;

        [SerializeField, Tooltip("List of building creation tasks that can be launched through this component.")]
        private List<BuildingCreationTask> creationTasks = new List<BuildingCreationTask>();
        public IReadOnlyList<BuildingCreationTask> CreationTasks => creationTasks;

        [SerializeField, Tooltip("List of building creation tasks that can be launched through this component after the building upgrades are unlocked.")]
        private BuildingCreationTask[] upgradeTargetCreationTasks = new BuildingCreationTask[0];
        public IReadOnlyList<BuildingCreationTask> UpgradeTargetCreationTasks => upgradeTargetCreationTasks;

        private BuildingCreationTaskHandler buildingCreationTaskHandler = new BuildingCreationTaskHandler();
        #endregion

        #region Initialization/Terminating
        protected override void OnInit()
        {
            if(!Entity.IsFactionEntity())
            {
                logger.LogError($"[{GetType().Name} - {factionEntity.Code}] This component can only be attached to faction entities (units or buildings)!");
                return;
            }

            this.factionEntity = Entity as IFactionEntity;

            buildingCreationTaskHandler.Init(gameMgr, this, creationTasks, upgradeTargetCreationTasks);
        }

        protected override void OnDisabled()
        {
            buildingCreationTaskHandler.Disable();
        }
        #endregion

        #region Task UI
        protected override bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
            => buildingCreationTaskHandler.OnTaskUICacheUpdate(taskUIAttributesCache, disabledTaskCodesCache);

        public override bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes)
            => buildingCreationTaskHandler.OnTaskUIClick(taskAttributes);
        #endregion
    }
}