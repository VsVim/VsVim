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
                var ki2 = InputUtil.TryVirtualKeyCodeAndModifiersToKeyInput(virtualKeyCode, keyModifiers).Value;
                Assert.AreEqual(ki1, ki2);
            };

            InputUtil.CoreCharacters.ToList().ForEach(backandForth);
        }

        [Test]
        [Description("Apply shift to alpha")]
        public void TryChangeKeyModifiers1()
        {
            foreach (var letter in LettersLower)
            {
                var lower = InputUtil.CharToKeyInput(letter);
                var upper = InputUtil.CharToKeyInput(Char.ToUpper(letter));
                var opt = InputUtil.TryChangeKeyModifiers(lower, KeyModifiers.Shift);
                Assert.IsTrue(opt.IsSome());
                Assert.AreEqual(upper, opt.Value);
            }
        }

        [Test]
        [Description("Apply a shift remove to alhpa")]
        public void TryChangeKeyModifiers2()
        {
            foreach (var letter in LettersLower)
            {
                var lower = InputUtil.CharToKeyInput(letter);
                var upper = InputUtil.CharToKeyInput(Char.ToUpper(letter));
                var opt = InputUtil.TryChangeKeyModifiers(upper, KeyModifiers.None);
                Assert.IsTrue(opt.IsSome());
                Assert.AreEqual(lower, opt.Value);
            }
        }

        [Test]
        [Description("Apply control")]
        public void TryChangeKeyModifiers3()
        {
            foreach (var letter in LettersLower)
            {
                var lower = InputUtil.CharToKeyInput(letter);
                var opt = InputUtil.TryChangeKeyModifiers(lower, KeyModifiers.Control);
                Assert.IsTrue(opt.IsSome());
                Assert.AreEqual(KeyModifiers.Control, opt.Value.KeyModifiers);
            }
        }

    }
}
