using NUnit.Framework;
using Vim;

namespace VsVim.UnitTest
{
    [TestFixture]
    public sealed class KeyStrokeTest
    {
        [Test]
        public void KeyStroke1()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.None);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('c'), stroke.KeyInput);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('c'), stroke.AggregateKeyInput);
            Assert.AreEqual('c', stroke.Char);
        }

        [Test]
        public void KeyStroke2()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('c'), stroke.KeyInput);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('C'), stroke.AggregateKeyInput);
            Assert.AreEqual('c', stroke.Char);
        }

        [Test]
        public void KeyStroke3()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('c'), stroke.KeyInput);
            Assert.AreEqual(KeyInputUtil.CharWithControlToKeyInput('C'), stroke.AggregateKeyInput);
            Assert.AreEqual('c', stroke.Char);
        }

        [Test]
        public void Equals1()
        {
            var stroke1 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            var stroke2 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.AreEqual(stroke1, stroke2);
            Assert.IsTrue(stroke1 == stroke2);
            Assert.IsFalse(stroke1 != stroke2);
        }

        [Test]
        public void Equals2()
        {
            var stroke1 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('d'),
                KeyModifiers.Shift | KeyModifiers.Control);
            var stroke2 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.AreNotEqual(stroke1, stroke2);
            Assert.IsFalse(stroke1 == stroke2);
            Assert.IsTrue(stroke1 != stroke2);
        }
    }
}
