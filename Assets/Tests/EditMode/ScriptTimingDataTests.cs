using NUnit.Framework;

namespace PerfGoblin.Tests
{
    public class ScriptTimingDataTests
    {
        [Test]
        public void AccumulateManual_AddsToLastFrameMs()
        {
            var data = new ScriptTimingData { ScriptName = "Test" };
            data.AccumulateManual(1.5f);
            data.AccumulateManual(2.0f);

            Assert.AreEqual(3.5f, data.LastFrameMs, 0.001f);
            Assert.AreEqual(2, data.CallCount);
            Assert.IsTrue(data.HasPreciseTiming);
        }

        [Test]
        public void UpdateStats_SetsOkStatus_BelowWarnThreshold()
        {
            var data = new ScriptTimingData { ScriptName = "Test", LastFrameMs = 0.5f };
            data.UpdateStats(warnMs: 1.0f, badMs: 4.0f);

            Assert.AreEqual(PerformanceStatus.Ok, data.Status);
        }

        [Test]
        public void UpdateStats_SetsWarnStatus_AboveWarnThreshold()
        {
            var data = new ScriptTimingData { ScriptName = "Test", LastFrameMs = 2.0f };
            data.UpdateStats(warnMs: 1.0f, badMs: 4.0f);

            Assert.AreEqual(PerformanceStatus.Warn, data.Status);
        }

        [Test]
        public void UpdateStats_SetsBadStatus_AboveBadThreshold()
        {
            var data = new ScriptTimingData { ScriptName = "Test", LastFrameMs = 5.0f };
            data.UpdateStats(warnMs: 1.0f, badMs: 4.0f);

            Assert.AreEqual(PerformanceStatus.Bad, data.Status);
        }

        [Test]
        public void UpdateStats_TracksPeakMs()
        {
            var data = new ScriptTimingData { ScriptName = "Test" };

            data.LastFrameMs = 2.0f;
            data.UpdateStats(1.0f, 4.0f);

            data.LastFrameMs = 8.0f;
            data.UpdateStats(1.0f, 4.0f);

            data.LastFrameMs = 3.0f;
            data.UpdateStats(1.0f, 4.0f);

            Assert.AreEqual(8.0f, data.PeakMs, 0.001f);
        }

        [Test]
        public void UpdateStats_ComputesRunningAverage()
        {
            var data = new ScriptTimingData { ScriptName = "Test" };

            data.LastFrameMs = 2.0f;
            data.UpdateStats(1.0f, 4.0f);

            data.LastFrameMs = 4.0f;
            data.UpdateStats(1.0f, 4.0f);

            Assert.AreEqual(3.0f, data.AverageMs, 0.001f);
        }

        [Test]
        public void ResetFrame_ClearsPerFrameState()
        {
            var data = new ScriptTimingData { ScriptName = "Test" };
            data.AccumulateManual(5.0f);

            data.ResetFrame();

            Assert.AreEqual(0f, data.LastFrameMs);
            Assert.AreEqual(0, data.CallCount);
            Assert.AreEqual(0, data.GCAllocBytes);
            Assert.IsFalse(data.HasPreciseTiming);
        }

        [Test]
        public void UpdateStats_AtExactWarnThreshold_SetsWarn()
        {
            var data = new ScriptTimingData { ScriptName = "Test", LastFrameMs = 1.0f };
            data.UpdateStats(warnMs: 1.0f, badMs: 4.0f);

            Assert.AreEqual(PerformanceStatus.Warn, data.Status);
        }

        [Test]
        public void UpdateStats_AtExactBadThreshold_SetsBad()
        {
            var data = new ScriptTimingData { ScriptName = "Test", LastFrameMs = 4.0f };
            data.UpdateStats(warnMs: 1.0f, badMs: 4.0f);

            Assert.AreEqual(PerformanceStatus.Bad, data.Status);
        }
    }
}
