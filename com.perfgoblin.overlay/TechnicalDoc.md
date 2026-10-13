# PerfGoblin Technical Documentation

## Overview

PerfGoblin is a development-only Unity performance overlay distributed as a UPM package (`com.perfgoblin.overlay`). It renders a real-time HUD in the Game View during Play Mode showing FPS, memory breakdown, per-script timing, and frame spike detection.

**Unity version:** 2022.3+ (LTS) / Unity 6  
**Build stripping:** All code is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and the runtime assembly definition uses a matching define constraint. Release builds contain zero PerfGoblin code.

---

## Package Structure

```
com.perfgoblin.overlay/
├── package.json
├── README.md
├── LICENSE.md
├── CHANGELOG.md
├── TechnicalDoc.md
├── Runtime/
│   ├── PerfGoblin.Runtime.asmdef
│   ├── PerfGoblinManager.cs          # Singleton lifecycle manager
│   ├── PerfGoblinOverlay.cs          # UI Toolkit overlay controller
│   ├── Trackers/
│   │   ├── FrameTimeTracker.cs       # FPS & frame time measurement
│   │   ├── MemoryTracker.cs          # Memory breakdown via Profiler APIs
│   │   ├── ScriptTracker.cs          # Per-script performance tracking
│   │   └── SpikeDetector.cs          # Frame spike detection & attribution
│   ├── Data/
│   │   ├── PerfGoblinConfig.cs       # Configuration (persisted via EditorPrefs)
│   │   └── RingBuffer.cs             # Generic circular buffer
│   └── Resources/
│       ├── PerfGoblinOverlayDocument.uxml
│       └── PerfGoblinOverlayStyles.uss
├── Editor/
│   ├── PerfGoblin.Editor.asmdef
│   ├── PerfGoblinSettingsProvider.cs  # Project Settings page
│   ├── PerfGoblinEditorWindow.cs      # Window > PerfGoblin > Settings
│   └── PerfGoblinPanelSettingsCreator.cs  # Auto-creates PanelSettings asset
└── Samples~/
    └── DemoScene/
        ├── DemoScene.unity
        └── StressTestScripts/
            ├── HeavyUpdateScript.cs
            ├── AllocatingScript.cs
            └── SpikyScript.cs
```

### Assembly Definitions

| Assembly | Name | References | Platform | Define Constraints |
|----------|------|------------|----------|--------------------|
| Runtime | `PerfGoblin.Runtime` | None | All | `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| Editor | `PerfGoblin.Editor` | `PerfGoblin.Runtime` | Editor only | None |

---

## Lifecycle & Entry Point

### Initialization

PerfGoblin bootstraps itself automatically. There is no manual setup required.

```
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
                    │
                    ▼
        PerfGoblinManager.Initialize()
                    │
                    ▼
        Creates "[PerfGoblin]" GameObject
        DontDestroyOnLoad, HideFlags.NotEditable
                    │
                    ▼
        PerfGoblinManager.Awake()
            ├── PerfGoblinConfig.Load()           ← EditorPrefs / PlayerPrefs
            ├── new FrameTimeTracker(config)
            ├── new MemoryTracker(config)
            ├── new ScriptTracker(config)
            ├── new SpikeDetector(config, frameTime, scripts)
            └── new PerfGoblinOverlay(gameObject, config)
                    ├── AddComponent<UIDocument>()
                    ├── Load PanelSettings (Resources or create)
                    ├── Load UXML + USS from Resources
                    └── Schedule SetupReferences() (next frame)
```

The `[DefaultExecutionOrder(-10000)]` attribute on `PerfGoblinManager` ensures it runs before all user scripts, which is critical for accurate timing measurement.

### Frame Update Loop

Each frame follows this execution order:

```
PerfGoblinManager.Update()          [execution order -10000, runs first]
    ├── Input.GetKeyDown(ToggleKey)  → toggle overlay visibility
    ├── FrameTimeTracker.Update()    → measure dt, update EMA, push to history
    ├── MemoryTracker.Update()       → sample memory (every N frames)
    └── ScriptTracker.Update()       → reset frame data, periodic MonoBehaviour scan,
                                       start Stopwatch for total script time

        ... all user Update() callbacks run here ...

PerfGoblinManager.LateUpdate()
    ├── FrameTimeTracker.LateUpdate()
    ├── ScriptTracker.LateUpdate()   → stop Stopwatch, read ProfilerRecorders,
    │                                  update stats, rebuild sorted list
    ├── SpikeDetector.Update()       → check threshold, snapshot offenders
    └── PerfGoblinOverlay.UpdateUI() → bind data to UI elements (if visible)
```

### Shutdown

```
PerfGoblinManager.OnDestroy()
    ├── MemoryTracker.Dispose()      → release ProfilerRecorders
    ├── ScriptTracker.Dispose()      → release ProfilerRecorders, unsubscribe events
    └── PerfGoblinOverlay.Dispose()  → unregister generateVisualContent, destroy PanelSettings
```

---

## Core Scripts

### PerfGoblinManager.cs

**Role:** Singleton coordinator. Owns all tracker instances and the overlay. Acts as the public API surface for manual profiling.

**Singleton pattern:** Lazy instantiation via `[RuntimeInitializeOnLoadMethod]`. The static `s_Instance` field prevents duplicates; a second `Awake()` call destroys itself.

**Public API:**

```csharp
// Toggle overlay
public static bool IsVisible { get; set; }

// Manual profiling markers
public static void BeginSample(string name);   // starts Stopwatch
public static void EndSample(string name);     // stops Stopwatch, records to ScriptTracker

// Access trackers
public static PerfGoblinConfig Config { get; }
public static FrameTimeTracker FrameTime { get; }
public static MemoryTracker Memory { get; }
public static ScriptTracker Scripts { get; }
public static SpikeDetector Spikes { get; }
```

**Manual sampling** uses `System.Diagnostics.Stopwatch` for high-precision timing. Results are stored in a static `Dictionary<string, ScriptTimingData>` and forwarded to `ScriptTracker.RecordManualSample()` so they appear in the Scripts panel alongside auto-detected entries.

---

### FrameTimeTracker.cs

**Role:** Measures FPS and frame time with smoothing, min/max/avg statistics, and history for the graph.

**Key properties:**

| Property | Type | Description |
|----------|------|-------------|
| `CurrentFPS` | float | Exponential moving average FPS |
| `CurrentFrameTimeMs` | float | Raw `Time.unscaledDeltaTime * 1000` |
| `AverageFPS` | float | Mean over 2-second rolling window |
| `MinFPS` / `MaxFPS` | float | Extremes in current window |
| `TargetFPS` | int | From `Application.targetFrameRate` or screen refresh rate |
| `FrameTimeHistory` | `RingBuffer<float>` | Last N frame times (default 300) |

**Smoothing algorithm:** Exponential Moving Average (EMA). The smoothing factor `alpha` is derived from the configured window size and the current delta time:

```csharp
float alpha = 2f / (config.FpsSmoothingWindow / dt + 1f);
smoothedFps = Lerp(smoothedFps, instantFps, alpha);
```

Uses `Time.unscaledDeltaTime` so pausing or slow-motion doesn't distort readings.

**Target FPS detection:** Checks `Application.targetFrameRate` first; if -1, falls back to `Screen.currentResolution.refreshRateRatio` (Unity 2022.3+), then defaults to 60.

---

### MemoryTracker.cs

**Role:** Samples memory usage via Unity's `Profiler` APIs and `ProfilerRecorder`, with optional editor overhead subtraction.

**Data sources:**

| API Call | What it measures |
|----------|------------------|
| `Profiler.GetTotalAllocatedMemoryLong()` | Total managed + native |
| `Profiler.GetTotalReservedMemoryLong()` | Total reserved |
| `Profiler.GetAllocatedMemoryForGraphicsDriver()` | GPU/GFX memory |
| `Profiler.GetMonoUsedSizeLong()` | Managed heap used |
| `Profiler.GetMonoHeapSizeLong()` | Managed heap reserved |
| `ProfilerRecorder("Texture Memory")` | Texture allocations |
| `ProfilerRecorder("Mesh Memory")` | Mesh allocations |
| `ProfilerRecorder("Audio Used Memory")` | Audio allocations |

**Sampling rate:** Only queries every `Config.MemorySampleInterval` frames (default 30) to avoid performance impact from expensive profiler calls.

**Editor overhead exclusion:** When `Config.ExcludeEditorOverhead` is enabled, the tracker captures a baseline on the first sample frame and subtracts it from all subsequent readings. This gives an approximate "game only" view by removing the editor's own memory footprint. The baseline can be reset via `ResetBaseline()`.

**Category breakdown:** Six categories with fixed colors:

| Category | Color | Source |
|----------|-------|--------|
| Textures | Purple (0.55, 0.36, 0.96) | ProfilerRecorder |
| Meshes | Green (0.13, 0.77, 0.37) | ProfilerRecorder |
| Audio | Orange (0.96, 0.62, 0.14) | ProfilerRecorder |
| Managed | Blue (0.23, 0.51, 0.96) | Profiler API |
| GFX | Red (0.94, 0.27, 0.27) | Profiler API |
| Other | Gray (0.5, 0.5, 0.5) | Total minus accounted |

---

### ScriptTracker.cs

**Role:** Detects active MonoBehaviours and measures their per-frame cost. Two measurement strategies with automatic fallback.

**Auto-detection:** Every `Config.ScriptScanIntervalSeconds` seconds (and on scene load), scans all MonoBehaviours via `FindObjectsByType<MonoBehaviour>()`. For each unique type:

1. Skips `UnityEngine.*` and core `PerfGoblin` namespaces
2. Uses reflection to check for `Update()`, `FixedUpdate()`, `LateUpdate()` methods (DeclaredOnly)
3. Attempts to create `ProfilerRecorder` instances using multiple marker name formats:
   - `FullName.Method()`, `ShortName.Method()`, `FullName.Method`, `ShortName.Method`

**Measurement strategies:**

1. **ProfilerRecorder (primary):** Reads per-script timing from Unity's profiler markers. Requires deep profiling for individual MonoBehaviour methods to be visible. Values are in nanoseconds, converted to milliseconds.

2. **Stopwatch fallback:** Measures total time between `PerfGoblinManager.Update()` (execution order -10000) and `LateUpdate()`. If no ProfilerRecorder data is available, distributes total time evenly across detected scripts. This is a rough estimate but shows relative impact.

**Status thresholds:**

| Status | Condition | Visual |
|--------|-----------|--------|
| Ok | < `ScriptWarnThresholdMs` (default 1ms) | Green dot |
| Warn | 1ms - `ScriptBadThresholdMs` (default 4ms) | Yellow dot |
| Bad | > `ScriptBadThresholdMs` | Red dot |

**ScriptTimingData** tracks per-script: `LastFrameMs`, `AverageMs` (rolling), `PeakMs`, `CallCount`, `Status`.

**Display:** Scripts are sorted by `LastFrameMs` descending (worst offenders first), capped at `Config.MaxDisplayedScripts`.

---

### SpikeDetector.cs

**Role:** Identifies frames that exceed a configurable threshold and attributes them to the most expensive scripts.

**Detection logic:**

```
spike triggered when: frameTimeMs > (1000 / targetFPS) * Config.SpikeMultiplier
```

Default: a spike is any frame that takes more than 2x the target frame time (e.g., > 33.3ms at 60 FPS).

**On spike detection:**

1. Snapshots the top 3 scripts from `ScriptTracker.SortedScripts` (copies data to preserve state)
2. Records a `SpikeEvent` with frame count, frame time, expected time, offenders, and timestamp
3. Pushes to a `RingBuffer<SpikeEvent>` (default capacity 100)
4. Increments a 10-second rolling window counter for frequency calculation

**Public properties:**

| Property | Description |
|----------|-------------|
| `RecentSpikes` | Last 10 spikes (newest first) |
| `SpikeFrequency` | Spikes per second over last 10 seconds |
| `MostFrequentOffender` | Script name appearing most across all recorded spikes |
| `TotalSpikeCount` | Total spikes in the ring buffer |

---

## UI System

### PerfGoblinOverlay.cs

**Role:** Builds and updates the UI Toolkit overlay. Not a MonoBehaviour; owned by PerfGoblinManager.

**Initialization:**

1. Adds `UIDocument` component to the manager GameObject (sort order 10000)
2. Loads or creates `PanelSettings` (scale mode: ScaleWithScreenSize, 1920x1080 reference)
3. Loads `PerfGoblinOverlayDocument.uxml` and `PerfGoblinOverlayStyles.uss` from Resources
4. On next frame (`schedule.Execute`), resolves all named elements and registers event handlers
5. Assigns an OS monospace font (`Menlo` > `Consolas` > `Courier New` > `LegacyRuntime.ttf`)

**UI element tree:**

```
perfgoblin-root (.perfgoblin-overlay)
├── Header (.pp-header)
│   ├── "PERFGOBLIN" label
│   ├── Version label ("v0.2.0")
│   └── Minimize button ("—" / "+")
└── Body (#pp-body) — hidden when minimized
    ├── FPS Panel (#panel-fps)
    │   ├── FPS value (large, color-coded), target, frame time ms
    │   ├── Min / Avg / Max stats
    │   └── Graph (generateVisualContent → Painter2D line + target line)
    ├── Memory Panel (#panel-memory)
    │   ├── Header with "All"/"Game Only" toggle + total
    │   ├── Stacked bar (% width per category)
    │   └── Legend (color dots + labels)
    ├── Scripts Panel (#panel-scripts)
    │   ├── Header with active count
    │   └── ScrollView with script rows (dot + name + ms)
    └── Spikes Panel (#panel-spikes)
        ├── Header with detected count
        └── ScrollView with spike rows (frame# + ms + offender)
```

**Performance optimizations:**

- All `VisualElement` references cached in `Awake`, never queried per frame
- `StringBuilder` reused for all string formatting
- Script/spike rows pooled and reused (only created when needed, hidden when excess)
- Script list only rebuilds when a hash of the sorted data changes
- Spike list only rebuilds when `TotalSpikeCount` changes
- Memory legend items pooled and toggled via `DisplayStyle`

**FPS graph rendering:** Uses `generateVisualContent` callback with `Painter2D`:

- Green line: frame time history normalized to 3x target frame time
- Orange line: target frame time reference line
- Repaints every frame via `MarkDirtyRepaint()`

**Interactive elements:**

- **Minimize button:** Toggles `pp-body` display between `Flex` and `None`. Removes header border when minimized.
- **Memory toggle:** Clicks toggle `Config.ExcludeEditorOverhead`, saves config, resets baseline if switching off. Visual state via `.active` CSS class.

**Position & scale:** Applied via inline styles in `ApplyPosition()`. The `transform-origin` is set to match the corner position so scaling expands inward.

---

## Data Structures

### RingBuffer\<T\>

Generic fixed-capacity circular buffer used by `FrameTimeTracker` (frame history) and `SpikeDetector` (spike history).

```csharp
public class RingBuffer<T>
{
    public RingBuffer(int capacity);
    public void Push(T item);          // O(1), overwrites oldest when full
    public T this[int index] { get; }  // 0 = oldest, Count-1 = newest
    public int Count { get; }
    public int Capacity { get; }
    public T Latest { get; }
    public IEnumerable<T> AsEnumerable();
    public void Clear();
}
```

Internally uses a fixed array with a head pointer and modulo arithmetic. Zero allocation during normal operation.

### PerfGoblinConfig

Serializable configuration class. All fields have sensible defaults. Persisted as JSON via `EditorPrefs` (editor) or `PlayerPrefs` (development builds) under key `"PerfGoblin_Config"`.

Editable at runtime via **Project Settings > PerfGoblin** or **Window > PerfGoblin > Settings**.

---

## Editor Integration

### PerfGoblinSettingsProvider.cs

Registers under `Project/PerfGoblin` via `[SettingsProvider]`. Exposes all `PerfGoblinConfig` fields organized in foldout sections: General, FPS, Memory, Scripts, Spikes, Overlay. Auto-saves on change via `EditorGUI.BeginChangeCheck()`.

### PerfGoblinEditorWindow.cs

Accessible via **Window > PerfGoblin > Settings**. Shows live status (Play Mode, PerfGoblin running, overlay visible) and provides buttons to open Project Settings or toggle the overlay.

### PerfGoblinPanelSettingsCreator.cs

`[InitializeOnLoad]` static class that runs on editor startup. Creates `Assets/Resources/PerfGoblinPanelSettings.asset` and `Assets/Resources/PerfGoblinTheme.asset` if they don't exist. This ensures the `UIDocument` has a valid `PanelSettings` with a `ThemeStyleSheet` assigned.

---

## Sample Scripts

Located in `Samples~/DemoScene/StressTestScripts/`, importable via Package Manager.

| Script | Purpose | Configurable |
|--------|---------|-------------|
| `HeavyUpdateScript` | CPU-bound `Update()` with nested math loops | `iterations` (100–10000) |
| `AllocatingScript` | GC pressure via per-frame string/list allocation | `allocationsPerFrame` (10–1000) |
| `SpikyScript` | Periodic frame spike simulation via busy-wait | `spikeIntervalSeconds` (1–10), `spikeMs` (10–100) |

All wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

---

## Performance Budget

PerfGoblin targets minimal self-overhead:

| Metric | Target |
|--------|--------|
| CPU | < 0.5ms per frame |
| Memory | < 2MB steady-state |
| GC | Zero per-frame allocations in update loop |
| GPU | Negligible (UI Toolkit batched rendering) |

**Strategies employed:**

- All VisualElement references cached at setup, never queried per frame
- StringBuilder reused for all string formatting
- Memory tracker samples every N frames (default 30), not every frame
- Script scanner runs every N seconds (default 5), not every frame
- UI rows pooled and reused; script list only rebuilds on data change
- RingBuffer uses fixed arrays with no allocation during push

---

## Configuration Reference

| Setting | Key | Default | Range | Description |
|---------|-----|---------|-------|-------------|
| Toggle Key | `ToggleKey` | F3 | Any KeyCode | Hotkey to show/hide overlay |
| Show On Start | `ShowOnPlayModeStart` | true | bool | Auto-show when entering Play Mode |
| FPS Smoothing | `FpsSmoothingWindow` | 0.5s | 0.1–2.0 | EMA window for FPS display |
| Frame History | `FrameHistoryCapacity` | 300 | 60–600 | Number of frames in graph |
| Memory Interval | `MemorySampleInterval` | 30 | 1–120 | Frames between memory samples |
| Exclude Editor | `ExcludeEditorOverhead` | false | bool | Subtract editor baseline from memory |
| Script Warn | `ScriptWarnThresholdMs` | 1.0 | 0.1–10 | Yellow status threshold (ms) |
| Script Bad | `ScriptBadThresholdMs` | 4.0 | 0.5–20 | Red status threshold (ms) |
| Auto Detect | `AutoDetectScripts` | true | bool | Scan for MonoBehaviours |
| Scan Interval | `ScriptScanIntervalSeconds` | 5 | 1–30 | Seconds between scans |
| Max Scripts | `MaxDisplayedScripts` | 10 | 3–30 | Maximum scripts shown |
| Spike Multiplier | `SpikeMultiplier` | 2.0 | 1.1–5.0 | Frame time multiplier for spike |
| Spike History | `SpikeHistoryCapacity` | 100 | 10–500 | Max stored spike events |
| Position | `Position` | TopRight | Enum | Overlay corner |
| Compact Mode | `CompactMode` | false | bool | (reserved for future use) |
| Opacity | `OverlayOpacity` | 0.92 | 0.3–1.0 | Panel transparency |
| Scale | `OverlayScale` | 1.5 | 0.5–3.0 | UI scale factor |
| Visible Panels | `VisiblePanels` | All | Flags | Which panels to show |
