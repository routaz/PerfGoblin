#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PerfGoblin
{
    public class PerfGoblinOverlay : IDisposable
    {
        readonly PerfGoblinConfig _config;
        readonly UIDocument _document;
        readonly StringBuilder _sb = new(128);

        VisualElement _root;
        VisualElement _body;
        Label _minimizeBtn;
        bool _minimized;
        VisualElement _panelFps;
        VisualElement _panelMemory;
        VisualElement _panelScripts;
        VisualElement _panelSpikes;

        Label _fpsValue;
        Label _fpsTarget;
        Label _fpsMs;
        Label _fpsMin;
        Label _fpsAvg;
        Label _fpsMax;
        VisualElement _fpsGraph;

        Label _memTotal;
        Label _memMode;
        Label _memEditorToggle;
        VisualElement _memBar;
        VisualElement _memLegend;

        Label _scriptsCount;
        VisualElement _scriptsList;

        Label _spikesCount;
        VisualElement _spikesList;

        readonly List<VisualElement> _memSegments = new();
        readonly List<VisualElement> _memLegendItems = new();
        readonly List<VisualElement> _scriptRows = new();
        readonly List<VisualElement> _spikeRows = new();

        int _lastScriptHash;
        int _lastSpikeCount;
        bool _pendingVisible;
        bool _initialized;

        // Drag state
        bool _isDragging;
        bool _isDragPositioned;
        Vector2 _dragStartPointerPos;
        Vector2 _dragStartRootPos;

        // Auto-popup alert state
        internal bool IsAutoPopped;

        // Config change tracking
        OverlayPosition _lastAppliedPosition;
        float _lastAppliedScale;
        float _lastAppliedOpacity;

        PanelSettings _panelSettings;

        public PerfGoblinOverlay(GameObject host, PerfGoblinConfig config)
        {
            _config = config;

            _document = host.AddComponent<UIDocument>();
            _document.sortingOrder = 10000;

            if (_document.panelSettings == null)
            {
                var loaded = Resources.Load<PanelSettings>("PerfGoblinPanelSettings");
                if (loaded != null)
                {
                    _document.panelSettings = loaded;
                }
                else
                {
                    _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                    _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                    _panelSettings.match = 0.5f;
                    _document.panelSettings = _panelSettings;
                }
            }

            var uxml = Resources.Load<VisualTreeAsset>("PerfGoblinOverlayDocument");
            if (uxml == null)
                uxml = LoadFromPackage<VisualTreeAsset>("PerfGoblinOverlayDocument");

            if (uxml == null)
            {
                Debug.LogError("[PerfGoblin] Could not load PerfGoblinOverlayDocument.uxml — overlay will not render.");
                return;
            }

            var uss = Resources.Load<StyleSheet>("PerfGoblinOverlayStyles");
            if (uss == null)
                uss = LoadFromPackage<StyleSheet>("PerfGoblinOverlayStyles");

            _document.visualTreeAsset = uxml;

            _document.rootVisualElement.schedule.Execute(() =>
            {
                SetupReferences(uss);
                _initialized = true;
                SetVisible(_pendingVisible);
            });
        }

        void SetupReferences(StyleSheet uss)
        {
            var rootVE = _document.rootVisualElement;
            if (uss != null && !rootVE.styleSheets.Contains(uss))
                rootVE.styleSheets.Add(uss);

            _root = rootVE.Q("perfgoblin-root");
            if (_root == null)
            {
                Debug.LogWarning("[PerfGoblin] Could not find perfgoblin-root in UXML.");
                return;
            }

            var font = Font.CreateDynamicFontFromOSFont("Menlo", 12);
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Courier New", 12);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _root.style.unityFont = font;

            _body = _root.Q("pp-body");
            _minimizeBtn = _root.Q<Label>("pp-minimize");
            var header = _root.Q(className: "pp-header");
            if (_minimizeBtn != null)
            {
                _minimizeBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    _minimized = !_minimized;
                    _minimizeBtn.text = _minimized ? "+" : "—";
                    if (_body != null)
                        _body.style.display = _minimized ? DisplayStyle.None : DisplayStyle.Flex;
                    if (header != null)
                    {
                        header.style.borderBottomWidth = _minimized ? 0 : 1;
                        header.style.marginBottom = _minimized ? 0 : 10;
                        header.style.paddingBottom = _minimized ? 0 : 8;
                    }
                });
            }

            // Make header draggable
            if (header != null)
            {
                header.RegisterCallback<PointerDownEvent>(OnDragStart);
                header.RegisterCallback<PointerMoveEvent>(OnDragMove);
                header.RegisterCallback<PointerUpEvent>(OnDragEnd);
                header.RegisterCallback<PointerCaptureOutEvent>(evt => _isDragging = false);
            }

            _panelFps = _root.Q("panel-fps");
            _panelMemory = _root.Q("panel-memory");
            _panelScripts = _root.Q("panel-scripts");
            _panelSpikes = _root.Q("panel-spikes");

            _fpsValue = _root.Q<Label>("fps-value");
            _fpsTarget = _root.Q<Label>("fps-target");
            _fpsMs = _root.Q<Label>("fps-ms");
            _fpsMin = _root.Q<Label>("fps-min");
            _fpsAvg = _root.Q<Label>("fps-avg");
            _fpsMax = _root.Q<Label>("fps-max");
            _fpsGraph = _root.Q("fps-graph");

            _memTotal = _root.Q<Label>("mem-total");
            _memMode = _root.Q<Label>("mem-mode");
            _memEditorToggle = _root.Q<Label>("mem-editor-toggle");
            _memBar = _root.Q("mem-bar");
            _memLegend = _root.Q("mem-legend");

            if (_memEditorToggle != null)
            {
                UpdateEditorToggleState();
                _memEditorToggle.RegisterCallback<ClickEvent>(evt =>
                {
                    _config.ExcludeEditorOverhead = !_config.ExcludeEditorOverhead;
                    if (!_config.ExcludeEditorOverhead)
                        PerfGoblinManager.Memory?.ResetBaseline();
                    _config.Save();
                    UpdateEditorToggleState();
                });
            }

            _scriptsCount = _root.Q<Label>("scripts-count");
            _scriptsList = _root.Q("scripts-list");

            _spikesCount = _root.Q<Label>("spikes-count");
            _spikesList = _root.Q("spikes-list");

            if (_fpsGraph != null)
                _fpsGraph.generateVisualContent += DrawFpsGraph;

            ApplyPosition();

            _lastAppliedPosition = _config.Position;
            _lastAppliedScale = _config.OverlayScale;
            _lastAppliedOpacity = _config.OverlayOpacity;
        }

        static T LoadFromPackage<T>(string name) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets(name + " t:" + typeof(T).Name);
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("com.perfgoblin"))
                {
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                }
            }
#endif
            return null;
        }

        void ApplyPosition()
        {
            if (_root == null) return;

            if (!_isDragPositioned)
            {
                _root.style.top = StyleKeyword.Auto;
                _root.style.bottom = StyleKeyword.Auto;
                _root.style.left = StyleKeyword.Auto;
                _root.style.right = StyleKeyword.Auto;

                int margin = 10;
                switch (_config.Position)
                {
                    case OverlayPosition.TopLeft:
                        _root.style.top = margin;
                        _root.style.left = margin;
                        _root.style.transformOrigin = new TransformOrigin(0, 0);
                        break;
                    case OverlayPosition.TopRight:
                        _root.style.top = margin;
                        _root.style.right = margin;
                        _root.style.transformOrigin = new TransformOrigin(Length.Percent(100), 0);
                        break;
                    case OverlayPosition.BottomLeft:
                        _root.style.bottom = margin;
                        _root.style.left = margin;
                        _root.style.transformOrigin = new TransformOrigin(0, Length.Percent(100));
                        break;
                    case OverlayPosition.BottomRight:
                        _root.style.bottom = margin;
                        _root.style.right = margin;
                        _root.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(100));
                        break;
                }
            }

            _root.style.opacity = _config.OverlayOpacity;

            float scale = Mathf.Clamp(_config.OverlayScale, 0.5f, 3f);
            _root.transform.scale = new Vector3(scale, scale, 1f);
        }

        public void SetVisible(bool visible)
        {
            _pendingVisible = visible;
            if (!_initialized) return;

            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            // Also toggle on the document root to guarantee full hide
            if (_document != null && _document.rootVisualElement != null)
                _document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void UpdateUI()
        {
            if (_root == null) return;

            // Detect runtime config changes (e.g. position changed via Project Settings)
            if (_config.Position != _lastAppliedPosition)
            {
                _lastAppliedPosition = _config.Position;
                _isDragPositioned = false;
                ApplyPosition();
            }
            else if (Mathf.Abs(_config.OverlayScale - _lastAppliedScale) > 0.001f
                  || Mathf.Abs(_config.OverlayOpacity - _lastAppliedOpacity) > 0.001f)
            {
                _lastAppliedScale = _config.OverlayScale;
                _lastAppliedOpacity = _config.OverlayOpacity;
                ApplyPosition();
            }

            UpdatePanelVisibility();
            UpdateFpsPanel();
            UpdateMemoryPanel();
            UpdateScriptsPanel();
            UpdateSpikesPanel();
        }

        void UpdatePanelVisibility()
        {
            SetPanelVisible(_panelFps, (_config.VisiblePanels & ActivePanels.FPS) != 0);
            SetPanelVisible(_panelMemory, (_config.VisiblePanels & ActivePanels.Memory) != 0);
            SetPanelVisible(_panelScripts, (_config.VisiblePanels & ActivePanels.Scripts) != 0);
            SetPanelVisible(_panelSpikes, (_config.VisiblePanels & ActivePanels.Spikes) != 0);
        }

        static void SetPanelVisible(VisualElement panel, bool visible)
        {
            if (panel != null)
                panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #region FPS Panel

        void UpdateFpsPanel()
        {
            if (_fpsValue == null) return;

            var ft = PerfGoblinManager.FrameTime;
            if (ft == null) return;

            int fps = Mathf.RoundToInt(ft.CurrentFPS);
            _fpsValue.text = fps.ToString();

            SetFpsColor(_fpsValue, fps, ft.TargetFPS);

            // Alert highlight when FPS triggered auto-popup
            bool fpsAlert = IsAutoPopped && ft.TargetFPS > 0 && fps < ft.TargetFPS * 0.5f;
            if (fpsAlert)
                _fpsValue.AddToClassList("pp-alert-highlight-fps");
            else
                _fpsValue.RemoveFromClassList("pp-alert-highlight-fps");

            _sb.Clear();
            _sb.Append("/ ").Append(ft.TargetFPS).Append(" FPS");
            _fpsTarget.text = _sb.ToString();

            _sb.Clear();
            _sb.AppendFormat("{0:F1}ms", ft.CurrentFrameTimeMs);
            _fpsMs.text = _sb.ToString();

            if (_fpsMin != null)
            {
                _sb.Clear();
                _sb.Append("Min: ").Append(Mathf.RoundToInt(ft.MinFPS));
                _fpsMin.text = _sb.ToString();
            }
            if (_fpsAvg != null)
            {
                _sb.Clear();
                _sb.Append("Avg: ").Append(Mathf.RoundToInt(ft.AverageFPS));
                _fpsAvg.text = _sb.ToString();
            }
            if (_fpsMax != null)
            {
                _sb.Clear();
                _sb.Append("Max: ").Append(Mathf.RoundToInt(ft.MaxFPS));
                _fpsMax.text = _sb.ToString();
            }

            _fpsGraph?.MarkDirtyRepaint();
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

        void DrawFpsGraph(MeshGenerationContext ctx)
        {
            var ft = PerfGoblinManager.FrameTime;
            if (ft == null || ft.FrameTimeHistory.Count < 2) return;

            var painter = ctx.painter2D;
            var rect = _fpsGraph.contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;

            var history = ft.FrameTimeHistory;
            int count = history.Count;
            float targetMs = 1000f / ft.TargetFPS;
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

            // Target line
            float targetY = rect.height - Mathf.Clamp01(targetMs / maxMs) * rect.height;
            painter.strokeColor = new Color(0.94f, 0.62f, 0.14f, 0.4f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, targetY));
            painter.LineTo(new Vector2(rect.width, targetY));
            painter.Stroke();
        }

        #endregion

        #region Memory Panel

        void UpdateEditorToggleState()
        {
            if (_memEditorToggle == null) return;
            if (_config.ExcludeEditorOverhead)
            {
                _memEditorToggle.text = "Game Only";
                _memEditorToggle.AddToClassList("active");
            }
            else
            {
                _memEditorToggle.text = "All";
                _memEditorToggle.RemoveFromClassList("active");
            }
        }

        void UpdateMemoryPanel()
        {
            if (_memTotal == null) return;

            var mem = PerfGoblinManager.Memory;
            if (mem == null) return;

            if (_memMode != null)
                _memMode.text = mem.ExcludingEditor ? "(game only)" : "";

            _sb.Clear();
            FormatBytes(_sb, mem.TotalAllocatedBytes);
            _memTotal.text = _sb.ToString();

            UpdateMemoryBar(mem);
            UpdateMemoryLegend(mem);
        }

        void UpdateMemoryBar(MemoryTracker mem)
        {
            if (_memBar == null) return;

            long total = mem.TotalAllocatedBytes;
            if (total <= 0) return;

            var categories = mem.Categories;

            while (_memSegments.Count < categories.Count)
            {
                var seg = new VisualElement();
                seg.AddToClassList("pp-mem-segment");
                _memBar.Add(seg);
                _memSegments.Add(seg);
            }

            while (_memSegments.Count > categories.Count)
            {
                var last = _memSegments[_memSegments.Count - 1];
                _memBar.Remove(last);
                _memSegments.RemoveAt(_memSegments.Count - 1);
            }

            for (int i = 0; i < categories.Count; i++)
            {
                float pct = (float)categories[i].Bytes / total * 100f;
                _memSegments[i].style.width = Length.Percent(pct);
                _memSegments[i].style.backgroundColor = categories[i].DisplayColor;
            }
        }

        void UpdateMemoryLegend(MemoryTracker mem)
        {
            if (_memLegend == null) return;

            var categories = mem.Categories;

            while (_memLegendItems.Count < categories.Count)
            {
                var item = new VisualElement();
                item.AddToClassList("pp-mem-legend-item");

                var dot = new VisualElement();
                dot.AddToClassList("pp-mem-legend-dot");
                item.Add(dot);

                var label = new Label();
                label.AddToClassList("pp-mem-legend-label");
                item.Add(label);

                _memLegend.Add(item);
                _memLegendItems.Add(item);
            }

            for (int i = 0; i < categories.Count; i++)
            {
                var item = _memLegendItems[i];
                item.style.display = DisplayStyle.Flex;
                item.ElementAt(0).style.backgroundColor = categories[i].DisplayColor;

                _sb.Clear();
                _sb.Append(categories[i].Name).Append(" ");
                FormatBytes(_sb, categories[i].Bytes);
                ((Label)item.ElementAt(1)).text = _sb.ToString();
            }

            for (int i = categories.Count; i < _memLegendItems.Count; i++)
                _memLegendItems[i].style.display = DisplayStyle.None;
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

        #endregion

        #region Scripts Panel

        void UpdateScriptsPanel()
        {
            if (_scriptsList == null) return;

            var scripts = PerfGoblinManager.Scripts;
            if (scripts == null) return;

            var sorted = scripts.SortedScripts;

            _sb.Clear();
            _sb.Append(sorted.Count).Append(" active");
            if (scripts.TotalScriptTimeMs >= 0.01f)
                _sb.AppendFormat(" · {0:F1}ms", scripts.TotalScriptTimeMs);
            _scriptsCount.text = _sb.ToString();

            int hash = ComputeScriptListHash(sorted);
            if (hash == _lastScriptHash) return;
            _lastScriptHash = hash;

            EnsureRows(_scriptsList, _scriptRows, sorted.Count, CreateScriptRow);

            for (int i = 0; i < sorted.Count; i++)
            {
                var data = sorted[i];
                var row = _scriptRows[i];
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

                // Alert highlight when script triggered auto-popup
                if (IsAutoPopped && data.Status == PerformanceStatus.Bad)
                    row.AddToClassList("pp-alert-highlight");
                else
                    row.RemoveFromClassList("pp-alert-highlight");

                string shortName = ShortenScriptName(data.ScriptName);
                ((Label)row.ElementAt(1)).text = shortName;

                // Show per-script ms only for scripts with precise timing (manual or profiler recorder)
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

            for (int i = sorted.Count; i < _scriptRows.Count; i++)
                _scriptRows[i].style.display = DisplayStyle.None;
        }

        static string ShortenScriptName(string name)
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

        static int ComputeScriptListHash(IReadOnlyList<ScriptTimingData> list)
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

        static VisualElement CreateScriptRow()
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

            return row;
        }

        #endregion

        #region Spikes Panel

        void UpdateSpikesPanel()
        {
            if (_spikesList == null) return;

            var spikes = PerfGoblinManager.Spikes;
            if (spikes == null) return;

            _sb.Clear();
            _sb.Append(spikes.TotalSpikeCount).Append(" detected");
            _spikesCount.text = _sb.ToString();

            if (spikes.TotalSpikeCount == _lastSpikeCount) return;
            _lastSpikeCount = spikes.TotalSpikeCount;

            var recentList = new List<SpikeEvent>();
            foreach (var spike in spikes.RecentSpikes)
                recentList.Add(spike);

            EnsureRows(_spikesList, _spikeRows, recentList.Count, CreateSpikeRow);

            for (int i = 0; i < recentList.Count; i++)
            {
                var spike = recentList[i];
                var row = _spikeRows[i];
                row.style.display = DisplayStyle.Flex;

                // Alert highlight on the most recent spike when auto-popped
                if (IsAutoPopped && i == 0)
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
                // Show subsystem category if it has meaningful data (> 1ms)
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
                        _sb.Append(ShortenScriptName(top.ScriptName));
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

            for (int i = recentList.Count; i < _spikeRows.Count; i++)
                _spikeRows[i].style.display = DisplayStyle.None;
        }

        static VisualElement CreateSpikeRow()
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

            return row;
        }

        #endregion

        static void EnsureRows(VisualElement container, List<VisualElement> pool, int needed, Func<VisualElement> factory)
        {
            while (pool.Count < needed)
            {
                var row = factory();
                container.Add(row);
                pool.Add(row);
            }
        }

        #region Drag Handling

        void OnDragStart(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            // Don't start drag when clicking the minimize button
            if (evt.target is VisualElement ve && ve.name == "pp-minimize") return;

            _isDragging = true;
            _dragStartPointerPos = (Vector2)evt.position;
            _dragStartRootPos = new Vector2(_root.layout.x, _root.layout.y);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnDragMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;

            Vector2 delta = (Vector2)evt.position - _dragStartPointerPos;

            // Require a small movement before committing to drag (avoids accidental repositioning on click)
            if (!_isDragPositioned && delta.sqrMagnitude < 9f) return;

            _root.style.left = _dragStartRootPos.x + delta.x;
            _root.style.top = _dragStartRootPos.y + delta.y;
            _root.style.right = StyleKeyword.Auto;
            _root.style.bottom = StyleKeyword.Auto;
            _root.style.transformOrigin = new TransformOrigin(0, 0);

            _isDragPositioned = true;
            evt.StopPropagation();
        }

        void OnDragEnd(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        #endregion

        public void Dispose()
        {
            if (_fpsGraph != null)
                _fpsGraph.generateVisualContent -= DrawFpsGraph;
            if (_panelSettings != null)
                UnityEngine.Object.Destroy(_panelSettings);
        }
    }
}
#endif
