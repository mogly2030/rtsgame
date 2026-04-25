using RTSEngine.Attack;
using RTSEngine.BuildingExtension;
using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.Model;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine.EntityComponent
{
    public interface IAttackComponent : IEntityTargetComponent
    {
        bool Revert { get; }

        bool RequireTarget { get; }

        IAttackDistanceHandler AttackDistanceHandler { get; }
        AttackWeapon Weapon { get; }
        AttackDamage Damage { get; }
        AttackLauncher Launcher { get; }
        AttackLOS LineOfSight { get; }

        bool IsInTargetRange { get; }

        AttackEngagementOptions EngageOptions { get; }

        TargetData<IFactionEntity> Target { get; }

        Transform WeaponTransform { get; }

        bool IsLocked { get; }

        bool IsAttackMoveEnabled { get; }

        bool IsCooldownActive { get; }
        bool IsAttackMoveActive { get; }
        float CurrReloadValue { get; }
        TargetEntityFinderData BorderTargetFinderData { get; }

        event CustomEventHandler<IAttackComponent, EventArgs> CooldownUpdated;
        event CustomEventHandler<IAttackComponent, EventArgs> ReloadUpdated;

        ErrorMessage CanSwitchAttack();
        ErrorMessage LockAttackAction(bool locked, bool playerCommand);
        ErrorMessage SetNextLaunchLogActionLocal(IReadOnlyCollection<AttackObjectLaunchLogInput> nextLaunchLog, IFactionEntity target, bool playerCommand);
        ErrorMessage SetSearchRangeCenterAction(IBorder newSearchRangeCenter, bool playerCommand);
        ErrorMessage SwitchAttackAction(bool playerCommand);

        void TriggerAttack();
    }
}
