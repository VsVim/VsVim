using System;
using System.Windows.Input;
using Microsoft.VisualStudio;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using VsVim.UnitTest.Utils;

namespace VsVim.UnitTest
{
    [TestFixture()]
    public sealed class OleCommandUtilTest
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

        private void VerifyConvert(VSConstants.VSStd2KCmdID cmd, VimKey vimKey, EditCommandKind kind)
        {
            VerifyConvert(cmd, KeyInputUtil.VimKeyToKeyInput(vimKey), kind);
        }

        private void VerifyConvert(VSConstants.VSStd2KCmdID cmd, KeyInput ki, EditCommandKind kind)
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)cmd, out command));
            Assert.AreEqual(ki, command.KeyInput);
            Assert.AreEqual(kind, command.EditCommandKind);
        }

        private void VerifyConvert(VSConstants.VSStd97CmdID cmd, VimKey vimKey, EditCommandKind kind)
        {
            VerifyConvert(cmd, KeyInputUtil.VimKeyToKeyInput(vimKey), kind);
        }

        private void VerifyConvert(VSConstants.VSStd97CmdID cmd, KeyInput ki, EditCommandKind kind)
        {
            EditCommand command;
            Assert.IsTrue(OleCommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)cmd, out command));
            Assert.AreEqual(ki, command.KeyInput);
            Assert.AreEqual(kind, command.EditCommandKind);
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
            Assert.AreEqual(EditCommandKind.UserInput, command.EditCommandKind);
            Assert.AreEqual(Key.A, command.KeyInput.Key);
        }

        // [Test,Ignore]
        public void TypeChar2()
        {
            var command = ConvertTypeChar('b');
            Assert.AreEqual(EditCommandKind.UserInput, command.EditCommandKind);
            Assert.AreEqual(Key.B, command.KeyInput.Key);
        }

        [Test]
        public void ArrowKeys()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.LEFT, VimKey.Left, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.LEFT_EXT, VimKey.Left, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.LEFT_EXT_COL, VimKey.Left, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.RIGHT, VimKey.Right, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.RIGHT_EXT, VimKey.Right, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.RIGHT_EXT_COL, VimKey.Right, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.UP, VimKey.Up, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.UP_EXT, VimKey.Up, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.UP_EXT_COL, VimKey.Up, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.DOWN, VimKey.Down, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.DOWN_EXT, VimKey.Down, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.DOWN_EXT_COL, VimKey.Down, EditCommandKind.UserInput);
        }

        [Test]
        public void Tab1()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.TabKey, EditCommandKind.UserInput);
        }

        [Test]
        public void F1Help1()
        {
            VerifyConvert(VSConstants.VSStd97CmdID.F1Help, VimKey.F1, EditCommandKind.UserInput);
        }

        [Test]
        public void Escape()
        {
            VerifyConvert(VSConstants.VSStd97CmdID.Escape, KeyInputUtil.EscapeKey, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.CANCEL, KeyInputUtil.EscapeKey, EditCommandKind.UserInput);
        }

        [Test]
        public void PageUp()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.PAGEUP, VimKey.PageUp, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.PAGEUP_EXT, VimKey.PageUp, EditCommandKind.UserInput);
        }

        [Test]
        public void PageDown()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.PAGEDN, VimKey.PageDown, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.PAGEDN_EXT, VimKey.PageDown, EditCommandKind.UserInput);
        }

        [Test]
        public void Backspace()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.BACKSPACE, VimKey.Back, EditCommandKind.UserInput);
        }

        /// <summary>
        /// Ensure we can map back and forth every KeyInput value which is considered t obe
        /// text input.  This is important for intercepting commands
        /// </summary>
        [Test]
        public void TryConvert_TextInputToOleCommandData()
        {
            var textView = EditorUtil.CreateTextView("");
            var buffer = EditorUtil.FactoryService.Vim.CreateBuffer(textView);
            buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            foreach (var cur in KeyInputUtil.VimKeyInputList)
            {
                if (!buffer.InsertMode.IsDirectInsert(cur))
                {
                    continue;
                }

                var oleCommandData = new OleCommandData();
                try
                {
                    KeyInput converted;
                    Guid commandGroup;
                    Assert.IsTrue(OleCommandUtil.TryConvert(cur, out commandGroup, out oleCommandData));

                    // We lose fidelity on these keys because they both get written out as numbers
                    // at this point
                    if (VimKeyUtil.IsKeypadKey(cur.Key))
                    {
                        continue;
                    }
                    Assert.IsTrue(OleCommandUtil.TryConvert(commandGroup, oleCommandData, out converted));
                    Assert.AreEqual(converted, cur);
                }
                finally
                {
                    OleCommandData.Release(ref oleCommandData);
                }
            }
        }

        /// <summary>
        /// Make sure the End key converts.
        /// 
        /// Even though the VSStd2KCmdID enumeration defines an END value, it appears to use EOL when
        /// the End key is hit.
        /// </summary>
        [Test]
        public void TryConvert_End()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.EOL, VimKey.End, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.EOL_EXT, VimKey.End, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.EOL_EXT_COL, VimKey.End, EditCommandKind.UserInput);
        }

        /// <summary>
        /// Make sure the Home key converts.
        /// 
        /// Even though the VSStd2KCmdID enumeration defines an HOME value, it appears to use BOL when
        /// the Home key is hit.
        /// </summary>
        [Test]
        public void TryConvert_Home()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.BOL, VimKey.Home, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.BOL_EXT, VimKey.Home, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.BOL_EXT_COL, VimKey.Home, EditCommandKind.UserInput);
        }
    }
}
