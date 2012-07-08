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
        public const string CharLettersLower = "abcdefghijklmnopqrstuvwxyz";
        public const string CharLettersUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string CharRest = " !@#$%^&*()[]{}-_=+\\|'\",<>./?:;`~1234567890";
        public const string CharAll =
            CharLettersLower +
            CharLettersUpper +
            CharRest;

        private KeyInput MaybeGetAlternate(KeyInput keyInput)
        {
            var value = KeyInputUtil.GetAlternate(keyInput);
            return value.IsSome()
                ? value.Value
                : keyInput;
        }

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

        /// <summary>
        /// Make sure that our control plus alpha case is properly handled 
        /// </summary>
        [Fact]
        public void ApplyModifiers_ControlToAlpha()
        {
            var baseCharCode = 0x1;
            for (var i = 0; i < CharLettersLower.Length; i++)
            {
                var target = (char)(baseCharCode + i);
                var keyInput = KeyInputUtil.CharToKeyInput(CharLettersLower[i]);
                var found = KeyInputUtil.ApplyModifiers(keyInput, KeyModifiers.Control);

                // The ApplyModifiers function will always return the primary key here but
                // we are interested in the alternate key in some cases
                found = MaybeGetAlternate(found);

                Assert.Equal(target, found.Char);
                Assert.Equal(keyInput.Key, found.Key);
                Assert.Equal(KeyModifiers.Control, found.KeyModifiers);
            }
        }

        /// <summary>
        /// Make sure that our control plus alpha case is properly handled 
        /// </summary>
        [Fact]
        public void ApplyModifiers_ControlToAlphaUpper()
        {
            var baseCharCode = 0x1;
            for (var i = 0; i < CharLettersUpper.Length; i++)
            {
                var target = (char)(baseCharCode + i);
                var keyInput = KeyInputUtil.CharToKeyInput(CharLettersUpper[i]);
                var found = KeyInputUtil.ApplyModifiers(keyInput, KeyModifiers.Control);

                // The ApplyModifiers function will always return the primary key here but
                // we are interested in the alternate key in some cases
                found = MaybeGetAlternate(found);
                Assert.Equal(target, found.Char);

                // Applying control to an upper case letter should normalize it to the 
                // lower case letter 
                var keyInputLower = KeyInputUtil.CharToKeyInput(Char.ToLower(keyInput.Char));
                Assert.Equal(keyInputLower.Key, found.Key);
                Assert.Equal(KeyModifiers.Control, found.KeyModifiers);
            }
        }

        /// <summary>
        /// If the ApplyModifiers call with less modifiers then the KeyInput shouldn't be affected
        /// and should return the original input
        /// </summary>
        [Fact]
        public void ApplyModifiers_Less()
        {
            var left = KeyInputUtil.CharWithControlToKeyInput('c');
            var right = KeyInputUtil.ApplyModifiers(left, KeyModifiers.None);
            Assert.Equal(left, right);
        }

        [Fact]
        public void CoreCharList1()
        {
            foreach (var cur in CharAll)
            {
                Assert.True(KeyInputUtil.VimKeyCharList.Contains(cur));
            }
        }

        [Fact]
        public void CharToKeyInput_LowerLetters()
        {
            foreach (var cur in CharLettersLower)
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
            foreach (var cur in CharLettersUpper)
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
                Assert.Equal(cur, ki.Char);

                if (CharAll.Contains(cur))
                {
                    Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
                }
            }
        }

        [Fact]
        public void CharToKeyInput_AllOurCharsMapToThemselves()
        {
            foreach (var cur in CharAll)
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

        /// <summary>
        /// Apply the modifiers to all non-alpha keys in the system and make sure that they
        /// produce a control + the original key
        /// </summary>
        [Fact]
        public void ApplyModifiersControlToAllKeysNonAlpha()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>())
            {
                if (cur == VimKey.None || cur == VimKey.RawCharacter || cur == VimKey.Question)
                {
                    continue;
                }

                if (Char.IsLetter(KeyInputUtil.VimKeyToKeyInput(cur).Char))
                {
                    continue;
                }

                var keyInput = KeyInputUtil.ApplyModifiersToVimKey(cur, KeyModifiers.Control);
                keyInput = MaybeGetAlternate(keyInput);
                Assert.Equal(cur, keyInput.Key);
                Assert.Equal(KeyModifiers.Control, keyInput.KeyModifiers & KeyModifiers.Control);
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
            foreach (var letter in CharLettersLower)
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
            foreach (var letter in CharLettersLower)
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
                Assert.True(KeyInputUtil.GetPrimary(cur).IsSome());
            }
        }

        [Fact]
        public void AllAlternatesShouldEqualTheirTarget()
        {
            foreach (var cur in KeyInputUtil.AlternateKeyInputList)
            {
                var target = KeyInputUtil.GetPrimary(cur).Value;
                Assert.Equal(target, cur);
                Assert.Equal(target.GetHashCode(), cur.GetHashCode());
            }
        }

        /// <summary>
        /// The char values should be equal between alternate keys and their primaries
        /// </summary>
        public void AlternateShouldHaveSameChar()
        {

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
                Assert.Equal(target, KeyInputUtil.GetPrimary(alternate).Value);
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

        /// <summary>
        /// There are 7 alternate KeyInput values defined in key-notation.  We should have them all
        /// </summary>
        [Fact]
        public void AlternateKeyInputComplete()
        {
            Assert.Equal(7, KeyInputUtil.AlternateKeyInputList.Length);
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

        /// <summary>
        /// The CharWithControlToKeyInput method should be routed through ApplyModifiers and 
        /// produce normalized KeyInput values
        /// </summary>
        [Fact]
        public void CharWithControlToKeyInput_Alpha()
        {
            foreach (var cur in CharLettersLower)
            {
                var left = KeyInputUtil.CharWithControlToKeyInput(cur);
                var right = KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput(cur), KeyModifiers.Control);
                Assert.Equal(right, left);
            }
        }

        /// <summary>
        /// The CharWithControlToKeyInput method should be routed through ApplyModifiers and 
        /// produce normalized KeyInput values even for non-alpha characters
        /// </summary>
        [Fact]
        public void CharWithControlToKeyInput_NonAlpha()
        {
            var keyInput = KeyInputUtil.CharWithControlToKeyInput('#');
            Assert.Equal(VimKey.Pound, keyInput.Key);
            Assert.Equal(KeyModifiers.Control, keyInput.KeyModifiers);
        }

        /// <summary>
        /// Make sure that the alpha keys produce the correct alpha characters
        /// </summary>
        [Fact]
        public void VimKeyToKeyInput_Alpha()
        {
            foreach (var cur in CharLettersLower)
            {
                var name = String.Format("Lower{0}", Char.ToUpper(cur));
                var vimKey = (VimKey)Enum.Parse(typeof(VimKey), name);
                Assert.Equal(KeyInputUtil.CharToKeyInput(cur), KeyInputUtil.VimKeyToKeyInput(vimKey));
            }
        }

        /// <summary>
        /// Do some sanity checks on the counts to make sure that everything is in line
        /// with the expectations
        /// </summary>
        [Fact]
        public void VimKeyToKeyInput_Sanity()
        {
            var count = Enum.GetValues(typeof(VimKey)).Length;

            // There are 2 keys we don't produce raw values for: RawChar and None
            Assert.Equal(count - 2, KeyInputUtil.VimKeyRawData.Length);
            Assert.Equal(count - 2, KeyInputUtil.VimKeyToKeyInputMap.Count);
        }
    }
}
