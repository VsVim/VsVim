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
