using System.Collections.Generic;

using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.Game;
using RTSEngine.ResourceExtension;
using RTSEngine.UI;
using RTSEngine.Faction;

namespace RTSEngine.EntityComponent
{ 
    public interface IEntityComponentTaskInput
    {
        IEntity Entity { get; }
        IEntityComponent SourceComponent { get; }
        int ID { get; }

        GameObject Object { get; }

        bool IsInitialized { get; }
        bool IsEnabled { get; }

        EntityComponentTaskUIData Data { get; }
        EntityComponentLockedTaskUIData MissingRequirementData { get; }
        IReadOnlyList<ResourceInput> RequiredResources { get; }
        IReadOnlyList<FactionEntityRequirement> FactionEntityRequirements { get; }

        int LaunchTimes { get; }
        int PendingAmount { get; }
        string Title { get; }

        void Init(IEntityComponent entityComponent, int taskID, IGameManager gameMgr);
        void Disable();

        ErrorMessage CanStart();
        void OnStart();

        void OnCancel();

        ErrorMessage CanComplete();
        void OnComplete();
        bool IsFactionTypeAllowed(FactionTypeInfo factionType);
    }
}
