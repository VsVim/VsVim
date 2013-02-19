using System;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Modes.Command;
using Xunit;

namespace Vim.UnitTest
{
    public class CommandModeTest : VimTestBase
    {
        private readonly ITextView _textView;
        private readonly IVimBuffer _vimBuffer;
        private readonly ITextBuffer _textBuffer;
        private readonly CommandMode _modeRaw;
        private readonly ICommandMode _mode;

        public CommandModeTest()
        {
            _textView = CreateTextView();
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = CreateVimBuffer(CreateVimBufferData(_textView));

            var factory = new MockRepository(MockBehavior.Strict);
            var commonOperations = CommonOperationsFactory.GetCommonOperations(_vimBuffer.VimBufferData);
            _modeRaw = new CommandMode(_vimBuffer, commonOperations);
            _mode = _modeRaw;
        }

        private void ProcessWithEnter(string input)
        {
            _mode.Process(input, enter: true);
        }

        /// <summary>
        /// Entering command mode should update the status
        /// </summary>
        [Fact]
        public void StatusOnColon1()
        {
            _mode.OnEnter(ModeArgument.None);
            Assert.Equal("", _mode.Command);
        }

        /// <summary>
        /// When leaving command mode we should not clear the status because it will remove error messages
        /// </summary>
        [Fact]
        public void StatusOnLeave()
        {
            _mode.OnLeave();
            Assert.Equal("", _mode.Command);
        }

        [Fact]
        public void Input1()
        {
            _mode.Process("fo");
            Assert.Equal("fo", _modeRaw.Command);
        }

        [Fact]
        public void Input3()
        {
            _mode.Process("foo");
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.Equal("fo", _modeRaw.Command);
        }

        [Fact]
        public void Input4()
        {
            _mode.Process("foo");
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.Equal(string.Empty, _modeRaw.Command);
        }

        /// <summary>
        /// Delete past the start of the command string
        /// </summary>
        [Fact]
        public void Input5()
        {
            _mode.Process('c');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.Equal(String.Empty, _modeRaw.Command);
        }

        /// <summary>
        /// Upper case letter
        /// </summary>
        [Fact]
        public void Input6()
        {
            _mode.Process("BACK");
            Assert.Equal("BACK", _modeRaw.Command);
        }

        [Fact]
        public void Input7()
        {
            _mode.Process("_bar");
            Assert.Equal("_bar", _modeRaw.Command);
        }

        [Fact]
        public void OnEnter1()
        {
            _mode.OnEnter(ModeArgument.None);
            Assert.Equal(String.Empty, _modeRaw.Command);
        }

        [Fact]
        public void OnEnter2()
        {
            _mode.OnEnter(ModeArgument.FromVisual);
            Assert.Equal(CommandMode.FromVisualModeString, _modeRaw.Command);
        }

        [Fact]
        public void ClearSelectionOnComplete1()
        {
            _textView.SetText("hello world");
            _textView.SelectAndMoveCaret(_textBuffer.GetSpan(0, 2));
            _mode.Process(KeyInputUtil.EnterKey);
            Assert.True(_textView.Selection.IsEmpty);
        }

        [Fact]
        public void ClearSelectionOnComplete2()
        {
            _textView.SetText("hello world");
            _textView.SelectAndMoveCaret(_textBuffer.GetSpan(0, 2));
            _mode.Process(KeyInputUtil.EnterKey);
            Assert.True(_textView.Selection.IsEmpty);
        }
    }
}
