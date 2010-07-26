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
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.LeftKey), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput3()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<Right>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.RightKey), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput4()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<S-A>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.CharToKeyInput('A'), opt.Value);
        }

        [Test]
        [Description("Not case sensitive")]
        public void TryStringToKeyInput5()
        {
            var opt = KeyNotationUtil.TryStringToKeyInput("<s-a>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.CharToKeyInput('A'), opt.Value);
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
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.HomeKey), list[0]);
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
            verifyFunc("<S-F11>", InputUtil.VimKeyAndModifiersToKeyInput(VimKey.F11Key, KeyModifiers.Shift));
            verifyFunc("<c-F11>", InputUtil.VimKeyAndModifiersToKeyInput(VimKey.F11Key, KeyModifiers.Control));
        }
    }
}
