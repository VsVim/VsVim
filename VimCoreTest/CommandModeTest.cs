using System;
using Microsoft.FSharp.Collections;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes.Command;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using Vim.Modes;

namespace VimCore.UnitTest
{
    [TestFixture, RequiresSTA]
    public class CommandModeTest
    {
        private MockRepository _factory;
        private Mock<ITextCaret> _caret;
        private Mock<ITextView> _textView;
        private Mock<ITextSelection> _selection;
        private Mock<IVimBuffer> _bufferData;
        private Mock<ICommandProcessor> _processor;
        private Mock<ICommonOperations> _operations;
        private ITextBuffer _buffer;
        private CommandMode _modeRaw;
        private ICommandMode _mode;

        [SetUp]
        public void SetUp()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _selection = _factory.Create<ITextSelection>();
            _selection.Setup(x => x.IsEmpty).Returns(true);
            _operations = _factory.Create<ICommonOperations>();
            _buffer = EditorUtil.CreateTextBuffer();
            _caret = MockObjectFactory.CreateCaret(factory: _factory);
            _caret.SetupProperty(x => x.IsHidden);
            _textView = MockObjectFactory.CreateTextView(
                textBuffer: _buffer,
                caret: _caret.Object,
                selection: _selection.Object,
                factory: _factory);

            _bufferData = MockObjectFactory.CreateVimBuffer(textView: _textView.Object, factory: _factory);
            _processor = _factory.Create<ICommandProcessor>();
            _modeRaw = new CommandMode(_bufferData.Object, _processor.Object, _operations.Object);
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
        public void StatusOnProcess()
        {
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("1"))).Returns(RunResult.Completed).Verifiable();
            _mode.Process("1", enter: true);
            _factory.Verify();
        }

        [Test, Description("Ensure multiple commands can be processed")]
        public void DoubleCommand1()
        {
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("2"))).Returns(RunResult.Completed).Verifiable();
            ProcessWithEnter("2");
            _factory.Verify();
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("3"))).Returns(RunResult.Completed).Verifiable();
            ProcessWithEnter("3");
            _factory.Verify();
        }

        [Test]
        public void Input1()
        {
            _mode.Process("fo");
            Assert.AreEqual("fo", _modeRaw.Command);
        }

        [Test]
        public void Input2()
        {
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("foo"))).Returns(RunResult.Completed).Verifiable();
            ProcessWithEnter("foo");
            _factory.Verify();
            Assert.AreEqual(String.Empty, _modeRaw.Command);
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
        public void OnEnter3()
        {
            _mode.OnEnter(ModeArgument.FromVisual);
            _processor
                .Setup(x => x.RunCommand(MatchUtil.CreateForCharList(CommandMode.FromVisualModeString)))
                .Returns(RunResult.Completed)
                .Verifiable();
            _mode.Process(KeyInputUtil.EnterKey);
            _factory.Verify();
        }

        [Test]
        public void OnEnter4()
        {
            _mode.OnEnter(ModeArgument.FromVisual);
            _mode.Process('a');
            _processor
                .Setup(x => x.RunCommand(MatchUtil.CreateForCharList(CommandMode.FromVisualModeString + "a")))
                .Returns(RunResult.Completed)
                .Verifiable();
            _mode.Process(KeyInputUtil.EnterKey);
            _factory.Verify();
        }

        [Test]
        public void ClearSelectionOnComplete1()
        {
            _processor.Setup(x => x.RunCommand(It.IsAny<FSharpList<char>>())).Returns(RunResult.Completed).Verifiable();
            _selection.Setup(x => x.IsEmpty).Returns(true).Verifiable();
            _mode.Process(KeyInputUtil.EnterKey);
            _factory.Verify();
        }

        [Test]
        public void ClearSelectionOnComplete2()
        {
            _textView.Setup(x => x.IsClosed).Returns(false).Verifiable();
            _processor.Setup(x => x.RunCommand(It.IsAny<FSharpList<char>>())).Returns(RunResult.Completed).Verifiable();
            _selection.Setup(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Setup(x => x.Clear()).Verifiable();
            _mode.Process(KeyInputUtil.EnterKey);
            _factory.Verify();
        }

        [Test]
        public void ClearSelectionOnComplete3()
        {
            _selection.Setup(x => x.IsEmpty).Returns(true).Verifiable();
            _mode.Process(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void ClearSelectionOnComplete4()
        {
            _textView.Setup(x => x.IsClosed).Returns(false).Verifiable();
            _selection.Setup(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Setup(x => x.Clear()).Verifiable();
            _buffer.SetText("hello world");
            _selection
                .Setup(x => x.StreamSelectionSpan)
                .Returns(new VirtualSnapshotSpan(_buffer.GetSpan(1, 2)))
                .Verifiable();
            _caret.Setup(x => x.MoveTo(_buffer.GetPoint(1))).Returns(new CaretPosition()).Verifiable();
            _caret.Setup(x => x.EnsureVisible()).Verifiable();
            _mode.Process(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }
    }
}
