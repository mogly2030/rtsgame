using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Attack;
using RTSEngine.Determinism;
using RTSEngine.Entities;
using RTSEngine.Movement;
using RTSEngine.NPC.Attack;
using RTSEngine.NPC.Event;
using RTSEngine.EntityComponent;

namespace RTSEngine.NPC.UnitExtension
{
    public class NPCUnitBehaviourManager : NPCComponentBase, INPCUnitBehaviourManager
    {
        #region Attributes
        // We can have multiple instances of this component
        public override bool IsSingleInstance => false;

        [SerializeField, EnforceType(typeof(IUnit), prefabOnly: true), Tooltip("Prefabs of unit types whose behaviour would managed by this component.")]
        private GameObject[] prefabs = new GameObject[0];
        private NPCActiveRegulatorMonitor regulatorMonitor;

        [Space, SerializeField, Tooltip("Pick parameters that allow to force the NPC faction to update the creation goals (target amount) of the assigned unit types.")]
        private NPCFactionEntityForceCreationData forceCreation = new NPCFactionEntityForceCreationData
        {
            enabled = true,

            targetCountUpdateDelay = new FloatRange(10.0f, 20.0f),
            targetCountUpdatePeriod = new FloatRange(3.0f, 7.0f),

            targetCountUpdateAmount = 1
        };

        /// <summary>
        /// Current behaviour state of the tracked units.
        /// </summary>
        public NPCUnitBehaviourState State { private set; get; }

        [Space, SerializeField, Tooltip("Pick parameters that allow to define the behaviour of the handled unit instances when an attack engagement order is set by the NPC faction.")]
        private NPCAttackEngageOrderUnitBehaviourData attackEngageOrderBehaviour = new NPCAttackEngageOrderUnitBehaviourData
        {
            send = true,
            sendIdleOnly = true,
            sendNoTargetThreatOnly = true,

            sendRatioRange = new FloatRange(0.8f, 0.9f),

            sendDelay = new FloatRange(0.0f, 2.0f),

            attack = true,

            sendBackOnAttackCancel = true
        };
        private bool awaitingAttackEngageOrderResponse = false;
        private NPCAttackEngageOrderTargetData nextAttackEngageOrderTargetData;

        [Space, SerializeField, Tooltip("Pick parameters that allow to define the behaviour of the handled unit instances when the NPC faction announces a territory defense state.")]
        private NPCTerritoryDefenseOrderUnitBehaviourData territoryDefenseOrderBehaviour = new NPCTerritoryDefenseOrderUnitBehaviourData
        {
            defend = true,

            forceChangeDefenseCenter = false,

            sendBackOnDefenseCancel = true
        };


        private List<IUnit> sendableUnits;

        // Game services
        protected IAttackManager attackMgr { private set; get; }
        protected IMovementManager mvtMgr { private set; get; }

        // NPC Components
        protected INPCUnitCreator npcUnitCreator { private set; get; }
        protected INPCEventPublisher npcEvent { private set; get; }
        #endregion

        #region Initializing/Terminating
        protected override void OnPreInit()
        {
            this.npcUnitCreator = npcMgr.GetNPCComponent<INPCUnitCreator>();
            this.npcEvent = npcMgr.GetNPCComponent<INPCEventPublisher>();

            this.attackMgr = gameMgr.GetService<IAttackManager>();
            this.mvtMgr = gameMgr.GetService<IMovementManager>();

            sendableUnits = new List<IUnit>(RTSOptimizations.INIT_SENDABLE_UNITS_CAPACITY);

            // Initial state
            forceCreation.timer = new TimeModifiedTimer(forceCreation.targetCountUpdateDelay.RandomValue + forceCreation.targetCountUpdatePeriod.RandomValue);
            awaitingAttackEngageOrderResponse = false;

            State = NPCUnitBehaviourState.idle;
            LogEvent($"[ORDER UPDATED] Initial Idle Order.");
        }

        protected override void OnPostInit()
        {
            ActivateUnitRegulators();

            npcEvent.AttackEngageOrder += HandleAttackEngageOrder;
            npcEvent.AttackCancelled += HandleAttackCancelled;

            npcEvent.TerritoryDefenseOrder += HandleTerritoryDefenseOrder;
            npcEvent.TerritoryDefenseCancelled += HandleTerritoryDefenseCancelled;
        }

        protected override void OnDestroyed()
        {
            npcEvent.AttackEngageOrder -= HandleAttackEngageOrder;
            npcEvent.AttackCancelled -= HandleAttackCancelled;

            npcEvent.TerritoryDefenseOrder -= HandleTerritoryDefenseOrder;
            npcEvent.TerritoryDefenseCancelled -= HandleTerritoryDefenseCancelled;

            regulatorMonitor.Disable();
        }

        private void ActivateUnitRegulators()
        {
            regulatorMonitor = new NPCActiveRegulatorMonitor(gameMgr, factionMgr);

            for (int i = 0; i < prefabs.Length; i++)
            {
                IUnit unit = prefabs[i].GetComponent<IUnit>();
                if (!logger.RequireValid(unit,
                    $"[{GetType().Name} - {factionMgr.FactionID}] 'Prefabs' field has some unassigned elements."))
                    return;

                NPCUnitRegulator nextRegulator;
                if ((nextRegulator = npcUnitCreator.ActivateUnitRegulator(unit)).IsValid())
                    regulatorMonitor.AddCode(nextRegulator.Prefab.Code);
            }
        }
        #endregion

        #region Forcing Creation
        protected override void OnActiveUpdate()
        {
            if (forceCreation.enabled)
                OnForceCreationUpdate();

            if (awaitingAttackEngageOrderResponse)
                HandleAttackEngageOrderResponse();
        }

        private void OnForceCreationUpdate()
        {
            if (!forceCreation.timer.ModifiedDecrease())
                return;

            forceCreation.timer.Reload(forceCreation.targetCountUpdatePeriod);

            for (int i = 0; i < regulatorMonitor.AllCodes.Count; i++)
            {
                NPCUnitRegulator nextUnitRegulator = npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]);

                nextUnitRegulator.UpdateTargetCount(nextUnitRegulator.TargetCount + forceCreation.targetCountUpdateAmount);

                LogEvent($"{regulatorMonitor.AllCodes[i]}: Updated target count to {nextUnitRegulator.TargetCount}");
            }
        }
        #endregion

        #region Attack Engage Behaviour
        private void HandleAttackEngageOrder(INPCAttackManager sender, NPCAttackEngageEventArgs args)
        {
            if (!attackEngageOrderBehaviour.send)
                return;

            nextAttackEngageOrderTargetData = new NPCAttackEngageOrderTargetData
            {
                target = args.Target,
                targetPosition = args.TargetPosition,

                delayTimer = new TimeModifiedTimer(
                    awaitingAttackEngageOrderResponse
                    ? nextAttackEngageOrderTargetData.delayTimer.CurrValue 
                    : attackEngageOrderBehaviour.sendDelay.RandomValue),
            };

            awaitingAttackEngageOrderResponse = true;
        }

        private void HandleAttackCancelled(INPCAttackManager sender, EventArgs args)
        {
            awaitingAttackEngageOrderResponse = false;
            nextAttackEngageOrderTargetData = new NPCAttackEngageOrderTargetData();

            if (!attackEngageOrderBehaviour.sendBackOnAttackCancel)
                return;

            SendToSpawnPoints();
        }

        private void HandleAttackEngageOrderResponse()
        {
            if (!nextAttackEngageOrderTargetData.delayTimer.ModifiedDecrease())
                return;

            State = NPCUnitBehaviourState.attacking;
            LogEvent($"[ORDER UPDATED] Attack Order Received.");

            awaitingAttackEngageOrderResponse = false;

            sendableUnits.Clear();
            for (int i = 0; i < regulatorMonitor.AllCodes.Count; i++)
            {
                string unitCode = regulatorMonitor.AllCodes[i];
                if (attackEngageOrderBehaviour.sendIdleOnly)
                {
                    sendableUnits.AddRange(npcUnitCreator.GetActiveUnitRegulator(unitCode).IdleInstances);
                }
                else if (attackEngageOrderBehaviour.sendNoTargetThreatOnly)
                {
                    sendableUnits.AddRange(npcUnitCreator.GetActiveUnitRegulator(unitCode).IdleInstances);
                    for (int j = 0; j < npcUnitCreator.GetActiveUnitRegulator(unitCode).NonIdleInstances.Count; j++)
                    {
                        IUnit unit = npcUnitCreator.GetActiveUnitRegulator(unitCode).NonIdleInstances[j];
                        if ((unit.FirstActiveAttackComponent.Target.instance != nextAttackEngageOrderTargetData.target)
                            && (!unit.CanAttack || !unit.FirstActiveAttackComponent.HasTarget || !unit.FirstActiveAttackComponent.Target.instance.CanAttack))
                            sendableUnits.Add(unit);
                    }
                }
                else
                    sendableUnits.AddRange(npcUnitCreator.GetActiveUnitRegulator(unitCode).Instances);
            }

            if (sendableUnits.Count == 0)
                return;

            if (attackEngageOrderBehaviour.attack)
            {
                attackMgr.LaunchAttack(
                    new LaunchAttackData<IReadOnlyList<IEntity>>
                    {
                        source = sendableUnits,
                        targetEntity = nextAttackEngageOrderTargetData.target,
                        targetPosition = nextAttackEngageOrderTargetData.target.IsValid() 
                            ? RTSHelper.GetAttackTargetPosition(sendableUnits[0], nextAttackEngageOrderTargetData.target)
                            : nextAttackEngageOrderTargetData.targetPosition,
                        playerCommand = true
                    });

                LogEvent($"[ATTACK ORDER] Sending in {sendableUnits.Count} units to attack target {nextAttackEngageOrderTargetData.target}");
            }
            else
            {
                LogEvent($"[ATTACK ORDER] Sending in {sendableUnits.Count} units to move to target {nextAttackEngageOrderTargetData.target}");

                mvtMgr.SetPathDestination(
                    new SetPathDestinationData<IReadOnlyList<IEntity>>
                    {
                        source = sendableUnits,
                        destination = nextAttackEngageOrderTargetData.target.IsValid() ? nextAttackEngageOrderTargetData.target.transform.position : nextAttackEngageOrderTargetData.targetPosition,
                        offsetRadius = nextAttackEngageOrderTargetData.target.IsValid() ? nextAttackEngageOrderTargetData.target.Radius : 0.0f,
                        target = nextAttackEngageOrderTargetData.target,
                        mvtSource = new MovementSource { playerCommand = true }
                    });
            }
        }
        #endregion

        #region Territory Defense Behaviour
        private void HandleTerritoryDefenseOrder(INPCDefenseManager sender, NPCTerritoryDefenseEngageEventArgs args)
        {
            if (!territoryDefenseOrderBehaviour.defend)
                return;

            State = NPCUnitBehaviourState.defending;
            LogEvent($"[ORDER UPDATED] Defense Order Received.");

            int count = 0;
            for (int i = 0; i < regulatorMonitor.AllCodes.Count; i++)
            {
                for (int j = 0; j < npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances.Count; j++)
                {
                    IUnit unit = npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances[j];

                    if (!unit.CanAttack
                        || (unit.FirstActiveAttackComponent.HasTarget && !territoryDefenseOrderBehaviour.forceChangeDefenseCenter))
                        continue;

                    // the playerCommand is set to ON in order to bypass the LOS constraints when having the attack units automatically find targets all over the border territory
                    if(unit.FirstActiveAttackComponent.BorderTargetFinderData.center != args.NextDefenseCenter.BorderComponent.Building.transform
                        || unit.FirstActiveAttackComponent.BorderTargetFinderData.range != args.NextDefenseCenter.BorderComponent.Size)
                        unit.FirstActiveAttackComponent.SetSearchRangeCenterAction(args.NextDefenseCenter.BorderComponent, playerCommand: true);
                    count++;
                }
            }

            LogEvent($"[DEFENSE ORDER] Sending in {count} units to defend building center {args.NextDefenseCenter.BorderComponent?.Building.gameObject.name}");
        }

        private void HandleTerritoryDefenseCancelled(INPCDefenseManager sender, EventArgs args)
        {
            int count = 0;
            for (int i = 0; i < regulatorMonitor.AllCodes.Count; i++)
            {
                for (int j = 0; j < npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances.Count; j++)
                {
                    IUnit unit = npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances[j];

                    if (!unit.CanAttack)
                        continue;

                    unit.FirstActiveAttackComponent.SetSearchRangeCenterAction(null, playerCommand: false);
                    count++;
                }
            }

            LogEvent($"[DEFENSE ORDER] Cancelling defense order for {count} units.");

            if (!territoryDefenseOrderBehaviour.sendBackOnDefenseCancel)
                return;

            SendToSpawnPoints();
        }
        #endregion

        #region Helper Methods
        private void SendToSpawnPoints()
        {
            State = NPCUnitBehaviourState.idle;
            LogEvent($"[ORDER UPDATED] Idle Order Receieved, Sending To Spawn.");


            for (int i = 0; i < regulatorMonitor.AllCodes.Count; i++)
            {
                LogEvent($"[SEND TO SPAWN ORDER] Sending units of code {regulatorMonitor.AllCodes[i]} back to spawn positions.");

                if (npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances.Count == 0)
                    continue;

                IUnit refUnit = npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances[0];
                mvtMgr.SetPathDestination(
                    new SetPathDestinationData<IReadOnlyList<IEntity>>
                    {
                        source = npcUnitCreator.GetActiveUnitRegulator(regulatorMonitor.AllCodes[i]).Instances,
                        destination = refUnit.SpawnRallypoint.IsValid() ? refUnit.SpawnRallypoint.Entity.transform.position : factionSlot.FactionSpawnPosition,
                        offsetRadius = refUnit.SpawnRallypoint.IsValid() ? refUnit.SpawnRallypoint.Entity.Radius : 0.0f,
                        target = null,
                        mvtSource = new MovementSource { playerCommand = true }
                    });
            }
        }
        #endregion

        #region Logging
        [SerializeField, ReadOnly]
        private NPCUnitBehaviourState currStateLog;

        [Serializable]
        private struct NPCUnitBehaviourLogData 
        {
            public string code;
            public int amount;
            public int pendingAmount;
            public int idleAmount;
        }
        [SerializeField, ReadOnly]
        private NPCUnitBehaviourLogData[] trackedUnits = new NPCUnitBehaviourLogData[0];

        protected override void UpdateActiveLogs()
        {
            currStateLog = State;

            trackedUnits = regulatorMonitor
                .AllCodes
                .Select(code =>
                {
                    var regulator = npcUnitCreator.GetActiveUnitRegulator(code);
                    if (!regulator.IsValid())
                        return new NPCUnitBehaviourLogData { };
                    else 
                        return new NPCUnitBehaviourLogData
                        {
                            code = code,
                            amount = regulator.Count,
                            pendingAmount = regulator.CurrPendingAmount,
                            idleAmount = regulator.IdleInstances.Count(),
                        };
                })
                .ToArray();
        }
        #endregion
    }

}
