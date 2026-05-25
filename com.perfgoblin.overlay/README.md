# PerfGoblin

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

PerfGoblin activates automatically when entering Play Mode. Press **F3** to toggle the overlay.

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
| Overlay Position | Top Right | Corner placement |
| Overlay Opacity | 0.92 | Panel transparency |

## Build Stripping

All PerfGoblin code uses `#if UNITY_EDITOR || DEVELOPMENT_BUILD` preprocessor guards and the Runtime assembly definition has a matching define constraint. Release builds contain zero PerfGoblin code.

## Performance Budget

PerfGoblin targets less than 0.5ms CPU overhead, under 2MB memory, and zero per-frame GC allocations.

## Sample Scene

Import the sample via Package Manager to get a demo scene with stress test scripts:
- **HeavyUpdateScript** — Expensive math in Update
- **AllocatingScript** — GC garbage every frame
- **SpikyScript** — Periodic large frame spikes

## Known Limitations

- Main thread profiling only (no Job System / async)
- No GPU timing (future: FrameTimingManager)
- No persistent session logging
- No network overlay for multiplayer
- No custom panel API

## License

MIT
