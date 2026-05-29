#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PerfGoblin
{
    public class MemoryPanelView
    {
        readonly PerfGoblinConfig _config;
        readonly IMemorySource _memory;
        readonly StringBuilder _sb;

        readonly Label _totalLabel;
        readonly Label _modeLabel;
        readonly Label _editorToggle;
        readonly VisualElement _bar;
        readonly VisualElement _legend;

        readonly List<VisualElement> _barSegments = new();
        readonly List<VisualElement> _legendItems = new();

        public MemoryPanelView(VisualElement root, PerfGoblinConfig config,
            IMemorySource memory, StringBuilder sb)
        {
            _config = config;
            _memory = memory;
            _sb = sb;

            _totalLabel = root.Q<Label>("mem-total");
            _modeLabel = root.Q<Label>("mem-mode");
            _editorToggle = root.Q<Label>("mem-editor-toggle");
            _bar = root.Q("mem-bar");
            _legend = root.Q("mem-legend");

            if (_editorToggle != null)
            {
                UpdateEditorToggleState();
                _editorToggle.RegisterCallback<ClickEvent>(evt =>
                {
                    _config.ExcludeEditorOverhead = !_config.ExcludeEditorOverhead;
                    if (!_config.ExcludeEditorOverhead)
                        _memory?.ResetBaseline();
                    _config.Save();
                    UpdateEditorToggleState();
                });
            }
        }

        void UpdateEditorToggleState()
        {
            if (_editorToggle == null) return;
            if (_config.ExcludeEditorOverhead)
            {
                _editorToggle.text = "Game Only";
                _editorToggle.AddToClassList("active");
            }
            else
            {
                _editorToggle.text = "All";
                _editorToggle.RemoveFromClassList("active");
            }
        }

        public void Update()
        {
            if (_totalLabel == null || _memory == null) return;

            if (_modeLabel != null)
                _modeLabel.text = _memory.ExcludingEditor ? "(game only)" : "";

            _sb.Clear();
            FormatBytes(_sb, _memory.TotalAllocatedBytes);
            _totalLabel.text = _sb.ToString();

            UpdateBar();
            UpdateLegend();
        }

        void UpdateBar()
        {
            if (_bar == null) return;

            long total = _memory.TotalAllocatedBytes;
            if (total <= 0) return;

            var categories = _memory.Categories;

            while (_barSegments.Count < categories.Count)
            {
                var seg = new VisualElement();
                seg.AddToClassList("pp-mem-segment");
                _bar.Add(seg);
                _barSegments.Add(seg);
            }

            while (_barSegments.Count > categories.Count)
            {
                var last = _barSegments[_barSegments.Count - 1];
                _bar.Remove(last);
                _barSegments.RemoveAt(_barSegments.Count - 1);
            }

            for (int i = 0; i < categories.Count; i++)
            {
                float pct = (float)categories[i].Bytes / total * 100f;
                _barSegments[i].style.width = Length.Percent(pct);
                _barSegments[i].style.backgroundColor = categories[i].DisplayColor;
            }
        }

        void UpdateLegend()
        {
            if (_legend == null) return;

            var categories = _memory.Categories;

            while (_legendItems.Count < categories.Count)
            {
                var item = new VisualElement();
                item.AddToClassList("pp-mem-legend-item");

                var dot = new VisualElement();
                dot.AddToClassList("pp-mem-legend-dot");
                item.Add(dot);

                var label = new Label();
                label.AddToClassList("pp-mem-legend-label");
                item.Add(label);

                _legend.Add(item);
                _legendItems.Add(item);
            }

            for (int i = 0; i < categories.Count; i++)
            {
                var item = _legendItems[i];
                item.style.display = DisplayStyle.Flex;
                item.ElementAt(0).style.backgroundColor = categories[i].DisplayColor;

                _sb.Clear();
                _sb.Append(categories[i].Name).Append(" ");
                FormatBytes(_sb, categories[i].Bytes);
                ((Label)item.ElementAt(1)).text = _sb.ToString();
            }

            for (int i = categories.Count; i < _legendItems.Count; i++)
                _legendItems[i].style.display = DisplayStyle.None;
        }

        static void FormatBytes(StringBuilder sb, long bytes)
        {
            if (bytes >= 1073741824L)
                sb.AppendFormat("{0:F1} GB", bytes / 1073741824.0);
            else if (bytes >= 1048576L)
                sb.AppendFormat("{0:F0} MB", bytes / 1048576.0);
            else if (bytes >= 1024L)
                sb.AppendFormat("{0:F0} KB", bytes / 1024.0);
            else
                sb.Append(bytes).Append(" B");
        }
    }
}
#endif
