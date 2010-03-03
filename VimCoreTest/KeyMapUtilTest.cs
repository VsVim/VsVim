using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using System.Windows.Input;

namespace VimCoreTest
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
            Assert.AreEqual(InputUtil.KeyToKeyInput(Key.Left), opt.Value);
        }

        [Test]
        public void TryStringToKeyInput3()
        {
            var opt = KeyMapUtil.TryStringToKeyInput("<Right>");
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(InputUtil.KeyToKeyInput(Key.Right), opt.Value);
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
    }
}
