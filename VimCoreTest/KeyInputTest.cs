using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCoreTest;
using VimCore;
using System.Windows.Input;

namespace VimCoreTest
{
    [TestClass]
    public class KeyInputTest
    {
        [TestMethod]
        public void IsDigit1()
        {
            var input = InputUtil.KeyToKeyInput(Key.D0);
            Assert.IsTrue(input.IsDigit);
        }

        [TestMethod]
        public void IsDigit2()
        {
            var input = InputUtil.KeyToKeyInput(Key.Enter);
            Assert.IsFalse(input.IsDigit);
        }

        [TestMethod]
        public void Equality1()
        {
            var i1 = new KeyInput('c', Key.C, ModifierKeys.None);
            Assert.AreEqual(i1, new KeyInput('c', Key.C, ModifierKeys.None));
            Assert.AreNotEqual(i1, new KeyInput('d', Key.C, ModifierKeys.None));
            Assert.AreNotEqual(i1, new KeyInput('c', Key.D, ModifierKeys.None));
            Assert.AreNotEqual(i1, new KeyInput('c', Key.C, ModifierKeys.Alt));
        }

        [TestMethod, Description("Boundary condition")]
        public void Equality2()
        {
            var i1 = new KeyInput('c', Key.C, ModifierKeys.None);
            Assert.AreNotEqual(i1, 42);
        }

        [TestMethod]
        public void CompareTo1()
        {
            var i1 = new KeyInput('c', Key.C, ModifierKeys.None);
            Assert.IsTrue(i1.CompareTo(new KeyInput('z', Key.C, ModifierKeys.None)) < 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput('c', Key.C, ModifierKeys.None)) == 0);
            Assert.IsTrue(i1.CompareTo(new KeyInput('a', Key.C, ModifierKeys.None)) > 0);
        }
    }
}
