using System;
using System.Collections.Generic;

using RTSEngine.Entities;
using RTSEngine.Event;
using RTSEngine.UI;

namespace RTSEngine.EntityComponent
{
    public interface IEntityComponent : IMonoBehaviour, IEntityPostInitializable
    {
        string Code { get; }

        bool IsActive { get; }
        
        IEntity Entity { get; }

        EntityComponentData Data { get; }

        event CustomEventHandler<IEntityComponent, EventArgs> ActiveStatusUpdate;

        ErrorMessage SetActive(bool active, bool playerCommand);
        ErrorMessage SetActiveLocal(bool active, bool playerCommand);

        bool OnTaskUIRequest(out IReadOnlyList<EntityComponentTaskUIAttributes> taskUIAttributes, out IReadOnlyList<string> disabledTaskCodes);

        bool OnTaskUIClick(EntityComponentTaskUIAttributes taskAttributes);

        ErrorMessage LaunchAction(byte actionID, SetTargetInputData input);
        ErrorMessage LaunchActionLocal(byte actionID, SetTargetInputData input);

        void HandleComponentUpgrade(IEntityComponent sourceEntityComponent);

        bool OnAwaitingTaskTargetSet(EntityComponentTaskUIAttributes taskAttributes, TargetData<IEntity> target);
    }
}
