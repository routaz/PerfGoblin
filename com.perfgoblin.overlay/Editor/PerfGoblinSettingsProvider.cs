#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
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
            EditorGUILayout.LabelField("PerfGoblin v0.2.0", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Lightweight performance overlay for development builds.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(10);

            _config.Enabled = EditorGUILayout.Toggle("Enabled", _config.Enabled);
            if (!_config.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "PerfGoblin is disabled. No GameObject, trackers, or profiler recorders will be created when entering Play Mode.",
                    MessageType.Info);
            }
            GUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(!_config.Enabled);

            _generalFoldout = EditorGUILayout.Foldout(_generalFoldout, "General", true);
            if (_generalFoldout)
            {
                EditorGUI.indentLevel++;
                _config.ToggleKey = (KeyCode)EditorGUILayout.EnumPopup("Toggle Key", _config.ToggleKey);
                _config.ShowOnPlayModeStart = EditorGUILayout.Toggle("Show On Play Mode Start", _config.ShowOnPlayModeStart);
                _config.AutoPopupOnThreshold = EditorGUILayout.Toggle("Auto Popup On Threshold", _config.AutoPopupOnThreshold);
                if (_config.AutoPopupOnThreshold)
                {
                    EditorGUI.indentLevel++;
                    _config.AutoPopupCooldownSeconds = EditorGUILayout.Slider("Popup Cooldown (s)", _config.AutoPopupCooldownSeconds, 1f, 30f);
                    _config.AutoDismissSeconds = EditorGUILayout.Slider("Auto Dismiss (s)", _config.AutoDismissSeconds, 0f, 30f);
                    if (_config.AutoDismissSeconds <= 0f)
                        EditorGUILayout.HelpBox("Auto dismiss is off. Use the toggle key to hide the overlay after it pops up.", MessageType.None);
                    else
                        EditorGUILayout.HelpBox("Overlay will auto-hide " + _config.AutoDismissSeconds.ToString("F0") + "s after all values return to normal.", MessageType.None);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.HelpBox(
                        "Overlay auto-shows when hidden and any threshold is exceeded:\n" +
                        "  • FPS drops below 50% of target\n" +
                        "  • Any script exceeds Bad threshold (" + _config.ScriptBadThresholdMs.ToString("F1") + "ms)\n" +
                        "  • Frame spike detected (>" + _config.SpikeMultiplier.ToString("F1") + "x target)\n\n" +
                        "Tip: set Show On Play Mode Start to off for a fully hidden start.",
                        MessageType.Info);
                }
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

                GUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "The overlay detects active MonoBehaviours and shows combined script time.\n" +
                    "Per-script timing requires manual instrumentation:\n\n" +
                    "  PerfGoblinProfiler.BeginSample(\"MyMethod\");\n" +
                    "  // ... your code ...\n" +
                    "  PerfGoblinProfiler.EndSample(\"MyMethod\");\n\n" +
                    "Place calls INSIDE the method body. No #if wrappers needed —\n" +
                    "calls are automatically stripped in release builds.\n\n" +
                    "Instrumented methods appear with precise per-method timing and warn/bad status.\n" +
                    "For full per-script breakdown, use Unity's Profiler window (Window > Analysis > Profiler).",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            _spikesFoldout = EditorGUILayout.Foldout(_spikesFoldout, "Spikes", true);
            if (_spikesFoldout)
            {
                EditorGUI.indentLevel++;
                _config.SpikeMultiplier = EditorGUILayout.Slider("Spike Multiplier", _config.SpikeMultiplier, 1.1f, 5f);
                float targetMs = 1000f / Application.targetFrameRate;
                if (Application.targetFrameRate <= 0) targetMs = 1000f / 60f;
                EditorGUILayout.HelpBox(
                    "A frame is flagged as a spike when its total frame time exceeds " +
                    _config.SpikeMultiplier.ToString("F1") + "x the target frame time.\n" +
                    "At " + (Application.targetFrameRate > 0 ? Application.targetFrameRate : 60) +
                    " FPS (" + targetMs.ToString("F1") + "ms target), spikes trigger above " +
                    (targetMs * _config.SpikeMultiplier).ToString("F0") + "ms.\n\n" +
                    "The overlay shows total frame time (e.g. 66ms) with the top offender script and its individual cost (e.g. 4.2ms).",
                    MessageType.Info);
                _config.SpikeDisplayDurationSeconds = EditorGUILayout.Slider("Display Duration (s)", _config.SpikeDisplayDurationSeconds, 5f, 60f);
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

            EditorGUI.EndDisabledGroup();

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
