using System;

using UnityEngine;

using RTSEngine.Determinism;
using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Logging;

namespace RTSEngine.Animation
{
    public class TimeModifiedAnimator : MonoBehaviour, IEntityPostInitializable, IMonoBehaviour
    {
        #region Attributes
        private Animator animator = null;
        private TimeModifiedFloat animatorSpeed;

        protected ITimeModifier timeModifier { private set; get; } 
        protected IGameLoggingService logger { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void OnEntityPostInit(IGameManager gameMgr, IEntity entity)
        {
            this.timeModifier = gameMgr.GetService<ITimeModifier>();
            this.logger = gameMgr.GetService<IGameLoggingService>();

            animator = gameObject.GetComponent<Animator>();

            if (!animator.IsValid())
            {
                logger.LogError($"[{GetType().Name}] The gameobject where this component is attached must have an '{typeof(Animator).Name}' component attached to it!", source: this);
                return;
            }

            animatorSpeed = new TimeModifiedFloat(animator.speed);
            animator.speed = animatorSpeed.Value;

            timeModifier.ModifierUpdated += HandleModifierUpdated;
        }

        public void Disable()
        {
            timeModifier.ModifierUpdated -= HandleModifierUpdated;
        }
        #endregion

        #region Handling Event: Time Modifier Update
        private void HandleModifierUpdated(ITimeModifier sender, EventArgs args)
        {
            animator.speed = animatorSpeed.Value;
        }
        #endregion
    }
}
