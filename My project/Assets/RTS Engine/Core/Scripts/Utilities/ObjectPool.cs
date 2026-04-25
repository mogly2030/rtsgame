using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Game;
using RTSEngine.Logging;

namespace RTSEngine.Utilities
{
    [System.Serializable]
    public struct PoolableObjectSpawnData
    {
        public GameObject prefab;
        [Min(1)]
        public int amount;
    }

    public class ObjectPool<T, V> : MonoBehaviour, IMonoBehaviour where T : IPoolableObject where V : PoolableObjectSpawnInput
    {
        #region Attributes
        [SerializeField, Tooltip("Define each poolable object prefab alongside the amount of instances to pre-spawn from it.")]
        private PoolableObjectSpawnData[] preSpawnData = new PoolableObjectSpawnData[0];

        public IReadOnlyDictionary<string, T> ObjectPrefabs { private set; get; }

        private Dictionary<string, Queue<T>> inactiveDic = null;

        private Dictionary<string, List<T>> activeDic = null;
        public IReadOnlyDictionary<string, IEnumerable<T>> ActiveDic
            => activeDic
                .ToDictionary(elem => elem.Key, elem => elem.Value.AsEnumerable());

        // Other components
        protected IGameManager gameMgr { private set; get; }
        protected IGameLoggingService logger { private set; get; } 
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;
            this.logger = gameMgr.GetService<IGameLoggingService>(); 

            inactiveDic = new Dictionary<string, Queue<T>>();
            activeDic = new Dictionary<string, List<T>>();

            LoadPrefabs();

            OnObjectPoolInit();
        }

        protected virtual void OnObjectPoolInit() { }
        #endregion

        #region Handling Prefabs
        private void LoadPrefabs()
        {
            for (int i = 0; i < preSpawnData.Length; i++)
            {
                // Maybe print errors here?
                if (!preSpawnData[i].prefab.IsValid())
                    continue;

                T comp = preSpawnData[i].prefab.GetComponent<T>();
                if (!comp.IsValid())
                    continue;

                for (int j = 0; j < preSpawnData[i].amount; j++)
                    Despawn(Get(comp, forceNewInstance: true));
            }
        }
        #endregion

        #region Spawning/Despawning Effect Objects
        private T Get(T prefab, bool forceNewInstance = false)
        {
            if(!prefab.IsValid())
                return default(T);

            if (inactiveDic.TryGetValue(prefab.Code, out Queue<T> currentQueue) == false) //if the queue for this effect object type is not found
            {
                currentQueue = new Queue<T>();
                inactiveDic.Add(prefab.Code, currentQueue); //add it
            }

            if (forceNewInstance || currentQueue.Count == 0)
            {
                T newEffect = GameObject.Instantiate(prefab.gameObject, RTSOptimizations.POOLABLE_OBJECT_INACTIVE_POSITION, Quaternion.identity).GetComponent<T>();
                newEffect.Init(gameMgr);
                currentQueue.Enqueue(newEffect);
            }

            return currentQueue.Dequeue();
        }

        protected T Spawn(T prefab)
        {
            T nextInstance = Get(prefab);

            if(!nextInstance.IsValid())
                return default(T);

            if (!activeDic.TryGetValue(nextInstance.Code, out var currActiveList))
            {
                currActiveList = new List<T>();
                activeDic.Add(nextInstance.Code, currActiveList);
            }
            currActiveList.Add(nextInstance);

            //nextInstance.gameObject.SetActive(true);

            nextInstance.enabled = true;

            nextInstance.transform.position = Vector3.zero;
            return nextInstance;
        }

        public void Despawn(T instance, bool destroyed = false)
        {
            if (activeDic.TryGetValue(instance.Code, out var currActiveList))
                currActiveList.Remove(instance);

            if (destroyed)
                return;

            instance.enabled = false;

            // Make sure it has no parent object anymore and it is inactive.
            instance.transform.SetParent(null, true);
            instance.transform.position = RTSOptimizations.POOLABLE_OBJECT_INACTIVE_POSITION;
            //instance.gameObject.SetActive(false);

            if (!inactiveDic.TryGetValue(instance.Code, out var currInactiveQueue))
            {
                currInactiveQueue = new Queue<T>();
                inactiveDic.Add(instance.Code, currInactiveQueue);
            }
            currInactiveQueue.Enqueue(instance);
        }
        #endregion
    }
}
