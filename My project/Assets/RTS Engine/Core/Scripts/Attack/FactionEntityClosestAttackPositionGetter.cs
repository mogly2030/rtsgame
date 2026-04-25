using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.Logging;

namespace RTSEngine.Attack
{
    public class FactionEntityClosestAttackPositionGetter : MonoBehaviour, IAttackTargetPositionGetter
    {
        private IFactionEntity factionEntity;

        [SerializeField, Tooltip("All possible attack positions!")]
        private Transform[] attackPositions = null;

        protected IGameLoggingService logger { private set; get; } 

        public void OnEntityPostInit(IGameManager gameMgr, IEntity entity)
        {
            this.logger = gameMgr.GetService<IGameLoggingService>(); 

            if(!entity.IsFactionEntity())
            {
                logger.LogError($"[{GetType().Name}] This component must be attached to parent or a child object of of an entity of type '{typeof(IFactionEntity).Name}'");
                return;
            }
            else if(!attackPositions.IsValid())
            {
                logger.LogError($"[{GetType().Name}] The 'Attack Positions' must be assigned!");
                return;
            }

            factionEntity = entity as IFactionEntity;
        }

        public void Disable()
        {
        }

        public Vector3 GetAttackTargetPosition(IEntity source)
        {
            if (!source.IsValid())
                return factionEntity.Selection.transform.position;

            Vector3 comparePosition = source.transform.position;

            float distance = Mathf.Infinity;
            Vector3 targetPosition = comparePosition; 

            foreach(var positionTransform in attackPositions)
            {
                float nextDistance = Vector3.Distance(positionTransform.position, comparePosition);
                if(nextDistance < distance)
                {
                    targetPosition = positionTransform.position;
                    distance = nextDistance;
                }    
            }

            return targetPosition;
        }
    }
}
