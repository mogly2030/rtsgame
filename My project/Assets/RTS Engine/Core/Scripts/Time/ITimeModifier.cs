using RTSEngine.Event;
using RTSEngine.Game;
using System;
using System.Collections.Generic;

namespace RTSEngine.Determinism
{
    public interface ITimeModifier : IPreRunGameService
    {
        TimeModifierOptions Options { get; }
        int CurrOptionIndex { get; }

        bool CanFreezeTimeOnPause { get; }

        event CustomEventHandler<ITimeModifier, EventArgs> ModifierUpdated;

        void SetOptions(TimeModifierOption[] modifierOptions, int initialOptionID);

        ErrorMessage SetModifier(float newModifier, bool playerCommand);
        ErrorMessage SetModifierLocal(float newModifier, bool playerCommand);

        void AddTimer(GlobalTimeModifiedTimer timeModifiedTimer, Action timerThroughCallback);
        void RemoveTimer(GlobalTimeModifiedTimer timer);
    }
}