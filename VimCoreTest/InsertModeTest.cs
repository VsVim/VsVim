using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Tests to verify the operation of Insert / Replace Mode
    /// </summary>
    [TestFixture]
    public sealed class InsertModeTest
    {
        private MockRepository _factory;
        private Vim.Modes.Insert.InsertMode _modeRaw;
        private IInsertMode _mode;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private Mock<ICommonOperations> _operations;
        private Mock<IDisplayWindowBroker> _broker;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _localSettings;
        private Mock<IEditorOptions> _editorOptions;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<ITextChangeTracker> _textChangeTracker;
        private Mock<IInsertUtil> _insertUtil;
        private Mock<IKeyboardDevice> _keyboardDevice;
        private Mock<IMouseDevice> _mouseDevice;
        private Mock<IVim> _vim;
        private Mock<IWordCompletionSessionFactoryService> _wordCompletionSessionFactoryService;

        [SetUp]
        public void SetUp()
        {
            Create(insertMode: true);
        }

        private void Create(params string[] lines)
        {
            Create(true, lines);
        }

        private void Create(bool insertMode, params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _factory.DefaultValue = DefaultValue.Mock;
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vim = _factory.Create<IVim>(MockBehavior.Loose);
            _editorOptions = _factory.Create<IEditorOptions>(MockBehavior.Loose);
            _globalSettings = _factory.Create<IVimGlobalSettings>();
            _localSettings = _factory.Create<IVimLocalSettings>();
            _localSettings.SetupGet(x => x.GlobalSettings).Returns(_globalSettings.Object);
            _textChangeTracker = _factory.Create<ITextChangeTracker>(MockBehavior.Loose);
            _textChangeTracker.SetupGet(x => x.CurrentChange).Returns(FSharpOption<TextChange>.None);
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();

            var buffer = MockObjectFactory.CreateVimBuffer(
                _textView,
                settings: _localSettings.Object,
                vim: _vim.Object,
                factory: _factory);
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.EditorOperations).Returns(_factory.Create<IEditorOperations>().Object);
            _broker = _factory.Create<IDisplayWindowBroker>();
            _broker.SetupGet(x => x.IsCompletionActive).Returns(false);
            _broker.SetupGet(x => x.IsQuickInfoActive).Returns(false);
            _broker.SetupGet(x => x.IsSignatureHelpActive).Returns(false);
            _insertUtil = _factory.Create<IInsertUtil>();

            // Setup the mouse.  By default we say it has no buttons down as that's the normal state
            _mouseDevice = _factory.Create<IMouseDevice>();
            _mouseDevice.SetupGet(x => x.IsLeftButtonPressed).Returns(false);

            // Setup the keyboard.  By default we don't say that any button is pressed.  Insert mode is usually
            // only concerned with arrow keys and we will set those up as appropriate for the typing tests
            _keyboardDevice = _factory.Create<IKeyboardDevice>();
            _keyboardDevice.Setup(x => x.IsKeyDown(It.IsAny<VimKey>())).Returns(false);

            _modeRaw = new Vim.Modes.Insert.InsertMode(
                buffer.Object,
                _operations.Object,
                _broker.Object,
                _editorOptions.Object,
                _undoRedoOperations.Object,
                _textChangeTracker.Object,
                _insertUtil.Object,
                !insertMode,
                _keyboardDevice.Object,
                _mouseDevice.Object,
                EditorUtil.FactoryService.WordUtilFactory.GetWordUtil(_textView),
                EditorUtil.FactoryService.WordCompletionSessionFactoryService);
            _mode = _modeRaw;
        }

        private void SetupMoveCaretLeft()
        {
            _insertUtil
                .Setup(x => x.RunInsertCommand(InsertCommand.NewMoveCaret(Direction.Left)))
                .Returns(CommandResult.NewCompleted(ModeSwitch.NewSwitchMode(ModeKind.Normal)))
                .Verifiable();
        }

        /// <summary>
        /// Make sure we can process escape
        /// </summary>
        [Test]
        public void CanProcess_Escape()
        {
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.EscapeKey));
        }

        /// <summary>
        /// Ensure that all known character values are considered direct input.  They cause direct
        /// edits to the buffer.  They are not commands.
        /// </summary>
        [Test]
        public void IsDirectInput_Chars()
        {
            foreach (var cur in KeyInputUtilTest.CharsAll)
            {
                var input = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(_mode.CanProcess(input));
                Assert.IsTrue(_mode.IsDirectInsert(input));
            }
        }

        /// <summary>
        /// Certain keys do cause buffer edits but are not direct input.  They are interpreted by Vim
        /// and given specific values based on settings.  While they cause edits the values passed down
        /// don't directly go to the buffer
        /// </summary>
        [Test]
        public void IsDirectInput_SpecialKeys()
        {
            Assert.IsFalse(_mode.IsDirectInsert(KeyInputUtil.EnterKey));
            Assert.IsFalse(_mode.IsDirectInsert(KeyInputUtil.AlternateEnterKey));
            Assert.IsFalse(_mode.IsDirectInsert(KeyInputUtil.VimKeyToKeyInput(VimKey.Tab)));
        }

        /// <summary>
        /// Make sure to move the caret left when exiting insert mode
        /// </summary>
        [Test]
        public void Escape_MoveCaretLeftOnExit()
        {
            _textView.SetText("hello world", 3);
            _broker.SetupGet(x => x.IsCompletionActive).Returns(false).Verifiable();
            _broker.SetupGet(x => x.IsQuickInfoActive).Returns(false).Verifiable();
            _broker.SetupGet(x => x.IsSignatureHelpActive).Returns(false).Verifiable();
            SetupMoveCaretLeft();
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Normal));
            _factory.Verify();
        }

        /// <summary>
        /// Make sure to dismiss any active completion windows when exiting.  We had the choice
        /// between having escape cancel only the window and escape canceling and returning
        /// to presambly normal mode.  The unanimous user feedback is that Escape should leave 
        /// insert mode no matter what.  
        /// </summary>
        [Test]
        public void Escape_DismissCompletionWindows()
        {
            _textView.SetText("hello world", 1);
            _broker
                .SetupGet(x => x.IsCompletionActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissDisplayWindows())
                .Verifiable();
            SetupMoveCaretLeft();
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Normal));
            _factory.Verify();
        }

        /// <summary>
        /// If the caret is in virtual space when leaving insert mode move it back to the real
        /// position.  This really only comes up in a few cases, primarily the 'C' command 
        /// which preserves indent by putting the caret in virtual space.  For example take the 
        /// following (- are spaces and ^ is caret).
        /// --cat
        ///
        /// Caret starts on the 'c' and 'autoindent' is on.  Execute the following
        ///  - cc
        ///  - Escape
        /// Now the caret is at position 0 on a blank line 
        /// </summary>
        [Test]
        public void Escape_LeaveVirtualSpace()
        {
            _textView.SetText("", "random data");
            var virtualPoint = new VirtualSnapshotPoint(_textView.TextSnapshot.GetPoint(0), 2);
            _textView.Caret.MoveTo(virtualPoint);
            _operations.Setup(x => x.MoveCaretToPoint(virtualPoint.Position)).Verifiable();
            _mode.Process(KeyInputUtil.EscapeKey);
            _operations.Verify();
        }

        [Test]
        public void Control_OpenBracket1()
        {
            var ki = KeyInputUtil.CharWithControlToKeyInput('[');
            var name = KeyInputSet.NewOneKeyInput(ki);
            Assert.IsTrue(_mode.CommandNames.Contains(name));
        }

        [Test]
        public void Control_OpenBracket2()
        {
            _broker
                .SetupGet(x => x.IsCompletionActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissDisplayWindows())
                .Verifiable();
            _insertUtil
                .Setup(x => x.RunInsertCommand(InsertCommand.NewMoveCaret(Direction.Left)))
                .Returns(CommandResult.NewCompleted(ModeSwitch.NewSwitchMode(ModeKind.Normal)))
                .Verifiable();
            var ki = KeyInputUtil.CharWithControlToKeyInput('[');
            var res = _mode.Process(ki);
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Normal));
            _factory.Verify();
        }

        /// <summary>
        /// Make sure we bind the shift left command
        /// </summary>
        [Test]
        public void Command_ShiftLeft()
        {
            _textView.SetText("hello world");
            _insertUtil.Setup(x => x.RunInsertCommand(InsertCommand.ShiftLineLeft)).Returns(CommandResult.NewCompleted(ModeSwitch.NoSwitch)).Verifiable();
            var res = _mode.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            Assert.IsTrue(res.IsHandledNoSwitch());
            _factory.Verify();
        }

        /// <summary>
        /// Make sure we bind the shift right command
        /// </summary>
        [Test]
        public void Command_ShiftRight()
        {
            SetUp();
            _textView.SetText("hello world");
            _insertUtil.Setup(x => x.RunInsertCommand(InsertCommand.ShiftLineRight)).Returns(CommandResult.NewCompleted(ModeSwitch.NoSwitch)).Verifiable();
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-T>"));
            _factory.Verify();
        }


        [Test]
        public void OnLeave1()
        {
            _mode.OnLeave();
            _factory.Verify();
        }

        [Test]
        public void NormalModeOneTimeCommand1()
        {
            var res = _mode.Process(KeyNotationUtil.StringToKeyInput("<C-o>"));
            Assert.IsTrue(res.IsSwitchModeWithArgument(ModeKind.Normal, ModeArgument.NewOneTimeCommand(ModeKind.Insert)));
        }

        [Test]
        public void ReplaceMode1()
        {
            Create(insertMode: false);
            Assert.AreEqual(ModeKind.Replace, _mode.ModeKind);
        }

        [Test]
        public void ReplaceMode2()
        {
            Create(insertMode: false);
            _editorOptions
                .Setup(x => x.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, false))
                .Verifiable();
            _mode.OnLeave();
            _factory.Verify();
        }

        /// <summary>
        /// When the caret moves due to the mouse being clicked that should complete the current text change
        /// </summary>
        [Test]
        public void TextChange_CaretMoveFromClickShouldComplete()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textChangeTracker.Setup(x => x.CompleteChange()).Verifiable();
            _mouseDevice.SetupGet(x => x.IsLeftButtonPressed).Returns(true).Verifiable();
            _textView.MoveCaretTo(7);
            _factory.Verify();
        }

        /// <summary>
        /// When the caret moves as a part of the edit then it shouldn't cause the change to complete
        /// </summary>
        [Test]
        public void TextChange_CaretMoveFromEdit()
        {
            Create("the quick brown fox");
            _textChangeTracker.Setup(x => x.CompleteChange()).Throws(new Exception());
            _mouseDevice.SetupGet(x => x.IsLeftButtonPressed).Returns(false).Verifiable();
            _textBuffer.Insert(0, "a");
            _textView.MoveCaretTo(7);
            _factory.Verify();
        }
    }
}
