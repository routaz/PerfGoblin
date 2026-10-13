# Changelog

## [Unreleased]

### Added
- `LICENSE` at the repository root; `package.json` now declares `"license": "MIT"`.

### Changed
- License copyright holder set to Mikael Karlbom.
- READMEs rewritten for accuracy: complete settings table, real git URL, `PerfGoblinProfiler` as the timing API.
- Corrected docs, the `PerfGoblinProfiler` XML doc and the Project Settings help text: calls must be wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, because PerfGoblin is not compiled into release builds.

### Fixed
- The "Demo Scene" sample was missing from the package: `Samples~/DemoScene` did not exist, so Package Manager listed a sample that could not be imported. It now contains `DemoScene.unity` and the stress test scripts (asset GUIDs unchanged).
- `package.json` version and the overlay's version label still said `0.1.0`; both now match the released `0.2.0`.

### Removed
- Comments that restated the code (XML API docs and rationale comments are unchanged).
- Committed IDE settings (`.idea/`, including inside the package); these are now git-ignored.

## [0.2.0] - 2026-05-29

### Added
- Global enable/disable toggle in Project Settings — when disabled, PerfGoblin skips initialization entirely (zero overhead)
- Spike display duration setting — spikes auto-expire from the list after a configurable time (default 30s)
- Unit test suite under Assets/Tests/EditMode with mock tracker implementations
- Play Mode performance benchmark tests validating CPU overhead, memory footprint, and GC allocations
- InternalsVisibleTo support for test assembly access

### Changed
- Introduced tracker interfaces (IFrameTimeSource, IMemorySource, IScriptSource, ISpikeSource) for dependency injection and testability
- Extracted FPS, Memory, Scripts, and Spikes panels into dedicated view classes under Runtime/Views
- Moved auto-popup logic from PerfGoblinManager into AutoPopupController
- Replaced fragile string-mangling script name matching with class name extraction
- PerfGoblinOverlay now receives tracker dependencies via constructor instead of static access
- Renamed Scripts panel to "SCRIPT PERFORMANCE" for clarity
- Cross-platform font selection using installed font detection instead of hardcoded fallback chain

## [0.1.0] - 2026-05-25

### Added
- Initial release
- Real-time FPS counter with smoothed display and frame time graph
- Memory breakdown panel with category visualization (Textures, Meshes, Audio, Managed, GFX)
- Automatic MonoBehaviour script performance tracking via ProfilerRecorder
- Manual profiling markers via BeginSample/EndSample API
- Frame spike detection with script attribution
- Configurable overlay position, opacity, and panel visibility
- Project Settings integration for all configuration options
- Editor window with status display and quick controls
- F3 hotkey toggle (configurable)
- Zero footprint in release builds via preprocessor stripping
- Sample scene with stress test scripts
