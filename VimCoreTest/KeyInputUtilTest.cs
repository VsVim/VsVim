using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;

namespace VimCore.Test
{
    /// <summary>
    /// Summary description for InputUtilTest
    /// </summary>
    [TestFixture]
    public class KeyInputUtilTest
    {
        public static readonly IEnumerable<char> LettersLower = "abcdefghijklmnopqrstuvwxyz";

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
            Assert.IsTrue(all.All(x => KeyInputUtil.CharToKeyInput(x).Char == x));
            Assert.IsTrue(upperCase.All(x => KeyInputUtil.CharToKeyInput(x).KeyModifiers == KeyModifiers.Shift));
        }

        [Test]
        public void MinusKey1()
        {
            var ki = KeyInputUtil.CharToKeyInput('_');
            Assert.AreEqual('_', ki.Char);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Test]
        public void MinusKey2()
        {
            var ki = KeyInputUtil.CharToKeyInput('-');
            Assert.AreEqual('-', ki.Char);
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
        }

        [Test]
        public void Percent1()
        {
            var ki = KeyInputUtil.CharToKeyInput('%');
            Assert.AreEqual('%', ki.Char);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Test]
        public void Tilde1()
        {
            var ki = KeyInputUtil.CharToKeyInput('~');
            Assert.AreEqual('~', ki.Char);
        }

        [Test, Description("In the case of a bad key it should return the default key")]
        public void WellKnownKeyToKeyInput1()
        {
            var key = KeyInputUtil.VimKeyToKeyInput(VimKey.NotWellKnown);
            Assert.IsNotNull(key);
        }

        [Test]
        public void WellKnownKeyToKeyInput2()
        {
            var key = KeyInputUtil.VimKeyToKeyInput(VimKey.Enter);
            Assert.AreEqual(VimKey.Enter, key.Key);
        }

        [Test]
        public void Keypad1()
        {
            var left = KeyInputUtil.CharToKeyInput('+');
            var right = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadPlus);
            Assert.AreNotEqual(left, right);
        }

        [Test]
        public void Keypad2()
        {
            var left = KeyInputUtil.CharToKeyInput('-');
            var right = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadMinus);
            Assert.AreNotEqual(left, right);
        }

        [Test]
        [Description("Apply shift to alpha")]
        public void ChangeKeyModifiers1()
        {
            foreach (var letter in LettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var upper = KeyInputUtil.CharToKeyInput(Char.ToUpper(letter));
                var opt = KeyInputUtil.ChangeKeyModifiers(lower, KeyModifiers.Shift);
                Assert.AreEqual(upper, opt);
            }
        }

        [Test]
        [Description("Apply a shift remove to alhpa")]
        public void ChangeKeyModifiers2()
        {
            foreach (var letter in LettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var upper = KeyInputUtil.CharToKeyInput(Char.ToUpper(letter));
                var opt = KeyInputUtil.ChangeKeyModifiers(upper, KeyModifiers.None);
                Assert.AreEqual(lower, opt);
            }
        }

        [Test]
        [Description("Apply control")]
        public void ChangeKeyModifiers3()
        {
            foreach (var letter in LettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var opt = KeyInputUtil.ChangeKeyModifiers(lower, KeyModifiers.Control);
                Assert.AreEqual(KeyModifiers.Control, opt.KeyModifiers);
            }
        }

        [Test]
        [Description("Shift to non-alpha")]
        public void ChangeKeyModifiers4()
        {
            var ki1 = KeyInputUtil.CharToKeyInput(']');
            var ki2 = KeyInputUtil.ChangeKeyModifiers(ki1, KeyModifiers.Shift);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('}'), ki2);
        }
    }
}
