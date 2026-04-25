using UnityEngine;

using RTSEngine.Attack;
using RTSEngine.Entities;

namespace RTSEngine.EntityComponent
{
    public class BuildingAttack : FactionEntityAttack
    {
        #region Attributes
        //'maxDistance' represents the attacking range for a building.
        [SerializeField, Tooltip("Define the minimum and maximum distances from the building to its target that allow it to engage the target.")]
        private BuildingAttackDistanceHandler attackDistance;
        public override IAttackDistanceHandler AttackDistanceHandler => attackDistance;
        #endregion

        #region Initializing/Terminating
        protected override void OnAttackInit()
        {
            attackDistance.Init(gameMgr, this);
        }
        #endregion

        #region Engaging Target
        protected override bool MustStopProgress()
        {
            return base.MustStopProgress()
                || !IsTargetInRange(Entity.transform.position, Target)
                || LineOfSight.IsObstacleBlocked(Entity.transform.position, RTSHelper.GetAttackTargetPosition(this, Target));
        }

        public override float GetProgressRange()
            => attackDistance.GetStoppingDistance(Target.instance, min: false);

        public override bool IsTargetInRange(Vector3 attackPosition, TargetData<IEntity> target)
        {
            return attackDistance.IsTargetInRange(attackPosition, target);
        }
        #endregion
    }
}
