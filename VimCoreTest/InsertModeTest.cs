using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Summary description for InputMode
    /// </summary>
    [TestFixture]
    public class InsertModeTest
    {
        private MockRepository _factory;
        private Mock<IVimBuffer> _data;
        private Vim.Modes.Insert.InsertMode _modeRaw;
        private IInsertMode _mode;
        private ITextView _textView;
        private Mock<ICommonOperations> _operations;
        private Mock<IDisplayWindowBroker> _broker;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _localSettings;
        private Mock<IEditorOptions> _editorOptions;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<ITextChangeTracker> _textChangeTracker;
        private Mock<IVim> _vim;

        [SetUp]
        public void SetUp()
        {
            SetUp(insertMode: true);
        }

        public void SetUp(bool insertMode)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _factory.DefaultValue = DefaultValue.Mock;
            _textView = EditorUtil.CreateTextView();
            _vim = _factory.Create<IVim>(MockBehavior.Loose);
            _editorOptions = _factory.Create<IEditorOptions>(MockBehavior.Loose);
            _globalSettings = _factory.Create<IVimGlobalSettings>();
            _localSettings = _factory.Create<IVimLocalSettings>();
            _localSettings.SetupGet(x => x.GlobalSettings).Returns(_globalSettings.Object);
            _textChangeTracker = _factory.Create<ITextChangeTracker>();
            _textChangeTracker.SetupGet(x => x.CurrentChange).Returns(FSharpOption<TextChange>.None);
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();
            _data = MockObjectFactory.CreateVimBuffer(
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
            _modeRaw = new Vim.Modes.Insert.InsertMode(
                _data.Object,
                _operations.Object,
                _broker.Object,
                _editorOptions.Object,
                _undoRedoOperations.Object,
                _textChangeTracker.Object,
                _isReplace: !insertMode);
            _mode = _modeRaw;
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
        /// Ensure we can process a variety of TextInput but that it's explicitly listed as such
        /// </summary>
        [Test]
        public void CanProcess_DirectInput()
        {
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsTrue(_mode.IsDirectInsert(KeyInputUtil.EnterKey));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.AlternateEnterKey));
            Assert.IsTrue(_mode.IsDirectInsert(KeyInputUtil.AlternateEnterKey));

            foreach (var cur in KeyInputUtilTest.CharsAll)
            {
                var input = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(_mode.CanProcess(input));
                Assert.IsTrue(_mode.IsDirectInsert(input));
            }
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
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(It.IsAny<SnapshotPoint>())).Verifiable();
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
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(It.IsAny<SnapshotPoint>())).Verifiable();
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
        public void Control_OpenBraket2()
        {
            _broker
                .SetupGet(x => x.IsCompletionActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissDisplayWindows())
                .Verifiable();
            var ki = KeyInputUtil.CharWithControlToKeyInput('[');
            var res = _mode.Process(ki);
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Normal));
            _factory.Verify();
        }

        [Test]
        public void ShiftLeft1()
        {
            _textView.SetText("hello world");
            _operations
                .Setup(x => x.ShiftLineRangeLeft(_textView.GetLineRange(0, 0), 1))
                .Verifiable(); ;
            var res = _mode.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            Assert.IsTrue(res.IsHandledNoSwitch());
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
            SetUp(insertMode: false);
            Assert.AreEqual(ModeKind.Replace, _mode.ModeKind);
        }

        [Test]
        public void ReplaceMode2()
        {
            SetUp(insertMode: false);
            _editorOptions
                .Setup(x => x.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, false))
                .Verifiable();
            _mode.OnLeave();
            _factory.Verify();
        }

        [Test]
        public void ShiftRight1()
        {
            SetUp();
            _textView.SetText("hello world");
            _operations.Setup(x => x.ShiftLineRangeRight(_textView.GetLineRange(0, 0), 1)).Verifiable();
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-T>"));
            _factory.Verify();
        }

    }
}
