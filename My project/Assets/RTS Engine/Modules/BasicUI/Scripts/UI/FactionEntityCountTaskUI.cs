using UnityEngine;
using TMPro;
using RTSEngine.Cameras;

namespace RTSEngine.UI
{
    public class FactionEntityCountTaskUI : BaseTaskUI<FactionEntityCountUIAttributes>
    {
        #region Attributes
        protected override Sprite Icon => Attributes.icon;

        protected override Color IconColor => Color.white;

        protected override bool IsTooltipEnabled => false;

        protected override string TooltipDescription => "";

        protected override bool hideButtonOnDisable => true;

        [SerializeField, Tooltip("UI Text to display the amount of the entities.")]
        private TextMeshProUGUI label = null;

        protected IMainCameraController mainCamCtrl { private set; get; } 
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (!logger.RequireValid(label,
                $"[{GetType().Name}] The 'Label' field must be assigned!"))
                return;

            this.mainCamCtrl = gameMgr.GetService<IMainCameraController>(); 
        }
        #endregion

        #region Disabling Task UI
        protected override void OnDisabled()
        {
            label.enabled = false;
        }
        #endregion

        #region Handling Attributes Reload
        protected override void OnReload()
        {
            label.enabled = true;
            label.text = $"{Attributes.amount}";
        }
        #endregion

        #region Handling Task UI Interaction
        protected override void OnClick()
        {
            selectionMgr.RemoveAll();
            var nextEntities = gameMgr.LocalFactionSlot.FactionMgr.GetFactionEntitiesListByCode(Attributes.code);
            selectionMgr.Add(nextEntities);
        }
        #endregion
    }
}
