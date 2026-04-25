using UnityEngine;

using RTSEngine.Utilities;
using RTSEngine.Event;
using System;

namespace RTSEngine.Effect
{

    public interface IEffectObject : IPoolableObject 
    {
        EffectObjectState State { get; }

        AudioSource AudioSourceComponent { get; }
        FollowTransform FollowTransform { get; }
        float CurrLifeTime { get; }

        event CustomEventHandler<IEffectObject, EventArgs> EnableEvent;
        event CustomEventHandler<IEffectObject, EventArgs> DisableEvent;
        event CustomEventHandler<IEffectObject, EventArgs> DeactivateEvent;

        void OnSpawn(EffectObjectSpawnInput input);
        void Deactivate(bool useDisableTime = true);
    }
}
