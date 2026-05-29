#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PerfGoblin
{
    public class ScriptsPanelView
    {
        readonly IScriptSource _scripts;
        readonly StringBuilder _sb;

        readonly Label _countLabel;
        readonly VisualElement _list;
        readonly List<VisualElement> _rows = new();

        int _lastHash;
        bool _isAutoPopped;

        public ScriptsPanelView(VisualElement root, IScriptSource scripts, StringBuilder sb)
        {
            _scripts = scripts;
            _sb = sb;

            _countLabel = root.Q<Label>("scripts-count");
            _list = root.Q("scripts-list");
        }

        public void SetAutoPopped(bool value) => _isAutoPopped = value;

        public void Update()
        {
            if (_list == null || _scripts == null) return;

            var sorted = _scripts.SortedScripts;

            _sb.Clear();
            _sb.Append(sorted.Count).Append(" active");
            if (_scripts.TotalScriptTimeMs >= 0.01f)
                _sb.AppendFormat(" · {0:F1}ms", _scripts.TotalScriptTimeMs);
            _countLabel.text = _sb.ToString();

            int hash = ComputeHash(sorted);
            if (hash == _lastHash) return;
            _lastHash = hash;

            EnsureRows(sorted.Count);

            for (int i = 0; i < sorted.Count; i++)
            {
                var data = sorted[i];
                var row = _rows[i];
                row.style.display = DisplayStyle.Flex;

                row.RemoveFromClassList("status-ok");
                row.RemoveFromClassList("status-warn");
                row.RemoveFromClassList("status-bad");

                var dot = row.ElementAt(0);
                dot.RemoveFromClassList("status-ok");
                dot.RemoveFromClassList("status-warn");
                dot.RemoveFromClassList("status-bad");

                if (data.HasPreciseTiming)
                {
                    string statusClass = data.Status switch
                    {
                        PerformanceStatus.Warn => "status-warn",
                        PerformanceStatus.Bad => "status-bad",
                        _ => "status-ok"
                    };
                    row.AddToClassList(statusClass);
                    dot.AddToClassList(statusClass);
                }
                else
                {
                    row.AddToClassList("status-ok");
                    dot.AddToClassList("status-ok");
                }

                if (_isAutoPopped && data.Status == PerformanceStatus.Bad)
                    row.AddToClassList("pp-alert-highlight");
                else
                    row.RemoveFromClassList("pp-alert-highlight");

                ((Label)row.ElementAt(1)).text = ShortenName(data.ScriptName);

                var msLabel = (Label)row.ElementAt(2);
                if (data.HasPreciseTiming)
                {
                    _sb.Clear();
                    _sb.AppendFormat("{0:F2}ms", data.LastFrameMs);
                    msLabel.text = _sb.ToString();
                }
                else
                {
                    msLabel.text = "";
                }
            }

            for (int i = sorted.Count; i < _rows.Count; i++)
                _rows[i].style.display = DisplayStyle.None;
        }

        void EnsureRows(int needed)
        {
            while (_rows.Count < needed)
            {
                var row = new VisualElement();
                row.AddToClassList("pp-script-row");

                var dot = new VisualElement();
                dot.AddToClassList("pp-status-dot");
                row.Add(dot);

                var nameLabel = new Label();
                nameLabel.AddToClassList("pp-script-name");
                row.Add(nameLabel);

                var msLabel = new Label();
                msLabel.AddToClassList("pp-script-ms");
                row.Add(msLabel);

                _list.Add(row);
                _rows.Add(row);
            }
        }

        internal static string ShortenName(string name)
        {
            int lastDot = name.LastIndexOf('.');
            if (lastDot > 0)
            {
                int secondLast = name.LastIndexOf('.', lastDot - 1);
                if (secondLast >= 0)
                    return name.Substring(secondLast + 1);
            }
            return name;
        }

        static int ComputeHash(IReadOnlyList<ScriptTimingData> list)
        {
            int hash = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                hash = hash * 31 + list[i].ScriptName.GetHashCode();
                if (list[i].HasPreciseTiming)
                {
                    hash = hash * 31 + Mathf.RoundToInt(list[i].LastFrameMs * 100);
                    hash = hash * 31 + (int)list[i].Status;
                }
            }
            return hash;
        }
    }
}
#endif
