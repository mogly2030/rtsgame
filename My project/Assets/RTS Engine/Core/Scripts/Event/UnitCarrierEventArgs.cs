using RTSEngine.Entities;
using RTSEngine.Model;
using System;
using UnityEngine;

namespace RTSEngine.Event
{
    public class UnitCarrierEventArgs : EventArgs
    {
        public IUnit Unit { private set; get; }
        public Transform Slot { private set; get; }
        public int SlotID { private set; get; }

        public UnitCarrierEventArgs(IUnit unit, Transform slot, int slotID)
        {
            this.Unit = unit;
            this.Slot = slot;
            this.SlotID = slotID;
        }
    }
}
