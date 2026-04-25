using RTSEngine.EntityComponent;
using RTSEngine.Event;
using RTSEngine.Model;
using System;
using UnityEngine;

namespace RTSEngine.Attack
{
    public enum AttackWeaponToggableType { componentActive, hasTarget }
    [System.Serializable]
    public class AttackWeapon : AttackSubComponent
    {
        #region Attributes
        [SerializeField, Tooltip("Objects, other than the main weapon object, enabled when the attack type is activated and hidden otherwise.")]
        private GameObject[] toggableObjects = new GameObject[0];
        [SerializeField, Tooltip("Choose how the toggable objects and the attack weapon will be enabled or disabled. Based on the attack component active status or based on whether there is an active target or not.")]
        private AttackWeaponToggableType toggleType = AttackWeaponToggableType.componentActive;
        private bool currActiveStatus;

        [SerializeField, Tooltip("Allow to update the weapon rotation?")]
        private bool updateRotation = true;

        [SerializeField, Tooltip("Only rotate the weapon when the target is inside the attacking range?")]
        private bool rotateInRangeOnly = false;

        //to the freeze the weapon's rotation on the Y axis then you should enable freezeRotationX and freezeRotationZ
        [SerializeField, Tooltip("Freeze calculating rotation in the look at vector on the X axis.")]
        private bool freezeRotationX = false;
        [SerializeField, Tooltip("Freeze calculating rotation in the look at vector on the Y axis.")]
        private bool freezeRotationY = false;
        [SerializeField, Tooltip("Freeze calculating rotation in the look at vector on the Z axis.")]
        private bool freezeRotationZ = false;

        [SerializeField, Tooltip("Is the weapon's object rotation smooth?")]
        private bool smoothRotation = true;
        [SerializeField, Tooltip("How smooth is the weapon's rotation? Only if smooth rotation is enabled!")]
        private float rotationDamping = 2.0f;

        [SerializeField, Tooltip("Force the weapon object to get back to an idle rotation when the attacker does not have an active target?")]
        private bool forceIdleRotation = true;
        [SerializeField, Tooltip("In case idle rotation is enabled, this represents the idle rotation euler angles.")]
        private Vector3 idleAngles = Vector3.zero;
        // Used to store the weapon's idle rotation so it is not calculated everytime through its euler angles
        private Quaternion idleRotation = Quaternion.identity;
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (SourceAttackComp.WeaponTransform.IsValid())
                idleRotation = Quaternion.Euler(idleAngles);

            for (int i = 0; i < toggableObjects.Length; i++)
                if (!toggableObjects[i].IsValid())
                    logger.LogError($"[{GetType().Name} - {SourceAttackComp.Code} - {SourceAttackComp.Entity.Code}] The Toggable Objects array field in the Attack Weapon tab includes one or more unassigned or incorrectly assigned elements!", source: SourceAttackComp);

            SourceAttackComp.TargetUpdated += HandleAttackTargetUpdateOrStop;
            SourceAttackComp.TargetStop += HandleAttackTargetUpdateOrStop;
            SourceAttackComp.ActiveStatusUpdate += HandleAttackActiveStatusUpdate;

            switch(toggleType)
            {
                case AttackWeaponToggableType.componentActive:
                    Toggle(SourceAttackComp.IsActive, force: true);
                    break;
                case AttackWeaponToggableType.hasTarget:
                    Toggle(SourceAttackComp.HasTarget, force: true);
                    break;
            }
        }

        protected override void OnDisabled()
        {
            SourceAttackComp.TargetUpdated -= HandleAttackTargetUpdateOrStop;
            SourceAttackComp.TargetStop -= HandleAttackTargetUpdateOrStop;
            SourceAttackComp.ActiveStatusUpdate -= HandleAttackActiveStatusUpdate;
        }
        #endregion

        #region Toggling Weapon
        private void HandleAttackActiveStatusUpdate(IEntityComponent sender, EventArgs args)
        {
            if (toggleType != AttackWeaponToggableType.componentActive)
                return;

            Toggle(SourceAttackComp.IsActive);
        }

        private void HandleAttackTargetUpdateOrStop(IEntityTargetComponent sender, TargetDataEventArgs args)
        {
            if (toggleType != AttackWeaponToggableType.hasTarget)
                return;

            Toggle(SourceAttackComp.HasTarget);
        }

        private void Toggle(bool enable, bool force = false)
        {
            if (!force && currActiveStatus == enable)
                return;

            for (int i = 0; i < toggableObjects.Length; i++)
                toggableObjects[i].SetActive(enable);

            if(SourceAttackComp.WeaponTransform.IsValid())
                SourceAttackComp.WeaponTransform.gameObject.SetActive(enable);

            currActiveStatus = enable;
        }
        #endregion

        #region Handling Active/Idle Rotation
        public void Update()
        {
            if (!updateRotation
                || !SourceAttackComp.WeaponTransform.IsValid())
                return;

            //if the attacker does not have an active target
            //or it does but we are not allowed to start weapon rotation until the target is in range
            if (!SourceAttackComp.HasTarget
                || (!SourceAttackComp.IsInTargetRange && rotateInRangeOnly))
                UpdateIdleRotation();
            else
                UpdateActiveRotation();
        }

        public void UpdateIdleRotation ()
        {
            //can not force idle rotation, stop here
            if (!forceIdleRotation)
                return;

            SourceAttackComp.WeaponTransform.localRotation = smoothRotation
                ? Quaternion.Slerp(SourceAttackComp.WeaponTransform.localRotation, idleRotation, Time.deltaTime * rotationDamping)
                : idleRotation;
        }

        public void UpdateActiveRotation ()
        {
            Vector3 lookAt = RTSHelper.GetAttackTargetPosition(SourceAttackComp, SourceAttackComp.Target) - SourceAttackComp.WeaponTransform.position;

            //which axis should not be rotated? 
            if (freezeRotationX == true)
                lookAt.x = 0.0f;
            if (freezeRotationY == true)
                lookAt.y = 0.0f;
            if (freezeRotationZ == true)
                lookAt.z = 0.0f;

            Quaternion targetRotation = Quaternion.LookRotation(lookAt);
            if (smoothRotation == false) //make the weapon instantly look at target
                SourceAttackComp.WeaponTransform.rotation = targetRotation;
            else //smooth rotation
                SourceAttackComp.WeaponTransform.rotation = Quaternion.Slerp(SourceAttackComp.WeaponTransform.rotation, targetRotation, Time.deltaTime * rotationDamping);
        }
        #endregion
    }
}
