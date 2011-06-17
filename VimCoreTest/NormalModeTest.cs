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
        private Mock<ICommonOperations> _operations;
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

        public void Create(IMotionUtil motionUtil, params string[] lines)
        {
            CreateCore(motionUtil, lines);
        }

        public void CreateCore(IMotionUtil motionUtil, params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
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
            _displayWindowBroker = _factory.Create<IDisplayWindowBroker>(MockBehavior.Strict);
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSignatureHelpActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(false);
            _vimData = new VimData();

            _globalSettings = new Vim.GlobalSettings();
            _localSettings = new LocalSettings(_globalSettings, EditorUtil.GetEditorOptions(_textView), _textView);
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
            _operations = _factory.Create<ICommonOperations>(MockBehavior.Strict);
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);
            _operations.SetupGet(x => x.TextView).Returns(_textView);

            var capture = new MotionCapture(
                _host.Object,
                _textView,
                _incrementalSearch.Object,
                new LocalSettings(new GlobalSettings(), EditorUtil.GetEditorOptions(_textView), _textView));
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
                .Setup(x => x.Begin(Path.Forward))
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
        public void Bind_Motion_l()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('l');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_h()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('h');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_BackSpace()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Back);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_k()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('k');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_j()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('j');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Left()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Left);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Right()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Right);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Up()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Up);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_Down()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Down);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_CtrlP()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('p'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_CtrlN()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('n'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_CtrlH()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('h'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_SpaceBar()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
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
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.FirstNonBlankOnCurrentLine));
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
        public void Bind_Motion_LineOrFirst()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.LineOrFirstToFirstNonBlank));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-Home>"));
            _commandUtil.Verify();
        }

        #endregion

        #region Scroll

        [Test]
        public void Bind_ScrollLines_Up_WithOption()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollLines(ScrollDirection.Up, true));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('u'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollLines_Down_WithOption()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollLines(ScrollDirection.Down, true));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollLines_Down()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollLines(ScrollDirection.Down, false));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollLines_Up()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollLines(ScrollDirection.Up, false));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('y'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Down()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('f'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Down_ViaShiftDown()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Down, KeyModifiers.Shift));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Down_ViaPageDown()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Up()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('b'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Up_ViaPageUp()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(VimKey.PageUp);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollPages_Up_ViaShiftUp()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Up, KeyModifiers.Shift));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollCaretLineToTop_KeepCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToTop(true));
            _mode.Process("zt");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollCaretLineToTop()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToTop(false));
            _mode.Process("z");
            _mode.Process(KeyInputUtil.EnterKey);
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollCaretLineToMiddle()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToMiddle(false));
            _mode.Process("z.");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollCaretLineToMiddle_KeepCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToMiddle(true));
            _mode.Process("zz");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollCaretLineToBottom()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToBottom(false));
            _mode.Process("z-");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_ScrollCaretLineToBottom_KeepCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToBottom(true));
            _mode.Process("zb");
            _commandUtil.Verify();
        }

        #endregion

        #region Motion

        [Test]
        public void Motion_Motion_Right()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process("l");
            _commandUtil.Verify();
        }

        /// <summary>
        /// Need to make sure that G is not being given a count when used as a motion
        /// </summary>
        [Test]
        public void Motion_G()
        {
            var util = new Mock<IMotionUtil>(MockBehavior.Strict);
            Create(util.Object, "hello world");
            var span = _textView.GetLine(0).Extent;
            var arg = new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, FSharpOption<int>.None);
            util
                .Setup(x => x.GetMotion(Motion.LineOrLastToFirstNonBlank, arg))
                .Returns(FSharpOption.Create(VimUtil.CreateMotionResult(span, motionKind: MotionKind.NewLineWise(CaretColumn.None))));
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

        [Test]
        public void Bind_YankLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.YankLines);
            _mode.Process("yy");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_YankLines_ViaY()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.YankLines);
            _mode.Process("Y");
            _commandUtil.Verify();
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
        public void Bind_PutAfterCaretWithIndent()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.PutAfterCaretWithIndent);
            _mode.Process("]p");
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
        public void Bind_PutBeforeCaretWithIndent()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.PutBeforeCaretWithIndent);
            _mode.Process("[p");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_PutBeforeCaretWithIndent_ViaCapitalP()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.PutBeforeCaretWithIndent);
            _mode.Process("[P");
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
                .Setup(x => x.Begin(Path.Forward))
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
                .Setup(x => x.Begin(Path.Backward))
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
                .Setup(x => x.Begin(Path.Forward))
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
        public void Bind_Motion_NextWord_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextWord(Path.Forward)));
            _mode.Process("*");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_NextWord_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextWord(Path.Backward)));
            _mode.Process("#");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_NextPartialWord_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextPartialWord(Path.Forward)));
            _mode.Process("g*");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_NextPartialWord_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextPartialWord(Path.Backward)));
            _mode.Process("g#");
            _commandUtil.Verify();
        }

        #endregion

        [Test]
        public void Bind_Motion_LastSearch_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(false)));
            _mode.Process("n");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Motion_LastSearch_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(true)));
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
        public void Bind_Undo()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.Undo);
            _mode.Process("u");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_Redo()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.Redo);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('r'));
            _commandUtil.Verify();
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
        public void Bind_GoToDefinition()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.GoToDefinition);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput(']'));
            _commandUtil.Verify();
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
        public void Bind_SetMark()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSetMarkToCaret('a'));
            _mode.Process("ma");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_JumpToMark()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJumpToMark('a'));
            _mode.Process("'a");
        }

        [Test]
        public void Bind_JumpToMark_BackTick()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJumpToMark('a'));
            _mode.Process("`a");
        }

        [Test]
        public void Bind_JumpToNewerPosition()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.JumpToNewerPosition);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_JumpToOlderPosition()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.JumpToOlderPosition);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _commandUtil.Verify();
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
                .Setup(x => x.Begin(Path.Forward))
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
                .Setup(x => x.Begin(Path.Forward))
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
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.OnEnter(ModeArgument.NewOneTimeCommand(ModeKind.Insert));
            var res = _mode.Process("h");
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Insert));
        }

        [Test]
        public void OneTimeCommand2()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
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
        public void Bind_RepeatLastSubstitute_WithNoFlags()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewRepeatLastSubstitute(false));
            _mode.Process("&");
            _commandUtil.Verify();
        }


        [Test]
        public void Bind_RepeatLastSubstitute_WithFlags()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewRepeatLastSubstitute(true));
            _mode.Process("g&");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_WriteBufferAndQuit()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.WriteBufferAndQuit);
            _mode.Process("ZZ");
            _commandUtil.Verify();
        }

        #endregion

        #region Visual Mode

        [Test]
        public void Bind_SwitchMode_VisualCharacter()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchMode(ModeKind.VisualCharacter, ModeArgument.None));
            _mode.Process('v');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_SwitchMode_VisualLine()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchMode(ModeKind.VisualLine, ModeArgument.None));
            _mode.Process('V');
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_SwitchMode_VisualBlock()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchMode(ModeKind.VisualBlock, ModeArgument.None));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            _commandUtil.Verify();
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
        public void Bind_GoToNextTab_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(Path.Forward));
            _mode.Process("gt");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToNextTab_ForwardViaPageDown()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(Path.Forward));
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageDown, KeyModifiers.Control));
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToNextTab_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(Path.Backward));
            _mode.Process("gT");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_GoToNextTab_BackwardViaPageUp()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(Path.Backward));
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageUp, KeyModifiers.Control));
            _commandUtil.Verify();
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
        public void Bind_OpenFoldUnderCaret()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.OpenFoldUnderCaret);
            _mode.Process("zo");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_CloseFoldUnderCaret()
        {
            Create(DefaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.CloseFoldUnderCaret);
            _mode.Process("zc");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_OpenAllFoldsUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.OpenAllFoldsUnderCaret);
            _mode.Process("zO");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_CloseAllFoldsUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.CloseAllFoldsUnderCaret);
            _mode.Process("zC");
            _commandUtil.Verify();
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
        public void Bind_FoldLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.FoldLines);
            _mode.Process("zF");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteFoldUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteFoldUnderCaret);
            _mode.Process("zd");
            _commandUtil.Verify();
        }

        [Test]
        public void Bind_DeleteAllFoldsUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteAllFoldsUnderCaret);
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
