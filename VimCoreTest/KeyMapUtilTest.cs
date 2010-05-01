using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Vim.Extensions;

namespace VimCore.Test
{
    [TestFixture]
    public class KeyMapUtilTest
    {
        [Test]
        [Description("Make sure the shift keys are present")]
        public void KeyNotationList1()
        {
            var names = KeyMapUtil.KeyNotationList.Select(x => x.Item1);
            Assert.IsTrue(names.Contains("<S-Left>"));
            Assert.IsTrue(names.Contains("<S-F1>"));
            Assert.IsTrue(names.Contains("<S-Home>"));
        }

        [Test]
        [Description("Make sure the control keys are present")]
        public void KeyNotationList2()
        {
            var names = KeyMapUtil.KeyNotationList.Select(x => x.Item1);
            Assert.IsTrue(names.Contains("<C-Left>"));
            Assert.IsTrue(names.Contains("<C-F1>"));
            Assert.IsTrue(names.Contains("<C-Home>"));
        }

        [Test]
        [Description("< must be expressed as <lt>")]
        public void TryStringToKeyInput1()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<");
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void TryStringToKeyInput2()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<Left>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.LeftKey), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput3()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<Right>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.RightKey), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput4()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<S-A>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.CharToKeyInput('A'), opt.Value);
        }

        [Test]
        [Description("Not case sensitive")]
        public void TryStringToKeyInput5()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<s-a>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.CharToKeyInput('A'), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput6()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<C-x>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual('x', opt.Value.Char);
            Assert.AreEqual(KeyModifiers.Control, opt.Value.KeyModifiers);
        }

        [Test]
        public void TryStringToKeyInputList1()
        {
            var opt = KeyMapUtil.TryStringToKeyInputList("ab");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('a', list[0].Char);
            Assert.AreEqual('b', list[1].Char);
        }

        [Test]
        public void TryStringToKeyInputList2()
        {
            var opt = KeyMapUtil.TryStringToKeyInputList("<foo");
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void TryStringToKeyInputList3()
        {
            var opt = KeyMapUtil.TryStringToKeyInputList("<Home>a");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.HomeKey), list[0]);
            Assert.AreEqual('a', list[1].Char);
        }

        [Test]
        public void TryStringToKeyInputList4()
        {
            var opt = KeyMapUtil.TryStringToKeyInputList("<C-x><C-o>");
            Assert.IsTrue(opt.IsSome());
            var list = opt.Value.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('x', list[0].Char);
            Assert.AreEqual('o', list[1].Char);
        }
    }
}
