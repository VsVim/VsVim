using System;
using System.Collections.Generic;
using System.Windows.Input;
using NUnit.Framework;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class KeyUtilTest
    {
        private void KeyToKeyInput(char c, Key key)
        {
            KeyToKeyInput(c, key, ModifierKeys.None);
        }

        private void KeyToKeyInput(char c, Key key, ModifierKeys mod)
        {
            var left = KeyInputUtil.CharToKeyInput(c);
            var right = KeyUtil.ConvertToKeyInput(key, mod);
            Assert.AreEqual(left, right);
        }

        private void WellKnownBothWays(VimKey wellKnownKey, Key key)
        {
            var left = KeyInputUtil.VimKeyToKeyInput(wellKnownKey);
            var right = KeyUtil.ConvertToKeyInput(key);
            Assert.AreEqual(left, right);
        }

        [Test]
        public void KeyToKeyInput()
        {
            KeyToKeyInput(' ', Key.Space);
            KeyToKeyInput(';', Key.OemSemicolon);
            KeyToKeyInput(':', Key.OemSemicolon, ModifierKeys.Shift);
            KeyToKeyInput('/', Key.Oem2);
            KeyToKeyInput('?', Key.Oem2, ModifierKeys.Shift);
            KeyToKeyInput('.', Key.OemPeriod);
            KeyToKeyInput('>', Key.OemPeriod, ModifierKeys.Shift);
            KeyToKeyInput(',', Key.OemComma);
            KeyToKeyInput('<', Key.OemComma, ModifierKeys.Shift);
            KeyToKeyInput('[', Key.OemOpenBrackets);
            KeyToKeyInput('{', Key.OemOpenBrackets, ModifierKeys.Shift);
            KeyToKeyInput(']', Key.OemCloseBrackets);
            KeyToKeyInput('}', Key.OemCloseBrackets, ModifierKeys.Shift);
            KeyToKeyInput('\b', Key.Back);
            KeyToKeyInput('\t', Key.Tab);
            KeyToKeyInput('-', Key.OemMinus);
            KeyToKeyInput('=', Key.OemPlus);
            KeyToKeyInput('+', Key.OemPlus, ModifierKeys.Shift);
            KeyToKeyInput('~', Key.OemTilde, ModifierKeys.Shift);
        }

        [Test]
        public void WellKnownBothWays()
        {
            WellKnownBothWays(VimKey.Left, Key.Left);
            WellKnownBothWays(VimKey.Right, Key.Right);
            WellKnownBothWays(VimKey.Up, Key.Up);
            WellKnownBothWays(VimKey.Down, Key.Down);
            WellKnownBothWays(VimKey.F1, Key.F1);
            WellKnownBothWays(VimKey.F2, Key.F2);
            WellKnownBothWays(VimKey.F3, Key.F3);
            WellKnownBothWays(VimKey.F4, Key.F4);
            WellKnownBothWays(VimKey.F5, Key.F5);
            WellKnownBothWays(VimKey.F6, Key.F6);
            WellKnownBothWays(VimKey.F7, Key.F7);
            WellKnownBothWays(VimKey.F8, Key.F8);
            WellKnownBothWays(VimKey.F9, Key.F9);
            WellKnownBothWays(VimKey.F10, Key.F10);
            WellKnownBothWays(VimKey.F11, Key.F11);
            WellKnownBothWays(VimKey.F12, Key.F12);
            WellKnownBothWays(VimKey.Delete, Key.Delete);
            WellKnownBothWays(VimKey.Escape, Key.Escape);
            WellKnownBothWays(VimKey.KeypadMultiply, Key.Multiply);
            WellKnownBothWays(VimKey.KeypadPlus, Key.Add);
            WellKnownBothWays(VimKey.KeypadMinus, Key.Subtract);
            WellKnownBothWays(VimKey.KeypadDecimal, Key.Decimal);
            WellKnownBothWays(VimKey.KeypadDivide, Key.Divide);
            WellKnownBothWays(VimKey.Keypad0, Key.NumPad0);
            WellKnownBothWays(VimKey.Keypad1, Key.NumPad1);
            WellKnownBothWays(VimKey.Keypad2, Key.NumPad2);
            WellKnownBothWays(VimKey.Keypad3, Key.NumPad3);
            WellKnownBothWays(VimKey.Keypad4, Key.NumPad4);
            WellKnownBothWays(VimKey.Keypad5, Key.NumPad5);
            WellKnownBothWays(VimKey.Keypad6, Key.NumPad6);
            WellKnownBothWays(VimKey.Keypad7, Key.NumPad7);
            WellKnownBothWays(VimKey.Keypad8, Key.NumPad8);
            WellKnownBothWays(VimKey.Keypad9, Key.NumPad9);
        }

        [Test]
        public void ConvertToKeyInput1()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.A, ModifierKeys.Shift);
            Assert.AreEqual('A', ki.Char);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Test]
        public void ConvertToKeyInput2()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.A, ModifierKeys.None);
            Assert.AreEqual('a', ki.Char);
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
        }

        [Test]
        public void ConvertToKeyInput3()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.A, ModifierKeys.Control);
            Assert.AreEqual('a', ki.Char);
            Assert.AreEqual(KeyModifiers.Control, ki.KeyModifiers);
        }

        [Test]
        public void ConvertToKeyInput4()
        {
            var list = new List<Tuple<char, Key>>() {
                    Tuple.Create('1', Key.D1),
                    Tuple.Create('2', Key.D2),
                    Tuple.Create('3', Key.D3) };
            foreach (var tuple in list)
            {
                var ki = KeyUtil.ConvertToKeyInput(tuple.Item2, ModifierKeys.None);
                Assert.AreEqual(tuple.Item1, ki.Char);
                Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
            }
        }

        [Test]
        public void ConvertToKeyInput5()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.F12, ModifierKeys.Shift | ModifierKeys.Control);
            Assert.AreEqual(VimKey.F12, ki.Key);
            Assert.AreEqual(KeyModifiers.Shift | KeyModifiers.Control, ki.KeyModifiers);
        }

        [Test]
        public void ConvertToKey1()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            Assert.AreEqual(Key.C, KeyUtil.ConvertToKey(ki));
        }

        [Test]
        public void ConvertToKey2()
        {
            var ki = KeyInputUtil.CharWithControlToKeyInput('c');
            Assert.AreEqual(Key.C, KeyUtil.ConvertToKey(ki));
        }

        [Test]
        public void ConvertToKey3()
        {
            var ki = KeyInputUtil.CharWithControlToKeyInput('c');
            Assert.AreEqual(ModifierKeys.Control, KeyUtil.ConvertToKeyAndModifiers(ki).Item2);
        }



    }
}
