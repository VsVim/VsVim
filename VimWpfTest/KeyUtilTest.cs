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
        private void CharBothway(char c, Key key)
        {
            CharBothway(c, key, ModifierKeys.None);
        }

        private void CharBothway(char c, Key key, ModifierKeys mod)
        {
            var input = InputUtil.CharToKeyInput(c);
            Assert.AreEqual(input.Char, c);
            Assert.AreEqual(key, input.Key);
            Assert.AreEqual(KeyUtil.ConvertToKeyModifiers(mod), input.KeyModifiers);
            var back = KeyUtil.ConvertToKeyInput(key, mod).Char;
            Assert.AreEqual(c, back);
        }

        private void WellKnownBothWays(WellKnownKey wellKnownKey, Key key)
        {
            var left = InputUtil.WellKnownKeyToKeyInput(wellKnownKey);
            var right = KeyUtil.ConvertToKeyInput(key);
            Assert.AreEqual(left, right);
        }

        [Test]
        public void CharBothway()
        {
            CharBothway(' ', Key.Space);
            CharBothway(';', Key.OemSemicolon);
            CharBothway(':', Key.OemSemicolon, ModifierKeys.Shift);
            CharBothway('/', Key.Oem2);
            CharBothway('?', Key.Oem2, ModifierKeys.Shift);
            CharBothway('.', Key.OemPeriod);
            CharBothway('>', Key.OemPeriod, ModifierKeys.Shift);
            CharBothway(',', Key.OemComma);
            CharBothway('<', Key.OemComma, ModifierKeys.Shift);
            CharBothway('[', Key.OemOpenBrackets);
            CharBothway('{', Key.OemOpenBrackets, ModifierKeys.Shift);
            CharBothway(']', Key.OemCloseBrackets);
            CharBothway('}', Key.OemCloseBrackets, ModifierKeys.Shift);
            CharBothway('\b', Key.Back);
            CharBothway('\t', Key.Tab);
            CharBothway('-', Key.OemMinus);
            CharBothway('=', Key.OemPlus);
            CharBothway('+', Key.OemPlus, ModifierKeys.Shift);
        }

        [Test]
        public void WellKnownBothWays()
        {
            WellKnownBothWays(WellKnownKey.LeftKey, Key.Left);
            WellKnownBothWays(WellKnownKey.RightKey, Key.Right);
            WellKnownBothWays(WellKnownKey.UpKey, Key.Up);
            WellKnownBothWays(WellKnownKey.DownKey, Key.Down);
            WellKnownBothWays(WellKnownKey.F1Key, Key.F1);
            WellKnownBothWays(WellKnownKey.F2Key, Key.F2);
            WellKnownBothWays(WellKnownKey.F3Key, Key.F3);
            WellKnownBothWays(WellKnownKey.F4Key, Key.F4);
            WellKnownBothWays(WellKnownKey.F5Key, Key.F5);
            WellKnownBothWays(WellKnownKey.F6Key, Key.F6);
            WellKnownBothWays(WellKnownKey.F7Key, Key.F7);
            WellKnownBothWays(WellKnownKey.F8Key, Key.F8);
            WellKnownBothWays(WellKnownKey.F9Key, Key.F9);
            WellKnownBothWays(WellKnownKey.F10Key, Key.F10);
            WellKnownBothWays(WellKnownKey.F11Key, Key.F11);
            WellKnownBothWays(WellKnownKey.F12Key, Key.F12);
            WellKnownBothWays(WellKnownKey.DeleteKey, Key.Delete);
            WellKnownBothWays(WellKnownKey.EscapeKey, Key.Escape);
        }

        [Test]
        public void KeyAndModifierToKeyInput1()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.A, ModifierKeys.Shift);
            Assert.AreEqual('A', ki.Char);
            Assert.AreEqual(Key.A, ki.Key);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Test]
        public void KeyAndModifierToKeyInput2()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.A, ModifierKeys.None);
            Assert.AreEqual('a', ki.Char);
            Assert.AreEqual(Key.A, ki.Key);
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
        }

        [Test]
        public void KeyAndModifierToKeyInput3()
        {
            var ki = KeyUtil.ConvertToKeyInput(Key.A, ModifierKeys.Control);
            Assert.AreEqual('a', ki.Char);
            Assert.AreEqual(Key.A, ki.Key);
            Assert.AreEqual(KeyModifiers.Control, ki.KeyModifiers);
        }

        [Test]
        public void KeyandModifierToKeyInput4()
        {
            var list = new List<Tuple<char,Key>>() {
                    Tuple.Create('1', Key.D1),
                    Tuple.Create('2', Key.D2),
                    Tuple.Create('3', Key.D3) };
            foreach (var tuple in list)
            {
                var ki = KeyUtil.ConvertToKeyInput(tuple.Item2, ModifierKeys.None);
                Assert.AreEqual(tuple.Item1, ki.Char);
                Assert.AreEqual(tuple.Item2, ki.Key);
                Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
            }
        }

    }
}
