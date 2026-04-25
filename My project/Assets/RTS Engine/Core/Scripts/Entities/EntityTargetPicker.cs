using UnityEngine;

namespace RTSEngine.Entities
{
    public abstract class EntityTargetPickerBase<T> : TargetPicker<T, CodeCategoryField> where T : IEntity
    {
        protected override bool IsInList(T entity)
            => entity.IsValid() && options.Contains(entity.Code, entity.Category);
    }

    [System.Serializable]
    public class EntityTargetPicker : EntityTargetPickerBase<IEntity>
    {
        [SerializeField, Tooltip("Allow to target units?")]
        private bool targetUnits = true;
        [SerializeField, Tooltip("Allow to target buildings?")]
        private bool targetBuildings = true;
        [SerializeField, Tooltip("Allow to target resources?")]
        private bool targetResources = true;

        public override bool IsValidTarget(IEntity target)
        {
            return (target.IsUnit() && !targetUnits) || (target.IsBuilding() && !targetBuildings) || (target.IsResource() && !targetResources)
                ? false
                : base.IsValidTarget(target);
        }
    }

    [System.Serializable]
    public class FactionEntityTargetPicker : EntityTargetPickerBase<IFactionEntity>
    {
        [SerializeField, Tooltip("Allow to target units?")]
        private bool targetUnits = true;
        [SerializeField, Tooltip("Allow to target buildings?")]
        private bool targetBuildings = true;

        public override bool IsValidTarget(IFactionEntity target)
        {
            return (target.IsUnit() && !targetUnits) || (target.IsBuilding() && !targetBuildings)
                ? false
                : base.IsValidTarget(target);
        }
    }
}
