
using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.EntityComponent;

namespace RTSEngine.Attack
{
    public enum LaunchAttackTypeMode { firstActive = 0, specific }

    public struct LaunchAttackData<T>
    {
        public T source;

        public LaunchAttackTypeMode mode;
        public string attackTypeCode;

        public IFactionEntity targetEntity;

        public Vector3 targetPosition;

        public bool playerCommand;

        public bool isMoveAttackRequest;

        public bool allowTerrainAttack;

        public bool ignoreLOS;

        public LaunchAttackBooleans BooleansToMask()
        {
            LaunchAttackBooleans nextMask = LaunchAttackBooleans.none; 
            if (isMoveAttackRequest)
                nextMask |= LaunchAttackBooleans.isMoveAttackRequest;
            if (allowTerrainAttack)
                nextMask |= LaunchAttackBooleans.allowTerrainAttack;
            if (ignoreLOS)
                nextMask |= LaunchAttackBooleans.ignoreLOS;

            return nextMask;
        }

        public SetTargetInputData ToSetTargetInputData(TargetData<IFactionEntity> target)
        {
            return new SetTargetInputData
            {
                target = target,
                playerCommand = playerCommand,

                includeMovement = false,
                isMoveAttackRequest = isMoveAttackRequest,

                fromTasksQueue = false,
                fromRallypoint = false,

                ignoreLOS = ignoreLOS,
                allowTerrainAttack = allowTerrainAttack
            };
        }

        public SetTargetInputData ToSetTargetInputData()
        {
            return new SetTargetInputData
            {
                target = targetEntity.ToTargetData(),
                playerCommand = playerCommand,

                includeMovement = false,
                isMoveAttackRequest = isMoveAttackRequest,

                fromTasksQueue = false,
                fromRallypoint = false,

                ignoreLOS = ignoreLOS,
                allowTerrainAttack = allowTerrainAttack
            };
        }
    }

    public enum LaunchAttackBooleans 
    {
        none = 0,
        isMoveAttackRequest = 1 << 0,
        allowTerrainAttack = 1 << 2,
        ignoreLOS = 1 << 3,
        all = ~0
    };
}