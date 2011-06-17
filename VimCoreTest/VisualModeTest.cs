using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes;
using Vim.Modes.Visual;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VisualModeTest
    {
        private MockRepository _factory;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private ITextSelection _selection;
        private Mock<IVimHost> _host;
        private Mock<IVimBuffer> _bufferData;
        private VisualMode _modeRaw;
        private IMode _mode;
        private IRegisterMap _map;
        private IMarkMap _markMap;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private Mock<ICommonOperations> _operations;
        private Mock<ISelectionTracker> _tracker;
        private Mock<IFoldManager> _foldManager;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IEditorOperations> _editorOperations;
        private Mock<IJumpList> _jumpList;
        private Mock<ICommandUtil> _commandUtil;

        public void Create(params string[] lines)
        {
            Create2(lines: lines);
        }

        public void Create2(
            ModeKind kind = ModeKind.VisualCharacter,
            params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _selection = _textView.Selection;
            _factory = new MockRepository(MockBehavior.Strict);
            _map = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice(_factory).Object);
            _markMap = new MarkMap(new TrackingLineColumnService());
            _tracker = _factory.Create<ISelectionTracker>();
            _tracker.Setup(x => x.Start());
            _tracker.Setup(x => x.ResetCaret());
            _tracker.Setup(x => x.UpdateSelection());
            _jumpList = _factory.Create<IJumpList>(MockBehavior.Loose);
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();
            _foldManager = _factory.Create<IFoldManager>();
            _editorOperations = _factory.Create<IEditorOperations>();
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.UndoRedoOperations).Returns(_undoRedoOperations.Object);
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);
            _operations.SetupGet(x => x.TextView).Returns(_textView);
            _host = _factory.Create<IVimHost>(MockBehavior.Loose);
            _commandUtil = _factory.Create<ICommandUtil>();
            _incrementalSearch = MockObjectFactory.CreateIncrementalSearch(factory: _factory);
            var globalSettings = new GlobalSettings();
            var localSettings = new LocalSettings(globalSettings, EditorUtil.GetEditorOptions(_textView), _textView);
            var motionUtil = VimUtil.CreateTextViewMotionUtil(
                _textView,
                _markMap,
                localSettings);
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _textView,
                "test",
                MockObjectFactory.CreateVim(_map, host: _host.Object, settings: globalSettings).Object,
                incrementalSearch: _incrementalSearch.Object,
                jumpList: _jumpList.Object,
                motionUtil: motionUtil);
            var capture = new MotionCapture(
                _host.Object,
                _textView,
                _incrementalSearch.Object,
                localSettings);
            var runner = new CommandRunner(
                _textView,
                _map,
                capture,
                _commandUtil.Object,
                (new Mock<IStatusUtil>()).Object,
                VisualKind.Character);
            _modeRaw = new VisualMode(_bufferData.Object, _operations.Object, kind, runner, capture, _tracker.Object);
            _mode = _modeRaw;
            _mode.OnEnter(ModeArgument.None);
        }

        [Test, Description("Movement commands")]
        public void Commands1()
        {
            Create("foo");
            var list = new KeyInput[] {
                KeyInputUtil.CharToKeyInput('h'),
                KeyInputUtil.CharToKeyInput('j'),
                KeyInputUtil.CharToKeyInput('k'),
                KeyInputUtil.CharToKeyInput('l'),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Left),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Right),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Up),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Down),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Back) };
            var commands = _mode.CommandNames.ToList();
            foreach (var item in list)
            {
                var name = KeyInputSet.NewOneKeyInput(item);
                Assert.Contains(name, commands);
            }
        }

        [Test]
        public void Process1()
        {
            Create("foo");
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsSwitchPreviousMode());
        }

        [Test, Description("Escape should always escape even if we're processing an inner key sequence")]
        public void Process2()
        {
            Create("foo");
            _mode.Process('g');
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsSwitchPreviousMode());
        }

        [Test]
        public void OnLeave1()
        {
            _tracker.Setup(x => x.Stop()).Verifiable();
            _mode.OnLeave();
            _tracker.Verify();
        }

        [Test, Description("Must handle arbitrary input to prevent changes but don't list it as a command")]
        public void PreventInput1()
        {
            Create(lines: "foo");
            var input = KeyInputUtil.CharToKeyInput('@');
            _operations.Setup(x => x.Beep()).Verifiable();
            Assert.IsFalse(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == input.Char));
            Assert.IsTrue(_mode.CanProcess(input));
            var ret = _mode.Process(input);
            Assert.IsTrue(ret.IsHandledNoSwitch());
            _operations.Verify();
        }

        [Test]
        public void Bind_YankSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.YankSelection);
            _mode.Process("y");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_YankLineSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.YankLineSelection);
            _mode.Process("Y");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteSelectedText()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteSelection);
            _mode.Process("d");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteSelectedText_ViaDelete()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteSelection);
            _mode.Process(VimKey.Delete);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteSelectedText_ViaX()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteSelection);
            _mode.Process("x");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Join_RemoveEmptySpaces()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewJoinSelection(JoinKind.RemoveEmptySpaces));
            _mode.Process("J");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Join_KeepEmptySpaces()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewJoinSelection(JoinKind.KeepEmptySpaces));
            _mode.Process("gJ");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.ChangeSelection);
            _mode.Process('c');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeSelection_ViaS()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.ChangeSelection);
            _mode.Process('s');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeLineSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeLineSelection(true));
            _mode.Process('C');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeLineSelection_ViaS()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeLineSelection(false));
            _mode.Process('S');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeLineSelection_ViaR()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeLineSelection(false));
            _mode.Process('R');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCase_Tilde()
        {
            Create("foo bar", "baz");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeCase(ChangeCharacterKind.ToggleCase));
            _mode.Process('~');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ShiftLeft()
        {
            Create("foo bar baz");
            _commandUtil.SetupCommandVisual(VisualCommand.ShiftLinesLeft);
            _mode.Process('<');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ShiftRight()
        {
            Create("foo bar baz");
            _commandUtil.SetupCommandVisual(VisualCommand.ShiftLinesRight);
            _mode.Process('>');
            _operations.Verify();
        }

        [Test]
        public void Bind_DeleteLineSelection()
        {
            Create("cat", "tree", "dog");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteLineSelection);
            _mode.Process("D");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutOverSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(false));
            _mode.Process('p');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutOverCaret_WithCaretMove()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(true));
            _mode.Process("gp");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutOverSelectio_ViaP()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(false));
            _mode.Process('P');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutPutOverSelection_WithCaretMoveViaP()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(true));
            _mode.Process("gP");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ReplaceSelection()
        {
            Create("");
            var keyInput = KeyInputUtil.CharToKeyInput('c');
            _commandUtil.SetupCommandVisual(VisualCommand.NewReplaceSelection(keyInput));
            _mode.Process("rc");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteLineSelection_ViaX()
        {
            Create("cat", "tree", "dog");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteLineSelection);
            _mode.Process("X");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_OpenFoldInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.OpenFoldInSelection);
            _mode.Process("zo");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_OpenAllFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.OpenAllFoldsInSelection);
            _mode.Process("zO");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_CloseAllFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.CloseAllFoldsInSelection);
            _mode.Process("zC");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_FoldSelection()
        {
            Create("foo bar");
            _commandUtil.SetupCommandVisual(VisualCommand.FoldSelection);
            _mode.Process("zf");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteFoldInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteFoldInSelection);
            _mode.Process("zd");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteAlLFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteAllFoldsInSelection);
            _mode.Process("zD");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteAllFoldsInBuffer()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteAllFoldsInBuffer);
            _mode.Process("zE");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_SwitchMode_Command()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchMode(ModeKind.Command, ModeArgument.FromVisual));
            _mode.Process(":");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Up()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<PageUp>"));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Down()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<PageDown>"));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_FormatLines()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.FormatLines);
            _mode.Process("=");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_LastSearch()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(true)));
            _mode.Process("N");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_LastSearchReverse()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(false)));
            _mode.Process("n");
            _commandUtil.Verify();
        }
    }
}
