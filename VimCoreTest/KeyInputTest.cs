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
            var input = InputUtil.VimKeyToKeyInput(VimKey.EnterKey);
            Assert.IsFalse(input.IsDigit);
        }

        [Test]
        public void Equality1()
        {
            var i1 = new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.None);
            Assert.AreEqual(i1, new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.None));
            Assert.AreNotEqual(i1, new KeyInput('d', VimKey.NotWellKnownKey, KeyModifiers.None));
            Assert.AreNotEqual(i1, new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.Shift));
            Assert.AreNotEqual(i1, new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.Alt));
        }

        [Test, Description("Boundary condition")]
        public void Equality2()
        {
            var i1 = new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.None);
            Assert.AreNotEqual(i1, 42);
        }

        [Test]
        public void CompareTo1()
        {
            var i1 = new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.None);
            Assert.IsTrue(i1.CompareTo(new KeyInput('z', VimKey.NotWellKnownKey, KeyModifiers.None)) < 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput('c', VimKey.NotWellKnownKey, KeyModifiers.None)) == 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput('a', VimKey.NotWellKnownKey, KeyModifiers.None)) > 0);
        }
    }
}
