using UnityEngine;
using TMPro;
using RTSEngine.Faction;

namespace RTSEngine.UI
{
    public class FactionSlotStatsTaskUI : BaseBareTaskUI<FactionSlotStatsUIAttributes>
    {
        #region Attributes
        [SerializeField, Tooltip("UI Text to display the amount of the entities.")]
        private TextMeshProUGUI label = null;
        #endregion

        #region Initializing/Terminating
        protected override void OnBareInit()
        {
            if (!logger.RequireValid(label,
                $"[{GetType().Name}] The 'Label' field must be assigned!"))
                return;
        }
        #endregion

        #region Disabling Task UI
        protected override void OnBareDisabled()
        {
            label.enabled = false;
        }
        #endregion

        #region Handling Attributes Reload
        protected override void OnBareReload()
        {
            label.enabled = true;
            label.text = Attributes.text;
            label.color = Attributes.color;
            label.fontStyle = (Attributes.state == FactionSlotState.eliminated ? FontStyles.Strikethrough : FontStyles.Normal) | FontStyles.Bold;
        }
        #endregion
    }
}
