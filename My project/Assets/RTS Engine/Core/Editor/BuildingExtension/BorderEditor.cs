using RTSEngine.BuildingExtension;
using RTSEngine.Utilities;
using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly.BuildingExtension
{
    [CustomEditor(typeof(Border))]
    public class BorderEditor : TabsEditorBase<Border>
    {
        protected override Int2D tabID {
            get => comp.tabID;
            set => comp.tabID = value;
        }

        private string[][] toolbars = new string[][] {
            new string[] { "Fields", "Debug" }
        };

        public override void OnInspectorGUI()
        {
            OnInspectorGUI(toolbars);
        }

        protected override void OnTabSwitch(string tabName)
        {
            switch(tabName)
            {
                case "Fields":
                    OnFieldsInspectorGUI();
                    break;
                case "Debug":
                    OnDebugInspectorGUI();
                    break;
            }
        }

        protected virtual void OnFieldsInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("size"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(SO.FindProperty("borderPrefabs"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(SO.FindProperty("maxBuildingsAmount"));
            EditorGUILayout.PropertyField(SO.FindProperty("buildingLimits"));
        }

        protected virtual void OnDebugInspectorGUI()
        {
            GUI.enabled = false;

            EditorGUILayout.PropertyField(SO.FindProperty("maxBuildingsAmount"));
            EditorGUILayout.IntField("Buildings In Range Count:", comp.IsActive ? comp.BuildingsInRange.Count - 1 : 0);

            if(comp.IsActive)
            {
                EditorGUILayout.Space();
                comp.showBuildingsFoldout = EditorGUILayout.Foldout(comp.showBuildingsFoldout, new GUIContent("Buildings In Range"));
                if (comp.showBuildingsFoldout)
                {
                    EditorGUI.indentLevel++;

                    for (int i = 0; i < comp.BuildingsInRange.Count; i++)
                    {
                        var building = comp.BuildingsInRange[i];
                        EditorGUILayout.ObjectField($"Slot {i}", building.IsValid() ? building.gameObject : null, typeof(GameObject), allowSceneObjects: true);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                comp.showResourcesFoldout = EditorGUILayout.Foldout(comp.showResourcesFoldout, new GUIContent("Resources In Range"));
                if (comp.showResourcesFoldout)
                {
                    EditorGUI.indentLevel++;

                    for (int i = 0; i < comp.ResourcesInRange.Count; i++)
                    {
                        var resource = comp.ResourcesInRange[i];
                        EditorGUILayout.ObjectField($"Slot {i}", resource.IsValid() ? resource.gameObject : null, typeof(GameObject), allowSceneObjects: true);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            GUI.enabled = true;
        }

    }
}