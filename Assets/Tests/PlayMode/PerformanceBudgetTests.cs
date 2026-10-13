using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace PerfGoblin.Tests
{
    public class PerformanceBudgetTests
    {
        const int WarmupFrames = 30;
        const int MeasureFrames = 120;
        const float MaxCpuOverheadMs = 0.5f;
        const long MaxMemoryBytes = 2 * 1024 * 1024;

        GameObject _perfGoblinGo;
        PerfGoblinManager _manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = new PerfGoblinConfig { Enabled = true, ShowOnPlayModeStart = false };
            _perfGoblinGo = new GameObject("[PerfGoblin-Test]");
            Object.DontDestroyOnLoad(_perfGoblinGo);

            for (int i = 0; i < 5; i++)
                yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_perfGoblinGo != null)
                Object.Destroy(_perfGoblinGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CpuOverhead_StaysUnderBudget()
        {
            var config = new PerfGoblinConfig();
            var frameTime = new FrameTimeTracker(config);
            var memory = new MemoryTracker(config);
            var scripts = new ScriptTracker(config);
            var spikes = new SpikeDetector(config, frameTime, scripts);

            for (int i = 0; i < WarmupFrames; i++)
            {
                frameTime.Update();
                memory.Update(Time.frameCount);
                scripts.Update();
                frameTime.LateUpdate();
                scripts.LateUpdate();
                spikes.Update();
                yield return null;
            }

            var sw = new Stopwatch();
            double totalMs = 0;

            for (int i = 0; i < MeasureFrames; i++)
            {
                sw.Restart();

                frameTime.Update();
                memory.Update(Time.frameCount);
                scripts.Update();
                frameTime.LateUpdate();
                scripts.LateUpdate();
                spikes.Update();

                sw.Stop();
                totalMs += sw.Elapsed.TotalMilliseconds;
                yield return null;
            }

            double avgMs = totalMs / MeasureFrames;

            memory.Dispose();
            scripts.Dispose();
            spikes.Dispose();

            UnityEngine.Debug.Log($"[PerfGoblin Benchmark] CPU overhead: {avgMs:F3}ms avg over {MeasureFrames} frames");
            Assert.Less(avgMs, MaxCpuOverheadMs,
                $"CPU overhead {avgMs:F3}ms exceeds budget of {MaxCpuOverheadMs}ms");
        }

        [UnityTest]
        public IEnumerator MemoryFootprint_StaysUnderBudget()
        {
            long memBefore = Profiler.GetTotalAllocatedMemoryLong();

            var config = new PerfGoblinConfig();
            var frameTime = new FrameTimeTracker(config);
            var memory = new MemoryTracker(config);
            var scripts = new ScriptTracker(config);
            var spikes = new SpikeDetector(config, frameTime, scripts);

            for (int i = 0; i < WarmupFrames; i++)
            {
                frameTime.Update();
                memory.Update(Time.frameCount);
                scripts.Update();
                frameTime.LateUpdate();
                scripts.LateUpdate();
                spikes.Update();
                yield return null;
            }

            long memAfter = Profiler.GetTotalAllocatedMemoryLong();
            long footprint = memAfter - memBefore;

            memory.Dispose();
            scripts.Dispose();
            spikes.Dispose();

            UnityEngine.Debug.Log($"[PerfGoblin Benchmark] Memory footprint: {footprint / 1024.0:F0} KB");
            Assert.Less(footprint, MaxMemoryBytes,
                $"Memory footprint {footprint / 1024.0:F0} KB exceeds budget of {MaxMemoryBytes / 1024} KB");
        }

        [UnityTest]
        public IEnumerator PerFrameGcAllocations_AreZero()
        {
            var config = new PerfGoblinConfig { AutoDetectScripts = false };
            var frameTime = new FrameTimeTracker(config);
            var memory = new MemoryTracker(config);
            var scripts = new ScriptTracker(config);
            var spikes = new SpikeDetector(config, frameTime, scripts);

            for (int i = 0; i < WarmupFrames; i++)
            {
                frameTime.Update();
                memory.Update(Time.frameCount);
                scripts.Update();
                frameTime.LateUpdate();
                scripts.LateUpdate();
                spikes.Update();
                yield return null;
            }

            long totalGcAlloc = 0;
            int gcFrames = 0;

            for (int i = 0; i < MeasureFrames; i++)
            {
                long gcBefore = Profiler.GetMonoUsedSizeLong();

                frameTime.Update();
                memory.Update(Time.frameCount);
                scripts.Update();
                frameTime.LateUpdate();
                scripts.LateUpdate();
                spikes.Update();

                long gcAfter = Profiler.GetMonoUsedSizeLong();
                long frameDelta = gcAfter - gcBefore;
                if (frameDelta > 0)
                {
                    totalGcAlloc += frameDelta;
                    gcFrames++;
                }

                yield return null;
            }

            memory.Dispose();
            scripts.Dispose();
            spikes.Dispose();

            UnityEngine.Debug.Log($"[PerfGoblin Benchmark] GC allocations: {totalGcAlloc} bytes across {gcFrames}/{MeasureFrames} frames");
            float gcPerFrame = MeasureFrames > 0 ? (float)totalGcAlloc / MeasureFrames : 0;
            Assert.Less(gcPerFrame, 64f,
                $"Average GC allocation per frame ({gcPerFrame:F0}B) should be near zero");
        }
    }
}
