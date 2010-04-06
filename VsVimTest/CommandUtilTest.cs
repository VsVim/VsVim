using VsVim;
using NUnit.Framework;
using System;
using Microsoft.VisualStudio;
using Vim;
using System.Runtime.InteropServices;
using System.Windows.Input;
using VsVimTest.Utils;

namespace VsVimTest
{
    [TestFixture()]
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

        // [Test, Description("Make sure we don't puke on missing data"),Ignore]
        public void TypeCharNoData()
        {
            EditCommand command;
            Assert.IsFalse(CommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, IntPtr.Zero, out command));
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
            Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.LEFT, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.LeftKey), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Right1()
        {
            EditCommand command;
            Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RIGHT, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.RightKey), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Up1()
        {
            EditCommand command;
            Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.UP, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.UpKey), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Down1()
        {
            EditCommand command;
            Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.DOWN, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.DownKey), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }

        [Test]
        public void Tab1()
        {
            EditCommand command;
            Assert.IsTrue(CommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TAB, out command));
            Assert.AreEqual(InputUtil.VimKeyToKeyInput(VimKey.TabKey), command.KeyInput);
            Assert.IsFalse(command.IsInput);
        }
   }
}
