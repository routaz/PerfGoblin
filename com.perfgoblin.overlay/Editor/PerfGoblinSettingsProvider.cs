#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PerfGoblin.Editor
{
    public static class PerfGoblinSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreatePerfGoblinSettings()
        {
            return new SettingsProvider("Project/PerfGoblin", SettingsScope.Project)
            {
                label = "PerfGoblin",
                guiHandler = DrawSettings,
                keywords = new[] { "performance", "profiler", "fps", "overlay", "perfgoblin" }
            };
        }

        static PerfGoblinConfig _config;
        static bool _generalFoldout = true;
        static bool _fpsFoldout = true;
        static bool _memoryFoldout = true;
        static bool _scriptsFoldout = true;
        static bool _spikesFoldout = true;
        static bool _overlayFoldout = true;

        static void DrawSettings(string searchContext)
        {
            if (_config == null)
                _config = PerfGoblinConfig.Load();

            EditorGUI.BeginChangeCheck();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("PerfGoblin v0.1.0", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Lightweight performance overlay for development builds.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(10);

            _generalFoldout = EditorGUILayout.Foldout(_generalFoldout, "General", true);
            if (_generalFoldout)
            {
                EditorGUI.indentLevel++;
                _config.ToggleKey = (KeyCode)EditorGUILayout.EnumPopup("Toggle Key", _config.ToggleKey);
                _config.ShowOnPlayModeStart = EditorGUILayout.Toggle("Show On Play Mode Start", _config.ShowOnPlayModeStart);
                EditorGUI.indentLevel--;
            }

            _fpsFoldout = EditorGUILayout.Foldout(_fpsFoldout, "FPS", true);
            if (_fpsFoldout)
            {
                EditorGUI.indentLevel++;
                _config.FpsSmoothingWindow = EditorGUILayout.Slider("Smoothing Window (s)", _config.FpsSmoothingWindow, 0.1f, 2f);
                _config.FrameHistoryCapacity = EditorGUILayout.IntSlider("Frame History Capacity", _config.FrameHistoryCapacity, 60, 600);
                EditorGUI.indentLevel--;
            }

            _memoryFoldout = EditorGUILayout.Foldout(_memoryFoldout, "Memory", true);
            if (_memoryFoldout)
            {
                EditorGUI.indentLevel++;
                _config.MemorySampleInterval = EditorGUILayout.IntSlider("Sample Interval (frames)", _config.MemorySampleInterval, 1, 120);
                _config.ExcludeEditorOverhead = EditorGUILayout.Toggle("Exclude Editor Overhead", _config.ExcludeEditorOverhead);
                if (_config.ExcludeEditorOverhead)
                    EditorGUILayout.HelpBox("Captures a memory baseline on first frame and subtracts it. Shows approximate game-only memory.", MessageType.Info);
                EditorGUI.indentLevel--;
            }

            _scriptsFoldout = EditorGUILayout.Foldout(_scriptsFoldout, "Scripts", true);
            if (_scriptsFoldout)
            {
                EditorGUI.indentLevel++;
                _config.AutoDetectScripts = EditorGUILayout.Toggle("Auto Detect Scripts", _config.AutoDetectScripts);
                _config.ScriptScanIntervalSeconds = EditorGUILayout.IntSlider("Scan Interval (s)", _config.ScriptScanIntervalSeconds, 1, 30);
                _config.MaxDisplayedScripts = EditorGUILayout.IntSlider("Max Displayed Scripts", _config.MaxDisplayedScripts, 3, 30);
                _config.ScriptWarnThresholdMs = EditorGUILayout.Slider("Warn Threshold (ms)", _config.ScriptWarnThresholdMs, 0.1f, 10f);
                _config.ScriptBadThresholdMs = EditorGUILayout.Slider("Bad Threshold (ms)", _config.ScriptBadThresholdMs, 0.5f, 20f);
                EditorGUI.indentLevel--;
            }

            _spikesFoldout = EditorGUILayout.Foldout(_spikesFoldout, "Spikes", true);
            if (_spikesFoldout)
            {
                EditorGUI.indentLevel++;
                _config.SpikeMultiplier = EditorGUILayout.Slider("Spike Multiplier", _config.SpikeMultiplier, 1.1f, 5f);
                _config.SpikeHistoryCapacity = EditorGUILayout.IntSlider("Spike History Capacity", _config.SpikeHistoryCapacity, 10, 500);
                EditorGUI.indentLevel--;
            }

            _overlayFoldout = EditorGUILayout.Foldout(_overlayFoldout, "Overlay", true);
            if (_overlayFoldout)
            {
                EditorGUI.indentLevel++;
                _config.Position = (OverlayPosition)EditorGUILayout.EnumPopup("Position", _config.Position);
                _config.CompactMode = EditorGUILayout.Toggle("Compact Mode", _config.CompactMode);
                _config.OverlayOpacity = EditorGUILayout.Slider("Opacity", _config.OverlayOpacity, 0.3f, 1f);
                _config.OverlayScale = EditorGUILayout.Slider("Scale", _config.OverlayScale, 0.5f, 3f);
                _config.VisiblePanels = (ActivePanels)EditorGUILayout.EnumFlagsField("Visible Panels", _config.VisiblePanels);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
                _config.Save();

            GUILayout.Space(10);
            if (GUILayout.Button("Reset to Defaults"))
            {
                _config.ResetToDefaults();
            }
        }
    }
}
#endif
