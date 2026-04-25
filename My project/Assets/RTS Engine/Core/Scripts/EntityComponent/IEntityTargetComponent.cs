using System;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Determinism;
using RTSEngine.Movement;
using RTSEngine.Attack;

namespace RTSEngine.EntityComponent
{
    [Serializable]
    public struct EntityTargetComponentData
    {
        public int targetKey;
        public Vector3 position;
        public Vector3 opPosition;
    }

    public struct SetTargetInputData
    {
        public string componentCode;

        public TargetData<IEntity> target;
        public bool playerCommand;

        public bool includeMovement;
        public bool isMoveAttackRequest;

        public bool fromTasksQueue;
        public bool fromRallypoint;

        public bool ignoreLOS;

        public bool allowTerrainAttack;

        public SetTargetInputDataBooleans BooleansToMask()
        {
            SetTargetInputDataBooleans nextMask = SetTargetInputDataBooleans.none;
            if (includeMovement)
                nextMask |= SetTargetInputDataBooleans.includeMovement;
            if (isMoveAttackRequest)
                nextMask |= SetTargetInputDataBooleans.isMoveAttackRequest;
            if (fromTasksQueue)
                nextMask |= SetTargetInputDataBooleans.fromTasksQueue;
            if (fromRallypoint)
                nextMask |= SetTargetInputDataBooleans.fromRallypoint;
            if (ignoreLOS)
                nextMask |= SetTargetInputDataBooleans.ignoreLOS;
            if (allowTerrainAttack)
                nextMask |= SetTargetInputDataBooleans.allowTerrainAttack;

            return nextMask;
        }

        public SetPathDestinationData<T> ToSetPathDestinationData<T>(T source)
        {
            return new SetPathDestinationData<T>
            {
                source = source,
                destination = target.position,
                offsetRadius = 0.0f,
                target = target.instance,
                mvtSource = new MovementSource
                {
                    playerCommand = playerCommand,
                    isMoveAttackRequest = isMoveAttackRequest,
                    fromTasksQueue = fromTasksQueue
                }
            };
        }

        public LaunchAttackData<T> ToLaunchAttackData<T>(T source, IEntity sourceEntityRef)
        {
            return new LaunchAttackData<T>
            {
                source = source,
                targetEntity = target.instance as IFactionEntity,
                targetPosition = target.instance.IsValid() ? RTSHelper.GetAttackTargetPosition(sourceEntityRef, target) : target.position,
                allowTerrainAttack = allowTerrainAttack,
                playerCommand = playerCommand,
                ignoreLOS = ignoreLOS,
                isMoveAttackRequest = isMoveAttackRequest,
            };
        }
    }

    public enum SetTargetInputDataBooleans 
    {
        none = 0,
        includeMovement = 1 << 0,
        isMoveAttackRequest = 1 << 1,
        fromTasksQueue = 1 << 2,
        fromRallypoint = 1 << 3,
        ignoreLOS = 1 << 4,
        allowTerrainAttack = 1 << 5,
        all = ~0
    };


    public interface IEntityTargetComponent : IEntityComponent
    {
        int Priority { get; }

        SetTargetInputData TargetInputData { get; }

        bool HasTarget { get; }
        bool RequireIdleEntity { get; }
        bool IsIdle { get; }

        AudioClip OrderAudio { get; }
        EntityTargetComponentData TargetData { get; }

        event CustomEventHandler<IEntityTargetComponent, TargetDataEventArgs> TargetUpdated;
        event CustomEventHandler<IEntityTargetComponent, TargetDataEventArgs> TargetStop;

        // When SetIdle on the IEntity is called and there is a source component assigned to the idling request
        // The source will not be set to idle and all entity target components will be queried to see if the provided source would allow or disallow the component to be set to idle.
        bool CanStopOnSetIdleSource(IEntityTargetComponent idleSource);
        void Stop();

        bool CanSearch { get; }

        bool IsTargetInRange(Vector3 sourcePosition, TargetData<IEntity> target);

        ErrorMessage IsTargetValid(SetTargetInputData testInput);
        ErrorMessage IsTargetValidOnSearch(SetTargetInputData testInput);
        bool IsTargetValid(SetTargetInputData testInput, out ErrorMessage errorMessage);

        ErrorMessage SetTarget(TargetData<IEntity> newTarget, bool playerCommand);
        ErrorMessage SetTarget(SetTargetInputData input);

        ErrorMessage SetTargetLocal(TargetData<IEntity> newTarget, bool playerCommand);
        ErrorMessage SetTargetLocal(SetTargetInputData input);
    }
}
