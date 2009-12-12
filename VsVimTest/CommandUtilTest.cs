using VsVim;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.VisualStudio;
using VimCore;
using System.Runtime.InteropServices;
using System.Windows.Input;
using VsVimTest.Utils;

namespace VsVimTest
{
    [TestClass()]
    public class CommandUtilTest
    {
        internal EditCommand ConvertTypeChar(char data)
        {
            using (var ptr = CharPointer.Create(data))
            {
                EditCommand command;
                Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, ptr.IntPtr, out command));
                return command;
            }
        }

        [TestMethod, Description("Make sure we don't puke on missing data")]
        public void TypeCharNoData()
        {
            EditCommand command;
            Assert.IsFalse(CommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, IntPtr.Zero, out command));
        }

        [TestMethod, Description("Delete key")]
        public void TypeDelete()
        {
            var command = ConvertTypeChar('\b');
            Assert.AreEqual(Key.Back, command.KeyInput.Key);
        }

        [TestMethod]
        public void TypeChar1()
        {
            var command = ConvertTypeChar('a');
            Assert.AreEqual(EditCommandKind.TypeChar, command.EditCommandKind);
            Assert.AreEqual(Key.A, command.KeyInput.Key);
        }

        public void TypeChar2()
        {
            var command = ConvertTypeChar('b');
            Assert.AreEqual(EditCommandKind.TypeChar, command.EditCommandKind);
            Assert.AreEqual(Key.B, command.KeyInput.Key);
        }
   }
}
