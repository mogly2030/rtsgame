using System.Collections.Generic;
using System.Linq;
using System;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Movement;
using RTSEngine.UI;
using RTSEngine.Faction;
using RTSEngine.Logging;
using RTSEngine.Terrain;
using RTSEngine.Audio;
using RTSEngine.Search;
using RTSEngine.Selection;
using RTSEngine.UnitExtension;
using RTSEngine.Utilities;
using RTSEngine.Model;

namespace RTSEngine.EntityComponent
{
    public partial class UnitCarrier : EntityComponentBase, IUnitCarrier
    {
        #region Attributes
        [HideInInspector]
        public Int2D tabID = new Int2D {x = 0, y = 0};

        /*
         * Action types and their parameters:
         * eject: target.position.x => 1 for call being from a destroyed carrier, else carrier is not destroyed, target.instance -> unit instance to remove
         * ejectAll: target.position.x => 1 for call being from a destroyed carrier, else carrier is not destroyed
         * callUnits: no parameters.
         * */
        public enum ActionType : byte { eject, ejectAll, callUnits }

        protected IFactionEntity factionEntity { private set; get; }

        [SerializeField, Tooltip("Define the units that can be carried.")]
        private FactionEntityTargetPicker targetPicker = new FactionEntityTargetPicker();

        [SerializeField, Tooltip("The maximum amount of units that can be carried at the same time."), Min(0)]
        private int capacity = 5;
        public int MaxAmount => capacity;

        [SerializeField, Tooltip("Define how custom slots amount for units that get added to the carrier. Default slot value is 1 for undefined units.")]
        private EntityAmountHandler customUnitSlots = new EntityAmountHandler();

        public int CurrAmount { private set; get; }
        public bool HasMaxAmount => CurrAmount >= MaxAmount;

        private IUnit[] carrierSlots = new IUnit[0];
        public IReadOnlyList<IUnit> CarrierSlots => carrierSlots;

        [SerializeField, Tooltip("The possible positions that units can use to enter the carrier. Each unit added unit will seek the closest position to enter the carrier.")]
        private Transform[] addablePositions = new Transform[0];

        [SerializeField, Tooltip("If populated, then this defines the types of terrain areas where units can interact with this carrier.")]
        private TerrainAreaType[] forcedTerrainAreas = new TerrainAreaType[0];

        [SerializeField, Tooltip("What audio clip to play when a unit goes into the carrier?")]
        private AudioClipFetcher addUnitAudio = new AudioClipFetcher();

        [SerializeField, Tooltip("The possible positions that units can occupy when inside the carrier. When there is no carrier position available for a unit, it will be deactivated.")]
        private Transform[] carrierPositions = new Transform[0];
        private Dictionary<IUnit, int> unitToCarrierPositionIndex;
        [SerializeField, HideInInspector]
        private List<int> freeCarrierPositionIndexes;

        [SerializeField, Tooltip("The possible positions that a unit inside the carrier transports to when ejected from the carrier. Leave empty to use the same addable positions for ejectable positions.")]
        private Transform[] ejectablePositions = new Transform[0];

        [SerializeField, Tooltip("Can stored units be ejected individually through a task?")]
        private bool canEjectSingleUnit = true;
        [SerializeField, Tooltip("Defines information used to display a single unit ejection task in the task panel.")]
        private EntityComponentTaskUIAsset ejectSingleUnitTaskUI = null;

        [SerializeField, Tooltip("Can stored units be ejected all together through a task?")]
        private bool canEjectAllUnits = true;
        [SerializeField, Tooltip("Defines information used to display all units ejection task in the task panel.")]
        private EntityComponentTaskUIAsset ejectAllUnitsTaskUI = null;

        [SerializeField, Tooltip("What audio clip to play when a unit is ejected from the APC?")]
        private AudioClipFetcher ejectUnitAudio = new AudioClipFetcher();

        [SerializeField, Tooltip("Send to rallypoint when a unit is ejected, if the carrier has a rallypoint component.")]
        private bool ejectToRallypoint = false;

        [SerializeField, Tooltip("Enable to allow stored units to be ejected when the carrier is destroyed.")]
        private bool ejectOnDestroy = true;

        [SerializeField, Tooltip("Allow the carried unit to launch a player issued movement command when it is carried which will make it leave this carrier first then proceed with the movement.")]
        private bool allowMovementToExitCarrier = false;
        public bool AllowMovementToExitCarrier => allowMovementToExitCarrier;

        [SerializeField, Tooltip("Units that are within this distance from the carrier are called.")]
        private float callUnitsRange = 20.0f;
        [SerializeField, Tooltip("Only call units that are idle?")]
        private bool callIdleOnly = false;
        [SerializeField, Tooltip("Can call units that have an attack component?")]
        private bool callAttackUnits = true;

        [SerializeField, Tooltip("Defines information used to display the task that calls units to get into the carrier in the task panel.")]
        private EntityComponentTaskUIAsset callUnitsTaskUI = null;

        [SerializeField, Tooltip("What audio clip to play when the carrier calls units in range?")]
        private AudioClipFetcher callUnitsAudio = new AudioClipFetcher();

        private bool initAsFreeFaction = false;
        [SerializeField, Tooltip("If the unit carrier is marked as a free unit, how would it interact with other units?")]
        private FreeFactionBehaviour freeFactionBehaviour = new FreeFactionBehaviour {
            allowFreeFaction = false,
            allowLocalPlayer = true,

            updateFactionOnOccupy = true,
            freeOnEjection = true,

            canBeAttackedOnFreeFaction = false,
            canBeAttackedOnValidFaction = true
        };

        // Game services
        protected ITerrainManager terrainMgr { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IGameAudioManager audioMgr { private set; get; }
        protected ISelectionManager selectionMgr { private set; get; }
        protected IMovementManager mvtMgr { private set; get; }
        protected IGridSearchHandler gridSearch { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitAdded;
        public event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitRemoved;
        public event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitCalled;

        private void RaiseUnitAdded(UnitCarrierEventArgs args)
        {
            var handler = UnitAdded;
            handler?.Invoke(this, args);
        }
        private void RaiseUnitRemoved(UnitCarrierEventArgs args)
        {
            var handler = UnitRemoved;
            handler?.Invoke(this, args);
        }
        private void RaiseUnitCalled(UnitCarrierEventArgs args)
        {
            var handler = UnitCalled;
            handler?.Invoke(this, args);
        }
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            this.terrainMgr = gameMgr.GetService<ITerrainManager>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>();
            this.audioMgr = gameMgr.GetService<IGameAudioManager>();
            this.selectionMgr = gameMgr.GetService<ISelectionManager>();
            this.mvtMgr = gameMgr.GetService<IMovementManager>();
            this.gridSearch = gameMgr.GetService<IGridSearchHandler>();

            this.factionEntity = Entity as IFactionEntity;

            if (!logger.RequireTrue(addablePositions.Length > 0 && addablePositions.All(t => t.IsValid()),
                $"[{GetType().Name} - {Entity.Code}] The field 'Addable Positions' is either empty or has unassigned elements!")

                || !logger.RequireTrue(ejectablePositions.Length == 0 || ejectablePositions.All(t => t.IsValid()),
                $"[{GetType().Name} - {Entity.Code}] The field 'Ejectable Positions' must be either empty or populated with valid elements!")

                || !logger.RequireTrue(forcedTerrainAreas.Length == 0 || forcedTerrainAreas.All(terrainArea => terrainArea.IsValid()),
                  $"[{GetType().Name} - {Entity.Code}] The 'Forced Terrain Areas' field must be either empty or populated with valid elements!"))
                return;

            carrierSlots = new IUnit[capacity];

            unitToCarrierPositionIndex = new Dictionary<IUnit, int>();
            freeCarrierPositionIndexes = new List<int>(capacity);
            for (int i = 0; i < capacity; i++)
                freeCarrierPositionIndexes.Add(i);

            CurrAmount = 0;

            // Free faction initial handling 
            initAsFreeFaction = factionEntity.IsFree;
            if (initAsFreeFaction)
                factionEntity.Health.CanBeAttacked = freeFactionBehaviour.canBeAttackedOnFreeFaction;

            factionEntity.Health.EntityDead += HandlEntityDead;
        }

        protected override void OnDisabled()
        {
            factionEntity.Health.EntityDead -= HandlEntityDead;
        }
        #endregion

        #region Handling Component Upgrade
        public override void HandleComponentUpgrade(IEntityComponent sourceEntityComponent)
        {
            UnitCarrier sourceUnitCarrier = sourceEntityComponent as UnitCarrier;
            if (!sourceUnitCarrier.IsValid())
                return;

            foreach (IUnit storedUnit in sourceUnitCarrier.CarrierSlots.ToArray())
            {
                if (!storedUnit.IsValid())
                    continue;

                sourceUnitCarrier.EjectActionLocal(storedUnit, destroyed: false, playerCommand: false);
                storedUnit.CarriableUnit.SetTargetLocal(Entity.ToTargetData(), playerCommand: false);
            }
        }
        #endregion

        #region Handling Events: IEntity (source)
        private void HandlEntityDead(IEntity sender, DeadEventArgs e)
        {
            if(!e.IsUpgrade)
                EjectAllAction(destroyed: true, playerCommand: false);
        }
        #endregion

        #region IAddableUnit/Adding Units
        public Vector3 GetAddablePosition(IUnit unit) => GetClosestPosition(unit, addablePositions);

        public ErrorMessage CanMove(IUnit unit, AddableUnitData addableData = default)
        {
            if (!Entity.CanLaunchTask)
                return ErrorMessage.taskSourceCanNotLaunch;

            if (!unit.IsValid())
                return ErrorMessage.invalid;
            if (!unit.IsInteractable)
                return ErrorMessage.uninteractable;
            if (unit.Health.IsDead)
                return ErrorMessage.healthDead;
            if (!unit.CarriableUnit.IsValid())
                return ErrorMessage.carriableComponentMissing;
            if (!addableData.ignoreMvt && !unit.CanMove())
                return ErrorMessage.mvtDisabled;

            if (Entity.IsFree)
            {
                if (!freeFactionBehaviour.IsEntityAllowed(unit) || (addableData.otherFactionBehaviour != CarriableUnitOtherFactionBehaviour.allowEnterFreeCarrier && addableData.otherFactionBehaviour != CarriableUnitOtherFactionBehaviour.allowAll))
                    return ErrorMessage.factionMismatch;
            }
            else if (!RTSHelper.IsSameFaction(unit, Entity))
            {
                if(addableData.otherFactionBehaviour != CarriableUnitOtherFactionBehaviour.allowAll || addableData.otherFactionBehaviour != CarriableUnitOtherFactionBehaviour.allowEnterOtherFactionCarrier)
                    return ErrorMessage.factionMismatch;
            }

            if (!targetPicker.IsValidTarget(unit))
                return ErrorMessage.targetPickerUndefined;

            if (CurrAmount + customUnitSlots.GetAmount(unit) > MaxAmount)
                return ErrorMessage.carrierCapacityReached;

            if (addableData.forceSlot && !freeCarrierPositionIndexes.Contains(addableData.forcedSlotID))
                return ErrorMessage.carrierForceSlotOccupied;

            return ErrorMessage.none;
        }

        public ErrorMessage Move(IUnit unit, AddableUnitData addableData)
        {
            if (addableData.ignoreMvt)
                return Add(unit, addableData);

            ErrorMessage errorMsg;
            if ((errorMsg = CanMove(unit, addableData)) != ErrorMessage.none)
            {
                if (addableData.playerCommand && Entity.IsLocalPlayerFaction())
                    playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                    {
                        message = errorMsg,

                        source = Entity,
                        target = unit
                    });

                return errorMsg;
            }

            Vector3 addablePosition = GetAddablePosition(unit);

            return mvtMgr.SetPathDestination(
                new SetPathDestinationData<IEntity>
                {
                    source = unit,
                    destination = addablePosition,
                    offsetRadius = 0.0f,
                    target = Entity,
                    mvtSource = new MovementSource
                    {
                        sourceTargetComponent = addableData.sourceTargetComponent,

                        targetAddableUnit = this,
                        targetAddableUnitPosition = addablePosition,

                        playerCommand = addableData.playerCommand,

                        isMoveAttackRequest = addableData.isMoveAttackRequest,

                        disableMarker = true
                    }
                });
        }

        // The same conditions for moving a unit to the carrier apply to adding it as well.
        public ErrorMessage CanAdd(IUnit unit, AddableUnitData addableData = default) => CanMove(unit, addableData);

        public ErrorMessage Add(IUnit unit, AddableUnitData addableData = default)
        {
            ErrorMessage errorMsg;
            if ((errorMsg = CanAdd(unit, addableData)) != ErrorMessage.none)
                return errorMsg;

            int positionIndex = addableData.forceSlot ? addableData.forcedSlotID : freeCarrierPositionIndexes[0];

            Transform nextCarrierSlot = null;

            if (positionIndex.IsValidIndex(carrierPositions) && carrierPositions[positionIndex].IsValid())
            {
                unit.MovementComponent.TargetPositionMarker.Toggle(false);
                unit.MovementComponent.SetActiveLocal(false, playerCommand: false);

                unit.MovementComponent.MovementStart += HandleCarriedUnitMovementStart;

                nextCarrierSlot = carrierPositions[positionIndex];
            }
            else
            {
                unit.gameObject.SetActive(false);
                unit.transform.SetParent(Entity.transform, true);
            }

            unit.SetIdle();
            selectionMgr.Remove(unit);

            carrierSlots[positionIndex] = unit;
            unitToCarrierPositionIndex.Add(unit, positionIndex);
            freeCarrierPositionIndexes.Remove(positionIndex);

            CurrAmount += customUnitSlots.GetAmount(unit);

            audioMgr.PlaySFX(Entity.AudioSourceComponent, addUnitAudio.Fetch(), false);

            unit.Health.EntityDead += OnCarriedUnitDead;

            // free faction handling
            // This is the first unit that gets added to the free carrier and we are allowed to update the faction on this
            if(factionEntity.IsFree 
                && freeFactionBehaviour.updateFactionOnOccupy
                && CurrAmount == 1
                && !unit.IsFree)
            {
                factionEntity.SetFactionLocal(unit, unit.FactionID);
                factionEntity.Health.CanBeAttacked = freeFactionBehaviour.canBeAttackedOnValidFaction;
            }

            var args = new UnitCarrierEventArgs(unit, nextCarrierSlot, positionIndex);
            unit.CarriableUnit.OnCarrierUnitAdded(this, args);
            RaiseUnitAdded(args);
            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);

            return ErrorMessage.none;
        }

        private void HandleCarriedUnitMovementStart(IMovementComponent movementComp, MovementEventArgs args)
        {
            movementComp.MovementStart -= HandleCarriedUnitMovementStart;

            EjectActionLocal(movementComp.Entity as IUnit, destroyed: false, playerCommand: false);;
        }
        #endregion

        #region Handling Events: Tracking Carrier Units
        private void OnCarriedUnitDead(IEntity sender, DeadEventArgs e)
        {
            // Directly eject unit since this is called from the local destroy method on the unit
            EjectActionLocal(sender as IUnit, false, false);
        }
        #endregion

        #region Handling Actions
        public override ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input)
        {
            switch ((ActionType)actionID)
            {
                case ActionType.eject:

                    return EjectActionLocal(input.target.instance as IUnit, Mathf.RoundToInt(input.target.position.x) == 1 ? true : false, input.playerCommand);

                case ActionType.ejectAll:

                    return EjectAllActionLocal(Mathf.RoundToInt(input.target.position.x) == 1 ? true : false, input.playerCommand);

                case ActionType.callUnits:

                    return CallUnitsActionLocal(input.playerCommand);

                default:
                    return base.LaunchActionLocal(actionID, input);
            }
        }
        #endregion

        #region Ejecting Units Action
        public Vector3 GetEjectablePosition(IUnit unit) => GetClosestPosition(unit, ejectablePositions.Length > 0 ? ejectablePositions : addablePositions);

        public ErrorMessage EjectAllAction(bool destroyed, bool playerCommand)
        {
            return LaunchAction(
                (byte)ActionType.ejectAll,
                new SetTargetInputData
                {
                    target = new TargetData<IEntity>
                    {
                        position = new Vector3(destroyed ? 1.0f : 0.0f, 0.0f, 0.0f)
                    },
                    playerCommand = playerCommand
                });
        }

        private ErrorMessage EjectAllActionLocal(bool destroyed, bool playerCommand)
        {
            if (!canEjectAllUnits)
                return ErrorMessage.inactive;

            ErrorMessage errorMessage;
            foreach(IUnit storedUnit in carrierSlots)
                if (storedUnit.IsValid() && (errorMessage = EjectActionLocal(storedUnit, destroyed, playerCommand)) != ErrorMessage.none)
                    return errorMessage;

            return ErrorMessage.none;
        }

        public ErrorMessage EjectAction(IUnit unit, bool destroyed, bool playerCommand)
        {
            if (!canEjectSingleUnit)
                return ErrorMessage.inactive;

            return LaunchAction(
                (byte)ActionType.eject,
                new SetTargetInputData
                {
                    target = new TargetData<IEntity>
                    {
                        instance = unit,
                        position = new Vector3(destroyed ? 1.0f : 0.0f, 0.0f, 0.0f)
                    },
                    playerCommand = playerCommand
                });
        }

        private ErrorMessage EjectActionLocal(IUnit unit, bool destroyed, bool playerCommand)
        {
            if (!unit.IsValid() || !carrierSlots.Contains(unit))
                return ErrorMessage.invalid;

            if(!terrainMgr.GetTerrainAreaPosition(
                GetEjectablePosition(unit),
                unit.MovementComponent.TerrainAreas,
                out Vector3 ejectionPosition))
            /*// Only eject if the ejection position is in a movable area for the unit:
            if (!mvtMgr.TryGetMovablePosition(
                GetEjectablePosition(unit),
                unit.MovementComponent.Controller.Radius,
                unit.MovementComponent.Controller.NavigationAreaMask,
                out Vector3 ejectionPosition))*/
            {
                if (playerCommand && Entity.IsLocalPlayerFaction())
                        playerMsgHandler.OnErrorMessage(new PlayerErrorMessageWrapper
                        {
                            message = ErrorMessage.mvtDisabled,

                            source = Entity,
                            target = unit
                        });

                return ErrorMessage.mvtDisabled;
            }

            int positionIndex = unitToCarrierPositionIndex[unit];

            if (unit.IsInteractable && unit.CarriableUnit.CurrSlot.IsValid())
            {
                unit.transform.position = ejectionPosition;

                unit.transform.SetParent(null, true);

                unit.MovementComponent.TargetPositionMarker.Toggle(true, ejectionPosition);
                //unit.MovementComponent.Controller.Enabled = true;
                unit.MovementComponent.SetActiveLocal(true, playerCommand: false);
            }
            else
            {
                unit.transform.position = ejectionPosition;
                unit.gameObject.SetActive(true);

                unit.transform.SetParent(null, true);
            }

            unit.SetIdle();

            carrierSlots[positionIndex] = null;
            unitToCarrierPositionIndex.Remove(unit);
            freeCarrierPositionIndexes.Add(positionIndex);

            CurrAmount -= customUnitSlots.GetAmount(unit);

            audioMgr.PlaySFX(Entity.AudioSourceComponent, ejectUnitAudio.Fetch(), false);

            unit.Health.EntityDead -= OnCarriedUnitDead;

            // free faction handling
            // unit carrier was initialized as a free faction entity, had its faction updated and is allowed to be reset to free faction on empty
            if(initAsFreeFaction && !factionEntity.IsFree
                && freeFactionBehaviour.freeOnEjection
                && CurrAmount == 0)
            {
                factionEntity.SetFactionLocal(null, RTSHelper.FREE_FACTION_ID);
                factionEntity.Health.CanBeAttacked = freeFactionBehaviour.canBeAttackedOnFreeFaction;
            }

            RaiseUnitRemoved(new UnitCarrierEventArgs(unit, unit.CarriableUnit.CurrSlot, positionIndex));
            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);

            // Destroying stored units due to carrier being destroyed?
            if (destroyed && !ejectOnDestroy)
                unit.Health.Destroy(false, null);

            // Unit was ejected normally, not due to carrier destruction
            // See if there is a rallypoint to send the unit to.
            else if (ejectToRallypoint && factionEntity.Rallypoint.IsValid())
                factionEntity.Rallypoint.SendAction(unit, playerCommand: false);

            return ErrorMessage.none;
        }
        #endregion

        #region Calling Units Action
        public ErrorMessage CanCallUnit(SetTargetInputData data)
        {
            IUnit unit = data.target.instance as IUnit;

            ErrorMessage errorMsg;
            if ((errorMsg = CanAdd(unit)) != ErrorMessage.none)
                return errorMsg;
            else if (callIdleOnly && !unit.IsIdle)
                return ErrorMessage.carrierIdleOnlyAllowed;
            else if (!callAttackUnits && unit.CanAttack)
                return ErrorMessage.carrierAttackerNotAllowed;

            return ErrorMessage.none;
        }

        public ErrorMessage CallUnitsAction(bool playerCommand)
        {
            return LaunchAction((byte)ActionType.callUnits, new SetTargetInputData { playerCommand = playerCommand });
        }

        private ErrorMessage CallUnitsActionLocal(bool playerCommand)
        {
            audioMgr.PlaySFX(Entity.AudioSourceComponent, callUnitsAudio.Fetch(), false);

            gridSearch.Search(
                Entity.transform.position,
                callUnitsRange,
                MaxAmount - CurrAmount,
                CanCallUnit,
                playerCommand,
                out IReadOnlyList<IUnit> unitsInRange
                );

            for (int i = 0; i < unitsInRange.Count; i++)
            {
                unitsInRange[i].CarriableUnit.SetTarget(this, unitsInRange[i].CarriableUnit.GetAddableData(playerCommand));

                RaiseUnitCalled(new UnitCarrierEventArgs(unitsInRange[i], null, -1));
            }

            return ErrorMessage.none;
        }
        #endregion

        #region Task UI
        protected override bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
        {
            if (!Entity.CanLaunchTask
                || !IsActive
                || !RTSHelper.IsLocalPlayerFaction(Entity))
                return false;

            // In the case of single unit ejection task, the task's code is appended by the unit's index in the "storedUnits" list since each individual unit's ejection is a unique task.
            // Not all properties used in the single unit ejection task are used: displayType is forced to single, fixedSlotIndex is disabled and icon is replaced by unit's icon.
            if (canEjectSingleUnit && ejectSingleUnitTaskUI.IsValid())
            {
                IEnumerable<int> storedUnitIDs = Enumerable.Range(0, carrierSlots.Length);
                for (int ID = 0; ID < carrierSlots.Length; ID++)
                {
                    if (!ejectSingleUnitTaskUI.Data.enabled || !carrierSlots[ID].IsValid())
                        disabledTaskCodesCache.Add($"{ejectSingleUnitTaskUI.Data.code}_{ID}");
                    else
                    {
                        taskUIAttributesCache.Add(
                            new EntityComponentTaskUIAttributes
                            {
                                data = new EntityComponentTaskUIData
                                {
                                    enabled = true,

                                    code = $"{ejectSingleUnitTaskUI.Data.code}_{ID}",
                                    displayType = EntityComponentTaskUIData.DisplayType.singleSelection,
                                    icon = carrierSlots[ID].Icon,

                                    forceSlot = false,

                                    panelCategory = ejectSingleUnitTaskUI.Data.panelCategory,
                                    tooltipEnabled = ejectSingleUnitTaskUI.Data.tooltipEnabled,
                                    hideTooltipOnClick = ejectSingleUnitTaskUI.Data.hideTooltipOnClick,
                                    description = ejectSingleUnitTaskUI.Data.description,
                                },

                                locked = false,
                            });
                    }
                }
            }

            if (canEjectAllUnits && ejectAllUnitsTaskUI.IsValid())
            {
                if (CurrAmount == 0 || !ejectAllUnitsTaskUI.Data.enabled)
                    disabledTaskCodesCache.Add(ejectAllUnitsTaskUI.Data.code);
                else
                    taskUIAttributesCache.Add(new EntityComponentTaskUIAttributes
                    {
                        data = ejectAllUnitsTaskUI.Data,
                    });
            }

            if (callUnitsTaskUI.IsValid())
            {
                if (HasMaxAmount || !callUnitsTaskUI.Data.enabled)
                    disabledTaskCodesCache.Add(callUnitsTaskUI.Data.code);
                else
                    taskUIAttributesCache.Add(new EntityComponentTaskUIAttributes
                    {
                        data = callUnitsTaskUI.Data,
                    });
            }

            return true;
        }

        public override bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes)
        {
            string taskCode = taskAttributes.data.code;

            if (ejectAllUnitsTaskUI.IsValid() && taskCode == ejectAllUnitsTaskUI.Data.code)
                EjectAllAction(false, true);
            else if (callUnitsTaskUI.IsValid() && taskCode == callUnitsTaskUI.Data.code)
                CallUnitsAction(true);
            else
            {
                // Check the creation of the eject single unit tasks in "OnTaskUIRequest()" for info on how the task code is set.
                string[] splits = taskCode.Split('_');
                EjectAction(carrierSlots[int.Parse(splits[splits.Length - 1])], false, true);
            }

            return true;
        }
        #endregion

        #region Activating/Deactivating Component
        protected override void OnActiveStatusUpdated()
        {
            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);
        }
        #endregion

        #region Helper Methods
        public bool IsUnitStored(IUnit unit) => carrierSlots.Contains(unit);

        private Vector3 GetClosestPosition(IUnit unit, Transform[] transforms)
        {
            Vector3 closestPosition = transforms
                .Select(t => t.position)
                .OrderBy(pos => (pos - unit.MovementComponent.StartPosition).sqrMagnitude)
                .First();

            logger.RequireTrue(terrainMgr.GetTerrainAreaPosition(closestPosition, forcedTerrainAreas, out Vector3 closestPositionAdjusted),
                $"[{GetType().Name} - {Entity.Code}] Unable to find a valid position on the defined terrain areas!");

            return closestPositionAdjusted;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private bool showStoredUnitsGizmos = true;
        [HideInInspector]
        public bool storedUnitsFoldout;

        protected void OnDrawGizmosSelected()
        {
            if (showStoredUnitsGizmos)
            {
                Gizmos.color = Color.green;
                foreach(var slot in CarrierSlots)
                {
                    if (slot.IsValid())
                        Gizmos.DrawWireSphere(slot.transform.position, slot.Radius);
                }
            }
        }
#endif

        #endregion
    }
}
