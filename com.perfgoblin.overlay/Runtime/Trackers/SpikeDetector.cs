#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

namespace PerfGoblin
{
    public class SpikeEvent
    {
        public int FrameCount;
        public float FrameTimeMs;
        public float ExpectedFrameTimeMs;
        public float TotalScriptTimeMs;
        public string SpikeCategory;
        public float SpikeCategoryMs;
        public List<ScriptTimingData> TopOffenders;
        public float TimeSinceStart;
    }

    public class SpikeDetector : IDisposable
    {
        readonly PerfGoblinConfig _config;
        readonly FrameTimeTracker _frameTime;
        readonly ScriptTracker _scripts;
        readonly RingBuffer<SpikeEvent> _history;

        // Subsystem profiler recorders for spike attribution
        readonly List<string> _subsystemNames = new();
        readonly List<ProfilerRecorder> _subsystemRecorders = new();

        int _spikeCountWindow;
        float _windowTimer;
        const float WindowDuration = 10f;

        public IEnumerable<SpikeEvent> RecentSpikes
        {
            get
            {
                int count = Mathf.Min(_history.Count, 10);
                int start = _history.Count - count;
                for (int i = _history.Count - 1; i >= start; i--)
                    yield return _history[i];
            }
        }

        public float SpikeFrequency => _spikeCountWindow / WindowDuration;

        public string MostFrequentOffender
        {
            get
            {
                var freq = new Dictionary<string, int>();
                foreach (var spike in _history.AsEnumerable())
                {
                    if (!string.IsNullOrEmpty(spike.SpikeCategory))
                    {
                        freq.TryGetValue(spike.SpikeCategory, out int cc);
                        freq[spike.SpikeCategory] = cc + 1;
                    }
                    else if (spike.TopOffenders != null)
                    {
                        foreach (var offender in spike.TopOffenders)
                        {
                            freq.TryGetValue(offender.ScriptName, out int c);
                            freq[offender.ScriptName] = c + 1;
                        }
                    }
                }
                string best = null;
                int bestCount = 0;
                foreach (var kvp in freq)
                {
                    if (kvp.Value > bestCount)
                    {
                        bestCount = kvp.Value;
                        best = kvp.Key;
                    }
                }
                return best;
            }
        }

        public int TotalSpikeCount => _history.Count;

        public SpikeDetector(PerfGoblinConfig config, FrameTimeTracker frameTime, ScriptTracker scripts)
        {
            _config = config;
            _frameTime = frameTime;
            _scripts = scripts;
            _history = new RingBuffer<SpikeEvent>(config.SpikeHistoryCapacity);
            InitSubsystemRecorders();
        }

        void InitSubsystemRecorders()
        {
            // First valid match per display name wins — order broadest marker first per category
            var candidates = new (ProfilerCategory Cat, string Marker, string Display)[]
            {
                // Rendering — built-in + SRP/URP/HDRP
                (ProfilerCategory.Render, "Camera.Render", "Rendering"),
                (ProfilerCategory.Render, "ScriptableRenderPipeline.Render", "Rendering"),
                (ProfilerCategory.Render, "RenderPipelineManager.DoRenderLoop_Internal", "Rendering"),
                (ProfilerCategory.Render, "UniversalRenderPipeline.RenderSingleCameraInternal", "Rendering"),

                // GPU / VSync wait
                (ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", "GPU/VSync"),
                (ProfilerCategory.Render, "Gfx.WaitForRenderThread", "GPU/VSync"),

                // Physics
                (new ProfilerCategory("Physics"), "Physics.Processing", "Physics"),
                (new ProfilerCategory("Physics"), "FixedUpdate.PhysicsFixedUpdate", "Physics"),

                // UI
                (new ProfilerCategory("UI"), "UGUI.Rendering", "UI"),
                (new ProfilerCategory("UI"), "Canvas.SendWillRenderCanvases", "UI"),
                (ProfilerCategory.Render, "Canvas.SendWillRenderCanvases", "UI"),

                // Animation
                (new ProfilerCategory("Animation"), "Animators.Update", "Animation"),
                (new ProfilerCategory("Animation"), "Directors.Update", "Animation"),

                // GC
                (ProfilerCategory.Memory, "GC.Collect", "GC"),
                (ProfilerCategory.Memory, "GarbageCollector.CollectIncremental", "GC"),

                // Loading / streaming
                (new ProfilerCategory("Loading"), "Loading.ReadObject", "Loading"),
                (new ProfilerCategory("Loading"), "AsyncReadManager.ReadFile", "Loading"),
            };

            var found = new HashSet<string>();
            foreach (var (cat, marker, display) in candidates)
            {
                if (found.Contains(display)) continue;
                try
                {
                    var rec = ProfilerRecorder.StartNew(cat, marker, 1);
                    if (rec.Valid)
                    {
                        _subsystemNames.Add(display);
                        _subsystemRecorders.Add(rec);
                        found.Add(display);
                    }
                    else
                    {
                        rec.Dispose();
                    }
                }
                catch { }
            }
        }

        (string Name, float Ms) GetTopSubsystem()
        {
            string topName = null;
            float topMs = 0f;
            for (int i = 0; i < _subsystemRecorders.Count; i++)
            {
                var rec = _subsystemRecorders[i];
                if (!rec.Valid) continue;
                float ms = rec.LastValue / 1_000_000f;
                if (ms > topMs)
                {
                    topMs = ms;
                    topName = _subsystemNames[i];
                }
            }
            return (topName, topMs);
        }

        public void Update()
        {
            float targetMs = 1000f / _frameTime.TargetFPS;
            float threshold = targetMs * _config.SpikeMultiplier;
            float currentMs = _frameTime.CurrentFrameTimeMs;

            _windowTimer += Time.unscaledDeltaTime;
            if (_windowTimer >= WindowDuration)
            {
                _windowTimer = 0f;
                _spikeCountWindow = 0;
            }

            if (currentMs > threshold)
            {
                var topOffenders = _scripts.SortedScripts
                    .Take(3)
                    .Select(s => new ScriptTimingData
                    {
                        ScriptName = s.ScriptName,
                        LastFrameMs = s.LastFrameMs,
                        AverageMs = s.AverageMs,
                        PeakMs = s.PeakMs,
                        CallCount = s.CallCount,
                        Status = s.Status
                    })
                    .ToList();

                var (catName, catMs) = GetTopSubsystem();

                _history.Push(new SpikeEvent
                {
                    FrameCount = Time.frameCount,
                    FrameTimeMs = currentMs,
                    ExpectedFrameTimeMs = targetMs,
                    TotalScriptTimeMs = _scripts.TotalScriptTimeMs,
                    SpikeCategory = catName,
                    SpikeCategoryMs = catMs,
                    TopOffenders = topOffenders,
                    TimeSinceStart = Time.realtimeSinceStartup
                });

                _spikeCountWindow++;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _subsystemRecorders.Count; i++)
            {
                var rec = _subsystemRecorders[i];
                if (rec.Valid) rec.Dispose();
            }
            _subsystemRecorders.Clear();
            _subsystemNames.Clear();
        }
    }
}
#endif
