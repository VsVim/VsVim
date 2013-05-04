using System;
using Microsoft.VisualStudio;
using Vim;
using Vim.UnitTest;
using VsVim.UnitTest.Utils;
using Xunit;

namespace VsVim.UnitTest
{
    public sealed class OleCommandUtilTest : VimTestBase
    {
        internal EditCommand ConvertTypeChar(char data)
        {
            using (var ptr = CharPointer.Create(data))
            {
                EditCommand command;
                Assert.True(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, ptr.IntPtr, KeyModifiers.None, out command));
                return command;
            }
        }
        private void VerifyConvertWithShift(VSConstants.VSStd2KCmdID cmd, VimKey vimKey, EditCommandKind kind)
        {
            var keyInput = KeyInputUtil.ApplyModifiers(KeyInputUtil.VimKeyToKeyInput(vimKey), KeyModifiers.Shift);
            VerifyConvert(cmd, keyInput, kind);
        }

        private void VerifyConvert(VSConstants.VSStd2KCmdID cmd, VimKey vimKey, EditCommandKind kind)
        {
            VerifyConvert(cmd, KeyInputUtil.VimKeyToKeyInput(vimKey), kind);
        }

        private void VerifyConvert(VSConstants.VSStd2KCmdID cmd, KeyInput ki, EditCommandKind kind)
        {
            VerifyConvert(cmd, KeyModifiers.None, ki, kind);
        }

        private void VerifyConvert(VSConstants.VSStd2KCmdID cmd, KeyModifiers modifiers, KeyInput ki, EditCommandKind kind)
        {
            EditCommand command;
            Assert.True(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)cmd, IntPtr.Zero, modifiers, out command));
            Assert.Equal(ki, command.KeyInput);
            Assert.Equal(kind, command.EditCommandKind);
        }

        private void VerifyConvert(VSConstants.VSStd97CmdID cmd, VimKey vimKey, EditCommandKind kind)
        {
            VerifyConvert(cmd, KeyInputUtil.VimKeyToKeyInput(vimKey), kind);
        }

        private void VerifyConvert(VSConstants.VSStd97CmdID cmd, KeyInput ki, EditCommandKind kind)
        {
            EditCommand command;
            Assert.True(OleCommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)cmd, IntPtr.Zero, KeyModifiers.None, out command));
            Assert.Equal(ki, command.KeyInput);
            Assert.Equal(kind, command.EditCommandKind);
        }

        /// <summary>
        /// Verify we can convert the given VimKey to the specified command id
        /// </summary>
        private void VerifyConvert(VimKey vimKey, VSConstants.VSStd2KCmdID cmd)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);
            OleCommandData oleCommandData;
            Assert.True(OleCommandUtil.TryConvert(keyInput, false, out oleCommandData));
            Assert.Equal(VSConstants.VSStd2K, oleCommandData.Group);
            Assert.Equal(new OleCommandData(cmd), oleCommandData);
        }

        /// <summary>
        /// Verify the given VimKey converts to the provided command id and vice versa 
        /// </summary>
        private void VerifyBothWays(VSConstants.VSStd2KCmdID cmd, VimKey vimKey, EditCommandKind kind = EditCommandKind.UserInput)
        {
            VerifyConvert(cmd, vimKey, kind);
        }

        // [Test, Description("Make sure we don't puke on missing data"),Ignore]
        public void TypeCharNoData()
        {
            EditCommand command;
            Assert.False(OleCommandUtil.TryConvert(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, IntPtr.Zero, KeyModifiers.None, out command));
        }

        /// <summary>
        /// When processing a raw character we shouldn't be reapplying the existing modifiers.  That only needs
        /// to be done for non-char commands
        ///
        /// Issue 961
        /// </summary>
        [Fact]
        public void TypeChar_WithModifiers()
        {
            var source = @"@£$€{[]}\";
            var modifiers = KeyModifiers.Alt | KeyModifiers.Control;
            foreach (var c in source)
            {
                using (var ptr = CharPointer.Create(c))
                {
                    EditCommand command;
                    Assert.True(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TYPECHAR, ptr.IntPtr, modifiers, out command));
                    Assert.Equal(c, command.KeyInput.Char);
                }
            }
        }

        [Fact]
        public void ArrowKeys()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.LEFT, VimKey.Left, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.RIGHT, VimKey.Right, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.UP, VimKey.Up, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.DOWN, VimKey.Down, EditCommandKind.UserInput);
        }

        [Fact]
        public void ArrowKey_WithModifiers()
        {
            var modifiers = KeyModifiers.Alt | KeyModifiers.Control;
            EditCommand command;
            Assert.True(OleCommandUtil.TryConvert(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.LEFT, IntPtr.Zero, modifiers, out command));
            Assert.Equal(modifiers, command.KeyInput.KeyModifiers);
        }

        /// <summary>
        /// The selection extender versions of the arrow keys should register as commands.  Something we 
        /// shouldn't be processing
        /// </summary>
        [Fact]
        public void Selectors_ArrowKeys()
        {
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.LEFT_EXT, VimKey.Left, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.LEFT_EXT_COL, VimKey.Left, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.RIGHT_EXT, VimKey.Right, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.RIGHT_EXT_COL, VimKey.Right, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.UP_EXT, VimKey.Up, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.UP_EXT_COL, VimKey.Up, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.DOWN_EXT, VimKey.Down, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.DOWN_EXT_COL, VimKey.Down, EditCommandKind.VisualStudioCommand);
        }

        [Fact]
        public void Selectors_Others()
        {
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.PAGEUP_EXT, VimKey.PageUp, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.PAGEDN_EXT, VimKey.PageDown, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.EOL_EXT, VimKey.End, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.EOL_EXT_COL, VimKey.End, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.BOL_EXT, VimKey.Home, EditCommandKind.VisualStudioCommand);
            VerifyConvertWithShift(VSConstants.VSStd2KCmdID.BOL_EXT_COL, VimKey.Home, EditCommandKind.VisualStudioCommand);
        }

        [Fact]
        public void Tab()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.TabKey, EditCommandKind.UserInput);
        }

        [Fact]
        public void BackTab()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.BACKTAB, KeyInputUtil.ApplyModifiers(KeyInputUtil.TabKey, KeyModifiers.Shift), EditCommandKind.UserInput);
        }

        /// <summary>
        /// Verify that the shift modifier is properly applied to a tab
        /// </summary>
        [Fact]
        public void Tab_WithShift()
        {
            var keyInput = KeyInputUtil.ApplyModifiers(KeyInputUtil.TabKey, KeyModifiers.Shift);
            Assert.Equal(keyInput.KeyModifiers, KeyModifiers.Shift);
            VerifyConvert(VSConstants.VSStd2KCmdID.TAB, KeyModifiers.Shift, keyInput, EditCommandKind.UserInput);
        }

        [Fact]
        public void F1Help1()
        {
            VerifyConvert(VSConstants.VSStd97CmdID.F1Help, VimKey.F1, EditCommandKind.UserInput);
        }

        [Fact]
        public void Escape()
        {
            VerifyConvert(VSConstants.VSStd97CmdID.Escape, KeyInputUtil.EscapeKey, EditCommandKind.UserInput);
            VerifyConvert(VSConstants.VSStd2KCmdID.CANCEL, KeyInputUtil.EscapeKey, EditCommandKind.UserInput);
        }

        [Fact]
        public void PageUp()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.PAGEUP, VimKey.PageUp, EditCommandKind.UserInput);
        }

        [Fact]
        public void PageDown()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.PAGEDN, VimKey.PageDown, EditCommandKind.UserInput);
        }

        [Fact]
        public void Backspace()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.BACKSPACE, VimKey.Back, EditCommandKind.UserInput);
        }

        /// <summary>
        /// Ensure we can map back and forth every KeyInput value which is considered t obe
        /// text input.  This is important for intercepting commands
        /// </summary>
        [Fact]
        public void TryConvert_TextInputToOleCommandData()
        {
            var textView = CreateTextView("");
            var buffer = Vim.CreateVimBuffer(textView);
            buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            foreach (var cur in KeyInputUtil.VimKeyInputList)
            {
                if (!buffer.InsertMode.IsDirectInsert(cur))
                {
                    continue;
                }

                var oleCommandData = OleCommandData.Empty;
                try
                {
                    KeyInput converted;
                    Assert.True(OleCommandUtil.TryConvert(cur, out oleCommandData));

                    // We lose fidelity on these keys because they both get written out as numbers
                    // at this point
                    if (VimKeyUtil.IsKeypadKey(cur.Key))
                    {
                        continue;
                    }
                    Assert.True(OleCommandUtil.TryConvert(oleCommandData, out converted));
                    Assert.Equal(converted, cur);
                }
                finally
                {
                    oleCommandData.Dispose();
                }
            }
        }

        /// <summary>
        /// Make sure the End key converts.
        /// 
        /// Even though the VSStd2KCmdID enumeration defines an END value, it appears to use EOL when
        /// the End key is hit.
        /// </summary>
        [Fact]
        public void TryConvert_End()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.EOL, VimKey.End, EditCommandKind.UserInput);
        }

        /// <summary>
        /// Make sure the Home key converts.
        /// 
        /// Even though the VSStd2KCmdID enumeration defines an HOME value, it appears to use BOL when
        /// the Home key is hit.
        /// </summary>
        [Fact]
        public void TryConvert_Home()
        {
            VerifyConvert(VSConstants.VSStd2KCmdID.BOL, VimKey.Home, EditCommandKind.UserInput);
        }

        /// <summary>
        /// Verify we can convert the Insert key in both directions
        /// </summary>
        [Fact]
        public void TryConvert_Insert()
        {
            VerifyBothWays(VSConstants.VSStd2KCmdID.TOGGLE_OVERTYPE_MODE, VimKey.Insert);
        }
    }
}
