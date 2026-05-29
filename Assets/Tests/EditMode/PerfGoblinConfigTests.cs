using NUnit.Framework;
using UnityEngine;

namespace PerfGoblin.Tests
{
    public class PerfGoblinConfigTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var config = new PerfGoblinConfig();

            Assert.IsTrue(config.Enabled);
            Assert.AreEqual(KeyCode.F3, config.ToggleKey);
            Assert.IsTrue(config.ShowOnPlayModeStart);
            Assert.AreEqual(0.5f, config.FpsSmoothingWindow, 0.001f);
            Assert.AreEqual(300, config.FrameHistoryCapacity);
            Assert.AreEqual(30, config.MemorySampleInterval);
            Assert.IsFalse(config.ExcludeEditorOverhead);
            Assert.AreEqual(1.0f, config.ScriptWarnThresholdMs, 0.001f);
            Assert.AreEqual(4.0f, config.ScriptBadThresholdMs, 0.001f);
            Assert.IsTrue(config.AutoDetectScripts);
            Assert.AreEqual(5, config.ScriptScanIntervalSeconds);
            Assert.AreEqual(10, config.MaxDisplayedScripts);
            Assert.AreEqual(2.0f, config.SpikeMultiplier, 0.001f);
            Assert.AreEqual(100, config.SpikeHistoryCapacity);
            Assert.IsFalse(config.AutoPopupOnThreshold);
            Assert.AreEqual(5f, config.AutoPopupCooldownSeconds, 0.001f);
            Assert.AreEqual(0f, config.AutoDismissSeconds, 0.001f);
            Assert.AreEqual(OverlayPosition.TopRight, config.Position);
            Assert.IsFalse(config.CompactMode);
            Assert.AreEqual(0.92f, config.OverlayOpacity, 0.001f);
            Assert.AreEqual(1.5f, config.OverlayScale, 0.001f);
            Assert.AreEqual(ActivePanels.All, config.VisiblePanels);
        }

        [Test]
        public void ResetToDefaults_RestoresAllValues()
        {
            var config = new PerfGoblinConfig();
            config.Enabled = false;
            config.ToggleKey = KeyCode.F5;
            config.ShowOnPlayModeStart = false;
            config.FpsSmoothingWindow = 2.0f;
            config.ScriptWarnThresholdMs = 10f;
            config.SpikeMultiplier = 5f;
            config.Position = OverlayPosition.BottomLeft;
            config.OverlayScale = 3f;

            config.ResetToDefaults();

            Assert.IsTrue(config.Enabled);
            Assert.AreEqual(KeyCode.F3, config.ToggleKey);
            Assert.IsTrue(config.ShowOnPlayModeStart);
            Assert.AreEqual(0.5f, config.FpsSmoothingWindow, 0.001f);
            Assert.AreEqual(1.0f, config.ScriptWarnThresholdMs, 0.001f);
            Assert.AreEqual(2.0f, config.SpikeMultiplier, 0.001f);
            Assert.AreEqual(OverlayPosition.TopRight, config.Position);
            Assert.AreEqual(1.5f, config.OverlayScale, 0.001f);
        }
    }
}
