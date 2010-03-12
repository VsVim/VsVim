using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VimCoreTest;
using Vim;
using System.Windows.Input;

namespace VimCoreTest
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
            var input = InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EnterKey);
            Assert.IsFalse(input.IsDigit);
        }

        [Test]
        public void Equality1()
        {
            var i1 = new KeyInput('c', Key.C, ModifierKeys.None);
            Assert.AreEqual(i1, new KeyInput('c', Key.C, ModifierKeys.None));
            Assert.AreNotEqual(i1, new KeyInput('d', Key.C, ModifierKeys.None));
            Assert.AreNotEqual(i1, new KeyInput('c', Key.D, ModifierKeys.None));
            Assert.AreNotEqual(i1, new KeyInput('c', Key.C, ModifierKeys.Alt));
        }

        [Test, Description("Boundary condition")]
        public void Equality2()
        {
            var i1 = new KeyInput('c', Key.C, ModifierKeys.None);
            Assert.AreNotEqual(i1, 42);
        }

        [Test]
        public void CompareTo1()
        {
            var i1 = new KeyInput('c', Key.C, ModifierKeys.None);
            Assert.IsTrue(i1.CompareTo(new KeyInput('z', Key.C, ModifierKeys.None)) < 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput('c', Key.C, ModifierKeys.None)) == 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput('a', Key.C, ModifierKeys.None)) > 0);
        }
    }
}
