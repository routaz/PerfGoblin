#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PerfGoblin
{
    public class SpikesPanelView
    {
        readonly ISpikeSource _spikes;
        readonly StringBuilder _sb;

        readonly Label _countLabel;
        readonly VisualElement _list;
        readonly List<VisualElement> _rows = new();

        bool _isAutoPopped;

        public SpikesPanelView(VisualElement root, ISpikeSource spikes, StringBuilder sb)
        {
            _spikes = spikes;
            _sb = sb;

            _countLabel = root.Q<Label>("spikes-count");
            _list = root.Q("spikes-list");
        }

        public void SetAutoPopped(bool value) => _isAutoPopped = value;

        public void Update()
        {
            if (_list == null || _spikes == null) return;

            var recentList = new List<SpikeEvent>();
            foreach (var spike in _spikes.RecentSpikes)
                recentList.Add(spike);

            _sb.Clear();
            _sb.Append(_spikes.TotalSpikeCount).Append(" detected");
            if (recentList.Count < _spikes.TotalSpikeCount)
                _sb.Append(" · ").Append(recentList.Count).Append(" recent");
            _countLabel.text = _sb.ToString();

            EnsureRows(recentList.Count);

            for (int i = 0; i < recentList.Count; i++)
            {
                var spike = recentList[i];
                var row = _rows[i];
                row.style.display = DisplayStyle.Flex;

                if (_isAutoPopped && i == 0)
                    row.AddToClassList("pp-alert-highlight");
                else
                    row.RemoveFromClassList("pp-alert-highlight");

                _sb.Clear();
                _sb.Append('#').Append(spike.FrameCount);
                ((Label)row.ElementAt(0)).text = _sb.ToString();

                _sb.Clear();
                _sb.AppendFormat("{0:F0}ms", spike.FrameTimeMs);
                ((Label)row.ElementAt(1)).text = _sb.ToString();

                _sb.Clear();
                if (!string.IsNullOrEmpty(spike.SpikeCategory) && spike.SpikeCategoryMs > 1f)
                {
                    _sb.Append(spike.SpikeCategory);
                    _sb.AppendFormat(" ({0:F1}ms)", spike.SpikeCategoryMs);
                }
                else if (spike.TopOffenders != null && spike.TopOffenders.Count > 0)
                {
                    var top = spike.TopOffenders[0];
                    float scriptPct = spike.FrameTimeMs > 0
                        ? spike.TotalScriptTimeMs / spike.FrameTimeMs : 0f;

                    if (scriptPct >= 0.05f)
                    {
                        _sb.Append(ScriptsPanelView.ShortenName(top.ScriptName));
                        _sb.AppendFormat(" ({0:F2}ms)", top.LastFrameMs);
                    }
                    else
                    {
#if UNITY_EDITOR
                        _sb.Append("editor/system");
#else
                        _sb.Append("engine/system");
#endif
                    }
                }
                else
                {
                    _sb.Append("—");
                }
                ((Label)row.ElementAt(2)).text = _sb.ToString();
            }

            for (int i = recentList.Count; i < _rows.Count; i++)
                _rows[i].style.display = DisplayStyle.None;
        }

        void EnsureRows(int needed)
        {
            while (_rows.Count < needed)
            {
                var row = new VisualElement();
                row.AddToClassList("pp-spike-row");

                var frame = new Label();
                frame.AddToClassList("pp-spike-frame");
                row.Add(frame);

                var ms = new Label();
                ms.AddToClassList("pp-spike-ms");
                row.Add(ms);

                var offender = new Label();
                offender.AddToClassList("pp-spike-offender");
                row.Add(offender);

                _list.Add(row);
                _rows.Add(row);
            }
        }
    }
}
#endif
