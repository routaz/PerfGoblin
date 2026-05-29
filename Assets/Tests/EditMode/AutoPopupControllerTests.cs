using NUnit.Framework;

namespace PerfGoblin.Tests
{
    public class AutoPopupControllerTests
    {
        PerfGoblinConfig _config;
        MockFrameTimeSource _frameTime;
        MockScriptSource _scripts;
        MockSpikeSource _spikes;

        [SetUp]
        public void SetUp()
        {
            _config = new PerfGoblinConfig
            {
                AutoPopupOnThreshold = true,
                AutoPopupCooldownSeconds = 0f,
                AutoDismissSeconds = 0f
            };
            _frameTime = new MockFrameTimeSource { CurrentFPS = 60f, TargetFPS = 60 };
            _scripts = new MockScriptSource();
            _spikes = new MockSpikeSource();
        }

        AutoPopupController CreateController()
        {
            return new AutoPopupController(_config, _frameTime, _scripts, _spikes);
        }

        [Test]
        public void Update_NoThresholdExceeded_ReturnsNone()
        {
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.None, action);
        }

        [Test]
        public void Update_FpsBelowHalfTarget_ReturnsShow()
        {
            _frameTime.CurrentFPS = 25f;
            _frameTime.TargetFPS = 60;
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.Show, action);
        }

        [Test]
        public void Update_ScriptAtBadStatus_ReturnsShow()
        {
            _scripts.AddScript("HeavyScript.Update()", 5.0f, PerformanceStatus.Bad);
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.Show, action);
        }

        [Test]
        public void Update_NewSpike_ReturnsShow()
        {
            _spikes.TotalSpikeCount = 1;
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.Show, action);
        }

        [Test]
        public void Update_AlreadyVisible_DoesNotShowAgain()
        {
            _frameTime.CurrentFPS = 10f;
            var controller = CreateController();

            var action = controller.Update(isVisible: true);

            Assert.AreEqual(AutoPopupController.Action.None, action);
        }

        [Test]
        public void Update_AutoPopupDisabled_ReturnsNone()
        {
            _config.AutoPopupOnThreshold = false;
            _frameTime.CurrentFPS = 10f;
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.None, action);
        }

        [Test]
        public void OnManualToggle_ClearsAutoPopped()
        {
            _frameTime.CurrentFPS = 10f;
            var controller = CreateController();

            controller.Update(isVisible: false);
            Assert.IsTrue(controller.IsAutoPopped);

            controller.OnManualToggle();
            Assert.IsFalse(controller.IsAutoPopped);
        }

        [Test]
        public void Update_FpsAtExactHalfTarget_DoesNotShow()
        {
            _frameTime.CurrentFPS = 30f;
            _frameTime.TargetFPS = 60;
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.None, action);
        }

        [Test]
        public void Update_FpsJustBelowHalfTarget_Shows()
        {
            _frameTime.CurrentFPS = 29.9f;
            _frameTime.TargetFPS = 60;
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.Show, action);
        }

        [Test]
        public void Update_OkAndWarnScripts_DoNotTrigger()
        {
            _scripts.AddScript("ScriptA.Update()", 0.5f, PerformanceStatus.Ok);
            _scripts.AddScript("ScriptB.Update()", 2.0f, PerformanceStatus.Warn);
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.None, action);
        }

        [Test]
        public void Update_ZeroTargetFps_FpsCheckSkipped()
        {
            _frameTime.CurrentFPS = 1f;
            _frameTime.TargetFPS = 0;
            var controller = CreateController();

            var action = controller.Update(isVisible: false);

            Assert.AreEqual(AutoPopupController.Action.None, action);
        }
    }
}
