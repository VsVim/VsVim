using System;
using System.Linq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;

namespace VimCore.Test
{
    [TestFixture]
    public class KeyNotationUtilTest
    {
        [Test]
        [Description("< must be expressed as <lt>")]
        public void TryStringToKeyInput1()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<");
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void TryStringToKeyInput2()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<Left>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(KeyInputUtil.VimKeyToKeyInput(VimKey.Left), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput3()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<Right>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(KeyInputUtil.VimKeyToKeyInput(VimKey.Right), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput4()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<S-A>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('A'), opt.Value);
        }

        [Test]
        [Description("Not case sensitive")]
        public void TryStringToKeyInput5()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<s-a>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('A'), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput6()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<C-x>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual('x', opt.Value.Char);
            Assert.AreEqual(KeyModifiers.Control, opt.Value.KeyModifiers);
        }

        [Test]
        public void TryStringToKeyInputList1()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("ab");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('a', list[0].Char);
            Assert.AreEqual('b', list[1].Char);
        }

        [Test]
        public void TryStringToKeyInputList2()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("<foo");
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void TryStringToKeyInputList3()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("<Home>a");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(KeyInputUtil.VimKeyToKeyInput(VimKey.Home), list[0]);
            Assert.AreEqual('a', list[1].Char);
        }

        [Test]
        public void TryStringToKeyInputList4()
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet("<C-x><C-o>");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.KeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('x', list[0].Char);
            Assert.AreEqual('o', list[1].Char);
        }

        [Test]
        public void StringToKeyInput1()
        {
            var data = KeyNotationUtil.StringToKeyInput("<C-]>");
            Assert.AreEqual(']', data.Char);
        }

        [Test]
        public void StringToKeyInput2()
        {
            Action<string, KeyInput> verifyFunc = (data, ki) =>
                {
                    var parsed = KeyNotationUtil.StringToKeyInput(data);
                    Assert.AreEqual(ki, parsed);
                };
            verifyFunc("<S-F11>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.F11, KeyModifiers.Shift));
            verifyFunc("<c-F11>", KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.F11, KeyModifiers.Control));
        }

        [Test]
        public void StringToKeyInput3()
        {
            Action<string, KeyInput> verifyFunc = (data, ki) =>
                {
                    var parsed = KeyNotationUtil.StringToKeyInput(data);
                    Assert.AreEqual(ki, parsed);
                };
            verifyFunc("CTRL-j", KeyInputUtil.CharWithControlToKeyInput('j'));
            verifyFunc("CTRL-j", KeyInputUtil.CharWithControlToKeyInput('j'));
        }

        [Test]
        public void StringToKeyInput4()
        {
            var data = KeyNotationUtil.StringToKeyInput("CTRL-o");
            Assert.AreEqual('o', data.Char);
            Assert.AreEqual(KeyModifiers.Control, data.KeyModifiers);
            Assert.AreEqual(VimKey.O, data.Key);
        }

        [Test]
        [Description("CTRL- modifiers on alphas should be lower case")]
        public void StringToKeyInput5()
        {
            var ki = KeyNotationUtil.StringToKeyInput("CTRL-O");
            Assert.AreEqual('o', ki.Char);
            Assert.AreEqual(KeyModifiers.Control, ki.KeyModifiers);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        [Description("Named keystrokes need to use the <C- syntax")]
        public void StringToKeyInput6()
        {
            KeyNotationUtil.StringToKeyInput("CTRL-Up");
        }

        [Test]
        public void SplitIntoKeyNotationEntries1()
        {
            CollectionAssert.AreEquivalent(
                new string[] { "a", "b" },
                KeyNotationUtil.SplitIntoKeyNotationEntries("ab"));
        }

        [Test]
        public void SplitIntoKeyNotationEntries2()
        {
            CollectionAssert.AreEquivalent(
                new string[] { "CTRL-j", "b" },
                KeyNotationUtil.SplitIntoKeyNotationEntries("CTRL-j_b"));
        }

        [Test]
        public void SplitIntoKeyNotationEntries3()
        {
            CollectionAssert.AreEquivalent(
                new string[] { "CTRL-J", "b" },
                KeyNotationUtil.SplitIntoKeyNotationEntries("CTRL-J_b"));
        }

        [Test]
        public void SplitIntoKeyNotationEntries4()
        {
            CollectionAssert.AreEquivalent(
                new string[] { "CTRL-J", "CTRL-b" },
                KeyNotationUtil.SplitIntoKeyNotationEntries("CTRL-J_CTRL-b"));

        }
    }
}
