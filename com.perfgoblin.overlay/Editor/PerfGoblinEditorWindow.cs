#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PerfGoblin.Editor
{
    public class PerfGoblinEditorWindow : EditorWindow
    {
        [MenuItem("Window/PerfGoblin/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<PerfGoblinEditorWindow>("PerfGoblin");
            window.minSize = new Vector2(300, 400);
        }

        PerfGoblinConfig _config;

        void OnEnable()
        {
            _config = PerfGoblinConfig.Load();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("PerfGoblin", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool isPlaying = EditorApplication.isPlaying;
            bool isActive = isPlaying && PerfGoblinManager.Config != null;

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Play Mode:", isPlaying ? "Active" : "Inactive");
            EditorGUILayout.LabelField("PerfGoblin:", isActive ? "Running" : "Stopped");
            if (isActive)
                EditorGUILayout.LabelField("Overlay:", PerfGoblinManager.IsVisible ? "Visible" : "Hidden");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Project Settings"))
                SettingsService.OpenProjectSettings("Project/PerfGoblin");

            if (isActive)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button(PerfGoblinManager.IsVisible ? "Hide Overlay" : "Show Overlay"))
                    PerfGoblinManager.IsVisible = !PerfGoblinManager.IsVisible;
            }
        }

        void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }
    }
}
#endif
