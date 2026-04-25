using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RTSEngine.Determinism
{
    [System.Serializable]
    public struct TimeModifierOption 
    {
        public string name;
        public float modifier;
    }

    public struct TimeModifierOptions
    {
        public TimeModifierOption[] values;
        public int initialValueIndex;
    }

    public class TimeModifier : MonoBehaviour, ITimeModifier
    {
        #region Attributes
        [SerializeField, Tooltip("The default modifier value (used when there is no IGameBuilder instance available in the map scene that overwrites the default value)")]
        private float defaultModifier = 1.0f;

        public TimeModifierOptions Options { private set; get; }
        public void SetOptions (TimeModifierOption[] modifierOptions, int initialOptionIndex)
        {
            if (!initialOptionIndex.IsValidIndex(modifierOptions))
            {
                logger.LogError($"[TimeModifier] Provided time modifier initial option index of {initialOptionIndex} is not a valid index of the provided options array of size {modifierOptions.Length}. Current options will not be changed.");
                return;
            }

            Options = new TimeModifierOptions
            {
                values = modifierOptions,
                initialValueIndex = initialOptionIndex
            };

            int index = -1;
            modifierToOptionIndex = Options
                .values
                .ToDictionary(val => val.modifier, val => { index++; return index; });
        }

        private IReadOnlyDictionary<float, int> modifierToOptionIndex;
        public int CurrOptionIndex { private set; get; }

#if UNITY_EDITOR
        [SerializeField, ReadOnly]
        private float currentModifier;
#endif
        public static float CurrentModifier { private set; get; } = 1.0f;
        public static float ApplyModifier(float input) => input * CurrentModifier;

        private List<GlobalTimeModifiedTimer> globalTimers;
        private Dictionary<GlobalTimeModifiedTimer, Action> globalTimersDic;

        // The use of nextTimers and nextTimersDic allows to temporarily store incoming global timers (in the AddTimer method)...
        // ... so that the possibility of a timer being removed during a frame and then instantly being re-added (due to a near-zero timer duration) in the same frame in the globlaTimers list does not occur...
        // At the beginning of each frame, all elements in the nextTimers and nextTimersDic are dumped into globalTimers and globalTimersDic respectivally before getting cleared
        private List<GlobalTimeModifiedTimer> nextTimers;
        private Dictionary<GlobalTimeModifiedTimer, Action> nextTimersDic;

        [SerializeField, ReadOnly]
        private int globalTimersCount = 0;

        // Optimization Attributes
        private int currFrameTimerRemovals;

        public bool CanFreezeTimeOnPause { private set; get; }

        // Game services
        protected IGameManager gameMgr { private set; get; }
        protected IInputManager inputMgr { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        #endregion

        #region Raising Events
        public event CustomEventHandler<ITimeModifier, EventArgs> ModifierUpdated;

        private void RaiseModifierUpdate()
        {
            var handler = ModifierUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;

            this.inputMgr = gameMgr.GetService<IInputManager>();
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.globalEvent = gameMgr.GetService<IGlobalEventPublisher>(); 

            if (!logger.RequireTrue(enabled,
                $"[{GetType().Name}] This component must be enabled in the inspector for the global timers to run as expected",
                source: this))
                return;

            globalEvent.GameStateUpdatedGlobal += HandleGameStateUpdatedGlobal;

            globalTimers = new List<GlobalTimeModifiedTimer>(RTSOptimizations.INIT_TIME_MODIFIER_DATA_CAPACITY);
            globalTimersDic = new Dictionary<GlobalTimeModifiedTimer, Action>(RTSOptimizations.INIT_TIME_MODIFIER_DATA_CAPACITY);
            nextTimers = new List<GlobalTimeModifiedTimer>(RTSOptimizations.INIT_TIME_MODIFIER_DATA_CAPACITY);
            nextTimersDic = new Dictionary<GlobalTimeModifiedTimer, Action>(RTSOptimizations.INIT_TIME_MODIFIER_DATA_CAPACITY);

            if(gameMgr.CurrBuilder.IsValid())
            {
                CanFreezeTimeOnPause = gameMgr.CurrBuilder.CanFreezeTimeOnPause;
                SetOptions(
                    gameMgr.CurrBuilder.Data.timeModifierOptions.values,
                    gameMgr.CurrBuilder.Data.timeModifierOptions.initialValueIndex);
            }    
            else
            {
                SetOptions(
                    new TimeModifierOption[] { new TimeModifierOption { name = "default", modifier = defaultModifier } }
                    ,initialOptionIndex: 0);
                CanFreezeTimeOnPause = true;
            }

            SetModifierLocal(Options.values[Options.initialValueIndex].modifier, playerCommand: false);

            CurrOptionIndex = Options.initialValueIndex;
        }

        private void OnDestroy()
        {
            if(globalEvent.IsValid())
                globalEvent.GameStateUpdatedGlobal -= HandleGameStateUpdatedGlobal;

            if (!ModifierUpdated.IsValid() || ModifierUpdated.GetInvocationList().IsValid())
                return;

            // Remove all subscribers manually in case individual subscribers haven't.
            foreach (Delegate subscriber in ModifierUpdated.GetInvocationList())
                ModifierUpdated -= subscriber as CustomEventHandler<ITimeModifier, EventArgs>;
        }
        #endregion

        #region Handling Game State Updated Global Event
        private void HandleGameStateUpdatedGlobal(IGameManager sender, EventArgs args)
        {
            switch(gameMgr.State)
            {
                case GameStateType.running:
                    ResetModifier(playerCommand: false);
                    break;
                case GameStateType.frozen:
                    SetModifierInternal(newModifier: 0.0f);
                    break;
                case GameStateType.pause:
                    if(CanFreezeTimeOnPause)
                        SetModifierInternal(newModifier: 0.0f);
                    break;
            }
        }
        #endregion

        #region Updating Time Modifier
        public ErrorMessage SetModifier(float newModifier, bool playerCommand)
        {
            return inputMgr.SendInput(new CommandInput
            {
                sourceMode = (byte)InputMode.master,
                targetMode = (byte)InputMode.setTimeModifier,

                floatValue = newModifier,
                playerCommand = playerCommand
            });
        }

        public ErrorMessage SetModifierLocal(float newModifier, bool playerCommand)
        {
            if (!logger.RequireTrue(newModifier >= 0.0f,
              $"[{GetType().Name}] Time Modifier must be >= 0.0f!"))
                return ErrorMessage.invalid;

            switch(gameMgr.State)
            {
                case GameStateType.running:
                    if (newModifier == 0.0f)
                    {
                        logger.LogError($"[{GetType().Name}] Can not set the time modifier to 0.0f when the game is running. Please use the IGameManager.SetGameState() method to freeze or pause the game instead!", source: this);
                        return ErrorMessage.invalid;
                    }
                    break;

                case GameStateType.frozen:
                    logger.LogError($"[{GetType().Name}] Can not set the time modifier to any value when the game is frozen. Please use the IGameManager.SetGameState() method to unfreeze the game first.", source: this);
                    return ErrorMessage.invalid;
            }

            SetModifierInternal(newModifier);

            return ErrorMessage.none;
        }

        private void SetModifierInternal(float newModifier)
        {
            CurrentModifier = Mathf.Max(0.0f, newModifier);
#if UNITY_EDITOR
            currentModifier = CurrentModifier;
#endif

            if (modifierToOptionIndex.TryGetValue(newModifier, out int newOptionIndex))
                CurrOptionIndex = newOptionIndex;

            RaiseModifierUpdate();
        }

        private void ResetModifier(bool playerCommand)
        {
            if (!logger.RequireTrue(gameMgr.State == GameStateType.running,
              $"[{GetType().Name}] Can only reset the time modifier when the game is running. Please use the IGameManager.SetGameState method first to run the game!"))
                return;

            SetModifier(Options.values[CurrOptionIndex].modifier,
                playerCommand);
        }
        #endregion

        #region Handling Global Timers
        private void Update()
        {
            int i;
            if (nextTimers.Count > 0)
            {
                for(i = 0; i < nextTimers.Count; i++)
                {
                    globalTimers.Add(nextTimers[i]);
                    globalTimersDic.Add(nextTimers[i], nextTimersDic[nextTimers[i]]);
                }

                nextTimers.Clear();
                nextTimersDic.Clear();
            }

            globalTimersCount = globalTimers.Count;

            if (globalTimersCount == 0)
                return;

            i = 0;
            currFrameTimerRemovals = 0;
            while(i < globalTimers.Count)
            {
                if(globalTimers[i].ModifiedDecrease())
                {
                    currFrameTimerRemovals++;
                    RemoveTimer(i);

                    if (RTSOptimizations.LIMIT_TIMER_REMOVALS_PER_FRAME && currFrameTimerRemovals >= RTSOptimizations.MAX_TIMER_REMOVALS_PER_FRAME)
                        break;
                    else
                        continue;
                }

                i++;
            }
        }

        public void AddTimer(GlobalTimeModifiedTimer timer, Action timerThroughCallback)
        {
            if (globalTimersDic.ContainsKey(timer) || nextTimersDic.ContainsKey(timer))
                return;

            nextTimers.Add(timer);
            nextTimersDic.Add(timer, timerThroughCallback);
        }

        private void RemoveTimer(int i)
        {
            globalTimersDic.TryGetValue(globalTimers[i], out Action value);

            globalTimersDic.Remove(globalTimers[i]);
            globalTimers.RemoveAt(i);

            if (value.IsValid())
                value();
        }

        public void RemoveTimer(GlobalTimeModifiedTimer timer)
        {
            if(nextTimersDic.ContainsKey(timer))
            {
                nextTimers.Remove(timer);
                nextTimersDic.Remove(timer);
            }

            if (!globalTimersDic.ContainsKey(timer))
                return;

            globalTimersDic.Remove(timer);
            globalTimers.Remove(timer);
        }
        #endregion
    }
}
