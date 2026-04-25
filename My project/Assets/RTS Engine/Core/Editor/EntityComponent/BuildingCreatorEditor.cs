using UnityEditor;
using RTSEngine.EntityComponent;
using RTSEngine.Utilities;

namespace RTSEngine.EditorOnly.EntityComponent
{
    [CustomEditor(typeof(BuildingCreator))]
    public class BuildingCreatorEditor : TabsEditorBase<BuildingCreator>
    {
        protected override Int2D tabID
        {
            get => comp.tabID;
            set => comp.tabID = value;
        }

        private string[][] toolbars = new string[][] {
            new string [] { "General", "Placement Tasks" },
        };

        public override void OnInspectorGUI()
        {
            OnInspectorGUI(toolbars);
        }

        protected override void OnTabSwitch(string tabName)
        {
            switch (tabName)
            {
                case "General":
                    OnGeneralInspectorGUI();
                    break;
                case "Placement Tasks":
                    OnPlacementTasksInspectorGUI();
                    break;
            }
        }

        protected virtual void OnGeneralInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("code"));
            EditorGUILayout.PropertyField(SO.FindProperty("isActive"));
        }

        protected virtual void OnPlacementTasksInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("creationTasks"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("upgradeTargetCreationTasks"));
        }
    }
}
