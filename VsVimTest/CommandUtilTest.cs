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
        public KeyInput ConvertTypeChar(char data)
        {
            using (var ptr = CharPointer.Create(data))
            {
                KeyInput ki = null;
                Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2KCmdID.TYPECHAR, ptr.IntPtr, out ki));
                return ki;
            }
        }

        [TestMethod, Description("Make sure we don't puke on missing data")]
        public void TypeCharNoData()
        {
            KeyInput ki = null;
            Assert.IsFalse(CommandUtil.TryConvert(VSConstants.VSStd2KCmdID.TYPECHAR, IntPtr.Zero, out ki));
        }

        [TestMethod, Description("Delete key")]
        public void TypeDelete()
        {
            var ki = ConvertTypeChar('\b');
            Assert.AreEqual(Key.Back, ki.Key);
        }

   }
}
