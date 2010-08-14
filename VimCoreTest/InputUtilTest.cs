using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;

namespace VimCore.Test
{
    /// <summary>
    /// Summary description for InputUtilTest
    /// </summary>
    [TestFixture]
    public class InputUtilTest
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
            Assert.IsTrue(all.All(x => InputUtil.CharToKeyInput(x).Char == x));
            Assert.IsTrue(upperCase.All(x => InputUtil.CharToKeyInput(x).KeyModifiers == KeyModifiers.Shift));
        }

        [Test]
        public void MinusKey1()
        {
            var ki = InputUtil.CharToKeyInput('_');
            Assert.AreEqual('_', ki.Char);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Test]
        public void MinusKey2()
        {
            var ki = InputUtil.CharToKeyInput('-');
            Assert.AreEqual('-', ki.Char);
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
        }

        [Test]
        public void Percent1()
        {
            var ki = InputUtil.CharToKeyInput('%');
            Assert.AreEqual('%', ki.Char);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Test]
        public void Tilde1()
        {
            var ki = InputUtil.CharToKeyInput('~');
            Assert.AreEqual('~', ki.Char);
        }

        [Test, Description("In the case of a bad key it should return the default key")]
        public void WellKnownKeyToKeyInput1()
        {
            var key = InputUtil.VimKeyToKeyInput(VimKey.NotWellKnown);
            Assert.IsNotNull(key);
        }

        [Test]
        public void WellKnownKeyToKeyInput2()
        {
            var key = InputUtil.VimKeyToKeyInput(VimKey.Enter);
            Assert.AreEqual(VimKey.Enter, key.Key);
        }

        [Test]
        public void Keypad1()
        {
            var left = InputUtil.CharToKeyInput('+');
            var right = InputUtil.VimKeyToKeyInput(VimKey.KeypadPlus);
            Assert.AreNotEqual(left, right);
        }

        [Test]
        public void Keypad2()
        {
            var left = InputUtil.CharToKeyInput('-');
            var right = InputUtil.VimKeyToKeyInput(VimKey.KeypadMinus);
            Assert.AreNotEqual(left, right);
        }

        [Test]
        public void TryVirtualKeyCodeAndModifiersToKeyInput1()
        {
            Action<char> backandForth = c =>
            {
                var opt = InputUtil.TryCharToVirtualKeyAndModifiers(c);
                Assert.IsTrue(opt.IsSome());
                var virtualKeyCode = opt.Value.Item1;
                var keyModifiers = opt.Value.Item2;
                var ki1 = InputUtil.CharToKeyInput(c);
                var ki2 = InputUtil.VirtualKeyCodeAndModifiersToKeyInput(virtualKeyCode, keyModifiers);
                Assert.AreEqual(ki1, ki2);
            };

            InputUtil.CoreCharacters.ToList().ForEach(backandForth);
        }

        [Test]
        [Description("Apply shift to alpha")]
        public void ChangeKeyModifiers1()
        {
            foreach (var letter in LettersLower)
            {
                var lower = InputUtil.CharToKeyInput(letter);
                var upper = InputUtil.CharToKeyInput(Char.ToUpper(letter));
                var opt = InputUtil.ChangeKeyModifiers(lower, KeyModifiers.Shift);
                Assert.AreEqual(upper, opt);
            }
        }

        [Test]
        [Description("Apply a shift remove to alhpa")]
        public void ChangeKeyModifiers2()
        {
            foreach (var letter in LettersLower)
            {
                var lower = InputUtil.CharToKeyInput(letter);
                var upper = InputUtil.CharToKeyInput(Char.ToUpper(letter));
                var opt = InputUtil.ChangeKeyModifiers(upper, KeyModifiers.None);
                Assert.AreEqual(lower, opt);
            }
        }

        [Test]
        [Description("Apply control")]
        public void ChangeKeyModifiers3()
        {
            foreach (var letter in LettersLower)
            {
                var lower = InputUtil.CharToKeyInput(letter);
                var opt = InputUtil.ChangeKeyModifiers(lower, KeyModifiers.Control);
                Assert.AreEqual(KeyModifiers.Control, opt.KeyModifiers);
            }
        }

        [Test]
        [Description("Shift to non-alpha")]
        public void ChangeKeyModifiers4()
        {
            var ki1 = InputUtil.CharToKeyInput(']');
            var ki2 = InputUtil.ChangeKeyModifiers(ki1, KeyModifiers.Shift);
            Assert.AreEqual(InputUtil.CharToKeyInput('}'), ki2);
        }
    }
}
