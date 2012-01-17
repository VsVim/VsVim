using System;
using EditorUtils.UnitTest;
using Microsoft.FSharp.Collections;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.Modes.Command;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [TestFixture, RequiresSTA]
    public class CommandModeTest : VimTestBase
    {
        private ITextView _textView;
        private IVimBuffer _vimBuffer;
        private ITextBuffer _textBuffer;
        private CommandMode _modeRaw;
        private ICommandMode _mode;

        [SetUp]
        public void SetUp()
        {
            _textView = CreateTextView();
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = CreateVimBuffer(CreateVimBufferData(_textView));

            var factory = new MockRepository(MockBehavior.Strict);
            var commonOperations = CommonOperationsFactory.GetCommonOperations(_vimBuffer.VimBufferData);
            var interpreter = new Interpreter.Interpreter(
                _vimBuffer,
                commonOperations,
                factory.Create<IFoldManager>().Object,
                factory.Create<IFileSystem>().Object,
                factory.Create<IBufferTrackingService>().Object);
            _modeRaw = new CommandMode(_vimBuffer, commonOperations, interpreter);
            _mode = _modeRaw;
        }

        private void ProcessWithEnter(string input)
        {
            _mode.Process(input, enter: true);
        }

        [Test, Description("Entering command mode should update the status")]
        public void StatusOnColon1()
        {
            _mode.OnEnter(ModeArgument.None);
            Assert.AreEqual("", _mode.Command);
        }

        [Test, Description("When leaving command mode we should not clear the status because it will remove error messages")]
        public void StatusOnLeave()
        {
            _mode.OnLeave();
            Assert.AreEqual("", _mode.Command);
        }

        [Test]
        public void Input1()
        {
            _mode.Process("fo");
            Assert.AreEqual("fo", _modeRaw.Command);
        }

        [Test]
        public void Input3()
        {
            _mode.Process("foo");
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.AreEqual("fo", _modeRaw.Command);
        }

        [Test]
        public void Input4()
        {
            _mode.Process("foo");
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(string.Empty, _modeRaw.Command);
        }

        [Test, Description("Delete past the start of the command string")]
        public void Input5()
        {
            _mode.Process('c');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.AreEqual(String.Empty, _modeRaw.Command);
        }

        [Test, Description("Upper case letter")]
        public void Input6()
        {
            _mode.Process("BACK");
            Assert.AreEqual("BACK", _modeRaw.Command);
        }

        [Test]
        public void Input7()
        {
            _mode.Process("_bar");
            Assert.AreEqual("_bar", _modeRaw.Command);
        }

        [Test]
        public void OnEnter1()
        {
            _mode.OnEnter(ModeArgument.None);
            Assert.AreEqual(String.Empty, _modeRaw.Command);
        }

        [Test]
        public void OnEnter2()
        {
            _mode.OnEnter(ModeArgument.FromVisual);
            Assert.AreEqual(CommandMode.FromVisualModeString, _modeRaw.Command);
        }

        [Test]
        public void ClearSelectionOnComplete1()
        {
            _textView.SetText("hello world");
            _textView.SelectAndMoveCaret(_textBuffer.GetSpan(0, 2));
            _mode.Process(KeyInputUtil.EnterKey);
            Assert.IsTrue(_textView.Selection.IsEmpty);
        }

        [Test]
        public void ClearSelectionOnComplete2()
        {
            _textView.SetText("hello world");
            _textView.SelectAndMoveCaret(_textBuffer.GetSpan(0, 2));
            _mode.Process(KeyInputUtil.EnterKey);
            Assert.IsTrue(_textView.Selection.IsEmpty);
        }
    }
}
