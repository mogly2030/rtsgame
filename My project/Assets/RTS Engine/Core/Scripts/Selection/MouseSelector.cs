
using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Controls;
using RTSEngine.Logging;

namespace RTSEngine.Selection
{
    public class MouseSelector : SelectorBase
    {
        #region Attributes
        [Space(), SerializeField, Tooltip("Define the key used to select multiple entities when held down.")]
        private ControlType multipleSelectionKey = null;
        public override bool MultipleSelectionModeEnabled => controls.IsControlTypeEnabled(multipleSelectionKey);

        // The reason behind waiting a frame between getting the selector conditions to be valid
        // and actually being able to use the selector is to allow using that frame for other component
        // inputs that use the same inputs as the selector to reset their states before the selector can work
        private bool waitAFrame = false;
        #endregion

        #region Initializing/Terminating
        protected override void OnInit()
        {
            if (!logger.RequireValid(multipleSelectionKey,
              $"[{GetType().Name}] Field 'Multiple Selection Key' has not been assigned! Functionality will be disabled.",
              type: LoggingType.warning))
                return;

            controls.InitControlType(multipleSelectionKey);
        }
        #endregion

        #region Handling Mouse Selection
        protected override void OnInactiveUpdate()
        {
            waitAFrame = true;
        }

        protected override void OnActiveUpdate()
        {
            if(waitAFrame)
            {
                waitAFrame = false;
                return;
            }

            // Do not continue if player is actively rotating the camera
            if (mainCameraController.RotationHandler.IsRotating)
                return;

            bool leftButtonDown = Input.GetMouseButtonUp(0);
            bool rightButtonDown = Input.GetMouseButtonUp(1);

            // If the mouse pointer is over a UI element or the minimap, we will not detect entity selection
            // In addition, we make sure that one of the mouse buttons are down
            if (!leftButtonDown && !rightButtonDown)
                return;

            if (raycast.Hit(mainCameraController.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
            {
                // Get the entity that the player clicked in or see if the player clicked on a terrain area.
                IEntity hitEntity = hit.transform.gameObject.GetComponent<EntitySelectionCollider>()?.Entity;
                bool hitTerrain = terrainMgr.IsTerrainArea(hit.transform.gameObject);

                if (rightButtonDown)
                {
                    // If there was an active awaiting task then disable it since it can only be completed with a left mouse button click
                    if (taskMgr.AwaitingTask.IsEnabled)
                    {
                        taskMgr.AwaitingTask.Disable();
                        return;
                    }

                    if (hitEntity.IsValid())
                        hitEntity.Selection.OnDirectAction();
                    else if (hitTerrain)
                        selectionMgr.GetEntitiesList(EntityType.all, false, true)
                            .SetTargetFirstMany(new SetTargetInputData
                            {
                                target = hit.point,
                                playerCommand = true,
                                includeMovement = true
                            });
                }
                else if (leftButtonDown)
                {
                    if (!hitEntity.IsValid())
                    {
                        // Complete awaiting task on terrain click.
                        if (hitTerrain && taskMgr.AwaitingTask.IsEnabled)
                        {
                            var entityTargetComps = taskMgr.AwaitingTask.Current.sourceTracker.EntityTargetComponents;
                            if (entityTargetComps[0] is IMovementComponent
                                || entityTargetComps[0] is IAttackComponent)
                            {
                                taskMgr.AwaitingTask.Current.sourceTracker.Entities
                                    .SetTargetFirstMany(new SetTargetInputData
                                    {
                                        target = hit.point,
                                        playerCommand = true,
                                        includeMovement = entityTargetComps[0] is IMovementComponent
                                    });
                            }
                            else
                            {
                                foreach (IEntityTargetComponent sourceComponent in entityTargetComps)
                                    sourceComponent.OnAwaitingTaskTargetSet(taskMgr.AwaitingTask.Current, hit.point);
                            }
                        }
                        // No awaiting task and terrain click = deselecting currently selected entities
                        else
                            selectionMgr.RemoveAll();

                        taskMgr.AwaitingTask.Disable();
                        return;
                    }

                    // Awaiting task is active with a valid hit entity
                    if (taskMgr.AwaitingTask.IsEnabled)
                    {
                        hitEntity.Selection.OnAwaitingTaskAction(taskMgr.AwaitingTask.Current);
                        taskMgr.AwaitingTask.Disable();
                    }
                    // If no awaiting task is active then proceed with regular selection
                    else
                        selectionMgr.Add(hitEntity, SelectionType.single, isLocalPlayerClickSelection: true);
                }
            }
        }
        #endregion

    }
}