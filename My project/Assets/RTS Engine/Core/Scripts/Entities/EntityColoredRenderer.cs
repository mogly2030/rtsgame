
using UnityEngine;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Utilities;

namespace RTSEngine.Entities
{
    public class EntityColoredRenderer : MonoBehaviour, IEntityPostInitializable
    {
        private IEntity entity;

        [Space(), SerializeField, Tooltip("What parts of the model will be colored with the faction colors?")]
        private ColoredRenderer[] coloredRenderers = new ColoredRenderer[0];

        public void OnEntityPostInit(IGameManager gameMgr, IEntity entity)
        {
            this.entity = entity;

            this.entity.FactionUpdateComplete += HandleFactionUpdateComplete;

            SetColors();
        }

        public void Disable()
        {
        }

        private void HandleFactionUpdateComplete(IEntity sender, FactionUpdateArgs args)
        {
            SetColors();
        }

        private void SetColors()
        {
            foreach (ColoredRenderer cr in coloredRenderers)
                cr.UpdateColor(entity.IsFree ? Color.white : entity.Slot.Data.color, entity);
        }
    }
}
