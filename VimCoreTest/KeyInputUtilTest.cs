using System;
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
    public class KeyInputUtilTest
    {
        public const string CharsLettersLower = "abcdefghijklmnopqrstuvwxyz";
        public const string CharsLettersUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string CharsRest = " !@#$%^&*()[]{}-_=+\\|'\",<>./?:;`~1234567890";
        public const string CharsAll =
            CharsLettersLower +
            CharsLettersUpper +
            CharsRest;

        [Test]
        public void CoreCharList1()
        {
            foreach (var cur in CharsAll)
            {
                CollectionAssert.Contains(KeyInputUtil.CoreCharacterList, cur);
            }
        }

        /// <summary>
        /// Make sure that all letters convert
        /// </summary>
        [Test]
        public void CharToKeyInput1()
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
        [Description("All of the core characters should map back to themselves")]
        public void CharToKeyInput2()
        {
            foreach (var cur in KeyInputUtil.CoreCharacterList)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(ki.RawChar.IsSome());
                Assert.AreEqual(cur, ki.Char);
            }
        }

        [Test]
        [Description("Verify our list of core characters map back to themselves")]
        public void CharToKeyInput4()
        {
            foreach (var cur in CharsAll)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(ki.RawChar.IsSome());
                Assert.AreEqual(cur, ki.Char);
            }
        }

        [Test]
        public void MinusKey1()
        {
            var ki = KeyInputUtil.CharToKeyInput('_');
            Assert.AreEqual('_', ki.Char);
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
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
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
        }

        [Test]
        public void Tilde1()
        {
            var ki = KeyInputUtil.CharToKeyInput('~');
            Assert.AreEqual('~', ki.Char);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void VimKeyToKeyInput1()
        {
            KeyInputUtil.VimKeyToKeyInput(VimKey.NotWellKnown);
        }

        [Test]
        public void VimKeyToKeyInput2()
        {
            var key = KeyInputUtil.VimKeyToKeyInput(VimKey.Enter);
            Assert.AreEqual(VimKey.Enter, key.Key);
        }

        [Test]
        public void VimKeyToKeyInput3()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>())
            {
                if (cur == VimKey.NotWellKnown)
                {
                    continue;
                }

                var ki = KeyInputUtil.VimKeyToKeyInput(cur);
                Assert.AreEqual(cur, ki.Key);
            }
        }

        [Test]
        public void VimKeyAndModifiersToKeyInput1()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>())
            {
                if (cur == VimKey.NotWellKnown)
                {
                    continue;
                }

                var ki = KeyInputUtil.VimKeyAndModifiersToKeyInput(cur, KeyModifiers.Control);
                Assert.AreEqual(cur, ki.Key);
                Assert.AreEqual(KeyModifiers.Control, ki.KeyModifiers & KeyModifiers.Control);
            }
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
            foreach (var letter in CharsLettersLower)
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
            foreach (var letter in CharsLettersLower)
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
            foreach (var letter in CharsLettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var opt = KeyInputUtil.ChangeKeyModifiers(lower, KeyModifiers.Control);
                Assert.AreEqual(KeyModifiers.Control, opt.KeyModifiers);
            }
        }

        [Test]
        [Description("Shift to non-alpha doesn't matter in Vim land")]
        public void ChangeKeyModifiers4()
        {
            var ki1 = KeyInputUtil.CharToKeyInput(']');
            var ki2 = KeyInputUtil.ChangeKeyModifiers(ki1, KeyModifiers.Shift);
            Assert.AreEqual(']', ki2.Char);
        }
    }
}
