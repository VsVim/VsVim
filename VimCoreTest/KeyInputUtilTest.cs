using System;
using System.Collections.Generic;
using System.Linq;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Summary description for InputUtilTest
    /// </summary>
    public sealed class KeyInputUtilTest
    {
        public const string CharsLettersLower = "abcdefghijklmnopqrstuvwxyz";
        public const string CharsLettersUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string CharsRest = " !@#$%^&*()[]{}-_=+\\|'\",<>./?:;`~1234567890";
        public const string CharsAll =
            CharsLettersLower +
            CharsLettersUpper +
            CharsRest;

        /// <summary>
        /// Verify that we properly unify upper case character combinations.
        /// </summary>
        [Fact]
        public void ApplyModifiers_UpperCase()
        {
            var keyInput = KeyInputUtil.CharToKeyInput('A');
            Assert.Equal(keyInput, KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput('a'), KeyModifiers.Shift));
            Assert.Equal(keyInput, KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput('A'), KeyModifiers.Shift));
        }

        [Fact]
        public void ApplyModifiers_ShiftToNonAlpha()
        {
            var keyInput = KeyInputUtil.ApplyModifiers(KeyInputUtil.TabKey, KeyModifiers.Shift);
            Assert.Equal(KeyModifiers.Shift, keyInput.KeyModifiers);
            Assert.Equal(VimKey.Tab, keyInput.Key);
        }

        /// <summary>
        /// There is a large set of characters for which normal input can't produce a shift modifier
        /// </summary>
        [Fact]
        public void ApplyModifiers_ShiftToSpecialChar()
        {
            var list = new[] { '<', '>', '(', '}' };
            foreach (var cur in list)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(cur);
                keyInput = KeyInputUtil.ApplyModifiers(keyInput, KeyModifiers.Shift);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
            }
        }

        /// <summary>
        /// Check for the special inputs which have chars to which shift is special
        /// </summary>
        [Fact]
        public void ApplyModifiers_ShiftToNonSpecialChar()
        {
            var list = new[] { VimKey.Back, VimKey.Escape, VimKey.Tab };
            foreach (var cur in list)
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(cur);
                keyInput = KeyInputUtil.ApplyModifiers(keyInput, KeyModifiers.Shift);
                Assert.Equal(KeyModifiers.Shift, keyInput.KeyModifiers);
            }
        }

        [Fact]
        public void CoreCharList1()
        {
            foreach (var cur in CharsAll)
            {
                Assert.True(KeyInputUtil.VimKeyCharList.Contains(cur));
            }
        }

        [Fact]
        public void CharToKeyInput_LowerLetters()
        {
            foreach (var cur in CharsLettersLower)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.Equal(cur, ki.Char);
                Assert.Equal(KeyModifiers.None, ki.KeyModifiers);

                var offset = ((int)cur) - ((int)'a');
                var key = (VimKey)((int)VimKey.LowerA + offset);
                Assert.Equal(key, ki.Key);
            }
        }

        [Fact]
        public void CharToKeyInput_UpperLetters()
        {
            foreach (var cur in CharsLettersUpper)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.Equal(cur, ki.Char);
                Assert.Equal(KeyModifiers.None, ki.KeyModifiers);

                var offset = ((int)cur) - ((int)'A');
                var key = (VimKey)((int)VimKey.UpperA + offset);
                Assert.Equal(key, ki.Key);
            }
        }

        [Fact]
        public void CharToKeyInput_AllCoreCharsMapToThemselves()
        {
            foreach (var cur in KeyInputUtil.VimKeyCharList)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.True(ki.RawChar.IsSome());
                Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
                Assert.Equal(cur, ki.Char);
            }
        }

        [Fact]
        public void CharToKeyInput_AllOurCharsMapToThemselves()
        {
            foreach (var cur in CharsAll)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.True(ki.RawChar.IsSome());
                Assert.Equal(cur, ki.Char);
            }
        }

        [Fact]
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
                Assert.True(KeyInputUtil.VimKeyInputList.Contains(cur));
            }
        }

        [Fact]
        public void MinusKey1()
        {
            var ki = KeyInputUtil.CharToKeyInput('_');
            Assert.Equal('_', ki.Char);
            Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
        }

        [Fact]
        public void MinusKey2()
        {
            var ki = KeyInputUtil.CharToKeyInput('-');
            Assert.Equal('-', ki.Char);
            Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
        }

        [Fact]
        public void Percent1()
        {
            var ki = KeyInputUtil.CharToKeyInput('%');
            Assert.Equal('%', ki.Char);
            Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
        }

        [Fact]
        public void Tilde1()
        {
            var ki = KeyInputUtil.CharToKeyInput('~');
            Assert.Equal('~', ki.Char);
        }

        [Fact]
        public void VimKeyToKeyInput1()
        {
            Assert.Throws<ArgumentException>(() => KeyInputUtil.VimKeyToKeyInput(VimKey.None));
        }

        /// <summary>
        /// Verify that all values of the VimKey enumeration are different.  This is a large enum 
        /// and it's possible for integrations and simple programming errors to lead to duplicate
        /// values
        /// </summary>
        [Fact]
        public void VimKey_AllValuesDifferent()
        {
            HashSet<VimKey> set = new HashSet<VimKey>();
            var all = Enum.GetValues(typeof(VimKey)).Cast<VimKey>().ToList();
            foreach (var value in all)
            {
                Assert.True(set.Add(value));
            }
            Assert.Equal(all.Count, set.Count);
        }

        [Fact]
        public void VimKeyToKeyInput3()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>())
            {
                if (cur == VimKey.None || cur == VimKey.RawCharacter)
                {
                    continue;
                }

                var ki = KeyInputUtil.VimKeyToKeyInput(cur);
                Assert.Equal(cur, ki.Key);
            }
        }

        [Fact]
        public void VimKeyAndModifiersToKeyInput1()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>())
            {
                if (cur == VimKey.None || cur == VimKey.RawCharacter)
                {
                    continue;
                }

                var ki = KeyInputUtil.ApplyModifiersToVimKey(cur, KeyModifiers.Control);
                Assert.Equal(cur, ki.Key);
                Assert.Equal(KeyModifiers.Control, ki.KeyModifiers & KeyModifiers.Control);
            }
        }

        [Fact]
        public void Keypad1()
        {
            var left = KeyInputUtil.CharToKeyInput('+');
            var right = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadPlus);
            Assert.NotEqual(left, right);
        }

        [Fact]
        public void Keypad2()
        {
            var left = KeyInputUtil.CharToKeyInput('-');
            var right = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadMinus);
            Assert.NotEqual(left, right);
        }

        [Fact]
        public void ChangeKeyModifiers_ShiftWontChangeAlpha()
        {
            foreach (var letter in CharsLettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var upper = KeyInputUtil.CharToKeyInput(Char.ToUpper(letter));
                var lowerWithShift = KeyInputUtil.ChangeKeyModifiersDangerous(lower, KeyModifiers.Shift);
                Assert.NotEqual(lowerWithShift, upper);
            }
        }

        [Fact]
        public void ChangeKeyModifiers_RemoveShiftWontLowerAlpha()
        {
            foreach (var letter in CharsLettersLower)
            {
                var lower = KeyInputUtil.CharToKeyInput(letter);
                var upper = KeyInputUtil.CharToKeyInput(Char.ToUpper(letter));
                var upperNoShift = KeyInputUtil.ChangeKeyModifiersDangerous(upper, KeyModifiers.None);
                Assert.NotEqual(lower, upperNoShift);
            }
        }

        [Fact]
        public void ChangeKeyModifiers_WontChangeChar()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.OpenBracket);
            var ki2 = KeyInputUtil.ChangeKeyModifiersDangerous(ki, KeyModifiers.Shift);
            Assert.Equal(ki.Char, ki2.Char);
        }

        [Fact]
        public void GetAlternateTarget_ShouldWorkWithAllValues()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputList)
            {
                Assert.True(KeyInputUtil.GetAlternateTarget(cur).IsSome());
            }
        }

        [Fact]
        public void AllAlternatesShouldEqualTheirTarget()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputList)
            {
                var target = KeyInputUtil.GetAlternateTarget(cur).Value;
                Assert.Equal(target, cur);
                Assert.Equal(target.GetHashCode(), cur.GetHashCode());
            }
        }

        [Fact]
        public void VerifyAlternateKeyInputPairListIsComplete()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputPairList)
            {
                var target = cur.Item1;
                var alternate = cur.Item2;
                Assert.Equal(alternate, target.GetAlternate().Value);
                Assert.Equal(alternate, KeyInputUtil.GetAlternate(target).Value);
                Assert.Equal(target, KeyInputUtil.GetAlternateTarget(alternate).Value);
            }

            Assert.Equal(KeyInputUtil.AlternateKeyInputPairList.Count(), KeyInputUtil.AlternateKeyInputList.Count());
        }

        /// <summary>
        /// Too many APIs are simply not setup to handle alternate keys and hence we keep them out of the core
        /// list.  APIs which want to include them should use the AlternateKeyInputList property directly
        /// </summary>
        [Fact]
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
                    Assert.False(bruteEqual);
                }
            }
        }

        [Fact]
        public void GetNonKeypadEquivalent_Numbers()
        {
            foreach (var i in Enumerable.Range(0, 10))
            {
                var keypadName = "Keypad" + i;
                var keypad = (VimKey)Enum.Parse(typeof(VimKey), keypadName);
                var equivalent = KeyInputUtil.GetNonKeypadEquivalent(KeyInputUtil.VimKeyToKeyInput(keypad));
                Assert.Equal("Number" + i, equivalent.Value.Key.ToString());
            }
        }

        [Fact]
        public void GetNonKeypadEquivalent_Divide()
        {
            var equivalent = KeyInputUtil.GetNonKeypadEquivalent(KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadDivide));
            Assert.Equal(VimKey.Forwardslash, equivalent.Value.Key);
        }

        [Fact]
        public void GetNonKeypadEquivalent_PreserveModifiers()
        {
            var keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.KeypadDivide, KeyModifiers.Control);
            var equivalent = KeyInputUtil.GetNonKeypadEquivalent(keyInput);
            Assert.Equal(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Forwardslash, KeyModifiers.Control), equivalent.Value);
        }
    }
}
