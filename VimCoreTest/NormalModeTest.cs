using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes.Normal;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class NormalModeTest
    {
        private NormalMode _modeRaw;
        private INormalMode _mode;
        private ITextView _textView;
        private IRegisterMap _map;
        private IVimData _vimData;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private MockRepository _factory;
        private Mock<IVimBuffer> _buffer;
        private Mock<IOperations> _operations;
        private Mock<IEditorOperations> _editorOperations;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private Mock<IJumpList> _jumpList;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IDisplayWindowBroker> _displayWindowBroker;
        private Mock<IFoldManager> _foldManager;
        private Mock<IVimHost> _host;
        private Mock<ICommandUtil> _commandUtil;
        private Register _unnamedRegister;

        static readonly string[] DefaultLines = new[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        public void Create(params string[] lines)
        {
            CreateCore(null, lines);
        }

        public void Create(ITextViewMotionUtil motionUtil, params string[] lines)
        {
            CreateCore(motionUtil, lines);
        }

        public void CreateCore(ITextViewMotionUtil motionUtil, params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _map = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            _unnamedRegister = _map.GetRegister(RegisterName.Unnamed);
            _factory = new MockRepository(MockBehavior.Strict);
            _editorOperations = _factory.Create<IEditorOperations>(MockBehavior.Loose);
            _incrementalSearch = MockObjectFactory.CreateIncrementalSearch(factory: _factory);
            _jumpList = _factory.Create<IJumpList>(MockBehavior.Strict);
            _statusUtil = _factory.Create<IStatusUtil>(MockBehavior.Strict);
            _foldManager = _factory.Create<IFoldManager>(MockBehavior.Strict);
            _host = _factory.Create<IVimHost>(MockBehavior.Loose);
            _commandUtil = _factory.Create<ICommandUtil>();
            _commandUtil
                .Setup(x => x.RunCommand(It.Is<Command>(y => y.IsLegacyCommand)))
                .Returns<Command>(c => c.AsLegacyCommand().Item.Function.Invoke(null));
            _displayWindowBroker = _factory.Create<IDisplayWindowBroker>(MockBehavior.Strict);
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSignatureHelpActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(false);
            _vimData = new VimData();

            _globalSettings = new Vim.GlobalSettings();
            _localSettings = new LocalSettings(_globalSettings, _textView);
            motionUtil = motionUtil ?? VimUtil.CreateTextViewMotionUtil(
                _textView,
                new MarkMap(new TrackingLineColumnService()),
                _localSettings);
            _buffer = MockObjectFactory.CreateVimBuffer(
                _textView,
                "test",
                MockObjectFactory.CreateVim(_map, host: _host.Object, vimData: _vimData).Object,
                _jumpList.Object,
                incrementalSearch: _incrementalSearch.Object,
                motionUtil: motionUtil,
                settings: _localSettings);
            _operations = _factory.Create<IOperations>(MockBehavior.Strict);
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);
            _operations.SetupGet(x => x.TextView).Returns(_textView);
            _operations.SetupGet(x => x.FoldManager).Returns(_foldManager.Object);

            var capture = new MotionCapture(
                _host.Object,
                _textView,
                _incrementalSearch.Object,
                new LocalSettings(new GlobalSettings(), _textView));
            var runner = new CommandRunner(_textView, _map, capture, _commandUtil.Object, _statusUtil.Object, VisualKind.Character);
            _modeRaw = new NormalMode(
                _buffer.Object,
                _operations.Object,
                _statusUtil.Object,
                _displayWindowBroker.Object,
                runner,
                capture);
            _mode = _modeRaw;
            _mode.OnEnter(ModeArgument.None);
        }

        private MotionResult CreateMotionResult(SnapshotSpan? span = null)
        {
            span = span ?? new SnapshotSpan(_textView.TextSnapshot, 0, 3);
            return VimUtil.CreateMotionResult(
                span.Value,
                true,
                MotionKind.Exclusive,
                OperationKind.LineWise);
        }

        [TearDown]
        public void TearDown()
        {
            _textView = null;
            _mode = null;
        }

        [Test]
        public void ModeKindTest()
        {
            Create(DefaultLines);
            Assert.AreEqual(ModeKind.Normal, _mode.ModeKind);
        }

        [Test, Description("Let enter go straight back to the editor in the default case")]
        public void EnterProcessing()
        {
            Create(DefaultLines);
            var can = _mode.CanProcess(KeyInputUtil.EnterKey);
            Assert.IsTrue(can);
        }

        #region CanProcess

        [Test, Description("Can process basic commands")]
        public void CanProcess1()
        {
            Create(DefaultLines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('u')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('h')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('j')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('i')));
        }

        [Test, Description("Can process even invalid commands else they end up as input")]
        public void CanProcess2()
        {
            Create(DefaultLines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('U')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('Z')));
        }

        [Test, Description("Must be able to process numbers")]
        public void CanProcess3()
        {
            Create(DefaultLines);
            foreach (var cur in Enumerable.Range(1, 8))
            {
                var c = char.Parse(cur.ToString());
                var ki = KeyInputUtil.CharToKeyInput(c);
                Assert.IsTrue(_mode.CanProcess(ki));
            }
        }

        [Test, Description("When in a need more state, process everything")]
        public void CanProcess4()
        {
            Create(DefaultLines);
            _incrementalSearch
                .Setup(x => x.Begin(SearchKind.ForwardWithWrap))
                .Returns(VimUtil.CreateBindData<SearchResult>());
            _mode.Process(KeyInputUtil.CharToKeyInput('/'));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('U')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('Z')));
        }

        [Test, Description("Don't process while a smart tag is open otherwise you prevent it from being used")]
        public void CanProcess5()
        {
            Create(DefaultLines);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(true);
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Left)));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Down)));
        }

        [Test, Description("Should be able to handle ever core character")]
        public void CanProcess6()
        {
            Create(DefaultLines);
            foreach (var cur in KeyInputUtil.VimKeyCharList)
            {
                Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput(cur)));
            }
        }

        [Test, Description("Must be able to handle certain movement keys")]
        public void CanProcess7()
        {
            Create(DefaultLines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.TabKey));
        }

        [Test, Description("Don't process while a completion window is open otherwise you prevent it from being used")]
        public void CanProcess8()
        {
            Create(DefaultLines);
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(true);
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Left)));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Down)));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.TabKey));
        }

        [Test]
        public void CanProcess_DontHandleControlTab()
        {
            Create("");
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.ChangeKeyModifiers(KeyInputUtil.TabKey, KeyModifiers.Control)));
        }

        #endregion

        #region Movement

        [Test]
        public void Bind_MoveCaretTo_l()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Right));
            _mode.Process('l');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_h()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Left));
            _mode.Process('h');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_Backspace()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Left));
            _mode.Process(VimKey.Back);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_k()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Up));
            _mode.Process('k');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_j()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Down));
            _mode.Process('j');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_Left()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Left));
            _mode.Process(VimKey.Left);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_Right()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Right));
            _mode.Process(VimKey.Right);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_Up()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Up));
            _mode.Process(VimKey.Up);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_Down()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Down));
            _mode.Process(VimKey.Down);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_CtrlP()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Up));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('p'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_CtrlN()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Down));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('n'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_CtrlH()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Left));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('h'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretTo_SpaceBar()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Right));
            _mode.Process(' ');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Word()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewWordForward(WordKind.NormalWord)));
            _mode.Process('w');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_BigWord()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewWordForward(WordKind.BigWord)));
            _mode.Process('W');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_WordBackward()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewWordBackward(WordKind.NormalWord)));
            _mode.Process('b');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Hat()
        {
            Create("   foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.FirstNonWhiteSpaceOnLine));
            _mode.Process('^');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Dollar()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.EndOfLine));
            _mode.Process('$');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_0()
        {
            Create("foo bar baz");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.BeginingOfLine));
            _mode.Process('0');
            _commandUtil.Verify();
        }

        [Test]
        public void Move_CHome_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToLineOrFirst(FSharpOption<int>.None)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Home, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void Move_CHome_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToLineOrFirst(FSharpOption.Create(42))).Verifiable();
            _mode.Process("42");
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Home, KeyModifiers.Control));
            _operations.Verify();
        }

        #endregion

        #region Scroll

        [Test]
        public void MoveCaretAndScrollUp1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.MoveCaretAndScrollLines(ScrollDirection.Up, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('u'));
            _operations.Verify();
        }

        [Test, Description("Don't break at line 0")]
        public void MoveCaretAndScrollUp2()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.MoveCaretAndScrollLines(ScrollDirection.Up, 2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('u'));
            _operations.Verify();
        }

        [Test]
        public void MoveCaretAndScrollDown1()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.MoveCaretAndScrollLines(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            _operations.Verify();
        }

        [Test]
        public void ScrollDown1()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _operations.Setup(x => x.ScrollLines(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
            _operations.Verify();
        }

        [Test]
        public void ScrollDown2()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _operations.Setup(x => x.ScrollLines(ScrollDirection.Down, 3)).Verifiable();
            _mode.Process('3');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
            _operations.Verify();
        }

        [Test]
        public void ScrollUp()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _operations.Setup(x => x.ScrollLines(ScrollDirection.Up, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('y'));
            _operations.Verify();
        }

        [Test]
        public void Scroll_zEnter()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineTop()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z", enter: true);
            _editorOperations.Verify();
        }

        [Test]
        public void ScrollPages1()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('f'));
            _operations.Verify();
        }

        [Test]
        public void ScrollPages2()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Down, 2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('f'));
            _operations.Verify();
        }

        [Test]
        public void ScollPages3()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Down, KeyModifiers.Shift));
            _operations.Verify();
        }

        [Test]
        public void ScrollPages4()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown));
            _operations.Verify();
        }

        [Test]
        public void ScrollPages5()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Up, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('b'));
            _operations.Verify();
        }

        [Test]
        public void ScrollPages6()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Up, 2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('b'));
            _operations.Verify();
        }

        [Test]
        public void ScrollPages7()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Up, 1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp));
            _operations.Verify();
        }

        [Test]
        public void ScrollPages8()
        {
            Create("foo bar");
            _operations.Setup(x => x.ScrollPages(ScrollDirection.Up, 1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Up, KeyModifiers.Shift));
            _operations.Verify();
        }

        [Test]
        public void Scroll_zt()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineTop()).Verifiable();
            _mode.Process("zt");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zPeriod()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineCenter()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z.");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zz()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineCenter()).Verifiable();
            _mode.Process("z.");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zDash()
        {
            Create(String.Empty);
            _editorOperations.Setup(x => x.ScrollLineBottom()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z-");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zb()
        {
            Create(String.Empty);
            _editorOperations.Setup(x => x.ScrollLineBottom()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z-");
            _editorOperations.Verify();
        }

        #endregion

        #region Motion

        [Test]
        public void Motion_MoveCaretRight()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Right));
            _mode.Process("l");
            _commandUtil.Verify();
        }

        [Test]
        public void Motion_MoveCaretRight_WithCount()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Right), count: 50);
            _mode.Process("50l");
            _commandUtil.Verify();
        }

        /// <summary>
        /// Need to make sure that G is not being given a count when used as a motion
        /// </summary>
        [Test]
        public void Motion_G()
        {
            var util = new Mock<ITextViewMotionUtil>(MockBehavior.Strict);
            Create(util.Object, "hello world");
            var span = _textView.GetLine(0).Extent;
            var arg = new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, FSharpOption<int>.None);
            util
                .Setup(x => x.GetMotion(Motion.LineOrLastToFirstNonWhiteSpace, arg))
                .Returns(FSharpOption.Create(VimUtil.CreateMotionResult(span, operationKind: OperationKind.LineWise)));
            _commandUtil
                .Setup(x => x.RunCommand(It.Is<Command>(y => y.AsNormalCommand().Item2.Count.IsNone())))
                .Returns(CommandResult.NewCompleted(ModeSwitch.NoSwitch))
                .Verifiable();
            _mode.Process("yG");
            util.Verify();
            _commandUtil.Verify();
        }

        #endregion

        #region Edits

        [Test]
        public void Bind_InsertLineBelow()
        {
            Create("how is", "foo");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertLineBelow);
            _mode.Process('o');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_InsertLineAbove()
        {
            Create("how is", "foo");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertLineAbove);
            _mode.Process('O');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteCharacterBeforeCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterBeforeCaret);
            _mode.Process("X");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteCharacterBeforeCaret_WithCountAndRegister()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterBeforeCaret, 2, RegisterName.OfChar('c').Value);
            _mode.Process("\"c2X");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ReplaceChar_Simple()
        {
            Create("the dog chased the cat");
            _commandUtil.SetupCommandNormal(NormalCommand.NewReplaceChar(KeyInputUtil.VimKeyToKeyInput(VimKey.LowerB)));
            _mode.Process("rb");
        }

        [Test]
        public void Bind_ReplaceChar_WithCount()
        {
            Create("the dog chased the cat");
            _commandUtil.SetupCommandNormal(NormalCommand.NewReplaceChar(KeyInputUtil.VimKeyToKeyInput(VimKey.LowerB)), count: 2);
            _mode.Process("2rb");
        }

        [Test]
        public void Bind_DeleteCharacterAtCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterAtCaret);
            _mode.Process("x");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteCharacterAtCaret_WithCountAndRegister()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterAtCaret, 2, RegisterName.OfChar('c').Value);
            _mode.Process("\"c2x");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteCharacterAtCaret_ViaDelete()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterAtCaret);
            _mode.Process(VimKey.Delete);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeMotion()
        {
            Create("the dog chases the ball");
            _commandUtil.SetupCommandMotion<NormalCommand.ChangeMotion>();
            _mode.Process("cw");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.ChangeLines);
            _mode.Process("cc");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeLines_ViaS()
        {
            Create("foo", "bar", "baz");
            _commandUtil.SetupCommandNormal(NormalCommand.ChangeLines);
            _mode.Process("S");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeTillEndOfLine()
        {
            Create("foo", "bar", "baz");
            _commandUtil.SetupCommandNormal(NormalCommand.ChangeTillEndOfLine);
            _mode.Process("C");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_SubstituteCharacterAtCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.SubstituteCharacterAtCaret);
            _mode.Process("s");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCaseCaretPoint_Tilde()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretPoint(ChangeCharacterKind.ToggleCase));
            _mode.Process("~");
            _commandUtil.Verify();
        }

        /// <summary>
        /// When tildeop is set this becomes a motion command
        /// </summary>
        [Test]
        public void Bind_TildeMotion()
        {
            Create("foo");
            _globalSettings.TildeOp = true;
            _commandUtil.SetupCommandMotion<NormalCommand.ChangeCaseMotion>();
            _mode.Process("~w");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCaseLine_Upper1()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase));
            _mode.Process("gUgU");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCaseLine_Upper2()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase));
            _mode.Process("gUU");
            _commandUtil.Verify();
        }


        [Test]
        public void Bind_ChangeCaseLine_Lower1()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToLowerCase));
            _mode.Process("gugu");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCaseLine_Lower2()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToLowerCase));
            _mode.Process("guu");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCaseLine_Rot13_1()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.Rot13));
            _mode.Process("g?g?");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ChangeCaseLine_Rot13_2()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.Rot13));
            _mode.Process("g??");
            _commandUtil.Verify();
        }

        #endregion

        #region Yank

        [Test]
        public void Bind_Yank()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.Yank>();
            _mode.Process("yw");
            _commandUtil.Verify();
        }

        [Test, Description("A yy should grab the end of line including line break information")]
        public void Yank_yy_1()
        {
            Create("foo", "bar");
            var span = _textView.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("yy");
            _operations.Verify();
        }

        [Test, Description("yy should yank the entire line even if the cursor is not at the start")]
        public void Yank_yy_2()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            var span = _textView.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("yy");
            _operations.Verify();
        }

        [Test]
        public void Yank_Y_1()
        {
            Create("foo", "bar");
            var span = _textView.GetLineRange(0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("Y");
            _operations.Verify();
        }

        [Test]
        public void Yank_Y_2()
        {
            Create("foo", "bar");
            var span = _textView.GetLineRange(0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('c'), RegisterOperation.Yank, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("\"cY");
            _operations.Verify();
        }

        [Test]
        public void Yank_Y_3()
        {
            Create("foo", "bar", "jazz");
            var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("2Y");
            _operations.Verify();
        }

        #endregion

        #region Paste

        [Test]
        public void Bind_PutAfterCaret()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutAfterCaret(false));
            _mode.Process("p");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutBeforeCaret()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutBeforeCaret(false));
            _mode.Process("P");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutAfterCaret_WithMove()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutAfterCaret(true));
            _mode.Process("gp");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutBeforeCaret_WithMove()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutBeforeCaret(true));
            _mode.Process("gP");
            _commandUtil.Verify();
        }

        #endregion

        #region Delete

        [Test]
        public void Bind_DeleteLines()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteLines);
            _mode.Process("dd");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteMotion()
        {
            Create("hello world");
            _commandUtil.SetupCommandMotion<NormalCommand.DeleteMotion>();
            _mode.Process("dw");
            _commandUtil.Verify();
        }

        /// <summary>
        /// Make sure that escape will cause the CommandRunner to exit when waiting
        /// for a Motion to complete
        /// </summary>
        [Test]
        public void Process_EscapeShouldExitMotion()
        {
            Create(DefaultLines);
            _mode.Process('d');
            Assert.IsTrue(_mode.CommandRunner.IsWaitingForMoreInput);
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsFalse(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [Test]
        public void Bind_DeleteTillEndOfLine()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteTillEndOfLine);
            _mode.Process("D");
            _commandUtil.Verify();
        }

        #endregion

        #region Incremental Search

        /// <summary>
        /// Make sure the incremental search begins when the '/' is typed
        /// </summary>
        [Test]
        public void IncrementalSearch_BeginOnForwardSearchChar()
        {
            Create("foo bar");
            _incrementalSearch
                .Setup(x => x.Begin(SearchKind.ForwardWithWrap))
                .Returns(VimUtil.CreateBindData<SearchResult>())
                .Verifiable();
            _mode.Process('/');
            _incrementalSearch.Verify();
        }

        /// <summary>
        /// Make sure the incremental search beigns when the '?' is typed
        /// </summary>
        [Test]
        public void IncrementalSearch_BeginOnBackwardSearchChar()
        {
            Create("foo bar");
            _incrementalSearch
                .Setup(x => x.Begin(SearchKind.BackwardWithWrap))
                .Returns(VimUtil.CreateBindData<SearchResult>())
                .Verifiable();
            _mode.Process('?');
            _incrementalSearch.Verify();
        }

        /// <summary>
        /// Once incremental search begins, make sure it handles any keystroke
        /// </summary>
        [Test]
        public void IncrementalSearch_HandlesAnyKey()
        {
            Create("foo bar");
            _incrementalSearch
                .Setup(x => x.Begin(SearchKind.ForwardWithWrap))
                .Returns(VimUtil.CreateBindData<SearchResult>())
                .Verifiable();
            _mode.Process('/');
            var ki = KeyInputUtil.CharToKeyInput((char)7);
            _mode.Process(ki);
            _incrementalSearch.Verify();
        }

        #endregion

        #region Next / Previous Word

        [Test]
        public void Bind_MoveCaretToNextWord_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToNextWord(Path.Forward));
            _mode.Process("*");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretToNextWord_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToNextWord(Path.Backward));
            _mode.Process("#");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretToNextPartialWord_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToNextPartialWord(Path.Forward));
            _mode.Process("g*");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretToNextPartialWord_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToNextPartialWord(Path.Backward));
            _mode.Process("g#");
            _commandUtil.Verify();
        }

        #endregion

        [Test]
        public void Bind_MoveCaretToLastSearch_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToLastSearch(false));
            _mode.Process("n");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_MoveCaretToLastSearch_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToLastSearch(true));
            _mode.Process("N");
            _commandUtil.Verify();
        }

        #region Shift

        [Test]
        public void Bind_ShiftRight()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.ShiftLinesRight);
            _mode.Process(">>");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ShiftMotionRight()
        {
            Create("foo", "bar");
            /// REPEAT TODO: Add tests for this
        }

        [Test]
        public void Bind_ShiftLeft()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.ShiftLinesLeft);
            _mode.Process("<<");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ShiftMotionLeft()
        {
            /// REPEAT TODO: Add tests for this
            Create("foo");
        }

        #endregion

        #region Misc

        [Test]
        public void Undo1()
        {
            Create("foo");
            _operations.Setup(x => x.Undo(1)).Verifiable();
            _mode.Process("u");
            _operations.Verify();
        }

        [Test]
        public void Undo2()
        {
            Create("foo");
            _operations.Setup(x => x.Undo(2)).Verifiable();
            _mode.Process("2u");
            _operations.Verify();
        }

        [Test]
        public void Redo1()
        {
            Create("foo");
            _operations.Setup(x => x.Redo(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('r'));
            _operations.Verify();
        }

        [Test]
        public void Redo2()
        {
            Create("bar");
            _operations.Setup(x => x.Redo(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('r'));
            _operations.Verify();
        }

        [Test]
        public void Bind_JoinLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJoinLines(JoinKind.RemoveEmptySpaces));
            _mode.Process("J");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_JoinLines_KeepEmptySpaces()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJoinLines(JoinKind.KeepEmptySpaces));
            _mode.Process("gJ");
            _commandUtil.Verify();
        }

        [Test]
        public void GoToDefinition1()
        {
            var def = KeyInputUtil.CharWithControlToKeyInput(']');
            Create("foo");
            _operations.Setup(x => x.GoToDefinitionWrapper()).Verifiable();
            _mode.Process(def);
            _operations.Verify();
        }

        [Test]
        public void GoToDefinition2()
        {
            Create(DefaultLines);
            var def = KeyInputUtil.CharWithControlToKeyInput(']');
            var name = KeyInputSet.NewOneKeyInput(def);
            Assert.IsTrue(_mode.CanProcess(def));
            Assert.IsTrue(_mode.CommandNames.Contains(name));
        }

        [Test]
        public void Bind_GoToLocalDeclaration()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.GoToLocalDeclaration);
            _mode.Process("gd");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToGlobalDeclaration()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.GoToGlobalDeclaration);
            _mode.Process("gD");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToFileUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToFileUnderCaret(false));
            _mode.Process("gf");
            _commandUtil.Verify();
        }

        [Test]
        public void SetMark_CanProcessM()
        {
            Create("");
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('m')));
            Assert.IsTrue(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == 'm'));
        }

        /// <summary>
        /// Inside mark mode we can process anything
        /// </summary>
        [Test, Description("Once we are in mark mode we can process anything")]
        public void SetMark_CanProcessAnything()
        {
            Create("");
            _mode.Process(KeyInputUtil.CharToKeyInput('m'));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharWithControlToKeyInput('c')));
        }

        [Test]
        public void SetMark_Simple()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSetMarkToCaret('a'));
            _mode.Process("ma");
            _commandUtil.Verify();
        }

        [Test]
        public void JumpToMark_SingleQuote()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJumpToMark('a'));
            _mode.Process("'a");
        }

        [Test]
        public void JumpToMark_BackTick()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJumpToMark('a'));
            _mode.Process("`a");
        }

        [Test]
        public void JumpNext1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.JumpNext(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            _operations.Verify();
        }

        [Test]
        public void JumpNext2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.JumpNext(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            _operations.Verify();
        }

        [Test]
        public void JumpNext3()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.JumpNext(1)).Verifiable();
            _mode.Process(KeyInputUtil.TabKey);
            _operations.Verify();
        }

        [Test]
        public void JumpPrevious1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.JumpPrevious(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _operations.Verify();
        }

        [Test]
        public void JumpPrevious2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.JumpPrevious(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _operations.Verify();
        }

        [Test]
        public void Bind_InsertAtEndOfLine()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertAtEndOfLine);
            _mode.Process('A');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_InsertAfterCaret()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertAfterCaret);
            _mode.Process('a');
            _commandUtil.Verify();
        }

        [Test]
        public void KeyRemapMode_DefaultIsNormal()
        {
            Create("foo bar");
            Assert.AreEqual(KeyRemapMode.Normal, _mode.KeyRemapMode);
        }

        [Test]
        public void KeyRemapMode_CommandInIncrementalSearch()
        {
            Create("foobar");
            _incrementalSearch
                .Setup(x => x.Begin(SearchKind.ForwardWithWrap))
                .Returns(VimUtil.CreateBindData<SearchResult>(remapMode: KeyRemapMode.Command));
            _mode.Process('/');
            Assert.AreEqual(KeyRemapMode.Command, _mode.KeyRemapMode);
        }

        [Test]
        public void KeyRemapMode_OperatorPendingAfterY()
        {
            Create("");
            _mode.Process('y');
            Assert.AreEqual(KeyRemapMode.OperatorPending, _mode.KeyRemapMode);
        }

        [Test]
        public void KeyRemapMode_OperatorPendingAfterD()
        {
            Create("");
            _mode.Process('d');
            Assert.AreEqual(KeyRemapMode.OperatorPending, _mode.KeyRemapMode);
        }

        [Test]
        public void KeyRemapMode_LanguageAfterF()
        {
            Create("");
            _mode.Process("df");
            Assert.AreEqual(KeyRemapMode.Language, _mode.KeyRemapMode);
        }

        [Test]
        public void IsWaitingForInput1()
        {
            Create("foobar");
            Assert.IsFalse(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForInput2()
        {
            Create("foobar");
            _incrementalSearch
                .Setup(x => x.Begin(SearchKind.ForwardWithWrap))
                .Returns(VimUtil.CreateBindData<SearchResult>());
            _mode.Process('/');
            Assert.IsTrue(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForInput3()
        {
            Create("");
            _mode.Process('y');
            Assert.IsTrue(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [Test]
        public void Command1()
        {
            Create("foo");
            _mode.Process("\"a");
            Assert.AreEqual("\"a", _modeRaw.Command);
        }

        [Test]
        public void Command2()
        {
            Create("bar");
            _mode.Process("\"f");
            Assert.AreEqual("\"f", _modeRaw.Command);
        }

        [Test]
        public void Command4()
        {
            Create(DefaultLines);
            _mode.Process('2');
            Assert.AreEqual("2", _mode.Command);
        }

        [Test]
        public void Command5()
        {
            Create(DefaultLines);
            _mode.Process("2d");
            Assert.AreEqual("2d", _mode.Command);
        }

        [Test]
        public void Commands1()
        {
            Create("foo");
            var found = _modeRaw.Commands.Single(x => x.KeyInputSet.Equals(KeyNotationUtil.StringToKeyInputSet("D")));
            Assert.AreEqual(CommandFlags.Repeatable, found.CommandFlags);
        }

        [Test]
        public void Commands2()
        {
            Create("foo");
            var found = _modeRaw.Commands.Single(x => x.KeyInputSet.Equals(KeyNotationUtil.StringToKeyInputSet("h")));
            Assert.AreNotEqual(CommandFlags.Repeatable, found.CommandFlags, "Movements should not be repeatable");
        }

        [Test]
        public void Commands3()
        {
            Create("foo", "bar", "baz");
            var found = _modeRaw.Commands.Single(x => x.KeyInputSet.Equals(KeyNotationUtil.StringToKeyInputSet("dd")));
            Assert.AreEqual(CommandFlags.Repeatable, found.CommandFlags);
        }

        /// <summary>
        /// Sanity check to ensure certain commands are not in fact repeatable
        /// </summary>
        [Test]
        public void VerifyCommandsNotRepeatable()
        {
            Create(String.Empty);
            Action<string> verify = str =>
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet(str);
                var command = _modeRaw.Commands.Where(x => x.KeyInputSet == keyInputSet).Single();
                Assert.IsTrue(CommandFlags.None == (command.CommandFlags & CommandFlags.Repeatable));
            };

            verify("n");
            verify("N");
            verify("*");
            verify("#");
            verify("h");
            verify("j");
            verify("k");
            verify("l");
            verify("<C-u>");
            verify("<C-d>");
            verify("<C-r>");
            verify("<C-y>");
            verify("<C-f>");
            verify("<S-Down>");
            verify("<PageDown>");
            verify("<PageUp>");
            verify("<C-b>");
            verify("<S-Up>");
            verify("<Tab>");
            verify("<C-i>");
            verify("<C-o>");
            verify("%");
            verify("<C-PageDown>");
            verify("<C-PageUp>");
        }

        [Test]
        public void Escape1()
        {
            Create(string.Empty);
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsHandled);
        }

        [Test]
        public void OneTimeCommand1()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Left));
            _mode.OnEnter(ModeArgument.NewOneTimeCommand(ModeKind.Insert));
            var res = _mode.Process("h");
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Insert));
        }

        [Test]
        public void OneTimeCommand2()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretTo(Direction.Left));
            _mode.OnEnter(ModeArgument.NewOneTimeCommand(ModeKind.Command));
            var res = _mode.Process("h");
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Command));
        }

        [Test]
        public void Bind_ReplaceAtCaret()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.ReplaceAtCaret);
            _mode.Process("R");
            _commandUtil.Verify();
        }

        [Test]
        [Description("Make sure it doesn't pass the flag")]
        public void Substitute1()
        {
            Create("foo bar");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.Confirm));
            _operations.Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None));
            _mode.Process("&");
        }

        [Test]
        [Description("No last substitute should do nothing")]
        public void Substitute2()
        {
            Create("foo bar");
            _mode.Process("&");
        }

        [Test]
        [Description("Flags are kept on full buffer substitute")]
        public void Substitute3()
        {
            Create("foo bar", "baz");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.Confirm));
            _operations.Setup(x => x.Substitute("a", "b", SnapshotLineRangeUtil.CreateForSnapshot(_textView.TextSnapshot), SubstituteFlags.Confirm));
            _mode.Process("g&");
        }

        [Test]
        public void Handle_ZZ()
        {
            Create("foo bar");
            _operations.Setup(x => x.Close(true)).Verifiable();
            _mode.Process("ZZ");
            _operations.Verify();
        }

        #endregion

        #region Visual Mode

        [Test]
        public void VisualMode1()
        {
            Create(DefaultLines);
            var res = _mode.Process('v');
            Assert.IsTrue(res.IsSwitchMode(ModeKind.VisualCharacter));
        }

        [Test]
        public void VisualMode2()
        {
            Create(DefaultLines);
            var res = _mode.Process('V');
            Assert.IsTrue(res.IsSwitchMode(ModeKind.VisualLine));
        }

        [Test]
        public void VisualMode3()
        {
            Create(DefaultLines);
            var res = _mode.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            Assert.IsTrue(res.IsSwitchMode(ModeKind.VisualBlock));
        }

        [Test]
        public void Bind_I()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.InsertAtFirstNonBlank);
            _mode.Process('I');
            _commandUtil.Verify();
        }

        [Test]
        public void gt_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToNextTab(Path.Forward, 1)).Verifiable();
            _mode.Process("gt");
            _operations.Verify();
        }

        [Test]
        public void gt_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToTab(2)).Verifiable();
            _mode.Process("2gt");
            _operations.Verify();
        }

        [Test]
        public void CPageDown_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToNextTab(Path.Forward, 1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageDown, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void CPageDown_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToTab(2)).Verifiable();
            _mode.Process("2");
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageDown, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void gT_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 1)).Verifiable();
            _mode.Process("gT");
            _operations.Verify();
        }

        [Test]
        public void gT_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 2)).Verifiable();
            _mode.Process("2gT");
            _operations.Verify();
        }

        [Test]
        public void CPageUp_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageUp, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void CPageUp_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageUp, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void Bind_FormatLines()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.FormatLines);
            _mode.Process("==");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_FormatMotion()
        {
            Create("the dog chased the ball");
            _commandUtil.SetupCommandMotion<NormalCommand.FormatMotion>();
            _mode.Process("=w");
            _commandUtil.Verify();
        }

        #endregion

        #region Folding

        [Test]
        public void Fold_zo()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.OpenFold(_textView.GetCaretLine().Extent, 1)).Verifiable();
            _mode.Process("zo");
            _operations.Verify();
        }

        [Test]
        public void Fold_zo_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.OpenFold(_textView.GetCaretLine().Extent, 3)).Verifiable();
            _mode.Process("3zo");
            _operations.Verify();
        }

        [Test]
        public void Fold_zc_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.CloseFold(_textView.GetCaretLine().Extent, 1)).Verifiable();
            _mode.Process("zc");
            _operations.Verify();
        }

        [Test]
        public void Fold_zc_2()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.CloseFold(_textView.GetCaretLine().Extent, 3)).Verifiable();
            _mode.Process("3zc");
            _operations.Verify();
        }

        [Test]
        public void Fold_zO_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.OpenAllFolds(_textView.GetCaretLine().Extent)).Verifiable();
            _mode.Process("zO");
            _operations.Verify();
        }

        [Test]
        public void Fold_zC_1()
        {
            Create(DefaultLines);
            _operations.Setup(x => x.CloseAllFolds(_textView.GetCaretLine().Extent)).Verifiable();
            _mode.Process("zC");
            _operations.Verify();
        }

        [Test]
        public void Bind_FoldMotion()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.FoldMotion>();
            _mode.Process("zfw");
            _commandUtil.Verify();
        }

        [Test]
        public void Fold_zF_1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _operations.Setup(x => x.FoldLines(1)).Verifiable();
            _mode.Process("zF");
            _operations.Verify();
        }

        [Test]
        public void Fold_zF_2()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _operations.Setup(x => x.FoldLines(2)).Verifiable();
            _mode.Process("2zF");
            _operations.Verify();
        }

        [Test]
        public void Fold_zd_1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _operations.Setup(x => x.DeleteOneFoldAtCursor()).Verifiable();
            _mode.Process("zd");
            _operations.Verify();
        }

        [Test]
        public void Fold_zD_1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _operations.Setup(x => x.DeleteAllFoldsAtCursor()).Verifiable();
            _mode.Process("zD");
            _operations.Verify();
        }

        [Test]
        public void Fold_zE_1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _foldManager.Setup(x => x.DeleteAllFolds()).Verifiable();
            _mode.Process("zE");
            _foldManager.Verify();
        }

        #endregion

        #region Split View

        [Test]
        public void Bind_GoToView_Down()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToView(Direction.Down));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-j>"));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToView_Right()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToView(Direction.Right));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-l>"));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToView_Left()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToView(Direction.Left));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-h>"));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToView_Up()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToView(Direction.Up));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-k>"));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToView_Up2()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToView(Direction.Up));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("k"));
            _commandUtil.Verify();
        }

        #endregion
    }
}
