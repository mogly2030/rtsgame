using UnityEditor;
using UnityEngine;

using RTSEngine.EntityComponent;
using RTSEngine.Utilities;

namespace RTSEngine.EditorOnly.EntityComponent
{
    [CustomEditor(typeof(BuildingWorkerManager))]
    public class BuildingWorkerManagerEditor : EntityWorkerManagerEditor<BuildingWorkerManager>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "Fields", "Debug" }
        };

        public override void OnInspectorGUI()
        {
            OnInspectorGUI(toolbars);
        }
    }

    [CustomEditor(typeof(ResourceWorkerManager))]
    public class ResourceWorkerManagerEditor : EntityWorkerManagerEditor<ResourceWorkerManager>
    {
        private string[][] toolbars = new string[][] {
            new string [] { "Fields", "Debug" }
        };

        public override void OnInspectorGUI()
        {
            OnInspectorGUI(toolbars);
        }
    }


    public class EntityWorkerManagerEditor<T> : TabsEditorBase<T>  where T : EntityWorkerManager
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
                case "Fields":
                    OnGeneralInspectorGUI();
                    break;
                case "Debug":
                    OnDebugInspectorGUI();
                    break;
            }
        }

        protected virtual void OnGeneralInspectorGUI()
        {
            EditorGUILayout.PropertyField(SO.FindProperty("code"));
            EditorGUILayout.PropertyField(SO.FindProperty("workerPositions"));
            EditorGUILayout.PropertyField(SO.FindProperty("forcedTerrainAreas"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(SO.FindProperty("defineMaxAmount"));
            EditorGUILayout.PropertyField(SO.FindProperty("maxWorkerAmount"));
        }

        protected virtual void OnDebugInspectorGUI()
        {
            GUI.enabled = false;

            EditorGUILayout.IntField("Max Amount", comp.MaxAmount);
            EditorGUILayout.IntField("Current Amount", comp.Workers.IsValid() ? comp.Amount : 0);
            EditorGUILayout.Toggle("Has Max Amount", comp.Workers.IsValid() ? comp.HasMaxAmount : false);
            if (comp.Workers.IsValid())
            {
                comp.showWorkersFoldout = EditorGUILayout.Foldout(comp.showWorkersFoldout, new GUIContent("Workers"));
                if (comp.showWorkersFoldout)
                {
                    for (int i = 0; i < comp.Workers.Count; i++)
                    {
                        var worker = comp.Workers[i];
                        EditorGUILayout.ObjectField($"Slot {i}", worker.IsValid() ? worker.gameObject : null, typeof(GameObject), allowSceneObjects: true);
                    }
                }
            }

            GUI.enabled = true;
            EditorGUILayout.PropertyField(SO.FindProperty("showWorkerPositionsGizmos"));
            EditorGUILayout.PropertyField(SO.FindProperty("showWorkersGizmos"));
            GUI.enabled = false;

            GUI.enabled = true;
        }
    }
}
