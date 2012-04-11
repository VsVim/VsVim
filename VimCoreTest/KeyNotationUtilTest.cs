using System.Linq;
using NUnit.Framework;
using Vim.Extensions;

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
                Assert.IsTrue(opt.IsSome());
                Assert.AreEqual(expected, opt.Value);
                Assert.AreEqual(expected, KeyNotationUtil.StringToKeyInput(input));
            }
            else
            {
                Assert.IsTrue(opt.IsNone());
            }
        }

        protected static void AssertMany(string input, string result)
        {
            AssertMany(input, KeyInputSetUtil.OfString(result));
        }

        protected static void AssertMany(string input, KeyInputSet keyInputSet)
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet(input);
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(opt.Value, keyInputSet);
        }

        [TestFixture]
        public sealed class Single : KeyNotationUtilTest
        {
            [Test]
            public void LessThanChar()
            {
                AssertSingle("<", VimKey.LessThan);
            }

            [Test]
            public void LeftKey()
            {
                AssertSingle("<Left>", VimKey.Left);
            }

            [Test]
            public void RightKey()
            {
                AssertSingle("<Right>", VimKey.Right);
                AssertSingle("<rIGht>", VimKey.Right);
            }

            [Test]
            public void ShiftAlphaShouldPromote()
            {
                AssertSingle("<S-A>", VimKey.UpperA);
                AssertSingle("<s-a>", VimKey.UpperA);
            }

            [Test]
            public void AlphaAloneIsCaseSensitive()
            {
                AssertSingle("a", VimKey.LowerA);
                AssertSingle("A", VimKey.UpperA);
            }

            [Test]
            public void ShiftNumberShouldNotPromote()
            {
                AssertSingle("<S-1>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.Number1, KeyModifiers.Shift));
                AssertSingle("<s-1>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.Number1, KeyModifiers.Shift));
            }

            [Test]
            public void AlphaWithControl()
            {
                AssertSingle("<C-x>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.LowerX, KeyModifiers.Control));
                AssertSingle("<c-X>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.UpperX, KeyModifiers.Control));
            }

            [Test]
            public void AlphaWithAltIsCaseSensitive()
            {
                AssertSingle("<A-b>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.LowerB, KeyModifiers.Alt));
                AssertSingle("<A-B>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.UpperB, KeyModifiers.Alt));
            }

            [Test]
            public void DontMapControlPrefixAsSingleKey()
            {
                AssertSingle("CTRL-x", expected: null);
            }

            [Test]
            public void NotationControlAndSymbol()
            {
                AssertSingle("<C-]>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.CloseBracket, KeyModifiers.Control));
            }

            [Test]
            public void NotationOfFunctionKey()
            {
                AssertSingle("<S-F11>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.F11, KeyModifiers.Shift));
                AssertSingle("<c-F11>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.F11, KeyModifiers.Control));
            }

            [Test]
            public void ShiftAndControlModifier()
            {
                AssertSingle("<C-S-A>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.UpperA, KeyModifiers.Control));
            }

            [Test]
            public void BackslashLiteral()
            {
                AssertSingle(@"\", VimKey.Backslash);
            }

            [Test]
            [Description("Case shouldn't matter")]
            public void CaseShouldntMatter()
            {
                var ki = KeyInputUtil.EscapeKey;
                var all = new string[] { "<ESC>", "<esc>", "<Esc>" };
                foreach (var cur in all)
                {
                    Assert.AreEqual(ki, KeyNotationUtil.StringToKeyInput(cur));
                }
            }

            [Test]
            public void HandleCommandKey()
            {
                var ki = KeyNotationUtil.StringToKeyInput("<D-a>");
                Assert.AreEqual(VimKey.LowerA, ki.Key);
                Assert.AreEqual(KeyModifiers.Command, ki.KeyModifiers);
            }

            /// <summary>
            /// Make sure we can parse out the nop key
            /// </summary>
            [Test]
            public void Nop()
            {
                var keyInput = KeyNotationUtil.StringToKeyInput("<nop>");
                Assert.AreEqual(VimKey.Nop, keyInput.Key);
                Assert.AreEqual(KeyModifiers.None, keyInput.KeyModifiers);
            }
        }

        [TestFixture]
        public sealed class Many : KeyNotationUtilTest
        {
            [Test]
            public void TwoAlpha()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("ab");
                Assert.IsTrue(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.AreEqual(2, list.Count);
                Assert.AreEqual('a', list[0].Char);
                Assert.AreEqual('b', list[1].Char);
            }

            [Test]
            public void InvalidLessThanPrefix()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<foo");
                Assert.IsTrue(opt.IsSome());
                var list = opt.Value.KeyInputs.Select(x => x.Char).ToList();
                CollectionAssert.AreEquivalent("<foo".ToList(), list);
            }

            [Test]
            public void NotationThenAlpha()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<Home>a");
                Assert.IsTrue(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.AreEqual(2, list.Count);
                Assert.AreEqual(KeyInputUtil.VimKeyToKeyInput(VimKey.Home), list[0]);
                Assert.AreEqual('a', list[1].Char);
            }

            [Test]
            public void TwoNotation()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<C-x><C-o>");
                Assert.IsTrue(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.AreEqual(2, list.Count);
                Assert.AreEqual('x', list[0].Char);
                Assert.AreEqual('o', list[1].Char);
            }

            /// <summary>
            /// By default the '\' key doesn't have any special meaning in mappings.  It only has escape
            /// properties when the 'B' flag isn't set in cpoptions
            /// </summary>
            [Test]
            public void EscapeLessThanLiteral()
            {
                AssertMany(@"\<home>", KeyInputSetUtil.OfVimKeyArray(VimKey.Backslash, VimKey.Home));
            }

            [Test]
            public void LessThanEscapeLiteral()
            {
                AssertMany(@"<lt>lt>", "<lt>");
            }
        }

        [TestFixture]
        public sealed class Misc : KeyNotationUtilTest
        {
            /// <summary>
            /// Case shouldn't matter
            /// </summary>
            [Test]
            public void StringToKeyInput8()
            {
                var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Space);
                ki = KeyInputUtil.ChangeKeyModifiersDangerous(ki, KeyModifiers.Shift);
                var all = new string[] { "<S-space>", "<S-SPACE>" };
                foreach (var cur in all)
                {
                    Assert.AreEqual(ki, KeyNotationUtil.StringToKeyInput(cur));
                }
            }

            [Test]
            public void SplitIntoKeyNotationEntries1()
            {
                CollectionAssert.AreEquivalent(
                    new[] { "a", "b" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("ab"));
            }

            [Test]
            public void SplitIntoKeyNotationEntries2()
            {
                CollectionAssert.AreEquivalent(
                    new[] { "<C-j>", "b" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<C-j>b"));
            }

            [Test]
            public void SplitIntoKeyNotationEntries3()
            {
                CollectionAssert.AreEquivalent(
                    new[] { "<C-J>", "b" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<C-J>b"));
            }

            [Test]
            public void SplitIntoKeyNotationEntries4()
            {
                CollectionAssert.AreEquivalent(
                    new[] { "<C-J>", "<C-b>" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<C-J><C-b>"));
            }

            [Test]
            public void SplitIntoKeyNotationEntries_InvalidModifierTreatesLessThanLiterally()
            {
                CollectionAssert.AreEquivalent(
                    new[] { "<", "b", "-", "j", ">" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<b-j>"));
            }

            [Test]
            public void TryStringToKeyInput_BadModifier()
            {
                Assert.IsTrue(KeyNotationUtil.TryStringToKeyInput("<b-j>").IsNone());
            }

            [Test]
            public void TryStringToKeyInputSet_BadModifier()
            {
                var result = KeyNotationUtil.TryStringToKeyInputSet("<b-j>");
                Assert.IsTrue(result.IsSome());
                var list = result.Value.KeyInputs.Select(x => x.Char);
                CollectionAssert.AreEquivalent(new[] { '<', 'b', '-', 'j', '>' }, list);
            }

            [Test]
            public void BackslasheInRight()
            {
                AssertMany(@"/\v", KeyInputSetUtil.OfVimKeyArray(VimKey.Forwardslash, VimKey.Backslash, VimKey.LowerV));
            }
        }
    }
}
