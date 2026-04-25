using System;

using UnityEngine;
using UnityEngine.UI;

using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Event;
using RTSEngine.Minimap.Cameras;
using RTSEngine.Utilities;
using RTSEngine.Model;

namespace RTSEngine.Minimap.Icons
{
    public class MinimapUIIcon : PoolableObject, IMinimapIcon
    {
        #region Attributes
        [SerializeField, Tooltip("The UI Image component to be used as the minimap icon.")]
        private Image image = null;
        private RectTransform rectTransform;

        private IEntity followEntity;
        public bool isFollowing;
        private float height;

        protected IMinimapCameraController minimapCameraController { private set; get; }
        protected IMinimapIconManager minimapIconMgr { private set; get; }
        #endregion

        #region Initializing/Terminating 
        protected sealed override void OnPoolableObjectInit()
        {
            this.minimapCameraController = gameMgr.GetService<IMinimapCameraController>();
            this.minimapIconMgr = gameMgr.GetService<IMinimapIconManager>(); 

            rectTransform = GetComponent<RectTransform>();

            followEntity = null;
            isFollowing = false;
        }

        protected sealed override void OnPoolableObjectDestroy()
        {
            ResetFollowEntity();
        }
        #endregion

        #region Handling Events: Movement Start/Stop
        private void HandleFollowEntityMovementStart(IMovementComponent sender, MovementEventArgs args)
        {
            isFollowing = true;
        }

        private void HandleFollowEntityMovementStop(IMovementComponent sender, EventArgs args)
        {
            isFollowing = false;
        }

        private void HandleFollowEntityPositionSet(IMovementComponent sender, EventArgs args)
        {
            UpdateFollowPosition();
        }
        #endregion

        #region Spawning/Despawning
        public void OnSpawn(MinimapIconSpawnInput input)
        {
            this.height = input.Height;

            if(!logger.RequireTrue(minimapCameraController.WorldPointToLocalPointInMinimapCanvas(
                   input.SourceEntity.transform.position, out Vector3 spawnPosition, height: input.Height),
                   $"[{GetType().Name}] Unable to find the target position of '{input.SourceEntity.transform.position}' to draw a minimap icon!"))
                return;

            base.OnSpawn(new PoolableObjectSpawnInput(
                parent: minimapCameraController.MinimapCanvas.transform,

                useLocalTransform: true,
                spawnPosition: spawnPosition,
                spawnRotation: Quaternion.identity 
            ));

            image.sprite = input.Data.icon.IsValid() ? input.Data.icon : minimapIconMgr.DefaultIcon;

            Color color = input.SourceEntity.SelectionColor;
            if (input.Data.changeDarkness)
            {
                Color.RGBToHSV(color, out float hue, out float saturation, out float v);
                color = Color.HSVToRGB(hue, saturation, v);
            }
            if(input.Data.changeTransparency)
                color.a = 1.0f - input.Data.transparency;
            image.color = color;

            ResetFollowEntity();
            SetFollowEntity(input.SourceEntity);
        }
        #endregion

        #region Handling Events: ICarriableUnit Added/Removed
        private void HandleUnitCarrierAdded(IUnitCarrier carrier, UnitCarrierEventArgs args)
        {
            FollowUntiCarrier(carrier);
        }

        private void HandleUnitCarrierRemoved(IUnitCarrier carrier, UnitCarrierEventArgs args)
        {
            ResetFollowUntiCarrier(carrier);
        }

        private void FollowUntiCarrier(IUnitCarrier carrier)
        {
            if (carrier.IsValid() && carrier.Entity.MovementComponent.IsValid())
            {
                carrier.Entity.MovementComponent.MovementStart += HandleFollowEntityMovementStart;
                carrier.Entity.MovementComponent.MovementStop += HandleFollowEntityMovementStop;
                carrier.Entity.MovementComponent.PositionSet += HandleFollowEntityPositionSet;
            }
        }

        private void ResetFollowUntiCarrier(IUnitCarrier carrier)
        {
            if (carrier.IsValid() && carrier.Entity.MovementComponent.IsValid())
            {
                carrier.Entity.MovementComponent.MovementStart -= HandleFollowEntityMovementStart;
                carrier.Entity.MovementComponent.MovementStop -= HandleFollowEntityMovementStop;
                carrier.Entity.MovementComponent.PositionSet -= HandleFollowEntityPositionSet;
            }
        }
        #endregion

        #region Following Moving Entity
        private void Update()
        {
            if (!isFollowing)
                return;

            UpdateFollowPosition();
        }

        private void UpdateFollowPosition()
        {
            minimapCameraController.WorldPointToLocalPointInMinimapCanvas(
                followEntity.transform.position,
                out Vector3 nextPosition, height: height);

            rectTransform.localPosition = nextPosition;
        }

        private void SetFollowEntity(IEntity sourceEntity)
        {
            followEntity = sourceEntity;

            if (!followEntity.IsValid())
                return;

            if (followEntity.MovementComponent.IsValid())
            {
                followEntity.MovementComponent.MovementStart += HandleFollowEntityMovementStart;
                followEntity.MovementComponent.MovementStop += HandleFollowEntityMovementStop;
                followEntity.MovementComponent.PositionSet += HandleFollowEntityPositionSet;
            }

            if(followEntity.IsUnit())
            {
                var unit = followEntity as IUnit;

                if (unit.CarriableUnit.IsValid())
                {
                    unit.CarriableUnit.UnitAdded += HandleUnitCarrierAdded;
                    unit.CarriableUnit.UnitRemoved += HandleUnitCarrierRemoved;
                }
            }
        }

        private void ResetFollowEntity()
        {
            isFollowing = false;

            if (!followEntity.IsValid())
                return;

            if (followEntity.MovementComponent.IsValid())
            {
                followEntity.MovementComponent.MovementStart -= HandleFollowEntityMovementStart;
                followEntity.MovementComponent.MovementStop -= HandleFollowEntityMovementStop;
                followEntity.MovementComponent.PositionSet -= HandleFollowEntityPositionSet;
            }

            if(followEntity.IsUnit())
            {
                var unit = followEntity as IUnit;

                if (unit.CarriableUnit.IsValid())
                {
                    ResetFollowUntiCarrier(unit.CarriableUnit.CurrCarrier);

                    unit.CarriableUnit.UnitAdded -= HandleUnitCarrierAdded;
                    unit.CarriableUnit.UnitRemoved -= HandleUnitCarrierRemoved;
                }
            }

            followEntity = null;
        }
        #endregion
    }
}
