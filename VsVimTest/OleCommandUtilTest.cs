using System;
using System.Windows.Input;
using Microsoft.VisualStudio;
using NUnit.Framework;
using Vim;
using VsVim;
using VsVimTest.Utils;

namespace VsVimTest
{
    [TestFixture()]
    public class OleCommandUtilTest
    {
        internal EditCommand ConvertTypeChar(char data)
        {
            using (var ptr = CharPointer.Create(data))
            {
                EditCommand command;
                Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, ptr.IntPtr, out command));
                return command;
            }
        }

        // [Test, Description("Make sure we don't puke on missing data"),Ignore]
        public void TypeCharNoData()
        {
            EditCommand command;
            Assert.IsFalse(OleCommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, IntPtr.Zero, out command));
        }

        // [Test, Description("Delete key"), Ignore]
        public void TypeDelete()
        {
            var command = ConvertTypeChar('\b');
            Assert.AreEqual(Key.Back, command.KeyInput.Key);
        }

        // [Test, Ignore]
        public void TypeChar1()
        {
            var command = ConvertTypeChar('a');
            Assert.AreEqual(EditCommandKind.TypeChar, command.EditCommandKind);
            Assert.AreEqual(Key.A, command.KeyInput.Key);
        }

        // [Test,Ignore]
        public void TypeChar2()
        {
            var command = ConvertTypeChar('b');
            Assert.AreEqual(EditCommandKind.TypeChar, command.EditCommandKind);
            Assert.AreEqual(Key.B, command.KeyInput.Key);
        }

        [Test]
        public void Left1()
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.LEFT, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.Left), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Right1()
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RIGHT, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.Right), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Up1()
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.UP, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.Up), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Down1()
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.DOWN, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.Down), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Tab1()
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TAB, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.Tab), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void F1Help1()
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.F1Help, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.F1), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }
   }
}
