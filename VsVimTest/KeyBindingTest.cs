using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsVim;
using System.Windows.Input;
using KeyBinding = VsVim.KeyBinding;

namespace VsVimTest
{
    [TestClass]
    public class KeyBindingTest
    {

        [TestMethod]
        public void Parse1()
        {
            var b = KeyBinding.Parse("foo::e");
            Assert.AreEqual("foo", b.Scope);
            Assert.AreEqual('e', b.FirstKeyInput.Char);
            Assert.AreEqual(Key.E, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void Parse2()
        {
            var b = KeyBinding.Parse("::b");
            Assert.AreEqual(String.Empty, b.Scope);
            Assert.AreEqual('b', b.FirstKeyInput.Char);
        }

        [TestMethod]
        public void Parse3()
        {
            var b = KeyBinding.Parse("::f2");
            Assert.AreEqual(Char.MinValue, b.FirstKeyInput.Char);
            Assert.AreEqual(Key.F2, b.FirstKeyInput.Key);
        }

        [TestMethod, Description("Parse a keybinding with , correctly")]
        public void Parse4()
        {
            var b = KeyBinding.Parse("::,");
            Assert.AreEqual(',', b.FirstKeyInput.Char);
            Assert.AreEqual(Key.OemComma, b.FirstKeyInput.Key);
            Assert.AreEqual(ModifierKeys.None, b.FirstKeyInput.ModifierKeys);
        }

        [TestMethod, Description("Double modifier")]
        public void Parse5()
        {
            var b = KeyBinding.Parse("::ctrl+shift+f");
            Assert.AreEqual(Key.F, b.FirstKeyInput.Key);
            Assert.IsTrue(0 != (ModifierKeys.Shift & b.FirstKeyInput.ModifierKeys));
            Assert.IsTrue(0 != (ModifierKeys.Control & b.FirstKeyInput.ModifierKeys));
        }

        [TestMethod, Description("Don't carry shift keys for letters")]
        public void Parse6()
        {
            var b = KeyBinding.Parse("::ctrl+D");
            Assert.AreEqual('d', b.FirstKeyInput.Char);
            Assert.AreEqual(ModifierKeys.Control, b.FirstKeyInput.ModifierKeys);
        }

        [TestMethod]
        public void ParseMultiple1()
        {
            var b = KeyBinding.Parse("::e, f");
            Assert.AreEqual(2, b.KeyInputs.Count());
        }

        [TestMethod, Description("With a comma key")]
        public void ParseMultiple2()
        {
            var b = KeyBinding.Parse("::,, f");
            Assert.AreEqual(2, b.KeyInputs.Count());
            Assert.AreEqual(Key.OemComma, b.KeyInputs.ElementAt(0).Key);
            Assert.AreEqual(Key.F, b.KeyInputs.ElementAt(1).Key);
        }

        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void BadParse1()
        {
            KeyBinding.Parse("::notavalidkey");
        }

        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void BadParse2()
        {
            KeyBinding.Parse("::ctrl+notavalidkey");
        }

        [TestMethod, ExpectedException(typeof(ArgumentException)), Description("Not supported because simply put I don't understand it")]
        public void BadParse3()
        {
            KeyBinding.Parse("::Num *");
        }

        [TestMethod]
        public void VsKeyBackSpace()
        {
            var b = KeyBinding.Parse("::Bkspce");
            Assert.AreEqual(Key.Back, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyLeftArrow()
        {
            var b = KeyBinding.Parse("::Left Arrow");
            Assert.AreEqual(Key.Left, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyRightArrow()
        {
            var b = KeyBinding.Parse("::Right Arrow");
            Assert.AreEqual(Key.Right, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyUpArrow()
        {
            var b = KeyBinding.Parse("::Up Arrow");
            Assert.AreEqual(Key.Up, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyDownArrow()
        {
            var b = KeyBinding.Parse("::Down Arrow");
            Assert.AreEqual(Key.Down, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyDownArrow2()
        {
            var b = KeyBinding.Parse("::Shift+Down Arrow");
            Assert.AreEqual(Key.Down, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyPageDown()
        {
            var b = KeyBinding.Parse("::PgDn");
            Assert.AreEqual(Key.PageDown, b.FirstKeyInput.Key);
        }

        [TestMethod]
        public void VsKeyPageUp()
        {
            var b = KeyBinding.Parse("::PgUp");
            Assert.AreEqual(Key.PageUp, b.FirstKeyInput.Key);
        }


    }
}
