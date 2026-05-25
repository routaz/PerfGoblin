#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PerfGoblin
{
    public class SpikeEvent
    {
        public int FrameCount;
        public float FrameTimeMs;
        public float ExpectedFrameTimeMs;
        public List<ScriptTimingData> TopOffenders;
        public float TimeSinceStart;
    }

    public class SpikeDetector
    {
        readonly PerfGoblinConfig _config;
        readonly FrameTimeTracker _frameTime;
        readonly ScriptTracker _scripts;
        readonly RingBuffer<SpikeEvent> _history;

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
                    if (spike.TopOffenders == null) continue;
                    foreach (var offender in spike.TopOffenders)
                    {
                        freq.TryGetValue(offender.ScriptName, out int c);
                        freq[offender.ScriptName] = c + 1;
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

                _history.Push(new SpikeEvent
                {
                    FrameCount = Time.frameCount,
                    FrameTimeMs = currentMs,
                    ExpectedFrameTimeMs = targetMs,
                    TopOffenders = topOffenders,
                    TimeSinceStart = Time.realtimeSinceStartup
                });

                _spikeCountWindow++;
            }
        }
    }
}
#endif
