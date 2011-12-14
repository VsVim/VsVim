using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    /// <summary>
    /// Summary description for InputUtilTest
    /// </summary>
    [TestFixture]
    public sealed class KeyInputUtilTest
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
                CollectionAssert.Contains(KeyInputUtil.VimKeyCharList, cur);
            }
        }

        [Test]
        public void CharToKeyInput_LowerLetters()
        {
            foreach (var cur in CharsLettersLower)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.AreEqual(cur, ki.Char);
                Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);

                var offset = ((int)cur) - ((int)'a');
                var key = (VimKey)((int)VimKey.LowerA + offset);
                Assert.AreEqual(key, ki.Key);
            }
        }

        [Test]
        public void CharToKeyInput_UpperLetters()
        {
            foreach (var cur in CharsLettersUpper)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.AreEqual(cur, ki.Char);
                Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);

                var offset = ((int)cur) - ((int)'A');
                var key = (VimKey)((int)VimKey.UpperA + offset);
                Assert.AreEqual(key, ki.Key);
            }
        }

        [Test]
        public void CharToKeyInput_AllCoreCharsMapToThemselves()
        {
            foreach (var cur in KeyInputUtil.VimKeyCharList)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(ki.RawChar.IsSome());
                Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
                Assert.AreEqual(cur, ki.Char);
            }
        }

        [Test]
        public void CharToKeyInput_AllOurCharsMapToThemselves()
        {
            foreach (var cur in CharsAll)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(ki.RawChar.IsSome());
                Assert.AreEqual(cur, ki.Char);
            }
        }

        [Test]
        public void CoreKeyInputList_ContainsSpecialKeys()
        {
            var array = new[]
            {
                KeyInputUtil.EnterKey,
                KeyInputUtil.EscapeKey,
                KeyInputUtil.TabKey,
                KeyInputUtil.LineFeedKey,
            };
            foreach (var cur in array)
            {
                Assert.IsTrue(KeyInputUtil.VimKeyInputList.Contains(cur));
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
            KeyInputUtil.VimKeyToKeyInput(VimKey.None);
        }

        /// <summary>
        /// Verify that all values of the VimKey enumeration are different.  This is a large enum 
        /// and it's possible for integrations and simple programming errors to lead to duplicate
        /// values
        /// </summary>
        [Test]
        public void VimKey_AllValuesDifferent()
        {
            HashSet<VimKey> set = new HashSet<VimKey>();
            var all = Enum.GetValues(typeof(VimKey)).Cast<VimKey>().ToList();
            foreach (var value in all)
            {
                Assert.IsTrue(set.Add(value));
            }
            Assert.AreEqual(all.Count, set.Count);
        }

        [Test]
        public void VimKeyToKeyInput3()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>())
            {
                if (cur == VimKey.None || cur == VimKey.RawCharacter)
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
                if (cur == VimKey.None || cur == VimKey.RawCharacter)
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
        public void ChangeKeyModifiers_ShiftWontChangeAlpha()
        {
            foreach (var letter in CharsLettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var upper = KeyInputUtil.CharToKeyInput(Char.ToUpper(letter));
                var lowerWithShift = KeyInputUtil.ChangeKeyModifiers(lower, KeyModifiers.Shift);
                Assert.AreNotEqual(lowerWithShift, upper);
            }
        }

        [Test]
        public void ChangeKeyModifiers_RemoveShiftWontLowerAlpha()
        {
            foreach (var letter in CharsLettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var upper = KeyInputUtil.CharToKeyInput(Char.ToUpper(letter));
                var upperNoShift = KeyInputUtil.ChangeKeyModifiers(upper, KeyModifiers.None);
                Assert.AreNotEqual(lower, upperNoShift);
            }
        }

        [Test]
        public void ChangeKeyModifiers_WontChangeChar()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.OpenBracket);
            var ki2 = KeyInputUtil.ChangeKeyModifiers(ki, KeyModifiers.Shift);
            Assert.AreEqual(ki.Char, ki2.Char);
        }

        [Test]
        public void GetAlternateTarget_ShouldWorkWithAllValues()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputList)
            {
                Assert.IsTrue(KeyInputUtil.GetAlternateTarget(cur).IsSome());
            }
        }

        [Test]
        public void AllAlternatesShouldEqualTheirTarget()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputList)
            {
                var target = KeyInputUtil.GetAlternateTarget(cur).Value;
                Assert.AreEqual(target, cur);
                Assert.AreEqual(target.GetHashCode(), cur.GetHashCode());
            }
        }

        [Test]
        public void VerifyAlternateKeyInputPairListIsComplete()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputPairList)
            {
                var target = cur.Item1;
                var alternate = cur.Item2;
                Assert.AreEqual(alternate, target.GetAlternate().Value);
                Assert.AreEqual(alternate, KeyInputUtil.GetAlternate(target).Value);
                Assert.AreEqual(target, KeyInputUtil.GetAlternateTarget(alternate).Value);
            }

            Assert.AreEqual(KeyInputUtil.AlternateKeyInputPairList.Count(), KeyInputUtil.AlternateKeyInputList.Count());
        }

        /// <summary>
        /// Too many APIs are simply not setup to handle alternate keys and hence we keep them out of the core
        /// list.  APIs which want to include them should use the AlternateKeyInputList property directly
        /// </summary>
        [Test]
        public void AllKeyInputsShouldNotIncludeAlternateKeys()
        {
            foreach (var current in KeyInputUtil.AlternateKeyInputList)
            {
                foreach (var core in KeyInputUtil.VimKeyInputList)
                {
                    // Can't use Equals since the core version of an alternate will be equal.  Just 
                    // check the values manually
                    var bruteEqual =
                        core.Key == current.Key &&
                        core.KeyModifiers == current.KeyModifiers &&
                        core.Char == current.Char;
                    Assert.IsFalse(bruteEqual);
                }
            }
        }
    }
}
