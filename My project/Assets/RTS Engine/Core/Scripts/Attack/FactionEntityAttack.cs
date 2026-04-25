using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

using RTSEngine.Attack;
using RTSEngine.BuildingExtension;
using RTSEngine.Determinism;
using RTSEngine.Entities;
using RTSEngine.UI;
using RTSEngine.Event;
using RTSEngine.Animation;
using RTSEngine.Audio;
using System;

namespace RTSEngine.EntityComponent
{
    public abstract class FactionEntityAttack : FactionEntityTargetProgressComponent<IFactionEntity>, IAttackComponent
    {
        #region Attributes
        /*
         * Action types and their parameters:
         * switchAttack: no parameters. Deactivates the currently active attack component attached to the entity and activates this one.
         * lockAttack: target.position.x => 0 to lock, else unlock.
         * */
        public enum ActionType : byte { switchAttack = 0, lockAttack = 1, setNextLaunchLog = 2, setSearchRangeCenter = 3, cancelAttack = 4 }

        // GENERAL
        public abstract IAttackDistanceHandler AttackDistanceHandler { get; }

        [SerializeField, Tooltip("If the attack is locked then the faction entity can not switch to it unless it is unlocked.")]
        private bool isLocked = false;
        public bool IsLocked => isLocked;

        [SerializeField, Tooltip("Get back to this attack type after an engagement from another attack type is complete.")]
        private bool revert = true;
        public bool Revert => revert;

        // ENGAGEMENT
        [SerializeField, Tooltip("Engagement options and settings for the attack.")]
        private AttackEngagementOptions engageOptions = new AttackEngagementOptions { engageFriendly = false, engageOnAssign = true, engageOnce = false, engageWhenAttacked = true, autoIgnoreAngleLOS = true };
        public AttackEngagementOptions EngageOptions => engageOptions;

        // When enabled, it allows the attacker to proceed with an attack iteration.
        [SerializeField, HideInInspector]
        private bool isAttackReady = true;

        // TARGET
        [SerializeField, Tooltip("Define the buildings/units that can be targetted and attacked using their code and/or categories.")]
        private AttackTargetPicker targetPicker = new AttackTargetPicker();

        [SerializeField, Tooltip("Does this attack require a target to be assigned to launch it? When disabled and terrain attack is active, the attacker can launch an attack towards a position in the terrain.")]
        private bool requireTarget = true;
        public bool RequireTarget => requireTarget;
        [SerializeField, Tooltip("When enabled, the attacker is able to keep launching terrain attack iterations. Otherwise, the attacker will launch one terrain attack iteration then stop.")]
        private bool allowMultipleTerrainAttacks = false;
        [SerializeField, HideInInspector]
        private bool terrainAttackActive = false;

        /// <summary>
        /// True when the attacker enters the attacking range to engage with its target.
        /// </summary>
        public bool IsInTargetRange { private set; get; } = false;

        /// <summary>
        /// If no target is required to be assigned to launch the attack then see if the terrain attack is currently enabled for the attacker.
        /// </summary>
        public override bool HasTarget => base.HasTarget || (!requireTarget && terrainAttackActive);

        // TIME
        [SerializeField, Tooltip("Reload time between two consecutive attack iterations.")]
        private float reloadDuration = 3.0f;
        protected TimeModifiedTimer reloadTimer;
        public float CurrReloadValue => reloadTimer.CurrValue;

        [SerializeField, Tooltip("If enabled, then another component must call the 'TriggerAttack()' method to trigger the attack launch.")]
        private bool delayTriggerEnabled = false;
        [SerializeField, HideInInspector]
        protected bool attackIterationTriggered = false;

        [SerializeField, Tooltip("Enable/disable cooldown time for the attack.")]
        private GlobalTimeModifiedTimer cooldown = new GlobalTimeModifiedTimer();
        public bool IsCooldownActive => cooldown.IsActive;
        public float CurrCooldownValue => cooldown.CurrValue;

        // LAUNCHER
        [SerializeField, Tooltip("Settings for launching the attack.")]
        private AttackLauncher launcher = new AttackLauncher();
        public AttackLauncher Launcher => launcher;

        // DAMAGE
        [SerializeField, Tooltip("Settings for the attack's damage effect.")]
        private AttackDamage damage = new AttackDamage();
        public AttackDamage Damage => damage;

        // WEAPON
        public Transform WeaponTransform => inProgressObject.IsValid() ? inProgressObject.transform : null;
        [SerializeField, Tooltip("Settings to handle the attack weapon.")]
        private AttackWeapon weapon = new AttackWeapon();
        public AttackWeapon Weapon => weapon;

        // LOS
        [SerializeField, Tooltip("Settings to handle the attack's line of sight.")]
        private AttackLOS lineOfSight = new AttackLOS();
        public AttackLOS LineOfSight => lineOfSight;

        // ATTACK-MOVE
        public virtual bool IsAttackMoveEnabled => false;
        public virtual bool IsAttackMoveActive { protected set; get; }

        // AI
        // Target finder used for NPC factions to locate enemies within the borders of a building center.
        private TargetEntityFinder<IFactionEntity> borderTargetFinder = null;
        public TargetEntityFinderData BorderTargetFinderData => borderTargetFinder.IsValid() ? borderTargetFinder.Data : new TargetEntityFinderData();

        // Pre-Assigned AttackLaunchLogs
        // When enabled, the attack launcher will use the following launch logs instead of following the settings in the inspector 
        public bool nextLaunchLogEnabled = false;
        private IReadOnlyCollection<AttackObjectLaunchLogInput> nextLaunchLog = null;
        // When enabled, the attack launcher will only use the above log if the targets do match 
        private bool nextLaunchLogTargetEnabled;
        private IFactionEntity nextLaunchLogTarget;

        // UI
        [SerializeField, Tooltip("How would the set target task look when the attack type is in cooldown?")]
        private EntityComponentLockedTaskUIData setTargetCooldownUIData = new EntityComponentLockedTaskUIData { color = Color.red, icon = null };
        [SerializeField, Tooltip("Defines information used to display the attack switch task in the task panel.")]
        private EntityComponentTaskUIAsset switchTaskUI = null;
        [SerializeField, Tooltip("How would the attack switch task look when the attack type is in cooldown?")]
        private EntityComponentLockedTaskUIData switchAttackCooldownUIData = new EntityComponentLockedTaskUIData { color = Color.red, icon = null };
        [SerializeField, Tooltip("Defines information used to display the attack cancel in the task panel when the entity is actively attacking and is used to stop the attack iteration.")]
        private EntityComponentTaskUIAsset cancelTaskUI = null;

        // AUDIO
        [SerializeField, Tooltip("What audio clip to play when the attack is launched?")]
        private AudioClipFetcher attackCompleteAudio = new AudioClipFetcher();

        // EVENTS
        [SerializeField, Tooltip("Triggered when the target enters in the attack range.")]
        private UnityEvent attackRangeEnterEvent = null;
        [SerializeField, Tooltip("Triggered when the attacker locks a target.")]
        private UnityEvent targetLockedEvent = null;
        [SerializeField, Tooltip("Triggered when one attack iteration is complete.")]
        private UnityEvent completeEvent = null;

        // Game services
        protected IAttackManager attackMgr { private set; get; }
        #endregion

        #region Raising Events
        private void OnCooldownOver()
        {
            RaiseCooldownUpdated();
            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this);
        }

        public event CustomEventHandler<IAttackComponent, EventArgs> CooldownUpdated;
        private void RaiseCooldownUpdated()
        {
            var handler = CooldownUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event CustomEventHandler<IAttackComponent, EventArgs> ReloadUpdated;
        private void RaiseReloadUpdated()
        {
            var handler = ReloadUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        protected override void OnProgressInit()
        {
            this.attackMgr = gameMgr.GetService<IAttackManager>();

            cooldown.Init(gameMgr, OnCooldownOver);

            // Init attack sub-components:
            damage.Init(gameMgr, this);
            launcher.Init(gameMgr, this);
            Weapon.Init(gameMgr, this);
            lineOfSight.Init(gameMgr, this);

            reloadTimer = new TimeModifiedTimer(0.0f);
            ResetReload(isAttackComplete: false);
            ResetAttack();

            factionEntity.Health.EntityHealthUpdated += HandleEntityHealthUpdated;

            OnAttackInit();
        }

        protected virtual void OnAttackInit() { }

        protected sealed override void OnTargetDisabled()
        {
            //unsub from events:
            factionEntity.Health.EntityHealthUpdated -= HandleEntityHealthUpdated;
            if (borderTargetFinder.IsValid())
                borderTargetFinder.Disable();

            damage.Disable();
            launcher.Disable();
            Weapon.Disable();
            lineOfSight.Disable();

            OnAttackDisabled();
        }

        protected virtual void OnAttackDisabled() { }
        #endregion

        #region Handling Event: Faction Entity Health Updated
        private void HandleEntityHealthUpdated(IEntity sender, HealthUpdateArgs e)
        {
            if (e.Value < 0.0
                && e.Source != null
                && factionEntity.IsIdle
                && engageOptions.engageWhenAttacked)
                SetTarget(new SetTargetInputData
                {
                    target = RTSHelper.ToTargetData(e.Source),
                    playerCommand = false,
                    // Turn on ignoring LOS so that if the unit/building is hit outside of its and it can engage back, we allow it
                    ignoreLOS = true
                });
        }
        #endregion

        #region Stopping Attack
        protected sealed override void OnProgressStop()
        {
            // In case the launcher is preparing to trigger attack objects, reset it.
            launcher.Reset();

            terrainAttackActive = false;
            IsInTargetRange = false;

            ResetReload(isAttackComplete: false);
            ResetAttack();

            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this, new TaskUIReloadEventArgs(reloadAll: false));

            OnAttackStop();
        }

        protected virtual void OnAttackStop() { }
        #endregion

        #region Handling Time Update
        protected override void OnUpdate()
        {
            if (isAttackReady
                && reloadTimer.CurrValue > 0)
            {
                reloadTimer.ModifiedDecrease();
                if (CurrReloadValue <= 0.0f)
                    RaiseReloadUpdated();
            }

            weapon.Update();
            launcher.Update();
        }
        #endregion

        #region Searching/Updating Target
        public override bool CanSearch => true;

        public override ErrorMessage IsTargetValid(SetTargetInputData data)
        {
            if (IsCooldownActive)
                return ErrorMessage.attackTypeInCooldown;

            TargetData<IFactionEntity> potentialTarget = data.target;

            if (!factionEntity.CanLaunchTask)
                return ErrorMessage.taskSourceCanNotLaunch;
            // See if the attack entity can attack without target
            else if (!potentialTarget.instance.IsValid())
                return !requireTarget && attackMgr.CanLaunchTerrainAttack(data.ToLaunchAttackData<IEntity>(Entity, Entity))
                    ? ErrorMessage.none
                    : ErrorMessage.attackTargetRequired;

            if (potentialTarget.instance == factionEntity)
                return ErrorMessage.invalid;
            else if (!potentialTarget.instance.IsInteractable)
                return ErrorMessage.uninteractable;
            else if (potentialTarget.instance.Health.IsDead)
                return ErrorMessage.healthDead;
            else if (!potentialTarget.instance.Health.CanBeAttacked)
                return ErrorMessage.attackTargetDisabled;
            else if (factionEntity.IsFriendlyFaction(potentialTarget.instance) && !engageOptions.engageFriendly)
                return ErrorMessage.factionIsFriendly;
            else if (gameMgr.InPeaceTime && !factionEntity.IsFree && !potentialTarget.instance.IsFree)
                return ErrorMessage.gamePeaceTimeActive;
            else if (!targetPicker.IsValidTarget(potentialTarget.instance))
                return ErrorMessage.targetPickerUndefined;
            // We leave checking for LOS as the last condition
            // Since 'ErrorMessage IsTargetValid(SetTargetInputData testInput)' which calls this method when SetTargetLocal is called..
            // ... has the flag for ignoring LOS which can override the decision made in this condition
            else if (!data.ignoreLOS && !data.playerCommand)
            {
                ErrorMessage errorMessage = LineOfSight.IsInSight(data.target, ignoreAngle: engageOptions.autoIgnoreAngleLOS, ignoreObstacle: engageOptions.autoIgnoreObstacleLOS);
                if (errorMessage != ErrorMessage.none)
                    return errorMessage;
            }

            return ErrorMessage.none;
        }

        // Use the AttackManager to handle setting the target of the attack.
        public override ErrorMessage SetTarget(SetTargetInputData input)
        {
            if (Entity.TasksQueue.IsValid() && Entity.TasksQueue.CanAdd(input))
            {
                return Entity.TasksQueue.Add(new SetTargetInputData
                {
                    componentCode = Code,

                    target = input.target,
                    playerCommand = input.playerCommand,
                });
            }

            return attackMgr.LaunchAttack(new LaunchAttackData<IEntity>
            {
                source = Entity,

                mode = LaunchAttackTypeMode.specific,
                attackTypeCode = Code,

                targetEntity = input.target.instance as IFactionEntity,
                targetPosition = RTSHelper.GetAttackTargetPosition(this, input.target),

                playerCommand = input.playerCommand,

                isMoveAttackRequest = input.isMoveAttackRequest,
                ignoreLOS = input.ignoreLOS,

                allowTerrainAttack = input.allowTerrainAttack
            });
        }

        protected override void OnTargetPostLocked(SetTargetInputData input, bool sameTarget)
        {
            base.OnTargetPostLocked(input, sameTarget);

            // to allow the entity to start a new attack iteration after it is assigned its target
            ResetReload(isAttackComplete: false);
            ResetAttack();

            // No target assigned but this is allowed for this attacker
            if (!Target.instance.IsValid() && !requireTarget)
                // Incoming terrain attack
                terrainAttackActive = true;

            targetLockedEvent.Invoke();
            globalEvent.RaiseEntityComponentTargetLockedGlobal(this, new TargetDataEventArgs(Target));

            globalEvent.RaiseEntityComponentTaskUIReloadRequestGlobal(this, new TaskUIReloadEventArgs(reloadAll: false));
        }
        #endregion

        #region Engaging Target
        // 'Progress' from FacitonEntityTargetComponent is used as the delay time in this component
        protected override void OnTargetUpdate() { }

        // Stop the engagement when the target is valid (no terrain attack) and it is dead or has been converted to a friendly faction while engaging friendly entities is not allowed.
        protected override bool MustStopProgress()
        {
            return !terrainAttackActive
                && (Target.instance.Health.IsDead
                    // Check whether the attacker can engage friendly entities
                    || (factionEntity.IsFriendlyFaction(Target.instance) && !engageOptions.engageFriendly));
        }

        // Enable engagement progress only if the target is inside the attack range
        protected override bool CanEnableProgress() => CanStartAttackIteration() == ErrorMessage.none;

        public ErrorMessage CanStartAttackIteration()
        {
            if (!isAttackReady)
                return ErrorMessage.attackTypeNotReady;
            else if (reloadTimer.CurrValue > 0.0f)
                return ErrorMessage.attackTypeReloadNonZero;
            else if (cooldown.IsActive)
                return ErrorMessage.attackTypeInCooldown;
            return LineOfSight.IsInSight(Target);
        }

        // Engagement progress is enabled when the target is inside the attack range.
        protected override void OnInProgressEnabled()
        {
            globalEvent.RaiseEntityComponentTargetStartGlobal(this, new TargetDataEventArgs(Target));

            if (IsInTargetRange == false) //if the attacker never was in the target's range but just entered it:
            {
                IsInTargetRange = true;

                attackRangeEnterEvent.Invoke();

                OnEnterTargetRange();
            }

            // Here 'progressDuration' is used for the delay timer.
            // If there's any sign of upcoming delay time then trigger the callback
            if (progressDuration > 0.0f || !attackIterationTriggered)
                OnDelayEnter();

            // Attacker must finish this attack iteration first before moving to the next one!
            isAttackReady = false;
        }

        protected virtual void OnEnterTargetRange() { }

        // Attacker is in range of its target however, it must make sure that its target is in its line of sight before it can launch the attack.
        protected override bool CanProgress() => true;

        protected override bool MustDisableProgress() => false;

        protected override void OnInProgressDisabledEffects()
        {
            ToggleSourceTargetEffect(false);
        }

        // Called when the attack delay time is through
        protected override void OnProgress()
        {
            DisableProgress();

            StartCoroutine(PrepareLaunch());
        }

        private IEnumerator PrepareLaunch()
        {
            // Wait for the attack to be triggered or go through directly if delay trigger is disabled.
            yield return new WaitWhile(() => !attackIterationTriggered);

            Launch();
        }

        protected virtual void OnDelayEnter() { }

        private void ResetReload(bool isAttackComplete)
        {
            reloadTimer.SetDefaultValue(isAttackComplete ? reloadDuration : reloadTimer.CurrValue);
            reloadTimer.Reload();
            RaiseReloadUpdated();
        }

        // resets fields to allow to start a new attack iteration
        private void ResetAttack()
        {
            // Does the attack need to be triggered from an external component?
            attackIterationTriggered = !delayTriggerEnabled;

            // A new attack iteration can be proceeded by the attacker
            isAttackReady = true;
        }

        public void TriggerAttack()
        {
            attackIterationTriggered = true;
        }

        private void Launch()
        {
            OnLaunch();

            // Reset reload for next attack iteration
            ResetReload(isAttackComplete: true);

            // Activate cooldown (if it can be enabled)
            cooldown.IsActive = true;
            if(cooldown.IsActive)
                RaiseCooldownUpdated();

            //reset the reload (done in the parent class, reload = progress), delay and the terrain attack mode right on launch, else the launch keeps getting triggered as long as the attack is not complete
            //attack is marked as complete depending on the settings of the AttackLauncher
            launcher.Trigger(Complete,
                nextLaunchLogEnabled && (!nextLaunchLogTargetEnabled || Target.instance.Equals(nextLaunchLogTarget))
                ? nextLaunchLog : null);

            nextLaunchLogEnabled = false;
            nextLaunchLog = null;

            // Disable next launch log data for next attack launch call.
            nextLaunchLogEnabled = false;
        }

        protected virtual void OnLaunch() { }

        private void Complete()
        {
            // Disable last terrain attack if one was active
            if(!allowMultipleTerrainAttacks)
                terrainAttackActive = false;

            completeEvent.Invoke();

            audioMgr.PlaySFX(factionEntity.AudioSourceComponent, attackCompleteAudio.Fetch(), false);
            if (factionEntity.AnimatorController.IsValid())
                factionEntity.AnimatorController.SetState(AnimatorState.idle);

            OnComplete();

            // This is not the attack type to revert, check if there is one and get back to it
            if (!Revert)
            {
                for (int i = 0; i < factionEntity.AttackComponents.Count; i++)
                {
                    if (factionEntity.AttackComponents[i].Revert)
                    {
                        factionEntity.AttackComponents[i].SwitchAttackAction(false);
                        break;
                    }
                }
            }

            ResetAttack();

            // Attack once or cooldown is active? cancel attack to prevent source from attacking again
            if (engageOptions.engageOnce == true
                || cooldown.IsActive)
                Stop();
        }

        protected virtual void OnComplete() { }
        #endregion

        #region Handling Actions
        public sealed override ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input)
        {
            switch ((ActionType)actionID)
            {
                case ActionType.switchAttack:
                    return SwitchAttackActionLocal(input.playerCommand);

                case ActionType.lockAttack:
                    return LockAttackActionLocal(Mathf.RoundToInt(input.target.position.x) == 0, input.playerCommand);

                case ActionType.setSearchRangeCenter:
                    return SetSearchRangeCenterActionLocal((input.target.instance as IBuilding)?.BorderComponent, input.playerCommand);

                // Currently, setting next launch log is enabled on a local level only.
                case ActionType.setNextLaunchLog:
                    return ErrorMessage.none;

                case ActionType.cancelAttack:
                    return CancelAttackActionLocal(input.playerCommand);

                default:
                    return base.LaunchActionLocal(actionID, input);
            }
        }
        #endregion

        #region Set Search Range Center
        // The playerCommand field here allows to 
        public ErrorMessage SetSearchRangeCenterAction(IBorder newSearchRangeCenter, bool playerCommand)
        {
            return LaunchAction((byte)ActionType.setSearchRangeCenter,
                new SetTargetInputData
                {
                    target = newSearchRangeCenter.IsValid() ? newSearchRangeCenter.Building.ToTargetData() : default,
                    playerCommand = playerCommand
                });
        }

        private ErrorMessage SetSearchRangeCenterActionLocal(IBorder newSearchRangeCenter, bool playerCommand)
        {
            if (newSearchRangeCenter.IsValid())
            {
                if (!borderTargetFinder.IsValid())
                    borderTargetFinder = new TargetEntityFinder<IFactionEntity>(
                        gameMgr,
                        source: this,
                        center: factionEntity.transform,
                        data: targetFinder.Data);

                borderTargetFinder.Range = newSearchRangeCenter.Size;
                borderTargetFinder.Center = newSearchRangeCenter.Building.transform;
                // The playerCommand allows to determine whether the auto-target assignment will be flagged as playerCommand or not.
                // Having it turned on allows to ignore LOS constraints for example
                borderTargetFinder.PlayerCommand = playerCommand;

                borderTargetFinder.IsActive = true;
            }
            else if (borderTargetFinder.IsValid())
            {
                borderTargetFinder.IsActive = false;
            }

            return ErrorMessage.none;
        }
        #endregion

        #region Set Next LaunchLog
        public ErrorMessage SetNextLaunchLogActionLocal(IReadOnlyCollection<AttackObjectLaunchLogInput> nextLaunchLog, IFactionEntity target, bool playerCommand)
        {
            nextLaunchLogEnabled = true;
            this.nextLaunchLog = nextLaunchLog;

            nextLaunchLogTarget = target;
            nextLaunchLogTargetEnabled = nextLaunchLogTarget.IsValid();

            return ErrorMessage.none;
        }
        #endregion

        #region Switching Attack (Action)
        public ErrorMessage CanSwitchAttack()
        {
            if (IsActive)
                return ErrorMessage.attackTypeActive;
            else if (IsLocked)
                return ErrorMessage.attackTypeLocked;
            else if (cooldown.IsActive)
                return ErrorMessage.attackTypeInCooldown;

            return ErrorMessage.none;
        }

        public ErrorMessage SwitchAttackAction(bool playerCommand)
        {
            ErrorMessage errorMessage;
            if ((errorMessage = CanSwitchAttack()) != ErrorMessage.none)
                return errorMessage;

            return LaunchAction((byte)ActionType.switchAttack, new SetTargetInputData { playerCommand = playerCommand });
        }

        private ErrorMessage SwitchAttackActionLocal(bool playerCommand)
        {
            // Assuming that the method is called from its non local counterpart first, it already satisfies the conditions for SetActive
            // Therefore, we call SetActiveLocal directly here

            globalEvent.RaiseAttackSwitchStartGlobal(this);

            // Deactivate currently active attack component
            IAttackComponent lastActiveAttackComp = factionEntity.FirstActiveAttackComponent;
            IFactionEntity lastActiveAttackCompTarget = null;
            if (lastActiveAttackComp.IsValid())
            {
                if (lastActiveAttackComp.HasTarget)
                    lastActiveAttackCompTarget = lastActiveAttackComp.Target.instance;
                lastActiveAttackComp.SetActiveLocal(false, playerCommand);
            }

            SetActiveLocal(true, playerCommand);

            globalEvent.RaiseAttackSwitchCompleteGlobal(this);
            if(lastActiveAttackCompTarget.IsValid())
                SetTarget(lastActiveAttackCompTarget.ToTargetData(), playerCommand: false);

            return ErrorMessage.none;
        }
        #endregion

        #region Locking Attack (Action)
        public ErrorMessage LockAttackAction (bool locked, bool playerCommand)
        {
            return LaunchAction(
                (byte)ActionType.lockAttack,
                new SetTargetInputData
                {
                    target = new Vector3(locked ? 0 : 1, 0, 0),
                    playerCommand = playerCommand
                });
        }

        private ErrorMessage LockAttackActionLocal (bool locked, bool playerCommand)
        {
            isLocked = locked;

            //if the attack is now locked while it is active then deactivate it.
            if(IsLocked && IsActive)
                SetActiveLocal(false, false);

            return ErrorMessage.none;
        }
        #endregion

        #region Task UI
        protected override bool OnTaskUICacheUpdate(List<EntityComponentTaskUIAttributes> taskUIAttributesCache, List<string> disabledTaskCodesCache)
        {
            if (RTSHelper.OnSingleTaskUIRequest(
                this,
                taskUIAttributesCache,
                disabledTaskCodesCache,
                SetTargetTaskUI,
                requireActiveComponent: false,
                showCondition: IsActive,
                lockedCondition: cooldown.IsActive,
                lockedData: setTargetCooldownUIData) == false)
                return false;

            if (switchTaskUI.IsValid())
            {
                if (!IsLocked && !IsActive)
                    taskUIAttributesCache.Add(
                        new EntityComponentTaskUIAttributes
                        {
                            data = switchTaskUI.Data,

                            locked = cooldown.IsActive,
                            lockedData = switchAttackCooldownUIData
                        });
                else
                    disabledTaskCodesCache.Add(switchTaskUI.Key);
            }

            if(cancelTaskUI.IsValid())
            {
                if (HasTarget)
                    taskUIAttributesCache.Add(
                        new EntityComponentTaskUIAttributes
                        {
                            data = cancelTaskUI.Data,

                            locked = false,
                        });
                else
                    disabledTaskCodesCache.Add(cancelTaskUI.Key);
            }
            return true;
        }

        public override bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes)
        {
            if (base.OnTaskUIClick(taskAttributes))
                return true;

            if (switchTaskUI.IsValid() && switchTaskUI.Key == taskAttributes.data.code)
            {
                SwitchAttackAction(true);
                return true;
            }
            else if(cancelTaskUI.IsValid() && cancelTaskUI.Key == taskAttributes.data.code)
            {
                CancelAttackAction(playerCommand: true);
                return true;
            }

            return false;
        }
        #endregion

        #region Cancel Attack
        public ErrorMessage CanCancelAttack()
        {
            if (!HasTarget)
                return ErrorMessage.targetUnassigned;

            return ErrorMessage.none;
        }

        public ErrorMessage CancelAttackAction(bool playerCommand)
        {
            ErrorMessage errorMessage;
            if ((errorMessage = CanCancelAttack()) != ErrorMessage.none)
                return errorMessage;

            return LaunchAction((byte)ActionType.cancelAttack, new SetTargetInputData { playerCommand = playerCommand });
        }

        private ErrorMessage CancelAttackActionLocal(bool playerCommand)
        {
            Stop();
            if(factionEntity.CanMove())
            {
                factionEntity.MovementComponent.Stop();
            }
            return ErrorMessage.none;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private bool showAttackIterationGizmos = true;

        protected override void OnDrawGizmosSelected()
        {
            DrawTargetGizmo();

            if (HasTarget && showAttackIterationGizmos)
            {

                bool inAttackRange = IsTargetInRange(factionEntity.transform.position, Target);

                if (!inAttackRange)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(Target.position, factionEntity.Radius);
                }

                Gizmos.color = inAttackRange ? Color.green : Color.red;
                float minAttackDistance = AttackDistanceHandler.GetStoppingDistance(Target.instance, min: true);
                float maxAttackDistance = AttackDistanceHandler.GetStoppingDistance(Target.instance, min: false);

                Vector3 attackPosition = factionEntity.transform.position;
                Gizmos.DrawWireSphere(attackPosition, minAttackDistance);
                Gizmos.DrawWireSphere(attackPosition, maxAttackDistance);
            }
        }
#endif
        #endregion
    }
}
