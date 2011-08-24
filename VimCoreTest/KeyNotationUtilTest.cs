using System.Linq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class KeyNotationUtilTest
    {
        private static void AssertSingle(string input, VimKey? key = null)
        {
            AssertSingle(input, key.HasValue ? KeyInputUtil.VimKeyToKeyInput(key.Value) : null);
        }

        private static void AssertSingle(string input, KeyInput expected = null)
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

        private static void AssertMany(string input, string result)
        {
            AssertMany(input, KeyInputSetUtil.OfString(result));
        }

        private static void AssertMany(string input, KeyInputSet keyInputSet)
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet(input);
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(opt.Value, keyInputSet);
        }

        [Test]
        public void Single_LessThanChar()
        {
            AssertSingle("<", VimKey.LessThan);
        }

        [Test]
        public void Single_LeftKey()
        {
            AssertSingle("<Left>", VimKey.Left);
        }

        [Test]
        public void Single_RightKey()
        {
            AssertSingle("<Right>", VimKey.Right);
            AssertSingle("<rIGht>", VimKey.Right);
        }

        [Test]
        public void Single_ShiftAlphaShouldPromote()
        {
            AssertSingle("<S-A>", VimKey.UpperA);
            AssertSingle("<s-a>", VimKey.UpperA);
        }

        [Test]
        public void Single_AlphaAloneIsCaseSensitive()
        {
            AssertSingle("a", VimKey.LowerA);
            AssertSingle("A", VimKey.UpperA);
        }

        [Test]
        public void Single_ShiftNumberShouldNotPromote()
        {
            AssertSingle("<S-1>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Number1, KeyModifiers.Shift));
            AssertSingle("<s-1>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Number1, KeyModifiers.Shift));
        }

        [Test]
        public void Single_AlphaWithControl()
        {
            AssertSingle("<C-x>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.LowerX, KeyModifiers.Control));
            AssertSingle("<c-X>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.UpperX, KeyModifiers.Control));
        }

        [Test]
        public void Single_AlphaWithAltIsCaseSensitive()
        {
            AssertSingle("<A-b>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.LowerB, KeyModifiers.Alt));
            AssertSingle("<A-B>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.UpperB, KeyModifiers.Alt));
        }

        [Test]
        public void Single_DontMapControlPrefixAsSingleKey()
        {
            AssertSingle("CTRL-x", expected: null);
        }

        [Test]
        public void Single_NotationControlAndSymbol()
        {
            AssertSingle("<C-]>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.CloseBracket, KeyModifiers.Control));
        }

        [Test]
        public void Single_NotationOfFunctionKey()
        {
            AssertSingle("<S-F11>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.F11, KeyModifiers.Shift));
            AssertSingle("<c-F11>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.F11, KeyModifiers.Control));
        }

        [Test]
        public void Single_ShiftAndControlModifier()
        {
            AssertSingle("<C-S-A>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.UpperA, KeyModifiers.Control));
        }

        [Test]
        public void Single_BackslashLiteral()
        {
            AssertSingle(@"\", VimKey.Backslash);
        }

        [Test]
        [Description("Case shouldn't matter")]
        public void Single_CaseShouldntMatter()
        {
            var ki = KeyInputUtil.EscapeKey;
            var all = new string[] { "<ESC>", "<esc>", "<Esc>" };
            foreach (var cur in all)
            {
                Assert.AreEqual(ki, KeyNotationUtil.StringToKeyInput(cur));
            }
        }

        [Test]
        public void Single_HandleCommandKey()
        {
            var ki = KeyNotationUtil.StringToKeyInput("<D-a>");
            Assert.AreEqual(VimKey.LowerA, ki.Key);
            Assert.AreEqual(KeyModifiers.Command, ki.KeyModifiers);
        }

        /// <summary>
        /// Make sure we can parse out the nop key
        /// </summary>
        [Test]
        public void Single_Nop()
        {
            var keyInput = KeyNotationUtil.StringToKeyInput("<nop>");
            Assert.AreEqual(VimKey.Nop, keyInput.Key);
            Assert.AreEqual(KeyModifiers.None, keyInput.KeyModifiers);
        }

        [Test]
        [Description("Case shouldn't matter")]
        public void StringToKeyInput8()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Space);
            ki = KeyInputUtil.ChangeKeyModifiers(ki, KeyModifiers.Shift);
            var all = new string[] { "<S-space>", "<S-SPACE>" };
            foreach (var cur in all)
            {
                Assert.AreEqual(ki, KeyNotationUtil.StringToKeyInput(cur));
            }
        }

        [Test]
        public void Many_TwoAlpha()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("ab");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('a', list[0].Char);
            Assert.AreEqual('b', list[1].Char);
        }

        [Test]
        public void Many_InvalidLessThanPrefix()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("<foo");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.Select(x => x.Char).ToList();
            CollectionAssert.AreEquivalent("<foo".ToList(), list);
        }

        [Test]
        public void Many_NotationThenAlpha()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("<Home>a");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(KeyInputUtil.VimKeyToKeyInput(VimKey.Home), list[0]);
            Assert.AreEqual('a', list[1].Char);
        }

        [Test]
        public void Many_TwoNotation()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("<C-x><C-o>");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('x', list[0].Char);
            Assert.AreEqual('o', list[1].Char);
        }

        [Test]
        public void Many_EscapeLessThanLiteral()
        {
            AssertMany(@"\<home>", "<home>");
        }

        [Test]
        public void Many_LessThanEscapeLiteral()
        {
            AssertMany(@"<lt>lt>", "<lt>");
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
    }
}
