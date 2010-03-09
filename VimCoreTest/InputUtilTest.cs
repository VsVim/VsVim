using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.FSharp.Core;
using System.Windows.Input;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for InputUtilTest
    /// </summary>
    [TestFixture]
    public class InputUtilTest
    {
        /// <summary>
        /// Make sure that all letters convert
        /// </summary>
        [Test()]
        public void CharToKeyTest()
        {
            var startLower = (int)('a');
            var startUpper = (int)('A');
            var lowerCase = Enumerable.Range(0, 26).Select(x => (char)(x + startLower));
            var upperCase = Enumerable.Range(0, 26).Select(x => (char)(x + startUpper));
            var all = lowerCase.Concat(upperCase);
            Assert.IsTrue(all.All(x => InputUtil.CharToKeyInput(x).Char == x));
            Assert.IsTrue(upperCase.All(x => InputUtil.CharToKeyInput(x).ModifierKeys == ModifierKeys.Shift));
        }

        private void CharBothway(char c, Key key)
        {
            CharBothway(c, key, ModifierKeys.None);
        }

        private void CharBothway(char c, Key key, ModifierKeys mod)
        {
            var input = InputUtil.CharToKeyInput(c);
            Assert.AreEqual(input.Char, c);
            Assert.AreEqual(key, input.Key);
            Assert.AreEqual(mod, input.ModifierKeys);
            var back = InputUtil.KeyInputToChar(new KeyInput(c, key, mod));
            Assert.AreEqual(c, back);
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
        public void KeyAndModifierToKeyInput1()
        {
            var ki = InputUtil.KeyAndModifierToKeyInput(Key.A, ModifierKeys.Shift);
            Assert.AreEqual('A', ki.Char);
            Assert.AreEqual(Key.A, ki.key);
            Assert.AreEqual(ModifierKeys.Shift, ki.ModifierKeys);
        }

        [Test]
        public void KeyAndModifierToKeyInput2()
        {
            var ki = InputUtil.KeyAndModifierToKeyInput(Key.A, ModifierKeys.None);
            Assert.AreEqual('a', ki.Char);
            Assert.AreEqual(Key.A, ki.key);
            Assert.AreEqual(ModifierKeys.None, ki.ModifierKeys);
        }

        [Test]
        public void MinusKey1()
        {
            var ki = InputUtil.CharToKeyInput('_');
            Assert.AreEqual('_', ki.Char);
            Assert.AreEqual(Key.OemMinus, ki.Key);
            Assert.AreEqual(ModifierKeys.Shift, ki.ModifierKeys);
        }

        [Test]
        public void MinusKey2()
        {
            var ki = InputUtil.CharToKeyInput('-');
            Assert.AreEqual('-', ki.Char);
            Assert.AreEqual(Key.OemMinus, ki.Key);
            Assert.AreEqual(ModifierKeys.None, ki.ModifierKeys);
        }

        [Test]
        public void Percent1()
        {
            var ki = InputUtil.CharToKeyInput('%');
            Assert.AreEqual('%', ki.Char);
            Assert.AreEqual(Key.D5, ki.Key);
            Assert.AreEqual(ModifierKeys.Shift, ki.ModifierKeys);
        }
    }
}
