using RTSEngine.Game;
using RTSEngine.Utilities;
using UnityEngine;

namespace RTSEngine.UI
{
    public interface IRequirementDisplayUIHandler : IPreRunGameService
    {
        void Despawn(IRequirementDisplayUI instance);
        IRequirementDisplayUI Spawn(RequirementDisplayUISpawnInput input);
    }

    public class RequirementDisplayUIHandler : ObjectPool<IRequirementDisplayUI, RequirementDisplayUISpawnInput>, IRequirementDisplayUIHandler
    {
        [SerializeField, Tooltip("Prefab used to display the requirement in UI elements.")]
        private RequirementUIElement prefab = null;

        protected override void OnObjectPoolInit()
        {
            if (!prefab.IsValid())
            {
                logger.LogError($"[{GetType().Name}] 'Prefab' field must be assigned!", source: this);
                return;
            }
        }

        public IRequirementDisplayUI Spawn(RequirementDisplayUISpawnInput input)
        {
            IRequirementDisplayUI nextReqDisplayUI = base.Spawn(prefab);
            if (!nextReqDisplayUI.IsValid())
                return null;

            nextReqDisplayUI.OnSpawn(input);

            return nextReqDisplayUI;
        }
        public void Despawn(IRequirementDisplayUI instance) => Despawn(instance, destroyed: false);
    }
}
