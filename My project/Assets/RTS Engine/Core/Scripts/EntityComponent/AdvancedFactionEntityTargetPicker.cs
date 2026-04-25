using UnityEngine;

using RTSEngine.Entities;

namespace RTSEngine.EntityComponent
{
    [System.Serializable]
    public class AdvancedFactionEntityTargetPicker : FactionEntityTargetPicker
    {

        [SerializeField, Tooltip("Allow to target units that are in range of the faction entity but not stored in it?")]
        private bool targetExternal = true;
        [SerializeField, Tooltip("Allow to target units stored inside the same faction entity?")]
        private bool targetStored = true;

        public bool IsValidTarget(IEntityTargetComponent sourceComponent, IFactionEntity target)
        {
            if (target.IsUnit())
            {
                IUnit targetUnit = target as IUnit;
                IFactionEntity sourceEntity = sourceComponent.Entity as IFactionEntity;

                // Unit has an active carrier where it stored
                if (targetUnit.CarriableUnit.IsValid()
                    && targetUnit.CarriableUnit.CurrCarrier.IsValid())
                {
                    // If the carrier is different than the source or it is the source but we can not target stored units
                    if ((sourceEntity.UnitCarrier.IsValid() && !sourceEntity.UnitCarrier.IsUnitStored(targetUnit))
                        || !targetStored)
                        return false;
                }
                else if (!targetExternal) // Unit is not carried by a UnitCarrier but we can not target non-stored units
                    return false;
            }

            return base.IsValidTarget(target);
        }
    }
}
