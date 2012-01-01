using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VsVim.UnitTest
{
    [TestFixture]
    public sealed class KeyStrokeTest
    {
        [Test]
        public void Constructor_WithNoModifier()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.None);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('c'), stroke.KeyInput);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('c'), stroke.AggregateKeyInput);
            Assert.AreEqual('c', stroke.Char);
        }

        [Test]
        public void Constructor_WithShiftModifier()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('#'),
                KeyModifiers.Shift);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('#'), stroke.KeyInput);
            Assert.AreEqual(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Pound, KeyModifiers.Shift), stroke.AggregateKeyInput);
            Assert.AreEqual('#', stroke.Char);
        }

        [Test]
        public void KeyStroke_WithShiftAndControlModifier()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('#'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('#'), stroke.KeyInput);
            Assert.AreEqual(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Pound, KeyModifiers.Shift | KeyModifiers.Control), stroke.AggregateKeyInput);
            Assert.AreEqual('#', stroke.Char);
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

        [Test]
        public void Equals3()
        {
            var value = EqualityUnit
                .Create(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), KeyModifiers.None))
                .WithEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), KeyModifiers.None))
                .WithNotEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('d'), KeyModifiers.None))
                .WithNotEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), KeyModifiers.Shift));
            EqualityUtil.RunAll(
                (x, y) => x == y,
                (x, y) => x != y,
                values: value);
        }
    }
}
