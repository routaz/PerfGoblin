# PerfGoblin PROTOTYPE

Lightweight development-only performance overlay for Unity. Provides real-time FPS, memory, script performance, and frame spike insights directly in the Game View during Play Mode.

**Unity 2022.3+ (LTS) required.** All code is stripped from release builds automatically.

## Installation

### Via Unity Package Manager (git URL)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL**
3. Enter the repository URL pointing to the `com.perfgoblin.overlay` folder

### Via local path

1. Clone this repository
2. In Package Manager, click **+** > **Add package from disk**
3. Navigate to `com.perfgoblin.overlay/package.json`

## Usage

PerfGoblin is **enabled by default**. When enabled, it activates automatically when entering Play Mode. Press **F3** to toggle the overlay.

To disable PerfGoblin without removing the package, uncheck **Enabled** in **Edit > Project Settings > PerfGoblin**. When disabled, no GameObject, trackers, or profiler recorders are created — zero overhead.

### Panels

- **FPS** — Smoothed FPS counter, frame time (ms), min/max/avg stats, and a rolling frame time graph
- **Memory** — Total allocation with category breakdown bar (Textures, Meshes, Audio, Managed, GFX)
- **Scripts** — Active MonoBehaviours sorted by cost, with color-coded status dots (green/yellow/red)
- **Spikes** — Detected frame spikes with the top offending script attributed

### Manual Profiling Markers

Add custom profiling sections to your code:

```csharp
PerfGoblin.PerfGoblinManager.BeginSample("AI Pathfinding");
// ... expensive code ...
PerfGoblin.PerfGoblinManager.EndSample("AI Pathfinding");
```

These appear in the Scripts panel alongside automatic detections.

## Configuration

Open **Edit > Project Settings > PerfGoblin** to configure:

| Setting | Default | Description |
|---------|---------|-------------|
| Toggle Key | F3 | Hotkey to show/hide overlay |
| Show On Play Mode Start | true | Auto-show when entering Play Mode |
| FPS Smoothing Window | 0.5s | Exponential moving average window |
| Memory Sample Interval | 30 frames | How often to query memory stats |
| Script Warn Threshold | 1.0ms | Yellow status threshold |
| Script Bad Threshold | 4.0ms | Red status threshold |
| Auto Detect Scripts | true | Scan for MonoBehaviours automatically |
| Spike Multiplier | 2.0x | Frame time multiplier for spike detection |
| Spike Display Duration | 30s | How long spikes stay visible in the list |
| Overlay Position | Top Right | Corner placement |
| Overlay Opacity | 0.92 | Panel transparency |

## Architecture

PerfGoblin follows SOLID principles with clear separation of concerns:

- **Tracker interfaces** (`IFrameTimeSource`, `IMemorySource`, `IScriptSource`, `ISpikeSource`) — define read-only contracts for each metric, enabling dependency injection and testability
- **Tracker implementations** (`FrameTimeTracker`, `MemoryTracker`, `ScriptTracker`, `SpikeDetector`) — collect performance data from Unity's Profiler API
- **Panel views** (`FpsPanelView`, `MemoryPanelView`, `ScriptsPanelView`, `SpikesPanelView`) — each panel owns its UI elements and update logic
- **AutoPopupController** — encapsulates auto-show/auto-dismiss logic, separate from the overlay
- **PerfGoblinOverlay** — coordinates panel views, handles drag and visibility
- **PerfGoblinManager** — orchestrates initialization and the update loop

## Build Stripping

All PerfGoblin code uses `#if UNITY_EDITOR || DEVELOPMENT_BUILD` preprocessor guards and the Runtime assembly definition has a matching define constraint. Release builds contain zero PerfGoblin code.

## Performance Budget

PerfGoblin targets the following overhead limits, validated by automated Play Mode benchmarks in `Assets/Tests/PlayMode/PerformanceBudgetTests.cs`:

| Metric | Budget | How it's measured |
|--------|--------|-------------------|
| CPU overhead | < 0.5ms per frame | Stopwatch around all tracker Update/LateUpdate calls, averaged over 120 frames |
| Memory footprint | < 2 MB | `Profiler.GetTotalAllocatedMemoryLong()` delta before/after initialization + warmup |
| GC allocations | ~0 bytes per frame | `Profiler.GetMonoUsedSizeLong()` delta per frame, averaged over 120 frames |

Run these benchmarks via **Window > General > Test Runner > PlayMode** tab.

## Sample Scene

Import the sample via Package Manager to get a demo scene with stress test scripts:
- **HeavyUpdateScript** — Expensive math in Update
- **AllocatingScript** — GC garbage every frame
- **SpikyScript** — Periodic large frame spikes

## Testing

Unit tests live in the host project under `Assets/Tests/EditMode/`, not in the package itself. This keeps the package lightweight while enabling full test coverage.

To run tests: open **Window > General > Test Runner**, select the **EditMode** tab, and click **Run All**.

Test coverage includes:
- **RingBuffer** — capacity, wrap-around, indexing, enumeration
- **ScriptTimingData** — accumulation, status thresholds, peak tracking
- **AutoPopupController** — FPS/script/spike triggers, manual toggle, edge cases
- **ScriptTracker/ScriptsPanelView** — class name extraction, name shortening
- **PerfGoblinConfig** — default values, reset behavior

Mock implementations of all tracker interfaces (`MockFrameTimeSource`, `MockScriptSource`, `MockSpikeSource`, `MockMemorySource`) are provided in `Assets/Tests/EditMode/Mocks/`.

## Known Limitations

- Main thread profiling only (no Job System / async)
- No GPU timing (future: FrameTimingManager)
- No persistent session logging
- No network overlay for multiplayer
- No custom panel API

## License

MIT
