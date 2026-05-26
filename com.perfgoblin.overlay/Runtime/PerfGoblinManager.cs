#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PerfGoblin
{
    [DefaultExecutionOrder(-10000)]
    public class PerfGoblinManager : MonoBehaviour
    {
        static PerfGoblinManager s_Instance;

        public static PerfGoblinConfig Config { get; private set; }
        public static bool IsVisible { get; set; }

        public static FrameTimeTracker FrameTime { get; private set; }
        public static MemoryTracker Memory { get; private set; }
        public static ScriptTracker Scripts { get; private set; }
        public static SpikeDetector Spikes { get; private set; }

        PerfGoblinOverlay _overlay;

        float _lastAutoPopupTime;
        int _lastAutoPopupSpikeCount;
        bool _autoPopped;          // true when overlay was shown by auto-popup (not manual toggle)
        float _autoDismissTimer;

        static readonly Dictionary<string, Stopwatch> s_ActiveSamples = new();
        static readonly Dictionary<string, ScriptTimingData> s_ManualTimings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (s_Instance != null) return;
            var go = new GameObject("[PerfGoblin]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.NotEditable;
            s_Instance = go.AddComponent<PerfGoblinManager>();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;

            Config = PerfGoblinConfig.Load();

            FrameTime = new FrameTimeTracker(Config);
            Memory = new MemoryTracker(Config);
            Scripts = new ScriptTracker(Config);
            Spikes = new SpikeDetector(Config, FrameTime, Scripts);

            _overlay = new PerfGoblinOverlay(gameObject, Config);

            IsVisible = Config.ShowOnPlayModeStart;
            _overlay.SetVisible(IsVisible);
        }

        void Update()
        {
            if (Input.GetKeyDown(Config.ToggleKey))
            {
                IsVisible = !IsVisible;
                _autoPopped = false;
                _overlay.IsAutoPopped = false;
                _overlay.SetVisible(IsVisible);
            }

            FrameTime.Update();
            Memory.Update(Time.frameCount);
            Scripts.Update();
        }

        void LateUpdate()
        {
            FrameTime.LateUpdate();
            Scripts.LateUpdate();
            Spikes.Update();

            // Auto-popup when hidden and thresholds exceeded
            if (!IsVisible && Config.AutoPopupOnThreshold)
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastAutoPopupTime > Config.AutoPopupCooldownSeconds && ShouldAutoPopup())
                {
                    _lastAutoPopupTime = now;
                    _autoPopped = true;
                    _autoDismissTimer = 0f;
                    IsVisible = true;
                    _overlay.IsAutoPopped = true;
                    _overlay.SetVisible(true);
                }
            }

            // Auto-dismiss: only when overlay was auto-popped and dismiss is configured (> 0)
            if (IsVisible && _autoPopped && Config.AutoDismissSeconds > 0f)
            {
                if (!ShouldAutoPopup())
                {
                    _autoDismissTimer += Time.unscaledDeltaTime;
                    if (_autoDismissTimer >= Config.AutoDismissSeconds)
                    {
                        _autoPopped = false;
                        _overlay.IsAutoPopped = false;
                        IsVisible = false;
                        _overlay.SetVisible(false);
                    }
                }
                else
                {
                    _autoDismissTimer = 0f;
                }
            }

            if (IsVisible)
                _overlay.UpdateUI();
        }

        bool ShouldAutoPopup()
        {
            // Check FPS: below 50% of target
            if (FrameTime.TargetFPS > 0 && FrameTime.CurrentFPS < FrameTime.TargetFPS * 0.5f)
                return true;

            // Check scripts: any script at Bad status
            var scripts = Scripts.SortedScripts;
            for (int i = 0; i < scripts.Count; i++)
            {
                if (scripts[i].Status == PerformanceStatus.Bad)
                    return true;
            }

            // Check spikes: new spike detected since last check
            if (Spikes.TotalSpikeCount > _lastAutoPopupSpikeCount)
            {
                _lastAutoPopupSpikeCount = Spikes.TotalSpikeCount;
                return true;
            }

            return false;
        }

        void OnDestroy()
        {
            Memory?.Dispose();
            Scripts?.Dispose();
            Spikes?.Dispose();
            _overlay?.Dispose();

            if (s_Instance == this)
                s_Instance = null;
        }

        public static void BeginSample(string name)
        {
            if (!s_ActiveSamples.ContainsKey(name))
                s_ActiveSamples[name] = new Stopwatch();

            s_ActiveSamples[name].Restart();
        }

        public static void EndSample(string name)
        {
            if (!s_ActiveSamples.TryGetValue(name, out var sw))
                return;

            sw.Stop();
            float ms = (float)sw.Elapsed.TotalMilliseconds;

            if (!s_ManualTimings.TryGetValue(name, out var data))
            {
                data = new ScriptTimingData { ScriptName = name };
                s_ManualTimings[name] = data;
            }

            data.AccumulateManual(ms);

            Scripts?.RecordManualSample(name, data);
        }

        public static IReadOnlyDictionary<string, ScriptTimingData> ManualTimings => s_ManualTimings;

        internal static void ResetManualTimingsFrame()
        {
            foreach (var kvp in s_ManualTimings)
                kvp.Value.ResetFrame();
        }
    }
}
#endif
