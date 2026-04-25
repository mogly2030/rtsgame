using RTSEngine.Event;
using RTSEngine.Model;
using RTSEngine.UnitExtension;
using UnityEngine;

namespace RTSEngine.EntityComponent
{
    public interface ICarriableUnit : IEntityTargetComponent
    {
        IUnitCarrier CurrCarrier { get; }
        Transform CurrSlot { get; }
        int CurrSlotID { get; }

        event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitAdded;
        event CustomEventHandler<IUnitCarrier, UnitCarrierEventArgs> UnitRemoved;

        //bool AllowMovementToExitCarrier { get; }

        AddableUnitData GetAddableData(bool playerCommand);
        AddableUnitData GetAddableData(SetTargetInputData input);
        void OnCarrierUnitAdded(IUnitCarrier carrier, UnitCarrierEventArgs args);
        ErrorMessage SetTarget(IUnitCarrier carrier, AddableUnitData addableData);
    }
}