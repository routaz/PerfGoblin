#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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

        FpsPanelView _fpsView;
        MemoryPanelView _memoryView;
        ScriptsPanelView _scriptsView;
        SpikesPanelView _spikesView;

        bool _pendingVisible;
        bool _initialized;

        bool _isDragging;
        bool _isDragPositioned;
        Vector2 _dragStartPointerPos;
        Vector2 _dragStartRootPos;

        internal bool IsAutoPopped;

        OverlayPosition _lastAppliedPosition;
        float _lastAppliedScale;
        float _lastAppliedOpacity;

        PanelSettings _panelSettings;

        readonly IFrameTimeSource _frameTime;
        readonly IMemorySource _memory;
        readonly IScriptSource _scripts;
        readonly ISpikeSource _spikes;

        public PerfGoblinOverlay(GameObject host, PerfGoblinConfig config,
            IFrameTimeSource frameTime, IMemorySource memory,
            IScriptSource scripts, ISpikeSource spikes)
        {
            _config = config;
            _frameTime = frameTime;
            _memory = memory;
            _scripts = scripts;
            _spikes = spikes;

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

            var installedFonts = new System.Collections.Generic.HashSet<string>(Font.GetOSInstalledFontNames());
            string[] preferredFonts = { "Consolas", "Menlo", "Monaco", "Courier New" };
            Font font = null;
            foreach (var name in preferredFonts)
            {
                if (installedFonts.Contains(name))
                {
                    font = Font.CreateDynamicFontFromOSFont(name, 12);
                    break;
                }
            }
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

            _fpsView = new FpsPanelView(_root, _frameTime, _sb);
            _memoryView = new MemoryPanelView(_root, _config, _memory, _sb);
            _scriptsView = new ScriptsPanelView(_root, _scripts, _sb);
            _spikesView = new SpikesPanelView(_root, _spikes, _sb);

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
            _root.style.scale = new Scale(new Vector2(scale, scale));
        }

        public void SetVisible(bool visible)
        {
            _pendingVisible = visible;
            if (!_initialized) return;

            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_document != null && _document.rootVisualElement != null)
                _document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void UpdateUI()
        {
            if (_root == null) return;

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

            _fpsView?.SetAutoPopped(IsAutoPopped);
            _scriptsView?.SetAutoPopped(IsAutoPopped);
            _spikesView?.SetAutoPopped(IsAutoPopped);

            _fpsView?.Update();
            _memoryView?.Update();
            _scriptsView?.Update();
            _spikesView?.Update();
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

        #region Drag Handling

        void OnDragStart(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
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
            _fpsView?.Dispose();
            if (_panelSettings != null)
                UnityEngine.Object.Destroy(_panelSettings);
        }
    }
}
#endif
