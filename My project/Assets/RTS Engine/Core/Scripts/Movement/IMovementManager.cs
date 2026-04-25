using RTSEngine.Effect;
using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Game;
using RTSEngine.Terrain;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine.Movement
{
    public interface IMovementManager : IPreRunGameService
    {
        float StoppingDistance { get; }

        IEffectObject MovementTargetEffect { get; }

        IMovementSystem MvtSystem { get; }
        float HeightSpecificStoppingDistance { get; }

        ErrorMessage SetPathDestination(SetPathDestinationData<IEntity> data);
        ErrorMessage SetPathDestinationLocal(SetPathDestinationData<IEntity> data);

        ErrorMessage SetPathDestination(SetPathDestinationData<IReadOnlyList<IEntity>> data);
        ErrorMessage SetPathDestinationLocal(SetPathDestinationData<IReadOnlyList<IEntity>> data);

        ErrorMessage GeneratePathDestination(IEntity entity, TargetData<IEntity> target, MovementFormationSelector formationSelector, float offset, MovementSource source, ref List<Vector3> pathDestinations, Func<PathDestinationInputData, Vector3, ErrorMessage> condition = null);
        ErrorMessage GeneratePathDestination(IReadOnlyList<IEntity> entities, TargetData<IEntity> target, MovementFormationSelector formationSelector, float offset, MovementSource source, ref List<Vector3> pathDestinations, Func<PathDestinationInputData, Vector3, ErrorMessage> condition = null);
        ErrorMessage GeneratePathDestination(IEntity refMvtSource, int amount, Vector3 direction, TargetData<IEntity> target, MovementFormationSelector formationSelector, float offset, MovementSource source, ref List<Vector3> pathDestinations, Func<PathDestinationInputData, Vector3, ErrorMessage> condition = null);
        ErrorMessage GeneratePathDestination(Vector3 originPosition, float range, IMovementComponent refMvtComp, out Vector3 targetPosition);

        bool TryGetMovablePosition(Vector3 center, float radius, LayerMask areaMask, out Vector3 movablePosition);
        bool GetRandomMovablePosition(IEntity entity, Vector3 origin, float range, out Vector3 targetPosition, bool playerCommand);

        ErrorMessage IsPositionClear(ref Vector3 targetPosition, float agentRadius, LayerMask navAreaMask, TerrainAreaMask areasMask, bool playerCommand);
        ErrorMessage IsPositionClear(ref Vector3 targetPosition, IMovementComponent refMvtComp, bool playerCommand);
        bool IsPositionReached(Vector3 inputPosition, Vector3 targetPosition);
    }
}