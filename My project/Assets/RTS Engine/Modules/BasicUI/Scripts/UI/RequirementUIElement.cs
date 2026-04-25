using UnityEngine;
using TMPro;
using RTSEngine.Effect;
using RTSEngine.Utilities;
using RTSEngine.Game;
using UnityEngine.UI;
using RTSEngine.Logging;

namespace RTSEngine.UI
{
    public interface IRequirementDisplayUI : IPoolableObject
    {
        void OnSpawn(RequirementDisplayUISpawnInput input);
        void Despawn();
    }

    public class RequirementDisplayUISpawnInput : PoolableObjectSpawnInput
    {
        public Sprite Icon { get; }
        public bool HasRequirement { get; }
        public int Amount { get; }

        public RequirementDisplayUISpawnInput(Transform parent, Sprite icon, bool hasRequirement, int amount)
            : base(parent, false, Vector3.zero, Quaternion.identity)
        {
            Icon = icon;
            HasRequirement = hasRequirement;
            Amount = amount;
        }
    }

    public class RequirementUIElement : MonoBehaviour, IRequirementDisplayUI
    {
        public string Code => "rtse_requirement_task_ui";

        [SerializeField, Tooltip("UI Image component to display the task's icon.")]
        protected Image image = null;
        [SerializeField, Tooltip("Color used on the requirement icon when the requirement is not met.")]
        private Color failColor = Color.red;

        [SerializeField, Tooltip("Child UI Text object used to display the requirement's amount.")]
        private TextMeshProUGUI amountText = null;

        protected IGameLoggingService logger { private set; get; }

        public void Init(IGameManager gameMgr)
        {
            this.logger = gameMgr.GetService<IGameLoggingService>(); 

            if (!image.IsValid())
            {
                logger.LogError($"[{GetType().Name}] 'Image' field must be assigned!", source: this);
                return;
            }

            if (!amountText.IsValid())
            {
                logger.LogError($"[{GetType().Name}] 'Amount Text' field must be assigned!", source: this);
                return;
            }
        }

        public void OnSpawn(RequirementDisplayUISpawnInput input)
        {
            if (input.parent != transform.parent)
            {
                transform.SetParent(input.parent);
                transform.localScale = Vector3.one;
            }

            image.enabled = true;
            image.sprite = input.Icon;
            image.color = input.HasRequirement ? Color.white : failColor;

            amountText.enabled = true;
            amountText.text = $"{input.Amount}";
        }

        public void Despawn()
        {
            image.enabled = false;
            amountText.enabled = false;
        }
    }
}
