using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Search;
using RTSEngine.Determinism;
using System;

namespace RTSEngine.EntityComponent
{
    /// <summary>
    /// Allows to define a search process (for an Entity instance).
    /// </summary>
    [System.Serializable]
    public class TargetEntityFinder<T> where T : IEntity
    {
        [SerializeField, Tooltip("Enable finding targets and set search period time.")]
        private GlobalTimeModifiedTimer reload = new GlobalTimeModifiedTimer(enabled: true);

        public IEntityTargetComponent Source { get; private set; }

        [SerializeField, HideInInspector]
        private bool isActive = false;
        /// <summary>
        /// Set or get whether searching for a target entity is active or not.
        /// </summary>
        public bool IsActive {
            set
            {
                isActive = Source.IsActive && value;
                reload.IsActive = isActive;
            }
            get => isActive;
        }

        // Where the search will be conducted from.
        [SerializeField, HideInInspector]
        private Transform center;
        public Transform Center
        {
            set
            {
                if (value.IsValid())
                    center = value;
            }
            get => center.IsValid() ? center : Source.Entity.transform;
        }

        public bool PlayerCommand { set; get; }

        [SerializeField, HideInInspector]
        private float range;
        /// <summary>
        /// 
        /// </summary>
        public float Range
        {
            set
            {
                if (value > 0.0f)
                    range = value;
            }
            get => range;
        }

        public bool IdleOnly { set; get; }

        [SerializeField, HideInInspector]
        public float reloadTime;
        public float ReloadTime
        {
            set
            {
                if (value > 0.0f)
                    reloadTime = value;
            }
            get => reloadTime;
        }
        public float CurrReloadValue => reload.CurrValue;

        private EntityTargetPicker typePicker;

        public TargetEntityFinderData Data => new TargetEntityFinderData
        {
            enabled = IsActive,
            idleOnly = IdleOnly,
            range = range,
            reloadTime = reloadTime,
            center = Center,
            typePicker = typePicker
        };

        protected IGridSearchHandler gridSearch { private set; get; }

        /// <summary>
        /// Initializes a TargetEntityFinder instance.
        /// </summary>
        /// <param name="source">Entity instance that the TargetEntityFinder is finding a target for.</param>
        public TargetEntityFinder (IGameManager gameMgr, IEntityTargetComponent source, Transform center, TargetEntityFinderData data)
        {
            this.gridSearch = gameMgr.GetService<IGridSearchHandler>(); 

            this.Source = source;
            this.Center = center;
            this.PlayerCommand = false;
            this.typePicker = data.typePicker;

            // We use the Enter/Exit Idle Entity events to start / stop the search timers
            this.Source.Entity.EntityEnterIdle += OnEntityIdleUpdated;
            this.Source.Entity.EntityExitIdle += OnEntityIdleUpdated;

            // We start a small random range for the reload and then set the proper reload after the first search
            reload.Init(gameMgr, SearchTarget, UnityEngine.Random.Range(0.1f, 0.25f));
            reloadTime = data.reloadTime;
            Range = data.range;
            IdleOnly = data.idleOnly;

            IsActive = (!data.idleOnly || source.Entity.IsIdle) && data.enabled && source.IsActive;
        }

        public void Disable()
        {
            IsActive = false;

            this.Source.Entity.EntityEnterIdle -= OnEntityIdleUpdated;
            this.Source.Entity.EntityExitIdle -= OnEntityIdleUpdated;
        }

        private void OnEntityIdleUpdated(IEntity entity, EventArgs args)
        {
            if (!IdleOnly)
                return;

            IsActive = entity.IsIdle;
        }

        private void SearchTarget()
            => SearchTarget(Data);

        public ErrorMessage IsTargetValid(SetTargetInputData data)
        {
            var errorMsg = Source.IsTargetValidOnSearch(data);
            if (errorMsg != ErrorMessage.none)
                return errorMsg;

            else if (!typePicker.IsValidTarget(data.target.instance))
                return ErrorMessage.targetPickerUndefined;

            return ErrorMessage.none;
        }

        public void SearchTarget(TargetEntityFinderData nextSearchData)
        {
            if (RTSHelper.IsMasterInstance()
                && Source.Entity.CanLaunchTask
                && !Source.HasTarget
                && (!nextSearchData.idleOnly || Source.Entity.IsIdle)
                && Source.CanSearch
                && gridSearch.Search(Center.position, nextSearchData.range, IsTargetValid, playerCommand: false, out T potentialTarget) == ErrorMessage.none)
            {
                Source.SetTarget(new TargetData<IEntity> { instance = potentialTarget, position = potentialTarget.transform.position }, playerCommand: PlayerCommand);
            }

            // Reload timer
            if (IsActive)
            {
                reload.SetDefaultValue(Data.reloadTime);
                reload.IsActive = true;
            }
        }
    }
}
