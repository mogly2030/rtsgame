using UnityEditor;
using UnityEngine;

using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using RTSEngine.Utilities;
using RTSEngine.ResourceExtension;

namespace RTSEngine.EditorOnly.EntityComponent
{
    [CustomEditor(typeof(CarriableUnit))]
    public class CarriableUnitEditor : FactionEntityTargetComponentEditor<CarriableUnit, IFactionEntity>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Target Search/Picker", "Setting Target" },
            new string [] { "Carriable Unit", "Debug"}
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IUnit - Target: IFactionEntity)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnComponentSpecificInspectorGUI(string tabName)
        {
            switch(tabName)
            {
                case "Carriable Unit":
                    EditorGUILayout.PropertyField(SO.FindProperty("allowDifferentFactions"));
                    //EditorGUILayout.PropertyField(SO.FindProperty("allowMovementToExitCarrier"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("ejectionTaskUI"));
                    break;
            }
        }

        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI(showTargetSearch: false);

            GUI.enabled = false;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Carrier", EditorStyles.boldLabel);

            EditorGUILayout.ObjectField("Carrier", comp.CurrCarrier.IsValid() ? comp.CurrCarrier.gameObject : null, typeof(GameObject), allowSceneObjects: true);
            EditorGUILayout.ObjectField("Carrier Slot", comp.CurrSlot, typeof(Transform), allowSceneObjects: true);
            EditorGUILayout.IntField("Carrier Slot ID", comp.CurrSlotID);

            GUI.enabled = true;
        }
    }

    [CustomEditor(typeof(Healer))]
    public class HealerEditor : FactionEntityTargetComponentEditor<Healer, IFactionEntity>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Target Search/Picker", "Setting Target" },
            new string [] { "Handling Progress", "Debug"}
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IFactionEntity - Target: IFactionEntity)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnTargetSearchPickerInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("targetFinderData"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("targetPicker"));
        }

        protected override void OnHandlingProgressInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("progressDuration"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressMaxDistance"));
            EditorGUILayout.PropertyField(SO.FindProperty("stoppingDistance"));
            EditorGUILayout.PropertyField(SO.FindProperty("healthPerProgress"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("inProgressObject"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressOverrideController"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressEnabledAudio"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("sourceEffect"));
            EditorGUILayout.PropertyField(SO.FindProperty("targetEffect"));
        }

        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI();
        }
    }

    [CustomEditor(typeof(Converter))]
    public class ConverterEditor : FactionEntityTargetComponentEditor<Converter, IFactionEntity>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Target Search/Picker", "Setting Target" },
            new string [] { "Handling Progress", "Debug"}
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IFactionEntity - Target: IFactionEntity)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnTargetSearchPickerInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("targetFinderData"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("targetPicker"));
        }


        protected override void OnHandlingProgressInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("progressDuration"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressMaxDistance"));
            EditorGUILayout.PropertyField(SO.FindProperty("stoppingDistance"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("inProgressObject"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressOverrideController"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressEnabledAudio"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("sourceEffect"));
            EditorGUILayout.PropertyField(SO.FindProperty("targetEffect"));
        }

        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI();
        }
    }

    [CustomEditor(typeof(Rallypoint))]
    public class RallypointEditor : FactionEntityTargetComponentEditor<Rallypoint, IEntity>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Setting Target" },
            new string [] { "Rallypoint", "Debug"}
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IFactionEntity - Target: IEntity/Vector3)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnGeneralInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("code"));
            EditorGUILayout.PropertyField(SO.FindProperty("isActive"));
            EditorGUILayout.PropertyField(SO.FindProperty("priority"));
        }

        protected override void OnSettingTargetInspectorGUI()
        {
            base.OnSettingTargetInspectorGUI();

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("setTargetResourceOfSameTypeOnDead"));
            EditorGUILayout.PropertyField(SO.FindProperty("setTargetResourceOnDeadRange"));
        }

        protected override void OnComponentSpecificInspectorGUI(string tabName)
        {
            switch(tabName)
            {
                case "Rallypoint":
                    EditorGUILayout.PropertyField(SO.FindProperty("gotoTransform"));
                    EditorGUILayout.PropertyField(SO.FindProperty("forcedTerrainAreas"));
                    EditorGUILayout.PropertyField(SO.FindProperty("forbiddenTerrainAreas"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("maxDistanceEnabled"));
                    if (SO.FindProperty("maxDistanceEnabled").boolValue == true)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(SO.FindProperty("maxDistance"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(SO.FindProperty("repositionToValidTerrainArea"));
                    EditorGUILayout.PropertyField(SO.FindProperty("repositionSize"));

                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(SO.FindProperty("attackMoveEnabled"));
                    break;
            }
        }

        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI(showTargetSearch = false);
        }
    }

    [CustomEditor(typeof(DropOffSource))]
    public class DropOffSourceEditor : FactionEntityTargetComponentEditor<DropOffSource, IFactionEntity>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Target Search/Picker", "Setting Target" },
            new string [] { "Dropoff Resources/Capacity", "Debug" }
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IUnit - Target: IFactionEntity with IResourceDropOff)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnComponentSpecificInspectorGUI(string tabName)
        {
            switch(tabName)
            {
                case "Dropoff Resources/Capacity":
                    EditorGUILayout.PropertyField(SO.FindProperty("dropOffResources"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("totalMaxCapacity"));
                    EditorGUILayout.PropertyField(SO.FindProperty("dropOffOnTargetAvailable"));
                    break;
            }
        }

        protected override void OnGeneralInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("code"));
            EditorGUILayout.PropertyField(SO.FindProperty("isActive"));
            EditorGUILayout.PropertyField(SO.FindProperty("priority"));
        }

        protected override void OnTargetSearchPickerInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("targetPicker"));

            EditorGUILayout.PropertyField(SO.FindProperty("maxDropOffDistanceEnabled"));
            if (SO.FindProperty("maxDropOffDistanceEnabled").boolValue == true)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(SO.FindProperty("maxDropOffDistance"));
                EditorGUI.indentLevel--;
            }
        }

        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI(showTargetSearch = false);

            GUI.enabled = false;

            EditorGUILayout.Space();
            EditorGUILayout.EnumFlagsField("Drop Off State", comp.State);
            EditorGUILayout.IntField("Collected Resource Sum", comp.CollectedResourcesSum);
            if (comp.CollectedResources.IsValid())
            {
                comp.editorFoldout = EditorGUILayout.Foldout(comp.editorFoldout, new GUIContent("Collected Resources"));
                if (comp.editorFoldout)
                {
                    foreach (var a in comp.CollectedResources)
                    {
                        EditorGUILayout.ObjectField("Resource Type", a.Key, typeof(ResourceTypeInfo), allowSceneObjects: false);
                        EditorGUILayout.IntField("Resource Amount", a.Value);
                    }
                }
            }

            GUI.enabled = true;
        }

    }

    [CustomEditor(typeof(ResourceCollector))]
    public class ResourceCollectorEditor : FactionEntityTargetProgressComponentEditor<ResourceCollector, IResource>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Target Search/Picker", "Setting Target" },
            new string [] { "Handling Progress", "Resource Collector", "Debug"},
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IUnit - Target: IResource)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnComponentSpecificInspectorGUI(string tabName)
        {
            switch(tabName)
            {
                case "Resource Collector":
                    EditorGUILayout.PropertyField(SO.FindProperty("collectableResources"));
                    break;
            }
        }
        protected override void OnHandlingProgressInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("progressDuration"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressMaxDistance"));
        }

        protected override void OnSettingTargetInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("setTargetTaskUI"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("onTargetResourceFullSearch"));
            EditorGUILayout.PropertyField(SO.FindProperty("onTargetResourceDepletedSearch"));
        }

    }

    [CustomEditor(typeof(Builder))]
    public class BuilderEditor : FactionEntityTargetProgressComponentEditor<Builder, IBuilding>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Target Search/Picker", "Setting Target" },
            new string [] { "Handling Progress",  "Placement Tasks", "Debug"},
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IUnit - Target: IBuilding)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnComponentSpecificInspectorGUI(string tabName)
        {
            switch(tabName)
            {
                case "Placement Tasks":
                    EditorGUILayout.PropertyField(SO.FindProperty("canPlaceBuildings"));

                    if (!SO.FindProperty("canPlaceBuildings").boolValue)
                        break;

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("creationTasks"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("upgradeTargetCreationTasks"));
                    break;
            }
        }

        protected override void OnTargetSearchPickerInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("targetFinderData"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("canConstruct"));

            if(SO.FindProperty("canConstruct").boolValue)
                EditorGUILayout.PropertyField(SO.FindProperty("constructionTargetPicker"));
        }

        protected override void OnHandlingProgressInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("progressMaxDistance"));

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("The 'Progress Duration' and 'HealtH Per Progress' fields are only relevant if the 'Construction Type' is set to 'Health' in the Building Manager component in the map scene.", MessageType.Warning);
            EditorGUILayout.PropertyField(SO.FindProperty("progressDuration"));
            EditorGUILayout.PropertyField(SO.FindProperty("healthPerProgress"));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("The 'Time Multiplier' field is only relevant if the 'Construction Type' is set to 'Time' in the Building Manager component in the map scene.", MessageType.Warning);
            EditorGUILayout.PropertyField(SO.FindProperty("timeMultiplier"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("inProgressObject"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressOverrideController"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressEnabledAudio"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("sourceEffect"));
            EditorGUILayout.PropertyField(SO.FindProperty("targetEffect"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("fetchConstructionAudioOnce"));
            EditorGUILayout.PropertyField(SO.FindProperty("constructionAudio"));
        }

    }

    [CustomEditor(typeof(UnitMovement))]
    public class UnitMovementEditor : FactionEntityTargetComponentEditor<UnitMovement, IEntity>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "General", "Setting Target" },
            new string [] { "Movement", "Rotation", "Debug"}
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField($"Entity Component (Source: IUnit - Target: IEntity/Vector3)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            OnInspectorGUI(toolbars);
        }

        protected override void OnGeneralInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("code"));
            EditorGUILayout.PropertyField(SO.FindProperty("isActive"));
            EditorGUILayout.PropertyField(SO.FindProperty("priority"));
        }

        protected override void OnComponentSpecificInspectorGUI(string tabName)
        {
            switch(tabName)
            {
                case "Movement":
                    EditorGUILayout.PropertyField(SO.FindProperty("movableTerrainAreas"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("formation"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("movementPriority"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("speed"));
                    EditorGUILayout.PropertyField(SO.FindProperty("acceleration"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("mvtAudio"), new GUIContent("Movement Audio"));
                    EditorGUILayout.PropertyField(SO.FindProperty("invalidMvtPathAudio"), new GUIContent("Invalid Path Audio"));
                    break;

                case "Rotation":
                    EditorGUILayout.PropertyField(SO.FindProperty("mvtAngularSpeed"), new GUIContent("Angular Speed"));

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("movementRotationEnabled"));
                    if (SO.FindProperty("movementRotationEnabled").boolValue)
                    {
                        EditorGUILayout.PropertyField(SO.FindProperty("canMoveAndRotate"));
                        if (SO.FindProperty("canMoveAndRotate").boolValue == false)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(SO.FindProperty("minMoveAngle"));
                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(SO.FindProperty("canIdleRotate"));
                    if (SO.FindProperty("canIdleRotate").boolValue == true)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(SO.FindProperty("smoothIdleRotation"));
                        if (SO.FindProperty("smoothIdleRotation").boolValue == true)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(SO.FindProperty("idleAngularSpeed"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }
                    break;
            }
        }

        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI(showTargetSearch: false);

            GUI.enabled = false;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(SO.FindProperty("isMoving"));
            EditorGUILayout.PropertyField(SO.FindProperty("isMovementPending"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Movement Path", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(SO.FindProperty("startPosition"), new GUIContent("Path Start Position"));
            EditorGUILayout.Vector3Field("Next Path Corner", comp.NextCorner);
            EditorGUILayout.Vector3Field("Path Destination", comp.Destination);
            EditorGUILayout.Toggle("Path Destination Reached", comp.DestinationReached);
            GUI.enabled = true;
            EditorGUILayout.PropertyField(SO.FindProperty("showPathDestination"));
            EditorGUILayout.PropertyField(SO.FindProperty("showPathNextCorner"));
            GUI.enabled = false;

            GUI.enabled = true;
        }
    }

    public class FactionEntityTargetProgressComponentEditor<T, V> : FactionEntityTargetComponentEditor<T, V> where T : FactionEntityTargetProgressComponent<V> where V : IEntity
    {
        protected override void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            base.OnDebugInspectorGUI();
            OnProgressInspectorGUI();
        }

        private void OnProgressInspectorGUI()
        {
            GUI.enabled = false;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Progress", EditorStyles.boldLabel);
            EditorGUILayout.Toggle("In Progress", comp.InProgress);
            EditorGUILayout.PropertyField(SO.FindProperty("progressDuration"), new GUIContent("Progress Reload Time"));
            EditorGUILayout.FloatField("Current Progress Time", comp.ProgressData.progressTime);

            GUI.enabled = true;
            EditorGUILayout.PropertyField(SO.FindProperty("showProgressGizmo"));
            GUI.enabled = false;

            GUI.enabled = true;
        }
    }

    public class FactionEntityTargetComponentEditor<T, V> : TabsEditorBase<T> where T : FactionEntityTargetComponent<V> where V : IEntity
    {
        protected override Int2D tabID {
            get => comp.tabID;
            set => comp.tabID = value;
        }

        public override void OnInspectorGUI(string[][] toolbars)
        {
            base.OnInspectorGUI(toolbars);
        }

        protected override void OnTabSwitch(string tabName)
        {

            switch (tabName)
            {
                case "General":
                    OnGeneralInspectorGUI();
                    break;
                case "Target Search/Picker":
                    OnTargetSearchPickerInspectorGUI();
                    break;
                case "Setting Target":
                    OnSettingTargetInspectorGUI();
                    break;
                case "Handling Progress":
                    OnHandlingProgressInspectorGUI();
                    break;
                case "Debug":
                    OnDebugInspectorGUI();
                    break;
                default:
                    OnComponentSpecificInspectorGUI(tabName);
                    break;
            }
        }

        protected virtual void OnGeneralInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("code"));
            EditorGUILayout.PropertyField(SO.FindProperty("isActive"));
            EditorGUILayout.PropertyField(SO.FindProperty("priority"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("requireIdleEntity"));
        }

        protected virtual void OnTargetSearchPickerInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("targetFinderData"));
        }

        protected virtual void OnSettingTargetInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("setTargetTaskUI"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("orderAudio"));
        }

        protected virtual void OnHandlingProgressInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("progressDuration"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressMaxDistance"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("inProgressObject"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressOverrideController"));
            EditorGUILayout.PropertyField(SO.FindProperty("progressEnabledAudio"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("sourceEffect"));
            EditorGUILayout.PropertyField(SO.FindProperty("targetEffect"));

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Toggle("Search Enabled", comp.TargetFinderData.enabled);
            EditorGUILayout.FloatField("Current Reload Value", comp.TargetFinderCurrReloadValue);
            EditorGUILayout.Space();
        }

        protected virtual void OnDebugInspectorGUI(bool showTargetSearch = true)
        {
            GUI.enabled = false;
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUILayout.Toggle("Is Active", comp.IsActive);
            EditorGUILayout.Toggle("Is Component Idle", comp.IsIdle);
            EditorGUILayout.Toggle("Is Entity Idle", comp.Entity.IsValid() ? comp.Entity.IsIdle : true);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.Toggle("Has Target", comp.HasTarget);
            EditorGUILayout.ObjectField("Target Object", comp.Target.instance?.gameObject, typeof(GameObject), allowSceneObjects: true);
            EditorGUILayout.Vector3Field("Target Position", comp.Target.position);
            EditorGUILayout.Vector3Field("Target (Optional) Position", comp.Target.opPosition);
            GUI.enabled = true;
            EditorGUILayout.PropertyField(SO.FindProperty("showTargetGizmo"));
            GUI.enabled = false;

            if (showTargetSearch)
            {
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Target Search", EditorStyles.boldLabel);
                EditorGUILayout.Toggle("Can Search", comp.CanSearch);
                EditorGUILayout.Toggle("Search Enabled", comp.TargetFinderData.enabled);
                EditorGUILayout.Toggle("Entity Can Launch Tasks", comp.Entity.IsValid() ? comp.Entity.CanLaunchTask : false);
                EditorGUILayout.Toggle("Search When Entity Idle Only", comp.TargetFinderData.idleOnly);
                EditorGUILayout.FloatField("Search Range", comp.TargetFinderData.range);
                EditorGUILayout.FloatField("Reload Time", comp.TargetFinderData.reloadTime);
                EditorGUILayout.FloatField("Current Reload Value", comp.TargetFinderCurrReloadValue);
            }

            GUI.enabled = true;
        }

        protected virtual void OnComponentSpecificInspectorGUI(string tabName)
        {
        }
    }
}
