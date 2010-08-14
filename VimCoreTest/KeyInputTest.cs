using NUnit.Framework;
using Vim;

namespace VimCore.Test
{
    [TestFixture]
    public class KeyInputTest
    {
        [Test]
        public void IsDigit1()
        {
            var input = KeyInputUtil.CharToKeyInput('0');
            Assert.IsTrue(input.IsDigit);
        }

        [Test]
        public void IsDigit2()
        {
            var input = KeyInputUtil.VimKeyToKeyInput(VimKey.Enter);
            Assert.IsFalse(input.IsDigit);
        }

        [Test]
        public void Equality1()
        {
            var i1 = new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'c');
            Assert.AreEqual(i1, new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'c'));
            Assert.AreNotEqual(i1, new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'd'));
            Assert.AreNotEqual(i1, new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.Shift, 'c'));
            Assert.AreNotEqual(i1, new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.Alt, 'c'));
        }

        [Test, Description("Boundary condition")]
        public void Equality2()
        {
            var i1 = new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'c');
            Assert.AreNotEqual(i1, 42);
        }

        [Test]
        public void Equality3()
        {
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('a') == KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('b') == KeyInputUtil.CharToKeyInput('b'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('c') == KeyInputUtil.CharToKeyInput('c'));
        }

        [Test]
        public void Equality4()
        {
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('a') != KeyInputUtil.CharToKeyInput('b'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('b') != KeyInputUtil.CharToKeyInput('c'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('c') != KeyInputUtil.CharToKeyInput('d'));
        }

        [Test]
        public void CompareTo1()
        {
            var i1 = KeyInputUtil.CharToKeyInput('c');
            Assert.IsTrue(i1.CompareTo(KeyInputUtil.CharToKeyInput('z')) < 0);
            Assert.IsTrue(i1.CompareTo(KeyInputUtil.CharToKeyInput('c')) == 0);
            Assert.IsTrue(i1.CompareTo(KeyInputUtil.CharToKeyInput('a')) > 0);
        }

    }
}
