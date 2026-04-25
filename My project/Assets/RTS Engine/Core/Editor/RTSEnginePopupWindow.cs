using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly
{
    public class RTSEnginePopupWindow : EditorWindow
    {
        private static RTSEnginePopupWindow currWindow = null;

        public static void Init()
        {
            currWindow = (RTSEnginePopupWindow)EditorWindow.GetWindow(typeof(RTSEnginePopupWindow), true, "RTS Engine");

            currWindow.minSize = new Vector2(400.0f, 150.0f);
            currWindow.maxSize = new Vector2(400.0f, 150.0f);

            currWindow.Show();
            currWindow.Focus();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Welcome to the RTS Engine asset by GameDevSpice!", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("If this is your first time launching the asset, make sure to import the preset layers.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUILayout.Button("Import Preset Layers"))
                RTSEditorHelper.ImportPresetLayers();
            if (GUILayout.Button("Documentation"))
                RTSEditorHelper.OpenDocumentation();
            if (GUILayout.Button("Close"))
                this.Close();
        }
    }
}
