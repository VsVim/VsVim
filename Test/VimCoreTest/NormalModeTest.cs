using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.Modes.Normal;
using Vim.UnitTest.Mock;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public sealed class NormalModeTest : VimTestBase
    {
        private NormalMode _modeRaw;
        private INormalMode _mode;
        private ITextView _textView;
        private IVimGlobalSettings _globalSettings;
        private MockRepository _factory;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private Mock<ICommandUtil> _commandUtil;
        private Register _unnamedRegister;

        private static readonly string[] s_defaultLines = new[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        internal void Create(params string[] lines)
        {
            CreateCore(null, lines);
        }

        internal void Create(IMotionUtil motionUtil, params string[] lines)
        {
            CreateCore(motionUtil, lines);
        }

        internal void CreateCore(IMotionUtil motionUtil, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _unnamedRegister = Vim.RegisterMap.GetRegister(RegisterName.Unnamed);
            _factory = new MockRepository(MockBehavior.Strict);
            _incrementalSearch = MockObjectFactory.CreateIncrementalSearch(factory: _factory);
            _commandUtil = _factory.Create<ICommandUtil>();

            _globalSettings = Vim.GlobalSettings;

            var vimTextBuffer = Vim.CreateVimTextBuffer(_textView.TextBuffer);
            var vimBufferData = CreateVimBufferData(vimTextBuffer, _textView);
            var operations = CommonOperationsFactory.GetCommonOperations(vimBufferData);
            motionUtil = motionUtil ?? new MotionUtil(vimBufferData, operations);
            var lineChangeTracker = new LineChangeTracker(vimBufferData);
            var incrementalSearch = new IncrementalSearch(vimBufferData, operations);

            var capture = new MotionCapture(vimBufferData, _incrementalSearch.Object);
            var runner = new CommandRunner(
                vimBufferData,
                capture,
                _commandUtil.Object,
                VisualKind.Character,
                KeyRemapMode.Normal);
            _modeRaw = new NormalMode(
                vimBufferData,
                operations,
                motionUtil,
                runner,
                capture,
                incrementalSearch);
            _mode = _modeRaw;
            _mode.OnEnter(ModeArgument.None);
        }

        [WpfFact]
        public void ModeKindTest()
        {
            Create(s_defaultLines);
            Assert.Equal(ModeKind.Normal, _mode.ModeKind);
        }

        /// <summary>
        /// Let enter go straight back to the editor in the default case
        /// </summary>
        [WpfFact]
        public void EnterProcessing()
        {
            Create(s_defaultLines);
            var can = _mode.CanProcess(KeyInputUtil.EnterKey);
            Assert.True(can);
        }

        #region CanProcess

        /// <summary>
        /// Can process basic commands
        /// </summary>
        [WpfFact]
        public void CanProcess1()
        {
            Create(s_defaultLines);
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('u')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('h')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('j')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('i')));
        }

        /// <summary>
        /// Can process even invalid commands else they end up as input
        /// </summary>
        [WpfFact]
        public void CanProcess2()
        {
            Create(s_defaultLines);
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('U')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('Z')));
        }

        /// <summary>
        /// Must be able to process numbers
        /// </summary>
        [WpfFact]
        public void CanProcess3()
        {
            Create(s_defaultLines);
            foreach (var cur in Enumerable.Range(1, 8))
            {
                var c = char.Parse(cur.ToString());
                var ki = KeyInputUtil.CharToKeyInput(c);
                Assert.True(_mode.CanProcess(ki));
            }
        }

        /// <summary>
        /// Ensure that all of the core characters are valid Normal Mode commands.  They all should
        /// be 
        /// </summary>
        [WpfFact]
        public void CanProcess_AllCoreCharacters()
        {
            Create(s_defaultLines);
            foreach (var cur in KeyInputUtilTest.CharAll)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(cur);
                Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput(cur)));
            }
        }

        [WpfFact]
        public void CanProcess_MovementKeys()
        {
            Create(s_defaultLines);
            Assert.True(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.True(_mode.CanProcess(KeyInputUtil.TabKey));
        }

        [WpfFact]
        public void CanProcess_DontHandleControlTab()
        {
            Create("");
            Assert.False(_mode.CanProcess(KeyInputUtil.ChangeKeyModifiersDangerous(KeyInputUtil.TabKey, VimKeyModifiers.Control)));
        }

        /// <summary>
        /// Must be able to process non-ASCII punctuation otherwise
        /// they will end up as input
        /// </summary>
        [WpfFact]
        public void CanProcessPrintableNonAscii()
        {
            // Reported in issue #1793.
            Create(s_defaultLines);
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('¤')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('¨')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('£')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('§')));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('´')));
        }

        #endregion

        #region Movement

        [WpfFact]
        public void Bind_Motion_l()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('l');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_h()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('h');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_BackSpace()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Back);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_k()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('k');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_j()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process('j');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Left()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Left);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Right()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Right);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Up()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Up);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Down()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(VimKey.Down);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_CtrlP()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('p'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_CtrlN()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('n'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_CtrlH()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('h'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_SpaceBar()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.MoveCaretToMotion>();
            _mode.Process(' ');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Word()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewWordForward(WordKind.NormalWord)));
            _mode.Process('w');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_BigWord()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewWordForward(WordKind.BigWord)));
            _mode.Process('W');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_WordBackward()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewWordBackward(WordKind.NormalWord)));
            _mode.Process('b');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Hat()
        {
            Create("   foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.FirstNonBlankOnCurrentLine));
            _mode.Process('^');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_Dollar()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.EndOfLine));
            _mode.Process('$');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_0()
        {
            Create("foo bar baz");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.BeginingOfLine));
            _mode.Process('0');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_LineOrFirst()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.LineOrFirstToFirstNonBlank));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-Home>"));
            _commandUtil.Verify();
        }

        #endregion

        #region Scroll

        [WpfFact]
        public void Bind_ScrollLines_Up_WithOption()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollLines(ScrollDirection.Up, true));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('u'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollLines_Down_WithOption()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollLines(ScrollDirection.Down, true));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollLines_Down()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollWindow(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollLines_Up()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollWindow(ScrollDirection.Up));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('y'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollPages_Down()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('f'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollPages_Down_ViaShiftDown()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Down, VimKeyModifiers.Shift));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollPages_Down_ViaPageDown()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Down));
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollPages_Up()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('b'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollPages_Up_ViaPageUp()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(VimKey.PageUp);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollPages_Up_ViaShiftUp()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollPages(ScrollDirection.Up));
            _mode.Process(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Up, VimKeyModifiers.Shift));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollCaretLineToTop_KeepCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToTop(true));
            _mode.Process("zt");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollCaretLineToTop()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToTop(false));
            _mode.Process("z");
            _mode.Process(KeyInputUtil.EnterKey);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollCaretLineToMiddle()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToMiddle(false));
            _mode.Process("z.");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollCaretLineToMiddle_KeepCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToMiddle(true));
            _mode.Process("zz");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollCaretLineToBottom()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToBottom(false));
            _mode.Process("z-");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ScrollCaretLineToBottom_KeepCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewScrollCaretLineToBottom(true));
            _mode.Process("zb");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_SelectNextMatch_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSelectNextMatch(SearchPath.Forward));
            _mode.Process("gn");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_SelectNextMatch_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSelectNextMatch(SearchPath.Backward));
            _mode.Process("gN");
            _commandUtil.Verify();
        }

        #endregion

        #region Motion

        [WpfFact]
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
        [WpfFact]
        public void Motion_G()
        {
            var util = new Mock<IMotionUtil>(MockBehavior.Strict);
            Create(util.Object, "hello world");
            var span = _textView.GetLine(0).Extent;
            var arg = new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, FSharpOption<int>.None);
            util
                .Setup(x => x.GetMotion(Motion.LineOrLastToFirstNonBlank, arg))
                .Returns(FSharpOption.Create(VimUtil.CreateMotionResult(span, motionKind: MotionKind.LineWise)));
            _commandUtil
                .Setup(x => x.RunCommand(It.Is<Command>(y => y.AsNormalCommand().CommandData.Count.IsNone())))
                .Returns(CommandResult.NewCompleted(ModeSwitch.NoSwitch))
                .Verifiable();
            _mode.Process("yG");
            util.Verify();
            _commandUtil.Verify();
        }

        #endregion

        #region Edits

        [WpfFact]
        public void Bind_InsertLineBelow()
        {
            Create("how is", "foo");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertLineBelow);
            _mode.Process('o');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_InsertLineAbove()
        {
            Create("how is", "foo");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertLineAbove);
            _mode.Process('O');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteCharacterBeforeCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterBeforeCaret);
            _mode.Process("X");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteCharacterBeforeCaret_WithCountAndRegister()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterBeforeCaret, 2, RegisterName.OfChar('c').Value);
            _mode.Process("\"c2X");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ReplaceChar_Simple()
        {
            Create("the dog chased the cat");
            _commandUtil.SetupCommandNormal(NormalCommand.NewReplaceChar(KeyInputUtil.CharToKeyInput('b')));
            _mode.Process("rb");
        }

        [WpfFact]
        public void Bind_ReplaceChar_WithCount()
        {
            Create("the dog chased the cat");
            _commandUtil.SetupCommandNormal(NormalCommand.NewReplaceChar(KeyInputUtil.CharToKeyInput('b')), count: 2);
            _mode.Process("2rb");
        }

        [WpfFact]
        public void Bind_DeleteCharacterAtCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterAtCaret);
            _mode.Process("x");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteCharacterAtCaret_WithCountAndRegister()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterAtCaret, 2, RegisterName.OfChar('c').Value);
            _mode.Process("\"c2x");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteCharacterAtCaret_ViaDelete()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteCharacterAtCaret);
            _mode.Process(VimKey.Delete);
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeMotion()
        {
            Create("the dog chases the ball");
            _commandUtil.SetupCommandMotion<NormalCommand.ChangeMotion>();
            _mode.Process("cw");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.ChangeLines);
            _mode.Process("cc");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeLines_ViaS()
        {
            Create("foo", "bar", "baz");
            _commandUtil.SetupCommandNormal(NormalCommand.ChangeLines);
            _mode.Process("S");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeTillEndOfLine()
        {
            Create("foo", "bar", "baz");
            _commandUtil.SetupCommandNormal(NormalCommand.ChangeTillEndOfLine);
            _mode.Process("C");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_SubstituteCharacterAtCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.SubstituteCharacterAtCaret);
            _mode.Process("s");
            _commandUtil.Verify();
        }

        [WpfFact]
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
        [WpfFact]
        public void Bind_TildeMotion()
        {
            Create("foo");
            _globalSettings.TildeOp = true;
            _commandUtil.SetupCommandMotion<NormalCommand.ChangeCaseMotion>();
            _mode.Process("~w");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeCaseLine_Upper1()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase));
            _mode.Process("gUgU");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeCaseLine_Upper2()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase));
            _mode.Process("gUU");
            _commandUtil.Verify();
        }


        [WpfFact]
        public void Bind_ChangeCaseLine_Lower1()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToLowerCase));
            _mode.Process("gugu");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeCaseLine_Lower2()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.ToLowerCase));
            _mode.Process("guu");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeCaseLine_Rot13_1()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.Rot13));
            _mode.Process("g?g?");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ChangeCaseLine_Rot13_2()
        {
            Create("again");
            _commandUtil.SetupCommandNormal(NormalCommand.NewChangeCaseCaretLine(ChangeCharacterKind.Rot13));
            _mode.Process("g??");
            _commandUtil.Verify();
        }

        #endregion

        #region Yank

        [WpfFact]
        public void Bind_Yank()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.Yank>();
            _mode.Process("yw");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_YankLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.YankLines);
            _mode.Process("yy");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_YankLines_ViaY()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.YankLines);
            _mode.Process("Y");
            _commandUtil.Verify();
        }

        #endregion

        #region Paste

        [WpfFact]
        public void Bind_PutAfterCaret()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutAfterCaret(false));
            _mode.Process("p");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_PutAfterCaretWithIndent()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.PutAfterCaretWithIndent);
            _mode.Process("]p");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_PutBeforeCaret()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutBeforeCaret(false));
            _mode.Process("P");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_PutBeforeCaretWithIndent()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.PutBeforeCaretWithIndent);
            _mode.Process("[p");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_PutBeforeCaretWithIndent_ViaCapitalP()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.PutBeforeCaretWithIndent);
            _mode.Process("[P");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_PutAfterCaret_WithMove()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutAfterCaret(true));
            _mode.Process("gp");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_PutBeforeCaret_WithMove()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.NewPutBeforeCaret(true));
            _mode.Process("gP");
            _commandUtil.Verify();
        }

        #endregion

        #region Delete

        [WpfFact]
        public void Bind_DeleteLines()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteLines);
            _mode.Process("dd");
            _commandUtil.Verify();
        }

        [WpfFact]
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
        [WpfFact]
        public void Process_EscapeShouldExitMotion()
        {
            Create(s_defaultLines);
            _mode.Process('d');
            Assert.True(_mode.CommandRunner.IsWaitingForMoreInput);
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.False(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [WpfFact]
        public void Bind_DeleteTillEndOfLine()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteTillEndOfLine);
            _mode.Process("D");
            _commandUtil.Verify();
        }

        #endregion

        #region Incremental Search

        #endregion

        #region Next / Previous Word

        [WpfFact]
        public void Bind_Motion_NextWord_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextWord(SearchPath.Forward)));
            _mode.Process("*");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_NextWord_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextWord(SearchPath.Backward)));
            _mode.Process("#");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_NextPartialWord_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextPartialWord(SearchPath.Forward)));
            _mode.Process("g*");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_NextPartialWord_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewNextPartialWord(SearchPath.Backward)));
            _mode.Process("g#");
            _commandUtil.Verify();
        }

        #endregion

        [WpfFact]
        public void Bind_Motion_LastSearch_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(false)));
            _mode.Process("n");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Motion_LastSearch_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewMoveCaretToMotion(Motion.NewLastSearch(true)));
            _mode.Process("N");
            _commandUtil.Verify();
        }

        #region Shift

        [WpfFact]
        public void Bind_ShiftRight()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.ShiftLinesRight);
            _mode.Process(">>");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ShiftMotionRight()
        {
            Create("foo", "bar");
            // REPEAT TODO: Add tests for this
        }

        [WpfFact]
        public void Bind_ShiftLeft()
        {
            Create("foo");
            _commandUtil.SetupCommandNormal(NormalCommand.ShiftLinesLeft);
            _mode.Process("<<");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ShiftMotionLeft()
        {
            // REPEAT TODO: Add tests for this
            Create("foo");
        }

        #endregion

        #region Misc

        [WpfFact]
        public void Bind_Undo()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.Undo);
            _mode.Process("u");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_Redo()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.Redo);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('r'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_JoinLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJoinLines(JoinKind.RemoveEmptySpaces));
            _mode.Process("J");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_JoinLines_KeepEmptySpaces()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJoinLines(JoinKind.KeepEmptySpaces));
            _mode.Process("gJ");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToDefinition()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.GoToDefinition);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput(']'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void GoToDefinition2()
        {
            Create(s_defaultLines);
            var def = KeyInputUtil.CharWithControlToKeyInput(']');
            var name = new KeyInputSet(def);
            Assert.True(_mode.CanProcess(def));
            Assert.Contains(name, _mode.CommandNames);
        }

        [WpfFact]
        public void Bind_GoToLocalDeclaration()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.GoToLocalDeclaration);
            _mode.Process("gd");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToGlobalDeclaration()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.GoToGlobalDeclaration);
            _mode.Process("gD");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToFileUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToFileUnderCaret(false));
            _mode.Process("gf");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void SetMark_CanProcessM()
        {
            Create("");
            Assert.True(_mode.CanProcess(KeyInputUtil.CharToKeyInput('m')));
            Assert.Contains(_mode.CommandNames, x => x.KeyInputs.First().Char == 'm');
        }

        /// <summary>
        /// Inside mark mode we can process anything
        /// </summary>
        [WpfFact]
        public void SetMark_CanProcessAnything()
        {
            Create("");
            _mode.Process(KeyInputUtil.CharToKeyInput('m'));
            Assert.True(_mode.CanProcess(KeyInputUtil.CharWithControlToKeyInput('c')));
        }

        [WpfFact]
        public void Bind_SetMark()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSetMarkToCaret('a'));
            _mode.Process("ma");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_JumpToMark()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJumpToMarkLine(Mark.OfChar('a').Value));
            _mode.Process("'a");
        }

        [WpfFact]
        public void Bind_JumpToMark_BackTick()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewJumpToMark(Mark.OfChar('a').Value));
            _mode.Process("`a");
        }

        [WpfFact]
        public void Bind_JumpToNewerPosition()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.JumpToNewerPosition);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_JumpToOlderPosition()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.JumpToOlderPosition);
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_InsertAtEndOfLine()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertAtEndOfLine);
            _mode.Process('A');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_InsertAfterCaret()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.InsertAfterCaret);
            _mode.Process('a');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void KeyRemapMode_DefaultIsNormal()
        {
            Create("foo bar");
            Assert.Equal(KeyRemapMode.Normal, _mode.KeyRemapMode);
        }

        [WpfFact]
        public void KeyRemapMode_OperatorPendingAfterY()
        {
            Create("");
            _mode.Process('y');
            Assert.Equal(KeyRemapMode.OperatorPending, _mode.KeyRemapMode);
        }

        [WpfFact]
        public void KeyRemapMode_OperatorPendingAfterD()
        {
            Create("");
            _mode.Process('d');
            Assert.Equal(KeyRemapMode.OperatorPending, _mode.KeyRemapMode);
        }

        [WpfFact]
        public void KeyRemapMode_LanguageAfterF()
        {
            Create("");
            _mode.Process("df");
            Assert.Equal(KeyRemapMode.Language, _mode.KeyRemapMode);
        }

        /// <summary>
        /// The 'g' keystroke can match multiple commands.  When this happens the second 
        /// keys stroke won't go through further mapping.  
        /// 
        /// The same behavior can be viewed for 'z'
        /// </summary>
        [WpfFact]
        public void KeyRemapMode_AfterG()
        {
            Create("");
            _mode.Process("g");
            Assert.Equal(_mode.KeyRemapMode, KeyRemapMode.None);
        }

        [WpfFact]
        public void IsWaitingForInput1()
        {
            Create("foobar");
            Assert.False(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [WpfFact]
        public void IsWaitingForInput3()
        {
            Create("");
            _mode.Process('y');
            Assert.True(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [WpfFact]
        public void Command1()
        {
            Create("foo");
            _mode.Process("\"a");
            Assert.Equal("\"a", _modeRaw.Command);
        }

        [WpfFact]
        public void Command2()
        {
            Create("bar");
            _mode.Process("\"f");
            Assert.Equal("\"f", _modeRaw.Command);
        }

        [WpfFact]
        public void Command4()
        {
            Create(s_defaultLines);
            _mode.Process('2');
            Assert.Equal("2", _mode.Command);
        }

        [WpfFact]
        public void Command5()
        {
            Create(s_defaultLines);
            _mode.Process("2d");
            Assert.Equal("2d", _mode.Command);
        }

        [WpfFact]
        public void Commands1()
        {
            Create("foo");
            var found = _modeRaw.Commands.Single(x => x.KeyInputSet.Equals(KeyNotationUtil.StringToKeyInputSet("D")));
            Assert.Equal(CommandFlags.Repeatable, found.CommandFlags);
        }

        /// <summary>
        /// Movements shouldn't be repeatable
        /// </summary>
        [WpfFact]
        public void Commands2()
        {
            Create("foo");
            var found = _modeRaw.Commands.Single(x => x.KeyInputSet.Equals(KeyNotationUtil.StringToKeyInputSet("h")));
            Assert.NotEqual(CommandFlags.Repeatable, found.CommandFlags);
        }

        [WpfFact]
        public void Commands3()
        {
            Create("foo", "bar", "baz");
            var found = _modeRaw.Commands.Single(x => x.KeyInputSet.Equals(KeyNotationUtil.StringToKeyInputSet("dd")));
            Assert.Equal(CommandFlags.Repeatable, found.CommandFlags);
        }

        /// <summary>
        /// Sanity check to ensure certain commands are not in fact repeatable
        /// </summary>
        [WpfFact]
        public void VerifyCommandsNotRepeatable()
        {
            Create(string.Empty);
            void verify(string str)
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet(str);
                var command = _modeRaw.Commands.Where(x => x.KeyInputSet == keyInputSet).Single();
                Assert.True(CommandFlags.None == (command.CommandFlags & CommandFlags.Repeatable));
            }

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

        [WpfFact]
        public void Escape1()
        {
            Create(string.Empty);
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.True(res.IsHandled);
        }

        [WpfFact]
        public void Bind_ReplaceAtCaret()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.ReplaceAtCaret);
            _mode.Process("R");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_RepeatLastSubstitute_WithNoFlags()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewRepeatLastSubstitute(false, false));
            _mode.Process("&");
            _commandUtil.Verify();
        }


        [WpfFact]
        public void Bind_RepeatLastSubstitute_WithFlags()
        {
            Create("foo bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewRepeatLastSubstitute(true, true));
            _mode.Process("g&");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_WriteBufferAndQuit()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.WriteBufferAndQuit);
            _mode.Process("ZZ");
            _commandUtil.Verify();
        }

        #endregion

        #region Visual Mode

        [WpfFact]
        public void Bind_SwitchModeVisualCommand_Character()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchModeVisualCommand(VisualKind.Character));
            _mode.Process('v');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_SwitchModeVisualCommand_Line()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchModeVisualCommand(VisualKind.Line));
            _mode.Process('V');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_SwitchModeVisualCommand_Block()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchModeVisualCommand(VisualKind.Block));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('v'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void BindAlternate_SwitchModeVisualCommand_Block()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewSwitchModeVisualCommand(VisualKind.Block));
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_I()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.InsertAtFirstNonBlank);
            _mode.Process('I');
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToNextTab_Forward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(SearchPath.Forward));
            _mode.Process("gt");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToNextTab_ForwardViaPageDown()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(SearchPath.Forward));
            _mode.Process(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.PageDown, VimKeyModifiers.Control));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToNextTab_Backward()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(SearchPath.Backward));
            _mode.Process("gT");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToNextTab_BackwardViaPageUp()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToNextTab(SearchPath.Backward));
            _mode.Process(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.PageUp, VimKeyModifiers.Control));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatCodeLines()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.FormatCodeLines);
            _mode.Process("==");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatCodeMotion()
        {
            Create("the dog chased the ball");
            _commandUtil.SetupCommandMotion<NormalCommand.FormatCodeMotion>();
            _mode.Process("=w");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatTextLines1()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewFormatTextLines(false));
            _mode.Process("gqgq");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatTextLines2()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewFormatTextLines(false));
            _mode.Process("gqq");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatTextMotion()
        {
            Create("the dog chased the ball");
            _commandUtil.SetupCommandMotion<NormalCommand.FormatTextMotion>();
            _mode.Process("gqw");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatTextLines1_PreservingCaretPosition()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewFormatTextLines(true));
            _mode.Process("gwgw");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FormatTextLines2_PreservingCaretPosition()
        {
            Create("foo", "bar");
            _commandUtil.SetupCommandNormal(NormalCommand.NewFormatTextLines(true));
            _mode.Process("gww");
            _commandUtil.Verify();
        }

        #endregion

        #region Folding

        [WpfFact]
        public void Bind_OpenFoldUnderCaret()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.OpenFoldUnderCaret);
            _mode.Process("zo");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_CloseFoldUnderCaret()
        {
            Create(s_defaultLines);
            _commandUtil.SetupCommandNormal(NormalCommand.CloseFoldUnderCaret);
            _mode.Process("zc");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_OpenAllFoldsUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.OpenAllFoldsUnderCaret);
            _mode.Process("zO");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_CloseAllFoldsUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.CloseAllFoldsUnderCaret);
            _mode.Process("zC");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ToggleFoldUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.ToggleFoldUnderCaret);
            _mode.Process("za");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_ToggleAllFolds()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.ToggleAllFolds);
            _mode.Process("zA");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FoldMotion()
        {
            Create("");
            _commandUtil.SetupCommandMotion<NormalCommand.FoldMotion>();
            _mode.Process("zfw");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_FoldLines()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.FoldLines);
            _mode.Process("zF");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteFoldUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteFoldUnderCaret);
            _mode.Process("zd");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteAllFoldsUnderCaret()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteAllFoldsUnderCaret);
            _mode.Process("zD");
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_DeleteAllFoldsInBuffer()
        {
            Create("");
            _commandUtil.SetupCommandNormal(NormalCommand.DeleteAllFoldsInBuffer);
            _mode.Process("zE");
            _commandUtil.Verify();
        }

        #endregion

        #region Split View

        [WpfFact]
        public void Bind_GoToView_Down()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToWindow(WindowKind.Down));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-j>"));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToView_Right()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToWindow(WindowKind.Right));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-l>"));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToView_Left()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToWindow(WindowKind.Left));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-h>"));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToView_Up()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToWindow(WindowKind.Up));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-k>"));
            _commandUtil.Verify();
        }

        [WpfFact]
        public void Bind_GoToView_Up2()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.NewGoToWindow(WindowKind.Up));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("k"));
            _commandUtil.Verify();
        }

        #endregion

        #region Other Window Commands

        [WpfFact]
        public void Bind_CloseWindow()
        {
            Create(string.Empty);
            _commandUtil.SetupCommandNormal(NormalCommand.CloseWindow);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("c"));
            _commandUtil.Verify();
        }

        #endregion
    }
}
