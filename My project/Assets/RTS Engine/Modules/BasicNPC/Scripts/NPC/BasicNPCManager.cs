using System;
using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Event;
using RTSEngine.Faction;
using RTSEngine.Game;
using RTSEngine.Logging;

namespace RTSEngine.NPC
{
    public class BasicNPCManager : MonoBehaviour, INPCManager
    {
        #region Attributes
        public NPCType Type { private set; get; }

        private Dictionary<Type, INPCComponent> oneInstanceComponents;
        private Dictionary<Type, IReadOnlyList<INPCComponent>> multipleInstanceComponents;

        public IFactionManager FactionMgr { private set; get; }

        protected IGameManager gameMgr { private set; get; }
        protected IGameLoggingService logger { private set; get; } 
        #endregion

        #region Raising Events
        public event CustomEventHandler<INPCManager, EventArgs> InitComplete;
        private void RaiseInitComplete()
        {
            var handler = InitComplete;
            handler?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Initializing/Terminating
        public void Init(NPCType npcType, IGameManager gameMgr, IFactionManager factionMgr)
        {
            this.Type = npcType;
            this.gameMgr = gameMgr;
            this.FactionMgr = factionMgr;

            this.logger = gameMgr.GetService<IGameLoggingService>(); 

            gameMgr.GameStartRunning += HandleGameStartRunning;

            oneInstanceComponents = new Dictionary<Type, INPCComponent>();
            multipleInstanceComponents = new Dictionary<Type, IReadOnlyList<INPCComponent>>();
        }

        private void OnDestroy()
        {
            gameMgr.GameStartRunning -= HandleGameStartRunning;
        }

        private void HandleGameStartRunning(IGameManager sender, EventArgs args)
        {
            var allComponents = GetComponentsInChildren<INPCComponent>();

            for (int i = 0; i < allComponents.Length; i++)
            {
                INPCComponent comp = allComponents[i];
                Type compType = comp.GetType().GetSuperInterfaceType<INPCComponent>();

                if (comp.IsSingleInstance)
                    oneInstanceComponents.Add(compType, comp);
                else if(!multipleInstanceComponents.ContainsKey(compType))
                {
                    List<INPCComponent> sameTypeComps = new List<INPCComponent>();
                    for (int j = 0; j < allComponents.Length; j++)
                    {
                        if (allComponents[j].GetType().GetSuperInterfaceType<INPCComponent>() == compType)
                            sameTypeComps.Add(allComponents[j]);
                    }
                    multipleInstanceComponents.Add(compType, sameTypeComps.AsReadOnly());
                }
            }

            for (int i = 0; i < allComponents.Length; i++)
                allComponents[i].Init(gameMgr, this);

            RaiseInitComplete();
        }
        #endregion

        #region NPC Component Handling
        public T GetNPCComponent<T>() where T : INPCComponent
        {
            if (!logger.RequireTrue(oneInstanceComponents.ContainsKey(typeof(T)),
                $"[NPCManager - {FactionMgr.FactionID}] NPC Faction does not have an active instance of type '{typeof(T)}' that implements the '{typeof(INPCComponent).Name}' interface!"))
                return default;

            return (T)oneInstanceComponents[typeof(T)];
        }

        public IReadOnlyList<T> GetNPCComponentSet<T>() where T : INPCComponent
        {
            if (!logger.RequireTrue(multipleInstanceComponents.ContainsKey(typeof(T)),
                $"[NPCManager - Faction ID: {FactionMgr.FactionID}] NPC Faction does not have an active set of instances of type '{typeof(T)}' that implement the '{typeof(INPCComponent).Name}' interface!"))
                return default;

            IReadOnlyList<INPCComponent> compsList = multipleInstanceComponents[typeof(T)];
            List<T> compsListCasted = new List<T>();
            for (int i = 0; i < compsList.Count; i++)
            {
                compsListCasted.Add((T)compsList[i]);
            }

            return compsListCasted.AsReadOnly();
        }
        #endregion
    }
}
