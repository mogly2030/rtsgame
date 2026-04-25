using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.UI;
using RTSEngine.UnitExtension;
using RTSEngine.Utilities;
using RTSEngine.Logging;

namespace RTSEngine.EntityComponent
{
    public class CarriableUnit : FactionEntityTargetComponent<IFactionEntity>, ICarriableUnit
    {
        #region Attributes
        /*
         * Action types and their parameters:
         * eject: Remove the unit instance from its current carrier 
         * */
        public enum ActionType : byte { eject }

        protected IUnit unit { private set; get; }

        public override bool IsIdle => true;

        public IUnitCarrier CurrCarrier { private set; get; }
        public Transform CurrSlot { private set; get; }
        public int CurrSlotID { private set; get; }
        private FollowTransform slotFollowHandler = null;

        [SerializeField, Tooltip("Defines information used to display a task in the task panel when the faction entity is selected, to allow the unit to be ejected from its carrier, if it has one.")]
        private EntityComponentTaskUIAsset ejectionTaskUI = null;

        [SerializeField, Tooltip("Allow the unit to enter carriers from other factions or from no faction?")]
        private CarriableUnitOtherFactionBehaviour otherFactionBehaviour = CarriableUnitOtherFactionBehaviour.allowEnterFreeCarrier;

        //[SerializeField, Tooltip("Allow the unit to enter carriers from other factions?")]
        //private bool allowMovementToExitCarrier = true;
        //public bool AllowMovementToExitCarrier => allowMovementToExitCarrier;
        #endregion

        #region Raising Events
        public event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitAdded;
        public event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitRemoved;

        private void RaiseUnitAdded(UnitCarrierEventArgs args)
        {
            var handler = UnitAdded;
            handler?.Invoke(CurrCarrier, args);
        }
        private void RaiseUnitRemoved(UnitCarrierEventArgs args)
        {
            var handler = UnitRemoved;
            handler?.Invoke(CurrCarrier, args);
        }
        #endregion


        #region Initializing/Terminating
        protected override void OnTargetInit()
        {
            this.unit = factionEntity as IUnit;

            CurrCarrier = null;
            CurrSlot = null;
            slotFollowHandler = new FollowTransform(unit.transform, null);
        }
        #endregion

        #region Handling Upgrades
        protected override void OnComponentUpgraded(FactionEntityTargetComponent<IFactionEntity> sourceFactionEntityTargetComponent)
        {
            ICarriableUnit sourceCarriableUnit = sourceFactionEntityTargetComponent as ICarriableUnit;
            if (sourceCarriableUnit.CurrCarrier.IsValid())
            {
                IUnitCarrier targetCarrier = sourceCarriableUnit.CurrCarrier;

                targetCarrier.EjectAction(unit, destroyed: false, playerCommand: false);

                SetTarget(targetCarrier.Entity.ToTargetData(), playerCommand: false);
            }
        }
        #endregion

        #region Handling AddableUnitData
        public AddableUnitData GetAddableData(bool playerCommand)
        {
            return new AddableUnitData(this, playerCommand, otherFactionBeahviour: otherFactionBehaviour);
        }

        public AddableUnitData GetAddableData(SetTargetInputData input)
        {
            return new AddableUnitData(this, input, otherFactionBeahviour: otherFactionBehaviour);
        }
        #endregion

        #region Searching/Updating Target
        public override ErrorMessage IsTargetValid(SetTargetInputData data)
        {
            TargetData<IFactionEntity> potentialTarget = data.target;

            if (!potentialTarget.instance.IsValid())
                return ErrorMessage.invalid;
            else if (!potentialTarget.instance.IsInteractable)
                return ErrorMessage.uninteractable;
            else if (!potentialTarget.instance.UnitCarrier.IsValid())
                return ErrorMessage.carrierMissing;

            return potentialTarget.instance.UnitCarrier.CanMove(
                unit,
                GetAddableData(data.playerCommand));
        }

        public override bool IsTargetInRange(Vector3 sourcePosition, TargetData<IEntity> target) => true;
        protected override void OnTargetPostLocked(SetTargetInputData input, bool sameTarget)
        {
            if (sameTarget)
                return;

            //Target.instance.UnitCarrier.UnitAdded += HandleTargetCarrierUnitAdded;

            Target.instance.UnitCarrier.Move(
                unit,
                GetAddableData(input));
        }
        #endregion

        #region Stopping
        protected override void OnStop()
        {
            if (CurrCarrier.IsValid())
            {
                //CurrCarrier.UnitAdded -= HandleTargetCarrierUnitAdded;
                CurrCarrier.UnitRemoved -= HandleTargetCarrierUnitRemoved;
            }
        }
        #endregion

        #region Handling Actions
        public override ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input)
        {
            switch ((ActionType)actionID)
            {
                case ActionType.eject:

                    return EjectActionLocal(input.playerCommand);

                default:
                    return ErrorMessage.undefined;
            }
        }

        public ErrorMessage EjectAction(bool playerCommand)
        {
            return LaunchAction(
                (byte)ActionType.eject,
                new SetTargetInputData { playerCommand = playerCommand });
        }

        public ErrorMessage EjectActionLocal(bool playerCommand)
        {
            if (!CurrCarrier.IsValid())
                return ErrorMessage.invalid;

            ErrorMessage ejectionErrorMsg = CurrCarrier.LaunchActionLocal(
                (byte)UnitCarrier.ActionType.eject,
                new SetTargetInputData
                {
                    target = new TargetData<IEntity>
                    {
                        instance = unit,
                        position = Vector3.zero // This refers to the fact that the unit is not being ejected due to it being destroyed
                    },
                    playerCommand = playerCommand
                });

            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);

            return ejectionErrorMsg;
        }
        #endregion

        #region Handling Events: Target Carrier Unit Added/Removed
        public void OnCarrierUnitAdded(IUnitCarrier carrier, UnitCarrierEventArgs args)
        {
            if (args.Unit != unit)
                return;

            CurrCarrier = carrier;
            CurrSlot = args.Slot;
            CurrSlotID = args.SlotID;

            if (CurrSlot.IsValid())
            {
                unit.MovementComponent.SetPosition(CurrSlot.position);
                slotFollowHandler.SetTarget(CurrSlot, enableCallback: false);
            }

            RaiseUnitAdded(args);

            //CurrCarrier.UnitAdded -= HandleTargetCarrierUnitAdded;
            CurrCarrier.UnitRemoved += HandleTargetCarrierUnitRemoved;
        }

        private void HandleTargetCarrierUnitRemoved(IUnitCarrier carrier, UnitCarrierEventArgs args)
        {
            if (carrier != CurrCarrier)
                return;

            // If this unit is confirmed to be added to its target carrier
            if (args.Unit == unit)
            {
                Stop();

                CurrCarrier.UnitRemoved -= HandleTargetCarrierUnitRemoved;

                RaiseUnitAdded(args);

                CurrCarrier = null;
                CurrSlot = null;
                CurrSlotID = -1;
                slotFollowHandler.ResetTarget();
            }
        }
        #endregion

        #region Task UI
        protected override bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
        {
            if (!base.OnTaskUICacheUpdate(taskUIAttributesCache, disabledTaskCodesCache))
                return false;

            if (ejectionTaskUI.IsValid())
            {
                if (CurrCarrier.IsValid())
                    taskUIAttributesCache.Add(
                        new EntityComponentTaskUIAttributes
                        {
                            data = ejectionTaskUI.Data,

                            locked = false
                        });
                else
                    disabledTaskCodesCache.Add(ejectionTaskUI.Key);
            }

            return true;
        }

        public override bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes)
        {
            if (!base.OnTaskUIClick(taskAttributes) // Check if this is not the set target task.
                && ejectionTaskUI.IsValid() && taskAttributes.data.code == ejectionTaskUI.Key)
            {
                EjectAction(playerCommand: true);

                return true;
            }

            return false;
        }
        #endregion

        #region Setting Target (IUnitCarrier)
        public ErrorMessage IsTargetValid(IUnitCarrier carrier, AddableUnitData addableData)
        {
            if (!carrier.IsValid() || !carrier.Entity.IsValid())
                return ErrorMessage.invalid;
            else if (!carrier.Entity.IsInteractable)
                return ErrorMessage.uninteractable;

            return carrier.CanMove(
                unit,
                addableData);
        }

        public ErrorMessage SetTarget(IUnitCarrier carrier, AddableUnitData addableData)
        {
            if (!factionEntity.CanLaunchTask)
                return ErrorMessage.taskSourceCanNotLaunch;

            // Update addable data with data that can only be set from this component
            addableData.sourceTargetComponent = this;
            addableData.otherFactionBehaviour = otherFactionBehaviour;

            ErrorMessage errorMsg;
            if ((errorMsg = IsTargetValid(carrier, addableData)) != ErrorMessage.none)
            {
                if (addableData.playerCommand && RTSHelper.IsLocalPlayerFaction(factionEntity))
                    playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                    {
                        message = errorMsg,

                        source = Entity,
                        target = carrier?.Entity
                    });

                return errorMsg;
            }

            if (HasTarget)
                Stop();

            bool sameTarget = carrier.Entity == Target.instance as IEntity;

            // If this component requires the entity to be idle to run then set the entity to idle before assigning the new target
            if (RequireIdleEntity)
                factionEntity.SetIdle(sameTarget ? this : null);

            Target = carrier.Entity.ToTargetData();

            if (addableData.playerCommand && Target.instance.IsValid() && factionEntity.IsLocalPlayerFaction())
                selector.FlashSelection(Target.instance, true);

            //Target.instance.UnitCarrier.UnitAdded += HandleTargetCarrierUnitAdded;

            Target.instance.UnitCarrier.Move(
                unit,
                addableData);

            RaiseTargetUpdated();

            return ErrorMessage.none;
        }
        #endregion

        #region Handling Carrier Slot
        private void Update()
        {
            if (!CurrCarrier.IsValid() || !CurrSlot.IsValid())
                return;

            slotFollowHandler.Update();
        }
        #endregion

    }
}
