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
            var input = InputUtil.CharToKeyInput('0');
            Assert.IsTrue(input.IsDigit);
        }

        [Test]
        public void IsDigit2()
        {
            var input = InputUtil.VimKeyToKeyInput(VimKey.Enter);
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
            Assert.IsTrue(InputUtil.CharToKeyInput('a') == InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(InputUtil.CharToKeyInput('b') == InputUtil.CharToKeyInput('b'));
            Assert.IsTrue(InputUtil.CharToKeyInput('c') == InputUtil.CharToKeyInput('c'));
        }

        [Test]
        public void Equality4()
        {
            Assert.IsTrue(InputUtil.CharToKeyInput('a') != InputUtil.CharToKeyInput('b'));
            Assert.IsTrue(InputUtil.CharToKeyInput('b') != InputUtil.CharToKeyInput('c'));
            Assert.IsTrue(InputUtil.CharToKeyInput('c') != InputUtil.CharToKeyInput('d'));
        }

        [Test]
        public void CompareTo1()
        {
            var i1 = new KeyInput('c', VimKey.NotWellKnown, KeyModifiers.None, 'c');
            Assert.IsTrue(i1.CompareTo(new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'z')) < 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'c')) == 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, 'a')) > 0);
        }

    }
}
