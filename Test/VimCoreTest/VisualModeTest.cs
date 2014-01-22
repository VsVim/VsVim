using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Modes.Visual;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class VisualModeTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private ITextSelection _selection;
        private MockRepository _factory;
        private Mock<ICommonOperations> _operations;
        private Mock<ISelectionTracker> _tracker;
        private Mock<ICommandUtil> _commandUtil;
        private VisualMode _modeRaw;
        private IMode _mode;

        private void Create(params string[] lines)
        {
            Create2(lines: lines);
        }

        private void Create2(
            ModeKind kind = ModeKind.VisualCharacter,
            params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            var vimTextBuffer = Vim.CreateVimTextBuffer(_textBuffer);
            var vimBufferData = CreateVimBufferData(vimTextBuffer, _textView);
            var visualKind = VisualKind.OfModeKind(kind).Value;

            _selection = _textView.Selection;
            _factory = new MockRepository(MockBehavior.Strict);
            _tracker = _factory.Create<ISelectionTracker>();
            _tracker.Setup(x => x.Start());
            _tracker.Setup(x => x.UpdateSelection());
            _tracker.Setup(x => x.RecordCaretTrackingPoint(It.IsAny<ModeArgument>()));
            _tracker.SetupGet(x => x.IsRunning).Returns(true);
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.TextView).Returns(_textView);
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), ViewFlags.Standard));
            _commandUtil = _factory.Create<ICommandUtil>();
            var motionUtil = new MotionUtil(vimBufferData, _operations.Object);
            var capture = new MotionCapture(vimBufferData, new IncrementalSearch(vimBufferData, _operations.Object));
            var runner = new CommandRunner(
                _textView,
                Vim.RegisterMap,
                capture,
                vimBufferData.LocalSettings,
                _commandUtil.Object,
                (new Mock<IStatusUtil>()).Object,
                VisualKind.Character,
                KeyRemapMode.Visual);
            _modeRaw = new VisualMode(vimBufferData, _operations.Object, motionUtil, visualKind, runner, capture, _tracker.Object);
            _mode = _modeRaw;
            _mode.OnEnter(ModeArgument.None);
        }

        /// <summary>
        /// Movement commands
        /// </summary>
        [Fact]
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

        [Fact]
        public void Process1()
        {
            Create("foo");
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.True(res.IsSwitchPreviousMode());
        }

        /// <summary>
        /// Escape should always escape even if we're processing an inner key sequence
        /// </summary>
        [Fact]
        public void Process2()
        {
            Create("foo");
            _mode.Process('g');
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.True(res.IsSwitchPreviousMode());
        }

        [Fact]
        public void OnLeave1()
        {
            Create();
            _tracker.Setup(x => x.Stop()).Verifiable();
            _mode.OnLeave();
            _tracker.Verify();
        }

        /// <summary>
        /// Must handle arbitrary input to prevent changes but don't list it as a command
        /// </summary>
        [Fact]
        public void PreventInput1()
        {
            Create(lines: "foo");
            var input = KeyInputUtil.CharToKeyInput('@');
            _operations.Setup(x => x.Beep()).Verifiable();
            Assert.False(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == input.Char));
            Assert.True(_mode.CanProcess(input));
            var ret = _mode.Process(input);
            Assert.True(ret.IsHandledNoSwitch());
            _operations.Verify();
        }

        [Fact]
        public void Bind_YankSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.YankSelection);
            _mode.Process("y");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_YankLineSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.YankLineSelection);
            _mode.Process("Y");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteSelectedText()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteSelection);
            _mode.Process("d");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteSelectedText_ViaDelete()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteSelection);
            _mode.Process(VimKey.Delete);
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteSelectedText_ViaX()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteSelection);
            _mode.Process("x");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_Join_RemoveEmptySpaces()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewJoinSelection(JoinKind.RemoveEmptySpaces));
            _mode.Process("J");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_Join_KeepEmptySpaces()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewJoinSelection(JoinKind.KeepEmptySpaces));
            _mode.Process("gJ");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ChangeSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.ChangeSelection);
            _mode.Process('c');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ChangeSelection_ViaS()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.ChangeSelection);
            _mode.Process('s');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ChangeLineSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeLineSelection(true));
            _mode.Process('C');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ChangeLineSelection_ViaS()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeLineSelection(false));
            _mode.Process('S');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ChangeLineSelection_ViaR()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeLineSelection(false));
            _mode.Process('R');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ChangeCase_Tilde()
        {
            Create("foo bar", "baz");
            _commandUtil.SetupCommandVisual(VisualCommand.NewChangeCase(ChangeCharacterKind.ToggleCase));
            _mode.Process('~');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ShiftLeft()
        {
            Create("foo bar baz");
            _commandUtil.SetupCommandVisual(VisualCommand.ShiftLinesLeft);
            _mode.Process('<');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ShiftRight()
        {
            Create("foo bar baz");
            _commandUtil.SetupCommandVisual(VisualCommand.ShiftLinesRight);
            _mode.Process('>');
            _operations.Verify();
        }

        [Fact]
        public void Bind_DeleteLineSelection()
        {
            Create("cat", "tree", "dog");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteLineSelection);
            _mode.Process("D");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_PutOverSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(false));
            _mode.Process('p');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_PutOverCaret_WithCaretMove()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(true));
            _mode.Process("gp");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_PutOverSelectio_ViaP()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(false));
            _mode.Process('P');
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_PutPutOverSelection_WithCaretMoveViaP()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.NewPutOverSelection(true));
            _mode.Process("gP");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ReplaceSelection()
        {
            Create("");
            var keyInput = KeyInputUtil.CharToKeyInput('c');
            _commandUtil.SetupCommandVisual(VisualCommand.NewReplaceSelection(keyInput));
            _mode.Process("rc");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteLineSelection_ViaX()
        {
            Create("cat", "tree", "dog");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteLineSelection);
            _mode.Process("X");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_OpenFoldInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.OpenFoldInSelection);
            _mode.Process("zo");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_OpenAllFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.OpenAllFoldsInSelection);
            _mode.Process("zO");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_CloseAllFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.CloseAllFoldsInSelection);
            _mode.Process("zC");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ToggleFoldInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.ToggleFoldInSelection);
            _mode.Process("za");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ToggleAllFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.ToggleAllFoldsInSelection);
            _mode.Process("zA");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_FoldSelection()
        {
            Create("foo bar");
            _commandUtil.SetupCommandVisual(VisualCommand.FoldSelection);
            _mode.Process("zf");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteFoldInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteAllFoldsInSelection);
            _mode.Process("zd");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteAlLFoldsInSelection()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.DeleteAllFoldsInSelection);
            _mode.Process("zD");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_DeleteAllFoldsInBuffer()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteAllFoldsInBuffer);
            _mode.Process("zE");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_SwitchMode_Command()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchMode(ModeKind.Command, ModeArgument.FromVisual));
            _mode.Process(":");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ScrollPages_Up()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<PageUp>"));
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_ScrollPages_Down()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<PageDown>"));
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_FormatLines()
        {
            Create("");
            _commandUtil.SetupCommandVisual(VisualCommand.FormatLines);
            _mode.Process("=");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_Motion_LastSearch()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(true)));
            _mode.Process("N");
            _commandUtil.Verify();
        }

        [Fact]
        public void Bind_Motion_LastSearchReverse()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(false)));
            _mode.Process("n");
            _commandUtil.Verify();
        }
    }
}
