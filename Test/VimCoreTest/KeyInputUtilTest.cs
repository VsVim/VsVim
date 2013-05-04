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
    public abstract class KeyInputUtilTest
    {
        public const string CharLettersLower = KeyInputUtil.CharLettersLower;
        public const string CharLettersUpper = KeyInputUtil.CharLettersUpper;
        public const string CharRest = KeyInputUtil.CharLettersExtra;
        public const string CharAll =
            CharLettersLower +
            CharLettersUpper +
            CharRest;

        public static readonly string[] AlternateArray = new []
            {
                @"<Nul>&<C-@>&0",
                @"<Tab>&<C-I>&9",
                @"<NL>&<C-J>&10",
                @"<FF>&<C-L>&12",
                @"<CR>&<C-M>&13",
                @"<Enter>&<C-M>&13",
                @"<Return>&<C-M>&13",
                @"<Esc>&<C-[>&27",
                @"<Space>& &32",
                @"<lt>&<&60",
                @"<Bslash>&\&92",
                @"<Bar>&|&124",
            };

        public sealed class ApplyModifiersTest : KeyInputUtilTest
        {
            private static KeyInput ApplyModifiers(char c, KeyModifiers keyModifiers)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(c);
                return KeyInputUtil.ApplyModifiers(keyInput, keyModifiers);
            }

            /// <summary>
            /// Verify that we properly unify upper case character combinations.
            /// </summary>
            [Fact]
            public void UpperCase()
            {
                var keyInput = KeyInputUtil.CharToKeyInput('A');
                Assert.Equal(keyInput, KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput('a'), KeyModifiers.Shift));
                Assert.Equal(keyInput, KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput('A'), KeyModifiers.Shift));
            }

            /// <summary>
            /// The shift key doesn't affect the display of tab
            /// </summary>
            [Fact]
            public void ShiftToTab()
            {
                var keyInput = KeyInputUtil.ApplyModifiers(KeyInputUtil.TabKey, KeyModifiers.Shift);
                Assert.Equal(KeyModifiers.Shift, keyInput.KeyModifiers);
                Assert.Equal(VimKey.Tab, keyInput.Key);
                Assert.Equal('\t', keyInput.Char);
            }

            /// <summary>
            /// There is a large set of characters for which normal input can't produce a shift modifier
            /// </summary>
            [Fact]
            public void ShiftToSpecialChar()
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
            public void ShiftToNonSpecialChar()
            {
                var list = new[] 
                {
                    KeyInputUtil.VimKeyToKeyInput(VimKey.Back),
                    KeyInputUtil.VimKeyToKeyInput(VimKey.Escape),
                    KeyInputUtil.TabKey
                };

                foreach (var current in list)
                {
                    var keyInput = KeyInputUtil.ApplyModifiers(current, KeyModifiers.Shift);
                    Assert.Equal(KeyModifiers.Shift, keyInput.KeyModifiers);
                }
            }

            /// <summary>
            /// Make sure that our control plus alpha case is properly handled 
            /// </summary>
            [Fact]
            public void ControlToAlpha()
            {
                var baseCharCode = 0x1;
                for (var i = 0; i < CharLettersLower.Length; i++)
                {
                    var target = (char)(baseCharCode + i);
                    var keyInput = KeyInputUtil.CharToKeyInput(CharLettersLower[i]);
                    var found = KeyInputUtil.ApplyModifiers(keyInput, KeyModifiers.Control);

                    Assert.Equal(target, found.Char);
                    Assert.Equal(KeyModifiers.None, found.KeyModifiers);
                }
            }

            /// <summary>
            /// Make sure that our control plus alpha case is properly handled 
            /// </summary>
            [Fact]
            public void ControlToAlphaUpper()
            {
                var baseCharCode = 0x1;
                for (var i = 0; i < CharLettersUpper.Length; i++)
                {
                    var target = (char)(baseCharCode + i);
                    var keyInputUpper = KeyInputUtil.ApplyModifiersToChar(CharLettersUpper[i], KeyModifiers.Control);
                    var keyInputLower = KeyInputUtil.ApplyModifiersToChar(CharLettersLower[i], KeyModifiers.Control);
                    Assert.Equal(keyInputUpper, keyInputLower);
                }
            }

            /// <summary>
            /// If the ApplyModifiers call with less modifiers then the KeyInput shouldn't be affected
            /// and should return the original input
            /// </summary>
            [Fact]
            public void Less()
            {
                var left = KeyInputUtil.CharWithControlToKeyInput('c');
                var right = KeyInputUtil.ApplyModifiers(left, KeyModifiers.None);
                Assert.Equal(left, right);
            }

            /// <summary>
            /// The application of Alt to the letter a should result in the character á 
            /// </summary>
            [Fact]
            public void AltToA()
            {
                var keyInput = ApplyModifiers('a', KeyModifiers.Alt);
                Assert.Equal('á', keyInput.Char);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
                Assert.Equal(VimKey.RawCharacter, keyInput.Key);
            }

            [Fact]
            public void AltToUpperA()
            {
                var keyInput = ApplyModifiers('a', KeyModifiers.Alt | KeyModifiers.Shift);
                Assert.Equal('Á', keyInput.Char);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
                Assert.Equal(VimKey.RawCharacter, keyInput.Key);
            }

            [Fact]
            public void AltToDigit()
            {
                const string expected = "°±²³´µ¶·¸¹";
                for (var i = 0; i < expected.Length; i++)
                {
                    var c = Char.Parse(i.ToString());
                    var keyInput = ApplyModifiers(c, KeyModifiers.Alt);
                    Assert.Equal(expected[i], keyInput.Char);
                    Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
                    Assert.Equal(VimKey.RawCharacter, keyInput.Key);
                }
            }

            /// <summary>
            /// In general the alt key combined with the simple keys should mirror the simple 
            /// values when they are created simply by char
            /// </summary>
            [Fact]
            public void AltMirror()
            {
                const string expected = "Á°±²³´µ¶·¸¹";
                const string source = "A0123456789";
                for (var i = 0; i < source.Length; i++)
                {
                    var left = KeyInputUtil.CharToKeyInput(expected[i]);
                    var right = KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput(source[i]), KeyModifiers.Alt);
                    Assert.Equal(left, right);
                }
            }
        }

        public sealed class EquivalentKeyTest
        {
            /// <summary>
            /// The ':help key-notation' page lists several key notations which have equivalent 
            /// non-named values.  The documentation is incorrect though in 2 cases: BS and Del.  
            /// Both of these keys have equivalent functions yet they represent different values
            /// internally because they can have separate key mappings.  This test is used to 
            /// confrim that we correctly implement this behavior.
            /// 
            /// Note: This behavior may be different for non-GUI versions of VIM.  But for 
            /// GUI versions this behavior is as tested below
            /// </summary>
            [Fact]
            public void AlternateSpecialCases()
            {
                Assert.NotEqual(KeyNotationUtil.StringToKeyInput("<BS>"), KeyNotationUtil.StringToKeyInput("<C-H>"));
                Assert.NotEqual(KeyNotationUtil.StringToKeyInput("<Del>"), KeyInputUtil.CharToKeyInput((char)127));
            }

            /// <summary>
            /// Make sure the equivalent keys are all equal to their decimal value.  This can be 
            /// verified in gVim by using the undocumented Char- key mapping syntax.  Ex
            ///   imap {Char-27} the escape key
            /// </summary>
            [Fact]
            public void EquivalentKeysToDecimal()
            {
                var list = new[] 
                { 
                    "Nul-0",
                    "Tab-9",
                    "NL-10",
                    "FF-12",
                    "CR-13",
                    "Return-13",
                    "Enter-13",
                    "Esc-27",
                    "Space-32",
                    "lt-60",
                    "Bslash-92",
                    "Bar-124" 
                };

                foreach (var entry in list)
                {
                    var pair = entry.Split('-');
                    var name = String.Format("<{0}>", pair[0]);
                    var c = (char)Int32.Parse(pair[1]);
                    var left = KeyNotationUtil.StringToKeyInput(name);
                    var right = KeyInputUtil.CharToKeyInput(c);
                    Assert.Equal(left, right);
                }
            }

            [Fact]
            public void AllAlternatesShouldEqualTheirTarget()
            {
                foreach (var current in AlternateArray)
                {
                    var all = current.Split('&');
                    var left = KeyNotationUtil.StringToKeyInput(all[0]);
                    var right = KeyNotationUtil.StringToKeyInput(all[1]);
                    Assert.Equal(left, right);
                    if (!String.IsNullOrEmpty(all[2]))
                    {
                        var number = Int32.Parse(all[2]);
                        var c = (char)number;
                        var third = KeyInputUtil.CharToKeyInput(c);
                        Assert.Equal(left, third);
                        Assert.Equal(right, third);
                    }
                }
            }

            /// <summary>
            /// Vim doesn't distinguish between a # and a Shift+# key.  Ensure that this logic holds up at 
            /// this layer
            /// </summary>
            [Fact]
            public void GetKeyInput_PoundWithShift()
            {
                var keyInput = KeyInputUtil.CharToKeyInput('#');
                Assert.Equal(keyInput, KeyInputUtil.ApplyModifiers(keyInput, KeyModifiers.Shift));
            }

            /// <summary>
            /// There are 3 alpha keys which are equivalent to named vim keys.  Make sure that they
            /// properly bind
            /// </summary>
            [Fact]
            public void ControlAlphaSpecial()
            {
                var list = new[] { 'j', 'l', 'm', 'i' };
                foreach (var current in list)
                {
                    var c = (char)(0x1 + (current - 'a'));
                    var keyInput = KeyInputUtil.CharToKeyInput(c);
                    Assert.NotEqual(VimKey.RawCharacter, keyInput.Key);

                    keyInput = KeyInputUtil.CharWithControlToKeyInput(current);
                    Assert.NotEqual(VimKey.RawCharacter, keyInput.Key);
                }
            }
        }

        public sealed class GetNonKeypadEquivalentTest : KeyInputUtilTest
        {
            [Fact]
            public void Numbers()
            {
                foreach (var i in Enumerable.Range(0, 10))
                {
                    var keypadName = "Keypad" + i;
                    var keypad = (VimKey)Enum.Parse(typeof(VimKey), keypadName);
                    var equivalent = KeyInputUtil.GetNonKeypadEquivalent(KeyInputUtil.VimKeyToKeyInput(keypad));
                    Assert.Equal(i.ToString(), equivalent.Value.Char.ToString());
                }
            }

            [Fact]
            public void Divide()
            {
                var equivalent = KeyInputUtil.GetNonKeypadEquivalent(KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadDivide));
                Assert.Equal('/', equivalent.Value.Char);
            }

            /// <summary>
            /// TODO: Need to verify this is the correct behavior here.  Need a real keyboard though
            /// </summary>
            [Fact]
            public void DontPreserveModifiers()
            {
                var keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.KeypadDivide, KeyModifiers.Control);
                var equivalent = KeyInputUtil.GetNonKeypadEquivalent(keyInput);
                Assert.Equal(KeyInputUtil.CharToKeyInput('/'), equivalent.Value);
            }

            [Fact]
            public void Enter()
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadEnter);
                Assert.Equal(KeyInputUtil.EnterKey, KeyInputUtil.GetNonKeypadEquivalent(keyInput).Value);
            }
        }

        public sealed class MiscTest : KeyInputUtilTest
        {
            [Fact]
            public void CoreCharList()
            {
                foreach (var cur in CharAll)
                {
                    Assert.True(KeyInputUtil.VimKeyCharList.Contains(cur));
                    Assert.True(KeyInputUtil.CharToKeyInputMap.ContainsKey(cur));
                }
            }

            public void CharToKeyInput_LowerLetters()
            {
                foreach (var cur in CharLettersLower)
                {
                    var ki = KeyInputUtil.CharToKeyInput(cur);
                    Assert.Equal(cur, ki.Char);
                    Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
                    Assert.Equal(VimKey.RawCharacter, ki.Key);
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
                    Assert.Equal(VimKey.RawCharacter, ki.Key);
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
                    if (cur == VimKey.None || cur == VimKey.RawCharacter)
                    {
                        continue;
                    }

                    if (Char.IsLetter(KeyInputUtil.VimKeyToKeyInput(cur).Char))
                    {
                        continue;
                    }

                    var keyInput = KeyInputUtil.ApplyModifiersToVimKey(cur, KeyModifiers.Control);
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

            /// <summary>
            /// Verify that the Dangerous function does indeed act dangerously
            /// </summary>
            [Fact]
            public void ChangeKeyModifiers_WontChangeChar()
            {
                var ki = KeyInputUtil.CharToKeyInput('[');
                var ki2 = KeyInputUtil.ChangeKeyModifiersDangerous(ki, KeyModifiers.Shift);
                Assert.Equal(ki.Char, ki2.Char);
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
                Assert.Equal(VimKey.RawCharacter, keyInput.Key);
                Assert.Equal(KeyModifiers.Control, keyInput.KeyModifiers);
                Assert.Equal('#', keyInput.Char);
            }

            /// <summary>
            /// Do some sanity checks on the counts to make sure that everything is in line
            /// with the expectations
            /// </summary>
            [Fact]
            public void SanityChecks()
            {
                // There are 2 keys we don't produce raw values for: RawChar and None
                var count = Enum.GetValues(typeof(VimKey)).Length;
                Assert.Equal(count - 2, KeyInputUtil.VimKeyRawData.Length);

                // 26 alpha letters plus the 6 special cases we consider
                Assert.Equal(32, KeyInputUtil.ControlCharToKeyInputMap.Count);
            }

            /// <summary>
            /// The KeyModifiers value on KeyInput suggests the "extra" modifiers that are attached
            /// to an expected input.  For values such as CTRL_H the control is needed to produce the
            /// character and hence is not "extra" hence it shoudn't be present.  In fact none of the
            /// predefined KeyInput values should have any modifier because none of them have "extra" 
            /// KeyModifiers 
            /// </summary>
            [Fact]
            public void NoControl()
            {
                var all = KeyInputUtil.VimKeyInputList;
                foreach (var keyInput in all)
                {
                    Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
                }
            }

            [Fact]
            public void TabKey()
            {
                Action<KeyInput> verify =
                    keyInput =>
                    {
                        Assert.Equal(VimKey.Tab, keyInput.Key);
                        Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
                        Assert.Equal('\t', keyInput.Char);
                    };

                verify(KeyInputUtil.TabKey);
                verify(KeyInputUtil.CharToKeyInput('\t'));
                verify(KeyInputUtil.VimKeyToKeyInput(VimKey.Tab));
            }

            /// <summary>
            /// Ensure that every recognized KeyInput value can map back and forth soley
            /// based on the character provided
            /// </summary>
            [Fact]
            public void Exhaustive()
            {
                foreach (var current in KeyInputUtil.VimKeyInputList)
                {
                    if (current.RawChar.IsNone())
                    {
                        continue;
                    }

                    if (VimKeyUtil.IsKeypadKey(current.Key))
                    {
                        continue;
                    }

                    // The 2 special keys which map differently when used by name vs. when
                    // mapped by char.  They are the keys which don't obey this rule
                    if (current.Key == VimKey.Back ||
                        current.Key == VimKey.Delete)
                    {
                        continue;
                    }

                    var keyInput = KeyInputUtil.CharToKeyInput(current.Char);
                    Assert.Equal(current, keyInput);
                }
            }

            [Fact]
            public void DoubleApplyControl()
            {
                var keyInput1 = KeyInputUtil.CharWithControlToKeyInput(';');
                var keyInput2 = KeyInputUtil.ApplyModifiers(keyInput1, KeyModifiers.Control);
                Assert.Equal(keyInput1, keyInput2);
            }
        }
    }
}
