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
        AutoPopupController _autoPopup;

        static readonly Dictionary<string, Stopwatch> s_ActiveSamples = new();
        static readonly Dictionary<string, ScriptTimingData> s_ManualTimings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (s_Instance != null) return;

            var config = PerfGoblinConfig.Load();
            if (!config.Enabled) return;

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

            _autoPopup = new AutoPopupController(Config, FrameTime, Scripts, Spikes);
            _overlay = new PerfGoblinOverlay(gameObject, Config, FrameTime, Memory, Scripts, Spikes);

            IsVisible = Config.ShowOnPlayModeStart;
            _overlay.SetVisible(IsVisible);
        }

        void Update()
        {
            if (Input.GetKeyDown(Config.ToggleKey))
            {
                IsVisible = !IsVisible;
                _autoPopup.OnManualToggle();
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

            var action = _autoPopup.Update(IsVisible);
            if (action == AutoPopupController.Action.Show)
            {
                IsVisible = true;
                _overlay.IsAutoPopped = true;
                _overlay.SetVisible(true);
            }
            else if (action == AutoPopupController.Action.Hide)
            {
                IsVisible = false;
                _overlay.IsAutoPopped = false;
                _overlay.SetVisible(false);
            }

            if (IsVisible)
                _overlay.UpdateUI();
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
