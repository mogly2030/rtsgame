using System;

using UnityEngine;
using RTSEngine.Entities;

namespace RTSEngine.Event
{
    public class AttackObjectTargetEventArgs : EventArgs
    {
        public GameObject TargetObject { get; }
        public IFactionEntity Target { get; }
        public Vector3 TargetPosition { get; }

        public AttackObjectTargetEventArgs(GameObject targetObject, IFactionEntity target, Vector3 targetPosition)
        {
            TargetObject = targetObject;
            Target = target;
            TargetPosition = targetPosition;
        }
    }
}
