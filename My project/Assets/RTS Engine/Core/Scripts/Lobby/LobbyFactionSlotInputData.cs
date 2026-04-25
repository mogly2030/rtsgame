using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine.Lobby
{
    [System.Serializable]
    public class LobbyFactionSlotInputData
    {
        public string name = "";

        public int colorID = 0;

        // the previous faction type ID prior to the last change in the NPC type
        public int prevFactionTypeID = 0;
        public bool isPrevFactionTypeRandom;

        public int factionTypeID = 0;
        public bool isFactionTypeRandom;

        public int npcTypeID = 0;
    }
}
