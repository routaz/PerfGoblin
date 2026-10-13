# PerfGoblin

A lightweight performance overlay for Unity, for use during development. It shows FPS, memory, script
cost, and frame spikes directly in the Game view during Play Mode, so you notice regressions while you
work instead of in a profiling session later.

**Requires Unity 2022.3 LTS or newer.** PerfGoblin is compiled only in the editor and development builds.

## Installation

In the Package Manager, choose **+ > Add package from git URL** and enter:

```
https://github.com/routaz/PerfGoblin.git?path=/com.perfgoblin.overlay
```

To work from a local clone instead, choose **+ > Add package from disk** and select
`com.perfgoblin.overlay/package.json`.

## Usage

PerfGoblin starts automatically when you enter Play Mode. Press **F3** to show or hide the overlay.
Drag the overlay to move it.

Hotkeys read input through the legacy Input Manager. Set **Project Settings > Player > Active Input
Handling** to *Input Manager (Old)* or *Both*.

To turn PerfGoblin off without removing the package, clear **Enabled** in
**Edit > Project Settings > PerfGoblin**. When it is off, no GameObject, trackers, or profiler
recorders are created, so it adds no overhead.

### Panels

- **FPS**: smoothed FPS, frame time in ms, min/max/average, and a rolling frame-time graph.
- **Memory**: total allocated memory, with a breakdown bar for textures, meshes, audio, managed, and graphics memory.
- **Scripts**: active MonoBehaviours sorted by cost, each with a green, yellow, or red status dot.
- **Spikes**: recent frame spikes, each attributed to an engine subsystem (rendering, physics, GC,
  and so on) and the most expensive scripts in that frame.

### Timing your own code

Wrap code in named samples to get precise timings for it. PerfGoblin is not compiled into release
builds, so guard the calls to keep those builds compiling:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
PerfGoblin.PerfGoblinProfiler.BeginSample("AI Pathfinding");
#endif
// ... code to measure ...
#if UNITY_EDITOR || DEVELOPMENT_BUILD
PerfGoblin.PerfGoblinProfiler.EndSample("AI Pathfinding");
#endif
```

Samples appear in the Scripts panel alongside the automatically detected scripts, and are listed
before them.

### Auto popup

With **Auto Popup On Threshold** enabled, a hidden overlay opens by itself when FPS drops below half
the target frame rate, a script crosses the red threshold, or a new spike is detected. Set
**Auto Dismiss** to close it again once things recover.

## Configuration

All settings are in **Edit > Project Settings > PerfGoblin**.

| Group | Setting | Default | Description |
|-------|---------|---------|-------------|
| General | Enabled | On | Turns PerfGoblin on or off entirely |
| | Toggle Key | F3 | Shows or hides the overlay |
| | Show On Play Mode Start | On | Opens the overlay when entering Play Mode |
| Auto popup | Auto Popup On Threshold | Off | Opens the overlay when a threshold is crossed |
| | Popup Cooldown | 5 s | Minimum time between automatic popups |
| | Auto Dismiss | 0 s | Closes an auto-opened overlay after this long without issues (0 = never) |
| FPS | Smoothing Window | 0.5 s | Window for the smoothed FPS value |
| | Frame History Capacity | 300 | Frames kept for the graph and statistics |
| Memory | Sample Interval | 30 frames | How often memory is sampled |
| | Exclude Editor Overhead | Off | Subtracts the editor's baseline memory, captured at the first sample |
| Scripts | Auto Detect Scripts | On | Finds active MonoBehaviours automatically |
| | Scan Interval | 5 s | How often to look for new scripts |
| | Max Displayed Scripts | 10 | Rows shown in the Scripts panel |
| | Warn Threshold | 1.0 ms | Cost at which a script turns yellow |
| | Bad Threshold | 4.0 ms | Cost at which a script turns red |
| Spikes | Spike Multiplier | 2.0× | A frame this many times slower than expected counts as a spike |
| | Display Duration | 30 s | How long a spike stays in the list |
| | Spike History Capacity | 100 | Spikes kept in history |
| Overlay | Position | Top Right | Starting corner |
| | Compact Mode | Off | Smaller layout |
| | Opacity | 0.92 | Panel opacity |
| | Scale | 1.5 | Overall size |
| | Visible Panels | All | Which panels are shown |

## Performance budget

PerfGoblin is meant to stay out of the way. Automated Play Mode benchmarks in
`Assets/Tests/PlayMode/PerformanceBudgetTests.cs` check these limits:

| Metric | Budget | How it is measured |
|--------|--------|--------------------|
| CPU overhead | < 0.5 ms per frame | Stopwatch around all tracker `Update`/`LateUpdate` calls, averaged over 120 frames |
| Memory footprint | < 2 MB | `Profiler.GetTotalAllocatedMemoryLong()` before and after initialization and warm-up |
| GC allocations | ~0 bytes per frame | `Profiler.GetMonoUsedSizeLong()` change per frame, averaged over 120 frames |

Run them from **Window > General > Test Runner**, on the **PlayMode** tab.

## Build stripping

The runtime assembly has the define constraint `UNITY_EDITOR || DEVELOPMENT_BUILD`, so it is compiled
only in the editor and in development builds. Release builds contain no PerfGoblin code, so any of
your own code that calls PerfGoblin must be guarded with `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

## Sample

In the Package Manager, select PerfGoblin and choose **Samples > Demo Scene > Import**. The sample
contains `DemoScene.unity` and three stress-test scripts:

- **HeavyUpdateScript**: expensive math in `Update`.
- **AllocatingScript**: allocates garbage every frame.
- **SpikyScript**: causes periodic large frame spikes.

## Architecture

- **Tracker interfaces** (`IFrameTimeSource`, `IMemorySource`, `IScriptSource`, `ISpikeSource`):
  read-only contracts for each metric, so views and tests depend on abstractions.
- **Trackers** (`FrameTimeTracker`, `MemoryTracker`, `ScriptTracker`, `SpikeDetector`): collect data
  from Unity's Profiler API.
- **Panel views** (`FpsPanelView`, `MemoryPanelView`, `ScriptsPanelView`, `SpikesPanelView`): each
  owns its UI elements and update logic.
- **`AutoPopupController`**: decides when the overlay opens and closes by itself.
- **`PerfGoblinOverlay`**: lays out the panels and handles dragging and visibility.
- **`PerfGoblinManager`**: initializes everything and runs the update loop.

## Testing

Tests are not shipped with the package. They live in the development project under `Assets/Tests/`.
Run them from **Window > General > Test Runner**.

The EditMode tests cover:

- **RingBuffer**: capacity, wrap-around, indexing, and enumeration.
- **ScriptTimingData**: accumulation, status thresholds, and peak tracking.
- **AutoPopupController**: FPS, script, and spike triggers, manual toggling, and edge cases.
- **ScriptTracker / ScriptsPanelView**: class name extraction and name shortening.
- **PerfGoblinConfig**: default values and reset behavior.

Mocks of every tracker interface are in `Assets/Tests/EditMode/Mocks/`.

## Known limitations

- Only the main thread is profiled. Jobs and async work are not measured.
- No GPU timing yet (planned: `FrameTimingManager`).
- No session logging.
- No overlay for multiplayer or network stats.
- No API for custom panels.

## License

MIT. See [LICENSE](LICENSE.md).
