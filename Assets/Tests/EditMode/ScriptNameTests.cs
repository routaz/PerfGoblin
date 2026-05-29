using NUnit.Framework;

namespace PerfGoblin.Tests
{
    public class ScriptNameTests
    {
        [Test]
        public void ExtractClassName_WithDot_ReturnsClassPart()
        {
            Assert.AreEqual("HeavyUpdateScript", ScriptTracker.ExtractClassName("HeavyUpdateScript.Update()"));
        }

        [Test]
        public void ExtractClassName_MultipleDots_ReturnsFirstSegment()
        {
            Assert.AreEqual("MyNamespace", ScriptTracker.ExtractClassName("MyNamespace.MyClass.Update()"));
        }

        [Test]
        public void ExtractClassName_NoDot_ReturnsFullName()
        {
            Assert.AreEqual("SimpleScript", ScriptTracker.ExtractClassName("SimpleScript"));
        }

        [Test]
        public void ExtractClassName_EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual("", ScriptTracker.ExtractClassName(""));
        }

        [Test]
        public void ExtractClassName_DotAtStart_ReturnsFullString()
        {
            Assert.AreEqual(".Update()", ScriptTracker.ExtractClassName(".Update()"));
        }

        [Test]
        public void ShortenName_TwoSegments_ReturnsFull()
        {
            Assert.AreEqual("MyScript.Update()", ScriptsPanelView.ShortenName("MyScript.Update()"));
        }

        [Test]
        public void ShortenName_ThreeSegments_ReturnsLastTwo()
        {
            Assert.AreEqual("MyClass.Update()", ScriptsPanelView.ShortenName("MyNamespace.MyClass.Update()"));
        }

        [Test]
        public void ShortenName_SingleSegment_ReturnsFull()
        {
            Assert.AreEqual("Update", ScriptsPanelView.ShortenName("Update"));
        }

        [Test]
        public void ShortenName_FourSegments_ReturnsLastTwo()
        {
            Assert.AreEqual("Inner.Method()", ScriptsPanelView.ShortenName("Root.Outer.Inner.Method()"));
        }
    }
}
