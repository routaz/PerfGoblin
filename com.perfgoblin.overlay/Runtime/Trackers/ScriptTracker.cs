#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace PerfGoblin
{
    public enum PerformanceStatus { Ok, Warn, Bad }

    public class ScriptTimingData
    {
        public string ScriptName;
        public float LastFrameMs;
        public float AverageMs;
        public float PeakMs;
        public int CallCount;
        public long GCAllocBytes;
        public PerformanceStatus Status;
        public bool HasPreciseTiming;

        float _runningTotal;
        int _sampleCount;

        public void AccumulateManual(float ms)
        {
            LastFrameMs += ms;
            CallCount++;
            HasPreciseTiming = true;
        }

        public void UpdateStats(float warnMs, float badMs)
        {
            _runningTotal += LastFrameMs;
            _sampleCount++;
            AverageMs = _runningTotal / _sampleCount;
            if (LastFrameMs > PeakMs)
                PeakMs = LastFrameMs;

            if (LastFrameMs >= badMs)
                Status = PerformanceStatus.Bad;
            else if (LastFrameMs >= warnMs)
                Status = PerformanceStatus.Warn;
            else
                Status = PerformanceStatus.Ok;
        }

        public void ResetFrame()
        {
            LastFrameMs = 0f;
            CallCount = 0;
            GCAllocBytes = 0;
            HasPreciseTiming = false;
        }
    }

    public class ScriptTracker : IDisposable, IScriptSource
    {
        readonly PerfGoblinConfig _config;
        readonly Dictionary<string, ProfilerRecorder> _recorders = new();
        readonly Dictionary<string, ScriptTimingData> _autoTimings = new();
        readonly Dictionary<string, ScriptTimingData> _manualTimings = new();
        readonly List<ScriptTimingData> _sortedResults = new();
        readonly HashSet<Type> _knownTypes = new();

        readonly Stopwatch _frameStopwatch = new();
        float _totalScriptTimeMs;
        float _lastScanTime;

        public IReadOnlyList<ScriptTimingData> SortedScripts => _sortedResults;
        public float TotalScriptTimeMs => _totalScriptTimeMs;
        public bool HasRecorderData { get; private set; }

        public ScriptTracker(PerfGoblinConfig config)
        {
            _config = config;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ScanMonoBehaviours();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ScanMonoBehaviours();

        public void ScanMonoBehaviours()
        {
            if (!_config.AutoDetectScripts) return;

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine")) continue;
                if (type.Namespace == "PerfGoblin" || type.Namespace == "PerfGoblin.Editor") continue;
                if (_knownTypes.Contains(type)) continue;

                _knownTypes.Add(type);
                string fullName = type.FullName;
                string shortName = type.Name;

                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                bool hasUpdate = type.GetMethod("Update", flags) != null;
                bool hasFixedUpdate = type.GetMethod("FixedUpdate", flags) != null;
                bool hasLateUpdate = type.GetMethod("LateUpdate", flags) != null;

                string[] methods = { "Update", "FixedUpdate", "LateUpdate" };
                bool[] has = { hasUpdate, hasFixedUpdate, hasLateUpdate };

                for (int i = 0; i < methods.Length; i++)
                {
                    if (!has[i]) continue;
                    string displayName = $"{shortName}.{methods[i]}()";
                    if (_autoTimings.ContainsKey(displayName)) continue;

                    string[] candidates =
                    {
                        $"{fullName}.{methods[i]}()",
                        $"{shortName}.{methods[i]}()",
                        $"{fullName}.{methods[i]}",
                        $"{shortName}.{methods[i]}",
                    };

                    var data = new ScriptTimingData { ScriptName = displayName };
                    _autoTimings[displayName] = data;

                    foreach (var markerName in candidates)
                    {
                        if (_recorders.ContainsKey(markerName)) break;
                        try
                        {
                            var rec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, markerName, 1);
                            if (rec.Valid)
                            {
                                _recorders[markerName] = rec;
                                break;
                            }
                            rec.Dispose();
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        public void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastScanTime > _config.ScriptScanIntervalSeconds)
            {
                _lastScanTime = now;
                ScanMonoBehaviours();
            }

            PerfGoblinManager.ResetManualTimingsFrame();

            foreach (var kvp in _autoTimings)
                kvp.Value.ResetFrame();

            _frameStopwatch.Restart();
        }

        public void LateUpdate()
        {
            _frameStopwatch.Stop();
            _totalScriptTimeMs = (float)_frameStopwatch.Elapsed.TotalMilliseconds;

            // Try ProfilerRecorder for each script individually
            float recorderTotalMs = 0f;
            int recorderHitCount = 0;
            foreach (var kvp in _autoTimings)
            {
                var data = kvp.Value;
                bool found = false;
                foreach (var rkvp in _recorders)
                {
                    string baseName = data.ScriptName.Replace("()", "");
                    // Require > 1000ns (1µs) to filter out noise from empty recorders
                    if (rkvp.Key.Contains(baseName) && rkvp.Value.Valid && rkvp.Value.LastValue > 1000)
                    {
                        data.LastFrameMs = rkvp.Value.LastValue / 1_000_000f;
                        data.CallCount = 1;
                        data.HasPreciseTiming = true;
                        recorderTotalMs += data.LastFrameMs;
                        recorderHitCount++;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    data.CallCount = -1; // mark for fallback
            }

            HasRecorderData = recorderHitCount > 0;

            // Fallback: scripts without recorder data get a share of the remaining time
            int unrecordedCount = _autoTimings.Count - recorderHitCount;
            if (unrecordedCount > 0 && _totalScriptTimeMs > 0.01f)
            {
                float remainingMs = Mathf.Max(0f, _totalScriptTimeMs - recorderTotalMs);
                float perScript = remainingMs / unrecordedCount;
                foreach (var kvp in _autoTimings)
                {
                    if (kvp.Value.CallCount == -1)
                    {
                        kvp.Value.LastFrameMs = perScript;
                        kvp.Value.CallCount = 1;
                    }
                }
            }
            else if (unrecordedCount > 0)
            {
                // Total time too small to distribute — zero out unrecorded scripts
                foreach (var kvp in _autoTimings)
                {
                    if (kvp.Value.CallCount == -1)
                        kvp.Value.CallCount = 0;
                }
            }

            foreach (var kvp in _autoTimings)
                kvp.Value.UpdateStats(_config.ScriptWarnThresholdMs, _config.ScriptBadThresholdMs);

            foreach (var kvp in _manualTimings)
                kvp.Value.UpdateStats(_config.ScriptWarnThresholdMs, _config.ScriptBadThresholdMs);

            RebuildSortedList();
        }

        public void RecordManualSample(string name, ScriptTimingData data)
        {
            _manualTimings[name] = data;
        }

        void RebuildSortedList()
        {
            _sortedResults.Clear();

            foreach (var kvp in _autoTimings)
            {
                string className = ExtractClassName(kvp.Value.ScriptName);
                bool overridden = false;
                foreach (var mkvp in _manualTimings)
                {
                    if (mkvp.Key.IndexOf(className, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        overridden = true;
                        break;
                    }
                }
                if (!overridden)
                    _sortedResults.Add(kvp.Value);
            }

            // Manual timings always included
            foreach (var kvp in _manualTimings)
                _sortedResults.Add(kvp.Value);

            // Sort: precise (manual/recorder) first, then by ms descending
            _sortedResults.Sort((a, b) =>
            {
                if (a.HasPreciseTiming != b.HasPreciseTiming)
                    return a.HasPreciseTiming ? -1 : 1;
                return b.LastFrameMs.CompareTo(a.LastFrameMs);
            });

            if (_sortedResults.Count > _config.MaxDisplayedScripts)
                _sortedResults.RemoveRange(_config.MaxDisplayedScripts, _sortedResults.Count - _config.MaxDisplayedScripts);
        }

        internal static string ExtractClassName(string scriptName)
        {
            int dotIndex = scriptName.IndexOf('.');
            return dotIndex > 0 ? scriptName.Substring(0, dotIndex) : scriptName;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            foreach (var rec in _recorders.Values)
            {
                if (rec.Valid)
                    rec.Dispose();
            }
            _recorders.Clear();
        }
    }
}
#endif
