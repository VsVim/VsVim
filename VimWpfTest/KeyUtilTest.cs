using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Windows.Input;

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
            var left = InputUtil.CharToKeyInput(c);
            var right = KeyUtil.ConvertToKeyInput(key, mod);
            Assert.AreEqual(left, right);
        }

        private void WellKnownBothWays(VimKey wellKnownKey, Key key)
        {
            var left = InputUtil.VimKeyToKeyInput(wellKnownKey);
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
        }

        [Test]
        public void WellKnownBothWays()
        {
            WellKnownBothWays(VimKey.LeftKey, Key.Left);
            WellKnownBothWays(VimKey.RightKey, Key.Right);
            WellKnownBothWays(VimKey.UpKey, Key.Up);
            WellKnownBothWays(VimKey.DownKey, Key.Down);
            WellKnownBothWays(VimKey.F1Key, Key.F1);
            WellKnownBothWays(VimKey.F2Key, Key.F2);
            WellKnownBothWays(VimKey.F3Key, Key.F3);
            WellKnownBothWays(VimKey.F4Key, Key.F4);
            WellKnownBothWays(VimKey.F5Key, Key.F5);
            WellKnownBothWays(VimKey.F6Key, Key.F6);
            WellKnownBothWays(VimKey.F7Key, Key.F7);
            WellKnownBothWays(VimKey.F8Key, Key.F8);
            WellKnownBothWays(VimKey.F9Key, Key.F9);
            WellKnownBothWays(VimKey.F10Key, Key.F10);
            WellKnownBothWays(VimKey.F11Key, Key.F11);
            WellKnownBothWays(VimKey.F12Key, Key.F12);
            WellKnownBothWays(VimKey.DeleteKey, Key.Delete);
            WellKnownBothWays(VimKey.EscapeKey, Key.Escape);
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
            var list = new List<Tuple<char,Key>>() {
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
            Assert.AreEqual(VimKey.F12Key, ki.Key);
            Assert.AreEqual(KeyModifiers.Shift | KeyModifiers.Control, ki.KeyModifiers);
        }

    }
}
