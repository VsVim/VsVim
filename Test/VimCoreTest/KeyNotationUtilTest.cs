using System;
using System.Linq;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class KeyNotationUtilTest
    {
        protected static void AssertSingle(string input, VimKey? key = null)
        {
            AssertSingle(input, key.HasValue ? KeyInputUtil.VimKeyToKeyInput(key.Value) : null);
        }

        protected static void AssertSingle(string input, KeyInput expected = null)
        {
            var opt = KeyNotationUtil.TryStringToKeyInput(input);
            if (expected != null)
            {
                Assert.True(opt.IsSome());
                Assert.Equal(expected, opt.Value);
                Assert.Equal(expected, KeyNotationUtil.StringToKeyInput(input));
            }
            else
            {
                Assert.True(opt.IsNone());
            }
        }

        protected static void AssertMany(string input, string result)
        {
            AssertMany(input, KeyInputSetUtil.OfString(result));
        }

        protected static void AssertMany(string input, KeyInputSet keyInputSet)
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet(input);
            Assert.True(opt.IsSome());
            Assert.Equal(opt.Value, keyInputSet);
        }

        public sealed class SingleTest : KeyNotationUtilTest
        {
            [Fact]
            public void LessThanChar()
            {
                AssertSingle("<", KeyInputUtil.CharToKeyInput('<'));
            }

            [Fact]
            public void LeftKey()
            {
                AssertSingle("<Left>", VimKey.Left);
            }

            [Fact]
            public void RightKey()
            {
                AssertSingle("<Right>", VimKey.Right);
                AssertSingle("<rIGht>", VimKey.Right);
            }

            [Fact]
            public void ShiftAlphaShouldPromote()
            {
                AssertSingle("<S-A>", KeyInputUtil.CharToKeyInput('A'));
                AssertSingle("<s-a>", KeyInputUtil.CharToKeyInput('A'));
            }

            [Fact]
            public void AlphaAloneIsCaseSensitive()
            {
                AssertSingle("a", KeyInputUtil.CharToKeyInput('a'));
                AssertSingle("A", KeyInputUtil.CharToKeyInput('A'));
            }

            [Fact]
            public void ShiftNumberShouldNotPromote()
            {
                AssertSingle("<S-1>", KeyInputUtil.ApplyModifiersToChar('1', KeyModifiers.Shift));
                AssertSingle("<s-1>", KeyInputUtil.ApplyModifiersToChar('1', KeyModifiers.Shift));
            }

            [Fact]
            public void AlphaWithControl()
            {
                AssertSingle("<C-x>", KeyInputUtil.ApplyModifiersToChar('x', KeyModifiers.Control));
                AssertSingle("<c-X>", KeyInputUtil.ApplyModifiersToChar('X', KeyModifiers.Control));
            }

            [Fact]
            public void AlphaWithAltIsCaseSensitive()
            {
                AssertSingle("<A-b>", KeyInputUtil.ApplyModifiersToChar('b', KeyModifiers.Alt));
                AssertSingle("<A-B>", KeyInputUtil.ApplyModifiersToChar('B', KeyModifiers.Alt));
            }

            [Fact]
            public void DontMapControlPrefixAsSingleKey()
            {
                AssertSingle("CTRL-x", expected: null);
            }

            [Fact]
            public void NotationControlAndSymbol()
            {
                AssertSingle("<C-]>", KeyInputUtil.ApplyModifiersToChar(']', KeyModifiers.Control));
            }

            [Fact]
            public void NotationOfFunctionKey()
            {
                AssertSingle("<S-F11>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.F11, KeyModifiers.Shift));
                AssertSingle("<c-F11>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.F11, KeyModifiers.Control));
            }

            [Fact]
            public void ShiftAndControlModifier()
            {
                AssertSingle("<C-S-A>", KeyInputUtil.ApplyModifiersToChar('A', KeyModifiers.Control));
            }

            [Fact]
            public void BackslashLiteral()
            {
                AssertSingle(@"\", new KeyInput(VimKey.RawCharacter, KeyModifiers.None, FSharpOption.Create('\\')));
            }

            /// <summary>
            /// Case shouldn't matter
            /// </summary>
            [Fact]
            public void CaseShouldntMatter()
            {
                var ki = KeyInputUtil.EscapeKey;
                var all = new string[] { "<ESC>", "<esc>", "<Esc>" };
                foreach (var cur in all)
                {
                    Assert.Equal(ki, KeyNotationUtil.StringToKeyInput(cur));
                }
            }

            [Fact]
            public void HandleCommandKey()
            {
                var ki = KeyNotationUtil.StringToKeyInput("<D-a>");
                Assert.Equal(VimKey.RawCharacter, ki.Key);
                Assert.Equal(KeyModifiers.Command, ki.KeyModifiers);
                Assert.Equal('a', ki.Char);
            }

            /// <summary>
            /// Make sure we can parse out the nop key
            /// </summary>
            [Fact]
            public void Nop()
            {
                var keyInput = KeyNotationUtil.StringToKeyInput("<nop>");
                Assert.Equal(VimKey.Nop, keyInput.Key);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
            }

            /// <summary>
            /// The C-S notation can be abbreviated CS
            /// </summary>
            [Fact]
            public void AlternateShiftAndControlWithNonPrintable()
            {
                Action<string, VimKey> assert = 
                    (name, vimKey) =>
                    {
                        var notation = String.Format("<CS-{0}>", name);
                        var keyInput = KeyNotationUtil.StringToKeyInput(notation);
                        Assert.Equal(vimKey, keyInput.Key);
                        Assert.Equal(KeyModifiers.Shift | KeyModifiers.Control, keyInput.KeyModifiers);
                    };
                assert("Enter", VimKey.Enter);
                assert("F2", VimKey.F2);
            }

            /// <summary>
            /// The CS-A syntax properly ignores the shift when it's applied to an alpha 
            /// </summary>
            [Fact]
            public void AlternateShiftandControlWithAlpha()
            {
                var keyInput = KeyNotationUtil.StringToKeyInput("<CS-A>");
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('a'), keyInput);
            }

            [Fact]
            public void Keypad()
            {
                Action<VimKey, string> func = (vimKey, name) =>
                    {
                        var keyInput = KeyNotationUtil.StringToKeyInput(name);
                        Assert.Equal(vimKey, keyInput.Key);
                    };
                func(VimKey.KeypadEnter, "<kEnter>");
                func(VimKey.KeypadDecimal, "<kPoint>");
                func(VimKey.KeypadDivide, "<kDivide>");
                func(VimKey.KeypadMinus, "<kMinus>");
                func(VimKey.KeypadMultiply, "<kMultiply>");
                func(VimKey.KeypadPlus, "<kPlus>");
            }

            [Fact]
            public void Mouse()
            {
                Action<VimKey, string> func = (vimKey, name) =>
                    {
                        var keyInput = KeyNotationUtil.StringToKeyInput(name);
                        Assert.Equal(vimKey, keyInput.Key);
                    };
                func(VimKey.LeftMouse, "<LeftMouse>");
                func(VimKey.LeftDrag, "<LeftDrag>");
                func(VimKey.LeftRelease, "<LeftRelease>");
                func(VimKey.MiddleMouse, "<MiddleMouse>");
                func(VimKey.MiddleDrag, "<MiddleDrag>");
                func(VimKey.MiddleRelease, "<MiddleRelease>");
                func(VimKey.RightMouse, "<RightMouse>");
                func(VimKey.RightDrag, "<RightDrag>");
                func(VimKey.RightRelease, "<RightRelease>");
            }
        }

        public sealed class ManyTest : KeyNotationUtilTest
        {
            [Fact]
            public void TwoAlpha()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("ab");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal('a', list[0].Char);
                Assert.Equal('b', list[1].Char);
            }

            [Fact]
            public void InvalidLessThanPrefix()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<foo");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.Select(x => x.Char).ToList();
                Assert.Equal("<foo".ToList(), list);
            }

            [Fact]
            public void NotationThenAlpha()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<Home>a");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal(KeyInputUtil.VimKeyToKeyInput(VimKey.Home), list[0]);
                Assert.Equal('a', list[1].Char);
            }

            [Fact]
            public void TwoNotation()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<C-x><C-o>");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('x'), list[0]);
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('o'), list[1]);
            }

            /// <summary>
            /// By default the '\' key doesn't have any special meaning in mappings.  It only has escape
            /// properties when the 'B' flag isn't set in cpoptions
            /// </summary>
            [Fact]
            public void EscapeLessThanLiteral()
            {
                var set = KeyInputSet.NewTwoKeyInputs(
                    KeyInputUtil.CharToKeyInput('\\'),
                    KeyInputUtil.VimKeyToKeyInput(VimKey.Home));
                AssertMany(@"\<home>", set);
            }

            [Fact]
            public void LessThanEscapeLiteral()
            {
                AssertMany(@"<lt>lt>", "<lt>");
            }

            [Fact]
            public void AlternateControAndShift()
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet(@"<CS-A><CS-Enter>");
                var list = keyInputSet.KeyInputs.ToList();
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('a'), list[0]);
                Assert.Equal(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Enter, KeyModifiers.Control | KeyModifiers.Shift), list[1]);
            }

            [Fact]
            public void SimpleTwoChars()
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet("ab");
                Assert.Equal(
                    new[] { "a", "b" },
                    keyInputSet.KeyInputs.Select(KeyNotationUtil.GetDisplayName));
            }

            [Fact]
            public void SplitIntoKeyNotationEntries2()
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet("<C-j>b");
                Assert.Equal(
                    new[] { "<NL>", "b" },
                    keyInputSet.KeyInputs.Select(KeyNotationUtil.GetDisplayName));
            }

            [Fact]
            public void SplitIntoKeyNotationEntries4()
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet("<C-j><C-b>");
                Assert.Equal(
                    new[] { "<NL>", "<C-B>" },
                    keyInputSet.KeyInputs.Select(KeyNotationUtil.GetDisplayName));
            }
        }

        public sealed class CharLiteralTest : KeyNotationUtilTest
        {
            [Fact]
            public void Simple()
            {
                var keyInut = KeyNotationUtil.StringToKeyInput("<Char-97>");
                Assert.Equal('a', keyInut.Char);
            }

            [Fact]
            public void SimpleDifferentCase()
            {
                var keyInut = KeyNotationUtil.StringToKeyInput("<cHAR-97>");
                Assert.Equal('a', keyInut.Char);
            }

            [Fact]
            public void Letters()
            {
                int baseCase = (int)'a';
                for (int i = 0; i < 26; i++)
                {
                    string msg = String.Format("<Char-{0}>", baseCase + i);
                    var keyInput = KeyNotationUtil.StringToKeyInput(msg);

                    var target = (char)(baseCase + i);
                    Assert.Equal(target, keyInput.Char);
                }
            }

            [Fact]
            public void OctalValue()
            {
                var keyInut = KeyNotationUtil.StringToKeyInput("<Char-0141>");
                Assert.Equal('a', keyInut.Char);
            }

            [Fact]
            public void HexidecimalValue()
            {
                var keyInut = KeyNotationUtil.StringToKeyInput("<Char-0x61>");
                Assert.Equal('a', keyInut.Char);
            }
        }

        public sealed class MiscTest : KeyNotationUtilTest
        {
            /// <summary>
            /// Case shouldn't matter
            /// </summary>
            [Fact]
            public void StringToKeyInput8()
            {
                var ki = KeyInputUtil.CharToKeyInput(' ');
                ki = KeyInputUtil.ChangeKeyModifiersDangerous(ki, KeyModifiers.Shift);
                var all = new string[] { "<S-space>", "<S-SPACE>" };
                foreach (var cur in all)
                {
                    Assert.Equal(ki, KeyNotationUtil.StringToKeyInput(cur));
                }
            }

            [Fact]
            public void TryStringToKeyInput_BadModifier()
            {
                Assert.True(KeyNotationUtil.TryStringToKeyInput("<b-j>").IsNone());
            }

            [Fact]
            public void TryStringToKeyInputSet_BadModifier()
            {
                var result = KeyNotationUtil.TryStringToKeyInputSet("<b-j>");
                Assert.True(result.IsSome());
                var list = result.Value.KeyInputs.Select(x => x.Char);
                Assert.Equal(new[] { '<', 'b', '-', 'j', '>' }, list);
            }

            [Fact]
            public void BackslasheInRight()
            {
                AssertMany(@"/\v", KeyInputSetUtil.OfCharArray('/', '\\', 'v'));
            }

            [Fact]
            public void TagNotSpecialName()
            {
                var keyInputList = KeyNotationUtil.StringToKeyInputSet("<dest>");
                Assert.Equal(
                    new[] { '<', 'd', 'e', 's', 't', '>' },
                    keyInputList.KeyInputs.Select(x => x.Char));
            }

            [Fact]
            public void UnmatchedLessThan()
            {
                var keyInputList = KeyNotationUtil.StringToKeyInputSet("<<s-a>");
                Assert.Equal(
                    new[] { '<', 'A' },
                    keyInputList.KeyInputs.Select(x => x.Char));
            }
        }

        public sealed class GetDisplayNameTest : KeyNotationUtilTest
        {
            /// <summary>
            /// When displaying the Control + alpha keys we should be displaying it in the C-X 
            /// format and not the raw character. 
            /// </summary>
            [Fact]
            public void AlphaAndControl()
            {
                foreach (var c in KeyInputUtilTest.CharLettersUpper)
                {
                    var keyInput = KeyInputUtil.CharWithControlToKeyInput(c);

                    // Certain combinations like CTRL-J have a primary key which gets displayed over
                    // them.  Don't test them here
                    if (keyInput.Key != VimKey.None)
                    {
                        continue;
                    }

                    var text = String.Format("<C-{0}>", c);
                    Assert.Equal(text, KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void Alpha()
            {
                foreach (var c in KeyInputUtilTest.CharLettersUpper)
                {
                    var keyInput = KeyInputUtil.CharToKeyInput(c);
                    Assert.Equal(c.ToString(), KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void AlphaLowerAndAlt()
            {
                foreach (var c in KeyInputUtilTest.CharLettersLower)
                {
                    var keyInput = KeyInputUtil.CharWithAltToKeyInput(c);
                    var shiftedChar = (char)(0x80 | (int)c);
                    Assert.Equal(shiftedChar.ToString(), KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void NonAlphaWithControl()
            {
                foreach (var c in "()#")
                {
                    var keyInput = KeyInputUtil.CharWithControlToKeyInput(c);
                    var text = String.Format("<C-{0}>", c);
                    Assert.Equal(text, KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void ControlHAndBackspace()
            {
                var left = KeyInputUtil.CharWithControlToKeyInput('h');
                var right = KeyNotationUtil.StringToKeyInput("<BS>");
                Assert.Equal("<C-H>", KeyNotationUtil.GetDisplayName(left));
                Assert.Equal("<BS>", KeyNotationUtil.GetDisplayName(right));
            }

            /// <summary>
            /// Verify that named keys get back the proper display name
            /// </summary>
            [Fact]
            public void NamedKeys()
            {
                Action<VimKey, string> func =
                    (vimKey, name) =>
                    {
                        var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);
                        Assert.Equal(name, KeyNotationUtil.GetDisplayName(keyInput));
                    };

                func(VimKey.Enter, "<CR>");
                func(VimKey.Escape, "<Esc>");
                func(VimKey.Delete, "<Del>");
            }

            [Fact]
            public void KeypadKeys()
            {
                Action<VimKey, string> func =
                    (vimKey, name) =>
                    {
                        var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);
                        Assert.Equal(name, KeyNotationUtil.GetDisplayName(keyInput));
                    };

                func(VimKey.KeypadEnter, "<kEnter>");
                func(VimKey.KeypadDecimal, "<kPoint>");
                func(VimKey.KeypadPlus, "<kPlus>");
                func(VimKey.KeypadMultiply, "<kMultiply>");
                func(VimKey.KeypadMinus, "<kMinus>");
                func(VimKey.KeypadDivide, "<kDivide>");
            }

            [Fact]
            public void Issue328()
            {
                var left = KeyNotationUtil.StringToKeyInput("<S-SPACE>");
                var right = KeyInputUtil.ApplyModifiersToChar(' ', KeyModifiers.Shift);
                Assert.Equal(left, right);
            }
        }
    }
}
