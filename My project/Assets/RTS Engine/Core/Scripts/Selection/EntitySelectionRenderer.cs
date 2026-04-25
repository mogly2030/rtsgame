using System.Collections;
using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Utilities;
using UnityEngine;

namespace RTSEngine.Selection
{
    public class EntitySelectionRenderer
        : MonoBehaviour,
            IEntitySelectionMarker,
            IEntityPreInitializable
    {
        #region Class Attributes
        private IEntity entity;

        public bool IsLocked { set; get; }

        [SerializeField, Tooltip("Renderer used to display the selection texture of an entity.")]
        private ColoredRenderer targetRenderer = new ColoredRenderer
        {
            colorPropertyName = "_Color",
            materialID = 0,
        };

        // REMOVE ME IN V > 2023.0.3
        [SerializeField]
        public Renderer selectionRenderer;

        [SerializeField]
        public int materialID;

        private Coroutine flashCoroutine;

        private IEntitySelection entitySelection;

        protected IGameLoggingService logger { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void OnEntityPreInit(IGameManager gameMgr, IEntity entity)
        {
            this.entity = entity;

            this.logger = gameMgr.GetService<IGameLoggingService>();

            this.entitySelection = entity.Selection;

            if (
                !logger.RequireValid(
                    targetRenderer.renderer,
                    $"[{GetType().Name} - {entity.Code}] The 'Selection Renderer' field must be assigned!"
                )
            )
                return;

            Disable();
            StopFlash();
        }
        #endregion

        #region Enabling/Disabling Selection Renderer
        public void Enable(Color color)
        {
            if (IsLocked)
                return;

            targetRenderer.UpdateColor(color, entity);
            targetRenderer.renderer.enabled = true;
        }

        public void Enable()
        {
            if (IsLocked)
                return;

            targetRenderer.UpdateColor(entitySelection.Entity.SelectionColor, entity);
            targetRenderer.renderer.enabled = true;
        }

        public void Disable()
        {
            if (IsLocked)
                return;

            targetRenderer.renderer.enabled = false;
        }
        #endregion

        #region Flashing Selection Renderer
        public void StartFlash(float totalDuration, float cycleDuration, Color flashColor)
        {
            if (IsLocked)
                return;

            StopFlash();

            targetRenderer.UpdateColor(flashColor, entity);
            flashCoroutine = StartCoroutine(Flash(totalDuration, cycleDuration));
        }

        public void StopFlash()
        {
            if (flashCoroutine.IsValid())
                StopCoroutine(flashCoroutine);

            // if the entity was selected before the selection flash.
            // Enable the selection plane with the actual entity's colors again
            if (entitySelection.IsSelected)
                Enable();
            else
                Disable();
        }

        private IEnumerator Flash(float totalDuration, float cycleDuration)
        {
            while (true)
            {
                yield return new WaitForSeconds(cycleDuration);

                targetRenderer.renderer.enabled = !targetRenderer.renderer.enabled;

                totalDuration -= cycleDuration;
                if (totalDuration <= 0.0f)
                    yield break;
            }
        }
        #endregion
    }
}
