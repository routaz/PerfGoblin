#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace PerfGoblin
{
    public class AutoPopupController
    {
        readonly PerfGoblinConfig _config;
        readonly IFrameTimeSource _frameTime;
        readonly IScriptSource _scripts;
        readonly ISpikeSource _spikes;

        float _lastPopupTime;
        int _lastSpikeCount;
        bool _autoPopped;
        float _dismissTimer;

        public bool IsAutoPopped => _autoPopped;

        public AutoPopupController(PerfGoblinConfig config, IFrameTimeSource frameTime,
            IScriptSource scripts, ISpikeSource spikes)
        {
            _config = config;
            _frameTime = frameTime;
            _scripts = scripts;
            _spikes = spikes;
        }

        public enum Action { None, Show, Hide }

        public Action Update(bool isVisible)
        {
            if (!isVisible && _config.AutoPopupOnThreshold)
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastPopupTime > _config.AutoPopupCooldownSeconds && ShouldPopup())
                {
                    _lastPopupTime = now;
                    _autoPopped = true;
                    _dismissTimer = 0f;
                    return Action.Show;
                }
            }

            if (isVisible && _autoPopped && _config.AutoDismissSeconds > 0f)
            {
                if (!ShouldPopup())
                {
                    _dismissTimer += Time.unscaledDeltaTime;
                    if (_dismissTimer >= _config.AutoDismissSeconds)
                    {
                        _autoPopped = false;
                        return Action.Hide;
                    }
                }
                else
                {
                    _dismissTimer = 0f;
                }
            }

            return Action.None;
        }

        public void OnManualToggle()
        {
            _autoPopped = false;
        }

        bool ShouldPopup()
        {
            if (_frameTime.TargetFPS > 0 && _frameTime.CurrentFPS < _frameTime.TargetFPS * 0.5f)
                return true;

            var scripts = _scripts.SortedScripts;
            for (int i = 0; i < scripts.Count; i++)
            {
                if (scripts[i].Status == PerformanceStatus.Bad)
                    return true;
            }

            if (_spikes.TotalSpikeCount > _lastSpikeCount)
            {
                _lastSpikeCount = _spikes.TotalSpikeCount;
                return true;
            }

            return false;
        }
    }
}
#endif
