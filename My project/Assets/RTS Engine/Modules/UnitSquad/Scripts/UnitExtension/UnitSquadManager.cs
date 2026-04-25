using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Determinism;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Game;
using RTSEngine.Logging;
using RTSEngine.Save.Game.Service;
using RTSEngine.Save.IO;

namespace RTSEngine.UnitExtension
{
    public class UnitSquadManager : MonoBehaviour, IUnitSquadManager, ISavableGameService
    {
        #region Attributes
        // keys: squad IDs, values: Actual squad instances
        private Dictionary<int, UnitSquad> squadTracker;
        private int nextSquadID = -1;

        private Dictionary<IUnit, UnitSquad> unitToSquadDic;

        [System.Serializable]
        private struct DefaultUnitSquadElement
        {
            [EnforceType(typeof(IUnit), sameScene: true), Tooltip("Input the units that are member of this default unit squad.")]
            public GameObject[] units;
        }
        [SerializeField, Tooltip("Each element of this list presents one default unit squad.")]
        private DefaultUnitSquadElement[] defaultUnitSquads = new DefaultUnitSquadElement[0];

        // Save/Load Attributes
        [System.Serializable]
        private struct UnitSquadSaveData
        {
            public int ID;
            public int[] unitKeys;
            public int spawnCount;
            public bool isSelected;
        }

        [System.Serializable]
        private struct UnitSquadManagerSaveData
        {
            public int nextSquadID;
            public UnitSquadSaveData[] squads;
        }

        public string SaveCode => "rtse_unit_squad_manager";

        protected IGameManager gameMgr { private set; get; }
        protected IGlobalEventPublisher globalEvent { private set; get; }
        protected IGameLoggingService logger { private set; get; }
        protected IInputManager inputMgr { private set; get; } 

        protected ISaveFormatter saveFormatter { private set; get; }
        #endregion

        #region Initializing/Terminating
        public void Init(IGameManager gameMgr)
        {
            this.gameMgr = gameMgr;
            this.globalEvent = this.gameMgr.GetService<IGlobalEventPublisher>();
            this.logger = gameMgr.GetService<IGameLoggingService>();
            this.inputMgr = gameMgr.GetService<IInputManager>(); 
            this.saveFormatter = gameMgr.GetService<ISaveFormatter>();

            squadTracker = new Dictionary<int, UnitSquad>();
            nextSquadID = 1;

            unitToSquadDic = new Dictionary<IUnit, UnitSquad>();

            if (gameMgr.ClearDefaultEntities)
                return;

            for (int i = 0; i < defaultUnitSquads.Length; i++)
            {
                JoinToNewSquad(defaultUnitSquads[i].units
                    .Select(unitObj => unitObj.IsValid() ? unitObj.GetComponent<IUnit>() : null)
                    .ToArray());
            }
        }
        #endregion

        #region Creating Unit Squads
        private bool JoinToNewSquadWithID(IReadOnlyList<IUnit> units, int nextID, int spawnCount)
        {
            if(!units.IsValid() || units.Count == 0)
            {
                logger.LogError($"[{GetType().Name}] Inputs units list to form a squad is not valid or is empty!");
                return false;
            }

            string unitCode = units[0].Code;
            int factionID = units[0].FactionID;
            foreach (IUnit unit in units)
            {
                if (unitCode != unit.Code)
                {
                    logger.LogError($"[{GetType().Name}] All units to join to new squad must have the same type/code!");
                    return false;
                }
                else if (factionID != unit.FactionID)
                {
                    logger.LogError($"[{GetType().Name}] All units to join to new squad must belong to the same faction!");
                    return false;
                }
                else if (unit.Squad.IsValid())
                {
                    logger.LogError($"[{GetType().Name}] One or more units already belong to an existing squad!");
                    return false;
                }

                if(IsInSquad(unit))
                {
                    logger.LogError($"[{GetType().Name}] One or more input units is already in a squad! Can not add to new squad!");
                    return false;
                }
            }

            if(squadTracker.ContainsKey(nextID))
            {
                logger.LogError($"[{GetType().Name}] Squad ID: {nextID} has been already used!");
                return false;
            }

            UnitSquad nextSquad = new UnitSquad(gameMgr, nextID, unitCode, factionID, spawnCount);
            squadTracker.Add(
                nextID,
                nextSquad);

            foreach (IUnit unit in units)
            {
                if (!nextSquad.Add(unit))
                {
                    logger.LogError($"[{GetType().Name}] Unable to add unit {unit} to new squad! Stopping and removing squad!");
                    nextSquad.Destroy();
                    return false;
                }
                unitToSquadDic.Add(unit, nextSquad);
                unit.Health.EntityDead += HandleSquadUnitDead;
            }

            nextSquad.SquadDead += HandleSquadDead;

            return true;
        }

        public bool JoinToNewSquad(IReadOnlyList<IUnit> units)
        {
            int spawnCount = units.Count;
            units = units.Distinct().ToArray();

            if(units.Count != spawnCount)
            {
                logger.LogError($"[{GetType().Name}] Unique unit instances in the input list does not match with the spawn count!");
                return false;
            }

            if(JoinToNewSquadWithID(units, nextSquadID, spawnCount))
            {
                nextSquadID++;
                return true;
            }

            return false;
        }
        #endregion

        #region Handling Event: Squad Dead, Squad Member (Unit) Dead
        private void HandleSquadDead(IUnitSquad squad, EventArgs args)
        {
            squadTracker[squad.ID].Destroy();
            squadTracker.Remove(squad.ID);
            squad.SquadDead -= HandleSquadDead;
        }

        private void HandleSquadUnitDead(IEntity unit, DeadEventArgs args)
        {
            unitToSquadDic.Remove(unit as IUnit);
            unit.Health.EntityDead -= HandleSquadUnitDead;
        }
        #endregion

        #region Help Methods
        public bool IsInSquad(IUnit unit)
        {
            return unitToSquadDic.ContainsKey(unit);
        }

        public bool GetSquad(IUnit unit, out IUnitSquad squad)
        {
            squad = null;
            if (IsInSquad(unit))
            {
                squad = unitToSquadDic[unit];
                return true;
            }
            return false;
        }
        #endregion

        #region Handling Save/Load
        public string Save()
        {
            return saveFormatter.ToSaveFormat<UnitSquadManagerSaveData>(
                new UnitSquadManagerSaveData
                {
                    nextSquadID = nextSquadID,
                    squads = squadTracker.Values
                        .Select(squad => new UnitSquadSaveData
                        {
                            ID = squad.ID,
                            unitKeys = squad.Units.Select(unit => unit.Key).ToArray(),
                            spawnCount = squad.SpawnCount,
                            isSelected = squad.IsSelected,
                        }
                        ).ToArray()
                });
        }

        public void Load(string data)
        {
            UnitSquadManagerSaveData saveData = saveFormatter.FromSaveFormat<UnitSquadManagerSaveData>(data);

            foreach(var nextSquad in saveData.squads)
            {
                IUnit[] units = nextSquad.unitKeys
                    .Select(key =>
                    {
                        if (!inputMgr.TryGetEntityInstanceWithKey(key, out IEntity unit))
                            logger.LogError($"[{GetType().Name}] Unable to find unit with key: {key}!");

                        return unit as IUnit;
                    })
                    .ToArray();

                if (JoinToNewSquadWithID(units, nextSquad.ID, nextSquad.spawnCount))
                {

                    if (nextSquad.isSelected)
                        squadTracker[nextSquad.ID].Select();
                }
            }

            nextSquadID = saveData.nextSquadID;
        }
        #endregion
    }
}
