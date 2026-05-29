#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PerfGoblin
{
    public class FpsPanelView
    {
        readonly IFrameTimeSource _frameTime;
        readonly StringBuilder _sb;

        readonly Label _valueLabel;
        readonly Label _targetLabel;
        readonly Label _msLabel;
        readonly Label _minLabel;
        readonly Label _avgLabel;
        readonly Label _maxLabel;
        readonly VisualElement _graph;

        bool _isAutoPopped;

        public FpsPanelView(VisualElement root, IFrameTimeSource frameTime, StringBuilder sb)
        {
            _frameTime = frameTime;
            _sb = sb;

            _valueLabel = root.Q<Label>("fps-value");
            _targetLabel = root.Q<Label>("fps-target");
            _msLabel = root.Q<Label>("fps-ms");
            _minLabel = root.Q<Label>("fps-min");
            _avgLabel = root.Q<Label>("fps-avg");
            _maxLabel = root.Q<Label>("fps-max");
            _graph = root.Q("fps-graph");

            if (_graph != null)
                _graph.generateVisualContent += DrawGraph;
        }

        public void SetAutoPopped(bool value) => _isAutoPopped = value;

        public void Update()
        {
            if (_valueLabel == null || _frameTime == null) return;

            int fps = Mathf.RoundToInt(_frameTime.CurrentFPS);
            _valueLabel.text = fps.ToString();

            SetFpsColor(_valueLabel, fps, _frameTime.TargetFPS);

            bool fpsAlert = _isAutoPopped && _frameTime.TargetFPS > 0 && fps < _frameTime.TargetFPS * 0.5f;
            if (fpsAlert)
                _valueLabel.AddToClassList("pp-alert-highlight-fps");
            else
                _valueLabel.RemoveFromClassList("pp-alert-highlight-fps");

            _sb.Clear();
            _sb.Append("/ ").Append(_frameTime.TargetFPS).Append(" FPS");
            _targetLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.AppendFormat("{0:F1}ms", _frameTime.CurrentFrameTimeMs);
            _msLabel.text = _sb.ToString();

            if (_minLabel != null)
            {
                _sb.Clear();
                _sb.Append("Min: ").Append(Mathf.RoundToInt(_frameTime.MinFPS));
                _minLabel.text = _sb.ToString();
            }
            if (_avgLabel != null)
            {
                _sb.Clear();
                _sb.Append("Avg: ").Append(Mathf.RoundToInt(_frameTime.AverageFPS));
                _avgLabel.text = _sb.ToString();
            }
            if (_maxLabel != null)
            {
                _sb.Clear();
                _sb.Append("Max: ").Append(Mathf.RoundToInt(_frameTime.MaxFPS));
                _maxLabel.text = _sb.ToString();
            }

            _graph?.MarkDirtyRepaint();
        }

        static void SetFpsColor(Label label, int fps, int target)
        {
            label.RemoveFromClassList("fps-good");
            label.RemoveFromClassList("fps-warn");
            label.RemoveFromClassList("fps-bad");

            float ratio = target > 0 ? (float)fps / target : 1f;
            if (ratio >= 0.9f)
                label.AddToClassList("fps-good");
            else if (ratio >= 0.5f)
                label.AddToClassList("fps-warn");
            else
                label.AddToClassList("fps-bad");
        }

        void DrawGraph(MeshGenerationContext ctx)
        {
            if (_frameTime == null || _frameTime.FrameTimeHistory.Count < 2) return;

            var painter = ctx.painter2D;
            var rect = _graph.contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;

            var history = _frameTime.FrameTimeHistory;
            int count = history.Count;
            float targetMs = 1000f / _frameTime.TargetFPS;
            float maxMs = targetMs * 3f;

            painter.strokeColor = new Color(0.13f, 0.77f, 0.37f, 0.8f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();

            for (int i = 0; i < count; i++)
            {
                float x = (float)i / (count - 1) * rect.width;
                float normalized = Mathf.Clamp01(history[i] / maxMs);
                float y = rect.height - normalized * rect.height;

                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();

            float targetY = rect.height - Mathf.Clamp01(targetMs / maxMs) * rect.height;
            painter.strokeColor = new Color(0.94f, 0.62f, 0.14f, 0.4f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, targetY));
            painter.LineTo(new Vector2(rect.width, targetY));
            painter.Stroke();
        }

        public void Dispose()
        {
            if (_graph != null)
                _graph.generateVisualContent -= DrawGraph;
        }
    }
}
#endif
