#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace PerfGoblin
{
    public class FrameTimeTracker : IFrameTimeSource
    {
        readonly PerfGoblinConfig _config;
        readonly RingBuffer<float> _history;

        float _smoothedFps;
        float _currentFrameTimeMs;
        float _minFps = float.MaxValue;
        float _maxFps;
        float _fpsAccumulator;
        int _fpsFrameCount;
        float _statsTimer;

        const float StatsWindowSeconds = 2f;

        public float CurrentFPS => _smoothedFps;
        public float CurrentFrameTimeMs => _currentFrameTimeMs;
        public float AverageFPS => _fpsFrameCount > 0 ? _fpsAccumulator / _fpsFrameCount : _smoothedFps;
        public float MinFPS => _minFps < float.MaxValue ? _minFps : 0f;
        public float MaxFPS => _maxFps;
        public int TargetFPS => GetTargetFPS();
        public RingBuffer<float> FrameTimeHistory => _history;

        public FrameTimeTracker(PerfGoblinConfig config)
        {
            _config = config;
            _history = new RingBuffer<float>(config.FrameHistoryCapacity);
            _smoothedFps = 60f;
        }

        public void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            _currentFrameTimeMs = dt * 1000f;
            float instantFps = 1f / dt;

            float alpha = 2f / (_config.FpsSmoothingWindow / dt + 1f);
            _smoothedFps = Mathf.Lerp(_smoothedFps, instantFps, alpha);

            _history.Push(_currentFrameTimeMs);

            _fpsAccumulator += instantFps;
            _fpsFrameCount++;
            if (instantFps < _minFps) _minFps = instantFps;
            if (instantFps > _maxFps) _maxFps = instantFps;

            _statsTimer += dt;
            if (_statsTimer >= StatsWindowSeconds)
            {
                _statsTimer = 0f;
                _fpsAccumulator = 0f;
                _fpsFrameCount = 0;
                _minFps = float.MaxValue;
                _maxFps = 0f;
            }
        }

        public void LateUpdate() { }

        static int GetTargetFPS()
        {
            if (Application.targetFrameRate > 0)
                return Application.targetFrameRate;
#if UNITY_2022_3_OR_NEWER
            var ratio = Screen.currentResolution.refreshRateRatio;
            if (ratio.denominator > 0)
                return Mathf.RoundToInt((float)(ratio.numerator / (double)ratio.denominator));
#endif
            return 60;
        }
    }
}
#endif
