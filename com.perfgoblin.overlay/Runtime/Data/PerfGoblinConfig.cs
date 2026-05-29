#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace PerfGoblin
{
    public enum OverlayPosition { TopLeft, TopRight, BottomLeft, BottomRight }

    [Flags]
    public enum ActivePanels
    {
        None = 0,
        FPS = 1 << 0,
        Memory = 1 << 1,
        Scripts = 1 << 2,
        Spikes = 1 << 3,
        All = FPS | Memory | Scripts | Spikes
    }

    [Serializable]
    public class PerfGoblinConfig
    {
        // General
        public bool Enabled = true;
        public KeyCode ToggleKey = KeyCode.F3;
        public bool ShowOnPlayModeStart = true;

        // FPS
        public float FpsSmoothingWindow = 0.5f;
        public int FrameHistoryCapacity = 300;

        // Memory
        public int MemorySampleInterval = 30;
        public bool ExcludeEditorOverhead = false;

        // Scripts
        public float ScriptWarnThresholdMs = 1.0f;
        public float ScriptBadThresholdMs = 4.0f;
        public bool AutoDetectScripts = true;
        public int ScriptScanIntervalSeconds = 5;
        public int MaxDisplayedScripts = 10;

        // Spikes
        public float SpikeMultiplier = 2.0f;
        public int SpikeHistoryCapacity = 100;
        public float SpikeDisplayDurationSeconds = 30f;

        // Auto Popup
        public bool AutoPopupOnThreshold = false;
        public float AutoPopupCooldownSeconds = 5f;
        public float AutoDismissSeconds = 0f;

        // Overlay
        public OverlayPosition Position = OverlayPosition.TopRight;
        public bool CompactMode = false;
        public float OverlayOpacity = 0.92f;
        public float OverlayScale = 1.5f;
        public ActivePanels VisiblePanels = ActivePanels.All;

        const string PrefsKey = "PerfGoblin_Config";

        public void Save()
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
#else
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
#endif
        }

        public static PerfGoblinConfig Load()
        {
            string json = null;
#if UNITY_EDITOR
            json = UnityEditor.EditorPrefs.GetString(PrefsKey, null);
#else
            json = PlayerPrefs.GetString(PrefsKey, null);
#endif
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    return JsonUtility.FromJson<PerfGoblinConfig>(json);
                }
                catch
                {
                    return new PerfGoblinConfig();
                }
            }
            return new PerfGoblinConfig();
        }

        public void ResetToDefaults()
        {
            var defaults = new PerfGoblinConfig();
            Enabled = defaults.Enabled;
            ToggleKey = defaults.ToggleKey;
            ShowOnPlayModeStart = defaults.ShowOnPlayModeStart;
            FpsSmoothingWindow = defaults.FpsSmoothingWindow;
            FrameHistoryCapacity = defaults.FrameHistoryCapacity;
            MemorySampleInterval = defaults.MemorySampleInterval;
            ExcludeEditorOverhead = defaults.ExcludeEditorOverhead;
            ScriptWarnThresholdMs = defaults.ScriptWarnThresholdMs;
            ScriptBadThresholdMs = defaults.ScriptBadThresholdMs;
            AutoDetectScripts = defaults.AutoDetectScripts;
            ScriptScanIntervalSeconds = defaults.ScriptScanIntervalSeconds;
            MaxDisplayedScripts = defaults.MaxDisplayedScripts;
            SpikeMultiplier = defaults.SpikeMultiplier;
            SpikeHistoryCapacity = defaults.SpikeHistoryCapacity;
            SpikeDisplayDurationSeconds = defaults.SpikeDisplayDurationSeconds;
            AutoPopupOnThreshold = defaults.AutoPopupOnThreshold;
            AutoPopupCooldownSeconds = defaults.AutoPopupCooldownSeconds;
            AutoDismissSeconds = defaults.AutoDismissSeconds;
            Position = defaults.Position;
            CompactMode = defaults.CompactMode;
            OverlayOpacity = defaults.OverlayOpacity;
            OverlayScale = defaults.OverlayScale;
            VisiblePanels = defaults.VisiblePanels;
            Save();
        }
    }
}
#endif
