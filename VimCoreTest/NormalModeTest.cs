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
using Vim.Modes;
using Vim.Modes.Normal;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using MockRepository = Vim.UnitTest.Mock.MockObjectFactory;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class NormalModeTest
    {
        private NormalMode _modeRaw;
        private INormalMode _mode;
        private IWpfTextView _view;
        private IRegisterMap _map;
        private IVimData _vimData;
        private Mock<IVimBuffer> _bufferData;
        private Mock<IOperations> _operations;
        private Mock<IEditorOperations> _editorOperations;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private Mock<IJumpList> _jumpList;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IChangeTracker> _changeTracker;
        private Mock<IDisplayWindowBroker> _displayWindowBroker;
        private Mock<IFoldManager> _foldManager;
        private Mock<IVimHost> _host;
        private Mock<IVisualSpanCalculator> _visualSpanCalculator;
        private Register _unnamedRegister;

        static string[] s_lines = new string[]
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
            _view = EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            _unnamedRegister = _map.GetRegister(RegisterName.Unnamed);
            _editorOperations = new Mock<IEditorOperations>();
            _incrementalSearch = new Mock<IIncrementalSearch>(MockBehavior.Strict);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _changeTracker = new Mock<IChangeTracker>(MockBehavior.Strict);
            _foldManager = new Mock<IFoldManager>(MockBehavior.Strict);
            _visualSpanCalculator = new Mock<IVisualSpanCalculator>(MockBehavior.Strict);
            _host = new Mock<IVimHost>(MockBehavior.Loose);
            _displayWindowBroker = new Mock<IDisplayWindowBroker>(MockBehavior.Strict);
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSignatureHelpActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(false);
            _vimData = new VimData();
            _bufferData = MockRepository.CreateVimBuffer(
                _view,
                "test",
                MockRepository.CreateVim(_map, changeTracker: _changeTracker.Object, host: _host.Object, vimData: _vimData).Object,
                _jumpList.Object,
                incrementalSearch: _incrementalSearch.Object);
            _operations = new Mock<IOperations>(MockBehavior.Strict);
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);
            _operations.SetupGet(x => x.TextView).Returns(_view);
            _operations.SetupGet(x => x.FoldManager).Returns(_foldManager.Object);

            motionUtil = motionUtil ?? new TextViewMotionUtil(_view, new Vim.LocalSettings(
                    new Vim.GlobalSettings(),
                    _view));
            var capture = new MotionCapture(_host.Object, _view, motionUtil, new MotionCaptureGlobalData());
            var runner = new CommandRunner(_view, _map, (IMotionCapture)capture, _statusUtil.Object);
            _modeRaw = new Vim.Modes.Normal.NormalMode(
                _bufferData.Object,
                _operations.Object,
                _statusUtil.Object,
                _displayWindowBroker.Object,
                (ICommandRunner)runner,
                (IMotionCapture)capture,
                _visualSpanCalculator.Object);
            _mode = _modeRaw;
            _mode.OnEnter(ModeArgument.None);
        }

        private MotionData CreateMotionData(SnapshotSpan? span = null)
        {
            span = span ?? new SnapshotSpan(_view.TextSnapshot, 0, 3);
            return new MotionData(
                span.Value,
                true,
                MotionKind.Exclusive,
                OperationKind.LineWise,
                FSharpOption<int>.None);
        }

        [TearDown]
        public void TearDown()
        {
            _view = null;
            _mode = null;
        }

        [Test]
        public void ModeKindTest()
        {
            Create(s_lines);
            Assert.AreEqual(ModeKind.Normal, _mode.ModeKind);
        }

        [Test, Description("Let enter go straight back to the editor in the default case")]
        public void EnterProcessing()
        {
            Create(s_lines);
            var can = _mode.CanProcess(KeyInputUtil.EnterKey);
            Assert.IsTrue(can);
        }

        #region CanProcess

        [Test, Description("Can process basic commands")]
        public void CanProcess1()
        {
            Create(s_lines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('u')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('h')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('j')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('i')));
        }

        [Test, Description("Can process even invalid commands else they end up as input")]
        public void CanProcess2()
        {
            Create(s_lines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('U')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('Z')));
        }

        [Test, Description("Must be able to process numbers")]
        public void CanProcess3()
        {
            Create(s_lines);
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
            Create(s_lines);
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap));
            _mode.Process(KeyInputUtil.CharToKeyInput('/'));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('U')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('Z')));
        }

        [Test, Description("Don't process while a smart tag is open otherwise you prevent it from being used")]
        public void CanProcess5()
        {
            Create(s_lines);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(true);
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Left)));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Down)));
        }

        [Test, Description("Should be able to handle ever core character")]
        public void CanProcess6()
        {
            Create(s_lines);
            foreach (var cur in KeyInputUtil.VimKeyCharList)
            {
                Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput(cur)));
            }
        }

        [Test, Description("Must be able to handle certain movement keys")]
        public void CanProcess7()
        {
            Create(s_lines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.TabKey));
        }

        [Test, Description("Don't process while a completion window is open otherwise you prevent it from being used")]
        public void CanProcess8()
        {
            Create(s_lines);
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(true);
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.EnterKey));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Left)));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Down)));
            Assert.IsFalse(_mode.CanProcess(KeyInputUtil.TabKey));
        }

        #endregion

        #region Motion

        private void AssertMotion(
            string motionName,
            Action<Mock<ITextViewMotionUtil>, SnapshotPoint, FSharpOption<int>, MotionData> setupMock,
            bool isMovement = true)
        {
            if (isMovement)
            {
                var mock = new Mock<ITextViewMotionUtil>(MockBehavior.Strict);
                Create(mock.Object, s_lines);
                var data = CreateMotionData();
                var point = _view.GetPoint(0);
                setupMock(mock, point, FSharpOption<int>.None, data);
                _operations.Setup(x => x.MoveCaretToMotionData(data)).Verifiable();
                _mode.Process(motionName);
                mock.Verify();
                _operations.Verify();
            }
        }


        [Test]
        public void Motion_G()
        {
            AssertMotion(
                "G",
                (util, point, count, data) =>
                    util
                        .Setup(x => x.LineOrLastToFirstNonWhitespace(count))
                        .Returns(data)
                        .Verifiable());
        }

        [Test]
        public void Motion_gg()
        {
            AssertMotion(
                "gg",
                (util, point, count, data) =>
                    util
                        .Setup(x => x.LineOrFirstToFirstNonWhitespace(count))
                        .Returns(data)
                        .Verifiable());
        }

        [Test]
        public void Motion_g_()
        {
            AssertMotion(
                "g_",
                (util, point, count, data) =>
                    util
                    .Setup(x => x.LastNonWhitespaceOnLine(CommandUtil.CountOrDefault(count)))
                    .Returns(data)
                    .Verifiable());
        }

        #endregion

        #region Movement

        [Test]
        public void Move_l()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process("l");
            _operations.Verify();
        }

        [Test]
        public void Move_l2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(2)).Verifiable();
            _mode.Process("2l");
            _operations.Verify();
        }

        [Test]
        public void Move_h()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process("h");
            _operations.Verify();
        }

        [Test]
        public void Move_h2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(2)).Verifiable();
            _mode.Process("2h");
            _operations.Verify();
        }

        [Test]
        public void Move_Backspace1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            _operations.Verify();
        }

        [Test]
        public void Move_Backspace2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            _operations.Verify();
        }

        [Test]
        public void Move_k()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretUp(1)).Verifiable();
            _mode.Process("k");
            _operations.Verify();
        }

        [Test]
        public void Move_j()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            _mode.Process("j");
            _operations.Verify();
        }

        [Test]
        public void Move_LeftArrow1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
            _operations.Verify();
        }

        [Test]
        public void Move_LeftArrow2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
            _operations.Verify();
        }

        [Test]
        public void Move_RightArrow1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Right));
            _operations.Verify();
        }

        [Test]
        public void Move_RightArrow2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Right));
            _operations.Verify();
        }

        [Test]
        public void Move_UpArrow1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretUp(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
            _operations.Verify();
        }

        [Test]
        public void Move_UpArrow2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretUp(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
            _operations.Verify();
        }

        [Test]
        public void Move_DownArrow1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
            _operations.Verify();
        }

        [Test]
        public void Move_DownArrow2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretDown(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
            _operations.Verify();
        }

        [Test]
        public void Move_CtrlP1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretUp(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('p'));
            _operations.Verify();
        }

        [Test]
        public void Move_CtrlN1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('n'));
            _operations.Verify();
        }

        [Test]
        public void Move_CtrlH1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('h'));
            _operations.Verify();
        }

        [Test]
        public void Move_SpaceBar1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharToKeyInput(' '));
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_w1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('w');
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_W1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('W');
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_b1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('b');
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_B1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('B');
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_Enter1()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process(KeyInputUtil.EnterKey);
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_Enter2()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.EnterKey);
            _operations.Verify();
        }

        [Test]
        public void Move_Motion_Hat1()
        {
            Create("foo bar");
            _view.MoveCaretTo(3);
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('^');
            _editorOperations.Verify();
        }

        [Test]
        public void Move_Motion_Hat2()
        {
            Create("   foo bar");
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('^');
            _editorOperations.Verify();

        }

        [Test]
        public void Move_Motion_Dollar1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _mode.Process('$');
            _editorOperations.Verify();
        }

        [Test]
        public void Move_0()
        {
            Create("foo bar baz");
            _operations.Setup(x => x.MoveCaretToMotionData(It.IsAny<MotionData>())).Verifiable();
            _view.MoveCaretTo(3);
            _mode.Process('0');
            _operations.Verify();
        }

        [Test]
        public void Move_CHome_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToLineOrFirst(FSharpOption<int>.None)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Home, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void Move_CHome_2()
        {
            Create(s_lines);
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
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.MoveCaretAndScrollLines(ScrollDirection.Up, 2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('u'));
            _operations.Verify();
        }

        [Test]
        public void MoveCaretAndScrollDown1()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.MoveCaretAndScrollLines(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            _operations.Verify();
        }

        [Test]
        public void ScrollDown1()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.Setup(x => x.ScrollLines(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
            _operations.Verify();
        }

        [Test]
        public void ScrollDown2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.Setup(x => x.ScrollLines(ScrollDirection.Down, 3)).Verifiable();
            _mode.Process('3');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
            _operations.Verify();
        }

        [Test]
        public void ScrollUp()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
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

        [Test]
        public void Scroll_zInvalid()
        {
            Create(String.Empty);
            _operations.Setup(x => x.Beep()).Verifiable();
            _mode.Process("z;");
            _operations.Verify();
        }

        #endregion

        #region Motion

        [Test, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion1()
        {
            Create(s_lines);
            _statusUtil.Setup(x => x.OnError(Resources.MotionCapture_InvalidMotion));
            _mode.Process("d@");
            _statusUtil.Verify();
        }

        [Test, Description("Invalid motion should bring us back to normal state")]
        public void BadMotion2()
        {
            Create(s_lines);
            _statusUtil.Setup(x => x.OnError(It.IsAny<string>())).Verifiable();
            _mode.Process("d@");
            var res = _mode.Process(KeyInputUtil.CharToKeyInput('i'));
            Assert.IsTrue(res.IsSwitchMode);
            _statusUtil.Verify();
        }

        [Test]
        public void Motion_l()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process("l");
            _operations.Verify();
        }

        [Test]
        public void Motion_2l()
        {
            Create(s_lines);
            _operations.Setup(x => x.MoveCaretRight(2)).Verifiable();
            _mode.Process("2l");
            _operations.Verify();
        }

        [Test]
        public void Motion_50l()
        {
            Create(s_lines);
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _operations.Setup(x => x.MoveCaretRight(50)).Verifiable();
            _mode.Process("50l");
            _operations.Verify();
        }


        #endregion

        #region Edits

        [Test]
        public void Edit_o_1()
        {
            Create("how is", "foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.Setup(x => x.InsertLineBelow()).Returns<ITextSnapshotLine>(null).Verifiable();
            var res = _mode.Process('o');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test, Description("Use o at end of buffer")]
        public void Edit_o_2()
        {
            Create("foo", "bar");
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _operations.Setup(x => x.InsertLineBelow()).Returns<ITextSnapshotLine>(null).Verifiable();
            _mode.Process('o');
            _operations.Verify();
        }

        [Test]
        public void Edit_O_1()
        {
            Create("foo");
            _operations.Setup(x => x.InsertLineAbove()).Returns<ITextSnapshotLine>(null).Verifiable();
            _mode.Process('O');
            _operations.Verify();
        }

        [Test]
        public void Edit_O_2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.InsertLineAbove()).Returns<ITextSnapshotLine>(null).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _mode.Process("O");
            _operations.Verify();
        }

        [Test]
        public void Edit_O_3()
        {
            Create("foo");
            _operations.Setup(x => x.InsertLineAbove()).Returns<ITextSnapshotLine>(null).Verifiable();
            var res = _mode.Process('O');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().item);
        }

        [Test]
        public void Edit_X_1()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterBeforeCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("X");
            _operations.Verify();
        }

        [Test]
        public void Edit_X_2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterBeforeCursor(2))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("2X");
            _operations.Verify();
        }

        [Test]
        public void Edit_r_1()
        {
            Create("foo");
            var ki = KeyInputUtil.CharToKeyInput('b');
            _operations.Setup(x => x.ReplaceChar(ki, 1)).Returns(true).Verifiable();
            _mode.Process("rb");
            _operations.Verify();
        }

        [Test]
        public void Edit_r_2()
        {
            Create("foo");
            var ki = KeyInputUtil.CharToKeyInput('b');
            _operations.Setup(x => x.ReplaceChar(ki, 2)).Returns(true).Verifiable();
            _mode.Process("2rb");
            _operations.Verify();
        }

        [Test]
        public void Edit_r_3()
        {
            Create("foo");
            var ki = KeyInputUtil.EnterKey;
            _operations.Setup(x => x.ReplaceChar(ki, 1)).Returns(true).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("r", enter: true);
            _operations.Verify();
        }

        [Test]
        public void Edit_r_4()
        {
            Create("food");
            _operations.Setup(x => x.Beep()).Verifiable();
            _operations.Setup(x => x.ReplaceChar(It.IsAny<KeyInput>(), 200)).Returns(false).Verifiable();
            _mode.Process("200ru");
            _operations.Verify();
        }

        [Test, Description("Escape should exit replace not be a part of it")]
        public void Edit_r_5()
        {
            Create("foo");
            _mode.Process("200r");
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsFalse(_mode.IsInReplace);
        }

        [Test]
        public void Edit_x_1()
        {
            Create("foo");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("x");
            _operations.Verify();
        }

        [Test]
        public void Edit_2x()
        {
            Create("foo");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(2))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("2x");
            _operations.Verify();
        }

        [Test]
        public void Edit_x_2()
        {
            Create("foo");
            var reg = _map.GetRegister('c');
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(reg, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("\"cx");
            _operations.Verify();
        }

        [Test]
        public void Edit_Del_1()
        {
            Create("foo");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process(VimKey.Delete);
            _operations.Verify();
        }

        [Test]
        public void Edit_c_1()
        {
            Create("foo bar");
            var motionData = new MotionData(
                _view.TextBuffer.GetSpan(0, 4),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations
                .Setup(x => x.ChangeSpan(motionData))
                .Returns(motionData.OperationSpan)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, motionData.OperationSpan, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("cw");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_c_2()
        {
            Create("foo bar");
            var reg = _map.GetRegister('c');
            var motionData = new MotionData(
                _view.TextBuffer.GetSpan(0, 4),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations
                .Setup(x => x.ChangeSpan(motionData))
                .Returns(motionData.OperationSpan)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(reg, RegisterOperation.Delete, motionData.OperationSpan, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("\"ccw");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_c_3()
        {
            Create("");
            var command =
                _mode.CommandRunner.Commands
                .Where(x => x.KeyInputSet.Name == "c" && x.IsMotionCommand)
                .Single();
            Assert.IsTrue(0 != (CommandFlags.LinkedWithNextTextChange & command.CommandFlags));
        }

        [Test]
        public void Edit_cc_1()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0, 0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            var res = _mode.Process("cc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_cc_2()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0, 1).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            var res = _mode.Process("2cc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_C_1()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLinesFromCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("C");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_C_2()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLinesFromCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('b'), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("\"bC");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test, Description("Delete from the cursor")]
        public void Edit_C_3()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLinesFromCursor(2))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('b'), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("\"b2C");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_s_1()
        {
            Create("foo bar");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("s");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_s_2()
        {
            Create("foo bar");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(2))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("2s");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_s_3()
        {
            Create("foo bar");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteCharacterAtCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('c'), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("\"cs");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_S_1()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLines(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            var res = _mode.Process("S");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_S_2()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLines(2))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            var res = _mode.Process("2S");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_S_3()
        {
            Create("foo", "bar", "baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLines(300))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            var res = _mode.Process("300S");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_Tilde1()
        {
            Create("foo");
            _operations.Setup(x => x.ChangeLetterCaseAtCursor(1)).Verifiable();
            _mode.Process("~");
            _operations.Verify();
        }

        [Test]
        public void Edit_Tilde2()
        {
            Create("foo");
            _operations.Setup(x => x.ChangeLetterCaseAtCursor(30)).Verifiable();
            _mode.Process("30~");
            _operations.Verify();
        }

        [Test, Description("When TildeOp is set it becomes a motion command")]
        public void Edit_Tilde3()
        {
            Create("foo");
            _bufferData.Object.Settings.GlobalSettings.TildeOp = true;
            _mode.Process("~");
        }

        [Test]
        public void Edit_Tilde4()
        {
            Create("foo");
            _bufferData.Object.Settings.GlobalSettings.TildeOp = true;
            _operations.Setup(x => x.ChangeLetterCase(_view.TextBuffer.GetLineRange(0, 0).Extent)).Verifiable();
            _mode.Process("~aw");
            _operations.Verify();
        }

        #endregion

        #region Yank

        [Test]
        public void Yank_yw()
        {
            Create("foo");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("yw");
            _operations.Verify();
        }

        [Test, Description("Yanks in the middle of the word should only get a partial")]
        public void Yank_yw_2()
        {
            Create("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var span = new SnapshotSpan(_view.TextSnapshot, 1, 3);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("yw");
            _operations.Verify();
        }

        [Test, Description("Yank word should go to the start of the next word including spaces")]
        public void Yank_yw_3()
        {
            Create("foo bar");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 4);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("yw");
            _operations.Verify();
        }

        [Test, Description("Non-default register")]
        public void Yank_yw_4()
        {
            Create("foo bar");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 4);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('c'), RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("\"cyw");
            _operations.Verify();
        }

        [Test]
        public void Yank_2yw()
        {
            Create("foo bar baz");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 8);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("2yw");
            _operations.Verify();
        }

        [Test]
        public void Yank_3yw()
        {
            Create("foo bar baz joe");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 12);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("3yw");
            _operations.Verify();
        }

        [Test]
        public void Yank_yaw()
        {
            Create("foo bar");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 4);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("yaw");
            _operations.Verify();
        }

        [Test]
        public void Yank_y2w()
        {
            Create("foo bar baz");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 8);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("y2w");
            _operations.Verify();
        }


        [Test]
        public void Yank_yaw_2()
        {
            Create("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 4);
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("yaw");
            _operations.Verify();
        }

        [Test]
        public void Yank_yaw_3()
        {
            Create(s_lines);
            _mode.Process("ya");
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsFalse(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [Test, Description("A yy should grab the end of line including line break information")]
        public void Yank_yy_1()
        {
            Create("foo", "bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak;
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
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak;
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
            var span = _view.GetLineRange(0).ExtentIncludingLineBreak;
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
            var span = _view.GetLineRange(0).ExtentIncludingLineBreak;
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
            var span = _view.GetLineRange(0, 1).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Yank, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("2Y");
            _operations.Verify();
        }

        #endregion

        #region Paste

        [Test]
        public void Paste_p()
        {
            Create("foo bar");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process('p');
            _operations.Verify();
        }

        [Test, Description("Paste from a non-default register")]
        public void Paste_p_2()
        {
            Create("foo");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister('j').UpdateValue("hey");
            _mode.Process("\"jp");
            _operations.Verify();
        }

        [Test, Description("Pasting a linewise motion should occur on the next line")]
        public void Paste_p_3()
        {
            Create("foo", "bar");
            var data = "baz" + Environment.NewLine;
            _operations.Setup(x => x.PasteAfterCursor(data, 1, OperationKind.LineWise, false)).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map.GetRegister(RegisterName.Unnamed).Value = new RegisterValue(StringData.NewSimple(data), OperationKind.LineWise);
            _mode.Process("p");
            _operations.Verify();
        }

        [Test]
        public void Paste_2p()
        {
            Create("foo");
            _operations.Setup(x => x.PasteAfterCursor("hey", 2, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process("2p");
            _operations.Verify();
        }

        [Test]
        public void Paste_P()
        {
            Create("foo");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process('P');
            _operations.Verify();
        }

        [Test, Description("Pasting a linewise motion should occur on the previous line")]
        public void Paste_P_2()
        {
            Create("foo", "bar");
            var data = "baz" + Environment.NewLine;
            _operations.Setup(x => x.PasteBeforeCursor(data, 1, OperationKind.LineWise, false)).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _map.GetRegister(RegisterName.Unnamed).Value = new RegisterValue(StringData.NewSimple(data), OperationKind.LineWise);
            _mode.Process('P');
            _operations.Verify();
        }

        [Test]
        public void Paste_2P()
        {
            Create("foo");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 2, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process("2P");
            _operations.Verify();
        }

        [Test]
        public void Paste_gp_1()
        {
            Create("foo");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process("gp");
            _operations.Verify();
        }

        [Test]
        public void Paste_gp_2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _map.GetRegister('c').UpdateValue("hey");
            _mode.Process("\"cgp");
            _operations.Verify();
        }

        [Test]
        public void Paste_gP_1()
        {
            Create("foo");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process("gP");
            _operations.Verify();
        }

        [Test]
        public void Paste_gP_2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _mode.Process("gP");
            _operations.Verify();
        }

        #endregion

        #region Delete

        [Test, Description("Make sure a dd is a linewise action")]
        public void Delete_dd_1()
        {
            Create("foo", "bar");
            var span = _view.GetLineRange(0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.DeleteLinesIncludingLineBreak(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("dd");
            _operations.Verify();
        }

        [Test]
        public void Delete_dd_2()
        {
            Create("foo", "bar");
            var span = _view.GetLineRange(0).ExtentIncludingLineBreak;
            _operations
                .Setup(x => x.DeleteLinesIncludingLineBreak(2))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            _mode.Process("2dd");
            _operations.Verify();
        }

        [Test]
        public void Delete_dw_1()
        {
            Create("foo bar baz");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 4);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("dw");
            _operations.Verify();
        }

        [Test, Description("Delete at the end of the line shouldn't delete newline")]
        public void Delete_dw_2()
        {
            Create("foo bar", "baz");
            var point = new SnapshotPoint(_view.TextSnapshot, 4);
            _view.Caret.MoveTo(point);
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            var span = new SnapshotSpan(point, _view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("dw");
            _operations.Verify();
        }

        [Test, Description("Escape should exit d")]
        public void Delete_d_1()
        {
            Create(s_lines);
            _mode.Process('d');
            Assert.IsTrue(_mode.CommandRunner.IsWaitingForMoreInput);
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsFalse(_mode.CommandRunner.IsWaitingForMoreInput);
        }

        [Test]
        public void Delete_D_1()
        {
            Create("foo bar");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLinesFromCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("D");
            _operations.Verify();
        }

        [Test]
        public void Delete_D_2()
        {
            Create("foo bar baz");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLinesFromCursor(1))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('b'), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("\"bD");
            _operations.Verify();
        }

        [Test]
        public void Delete_D_3()
        {
            Create("foo bar");
            var span = _view.GetLineRange(0).Extent;
            _operations
                .Setup(x => x.DeleteLinesFromCursor(3))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_unnamedRegister, RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("3D");
            _operations.Verify();
        }

        #endregion

        #region Regressions

        [Test, Description("Don't re-enter insert mode on every keystroke once you've left")]
        public void Regression_InsertMode()
        {
            Create(s_lines);
            var res = _mode.Process('i');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            res = _mode.Process('h');
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
        }

        [Test, Description("j past the end of the buffer")]
        public void Regression_DownPastBufferEnd()
        {
            Create("foo");
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            var res = _mode.Process('j');
            Assert.IsTrue(res.IsProcessed);
            res = _mode.Process('j');
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
        }

        #endregion

        #region Incremental Search

        [Test]
        public void IncrementalSearch1()
        {
            Create("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            _incrementalSearch.Verify();
        }

        [Test]
        public void IncrementalSearch2()
        {
            Create("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.BackwardWithWrap)).Verifiable();
            _mode.Process('?');
            _incrementalSearch.Verify();
        }

        [Test]
        public void IncrementalSearch3()
        {
            Create("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _mode.Process('/');
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchProcessResult.SearchComplete).Verifiable();
            _mode.Process('b');
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        [Test, Description("Make sure any key goes to incremental search")]
        public void IncrementalSearch4()
        {
            Create("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            var ki = KeyInputUtil.CharToKeyInput((char)7);
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchProcessResult.SearchComplete).Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _mode.Process(ki);
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        [Test, Description("After a true return incremental search should be completed")]
        public void IncrementalSearch5()
        {
            Create("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            var ki = KeyInputUtil.CharToKeyInput('c');
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchProcessResult.SearchComplete).Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _mode.Process(ki);
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        [Test, Description("Cancel should not add to the jump list")]
        public void IncrementalSearch6()
        {
            Create("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchProcessResult.SearchCancelled).Verifiable();
            _mode.Process(KeyInputUtil.CharToKeyInput((char)8));
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        #endregion

        #region Next / Previous Word

        [Test]
        public void NextWord1()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1)).Verifiable();
            _mode.Process("*");
            _operations.Verify();
        }

        [Test, Description("No matches should have no effect")]
        public void NextWord2()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 4)).Verifiable();
            _mode.Process("4*");
            _operations.Verify();
        }

        [Test]
        public void PreviousWord1()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1)).Verifiable();
            _mode.Process("#");
            _operations.Verify();
        }

        [Test]
        public void PreviousWord2()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 4)).Verifiable();
            _mode.Process("4#");
            _operations.Verify();
        }

        [Test]
        public void NextPartialWord1()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfPartialWordAtCursor(SearchKind.ForwardWithWrap, 1)).Verifiable();
            _mode.Process("g*");
            _operations.Verify();
        }

        [Test]
        public void PreviousPartialWord1()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfPartialWordAtCursor(SearchKind.BackwardWithWrap, 1)).Verifiable();
            _mode.Process("g#");
            _operations.Verify();
        }

        #endregion

        #region Search

        [Test]
        public void Search_n_1()
        {
            Create("foo");
            _operations.Setup(x => x.MoveToNextOccuranceOfLastSearch(1, false)).Verifiable();
            _mode.Process("n");
            _operations.Verify();
        }

        [Test]
        public void Search_n_2()
        {
            Create("foo");
            _operations.Setup(x => x.MoveToNextOccuranceOfLastSearch(2, false)).Verifiable();
            _mode.Process("2n");
            _operations.Verify();
        }

        [Test]
        public void Search_N_1()
        {
            Create("foo");
            _operations.Setup(x => x.MoveToNextOccuranceOfLastSearch(1, true)).Verifiable();
            _mode.Process("N");
            _operations.Verify();
        }

        [Test]
        public void Search_N_2()
        {
            Create("foo");
            _operations.Setup(x => x.MoveToNextOccuranceOfLastSearch(2, true)).Verifiable();
            _mode.Process("2N");
            _operations.Verify();
        }

        #endregion

        #region Shift

        [Test]
        public void ShiftRight1()
        {
            Create("foo");
            _operations
                .Setup(x => x.ShiftLinesRight(1))
                .Verifiable();
            _mode.Process(">>");
            _operations.Verify();
        }

        [Test, Description("With a count")]
        public void ShiftRight2()
        {
            Create("foo", "bar");
            var tss = _view.TextSnapshot;
            _operations
                .Setup(x => x.ShiftLinesRight(2))
                .Verifiable();
            _mode.Process("2>>");
            _operations.Verify();
        }

        [Test, Description("With a motion")]
        public void ShiftRight3()
        {
            Create("foo", "bar");
            var range = _view.GetLineRange(0, 1);
            _operations
                .Setup(x => x.ShiftLineRangeRight(1, range))
                .Verifiable();
            _mode.Process(">j");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft1()
        {
            Create("foo");
            _operations
                .Setup(x => x.ShiftLinesLeft(1))
                .Verifiable();
            _mode.Process("<<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft2()
        {
            Create(" foo");
            _operations
                .Setup(x => x.ShiftLinesLeft(1))
                .Verifiable();
            _mode.Process("<<");
            _operations.Verify();
        }

        [Test, Description("With a count")]
        public void ShiftLeft3()
        {
            Create("     foo", "     bar");
            var tss = _view.TextSnapshot;
            _operations
                .Setup(x => x.ShiftLinesLeft(2))
                .Verifiable();
            _mode.Process("2<<");
            _operations.Verify();
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
        public void Join_NoArguments()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.Join(_view.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            _mode.Process("J");
            _operations.Verify();
        }

        [Test]
        [Description("A count of 2 is the same as 1 or no count")]
        public void Join_2IstheSameAs1()
        {
            Create("foo", "  bar", "baz");
            _operations
                .Setup(x => x.Join(_view.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            _mode.Process("2J");
            _operations.Verify();
        }

        [Test]
        [Description("Join more than 1 line")]
        public void Join_MoreThan2Lines()
        {
            Create("foo", "  bar", "baz");
            _operations
                .Setup(x => x.Join(_view.GetLineRange(0, 2), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            _mode.Process("3J");
            _operations.Verify();
        }

        [Test]
        [Description("Join should beep if the count exceeds the number of lines in the buffer")]
        public void Join_CountExceedsLinesInBuffer()
        {
            Create("foo", "  bar", "baz");
            _host.Setup(x => x.Beep()).Verifiable();
            _mode.Process("33J");
            _host.Verify();
        }

        [Test]
        public void Join_KeepEmptySpaces()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.Join(_view.GetLineRange(0, 1), JoinKind.KeepEmptySpaces))
                .Verifiable();
            _mode.Process("gJ");
            _operations.Verify();
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
            Create(s_lines);
            var def = KeyInputUtil.CharWithControlToKeyInput(']');
            var name = KeyInputSet.NewOneKeyInput(def);
            Assert.IsTrue(_mode.CanProcess(def));
            Assert.IsTrue(_mode.CommandNames.Contains(name));
        }

        [Test]
        public void GoTo_gd1()
        {
            Create("foo bar");
            _operations.Setup(x => x.GoToLocalDeclaration()).Verifiable();
            _mode.Process("gd");
            _operations.Verify();
        }

        [Test]
        public void GoTo_gd2()
        {
            Create("foo bar");
            _operations.Setup(x => x.GoToLocalDeclaration()).Verifiable();
            _mode.Process("gd");
            _operations.Verify();
        }

        [Test]
        public void GoTo_gD1()
        {
            Create("foo bar");
            _operations.Setup(x => x.GoToGlobalDeclaration()).Verifiable();
            _mode.Process("gD");
            _operations.Verify();
        }

        [Test]
        public void GoTo_gf1()
        {
            Create("foo bar");
            _operations.Setup(x => x.GoToFile()).Verifiable();
            _mode.Process("gf");
            _operations.Verify();
        }

        [Test]
        public void GoToMatch1()
        {
            Create("foo bar");
            _operations.Setup(x => x.GoToMatch()).Returns(true);
            Assert.IsTrue(_mode.Process(KeyInputUtil.CharToKeyInput('%')).IsProcessed);
            _operations.Verify();
        }

        [Test]
        public void Mark1()
        {
            Create(s_lines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('m')));
            Assert.IsTrue(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == 'm'));
        }

        [Test, Description("Once we are in mark mode we can process anything")]
        public void Mark2()
        {
            Create(s_lines);
            _mode.Process(KeyInputUtil.CharToKeyInput('m'));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharWithControlToKeyInput('c')));
        }

        [Test]
        public void Mark3()
        {
            Create(s_lines);
            _operations.Setup(x => x.SetMark(_bufferData.Object, _view.Caret.Position.BufferPosition, 'a')).Returns(Result._unique_Succeeded).Verifiable();
            _mode.Process(KeyInputUtil.CharToKeyInput('m'));
            _mode.Process(KeyInputUtil.CharToKeyInput('a'));
            _operations.Verify();
        }

        [Test, Description("Bad mark should beep")]
        public void Mark4()
        {
            Create(s_lines);
            _operations.Setup(x => x.Beep()).Verifiable();
            _operations.Setup(x => x.SetMark(_bufferData.Object, _view.Caret.Position.BufferPosition, ';')).Returns(Result.NewFailed("foo")).Verifiable();
            _mode.Process(KeyInputUtil.CharToKeyInput('m'));
            _mode.Process(KeyInputUtil.CharToKeyInput(';'));
            _operations.Verify();
        }

        [Test]
        public void JumpToMark1()
        {
            Create(s_lines);
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('\'')));
            Assert.IsTrue(_mode.CanProcess(KeyInputUtil.CharToKeyInput('`')));
            Assert.IsTrue(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == '\''));
            Assert.IsTrue(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == '`'));
        }

        [Test]
        public void JumpToMark2()
        {
            Create("foobar");
            _operations
                .Setup(x => x.JumpToMark('a', _bufferData.Object.MarkMap))
                .Returns(Result._unique_Succeeded)
                .Verifiable();
            _mode.Process('\'');
            _mode.Process('a');
            _operations.Verify();
        }

        [Test]
        public void JumpToMark3()
        {
            Create("foobar");
            _operations
                .Setup(x => x.JumpToMark('a', _bufferData.Object.MarkMap))
                .Returns(Result._unique_Succeeded)
                .Verifiable();
            _mode.Process('`');
            _mode.Process('a');
            _operations.Verify();
        }

        [Test]
        public void JumpNext1()
        {
            Create(s_lines);
            _operations.Setup(x => x.JumpNext(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            _operations.Verify();
        }

        [Test]
        public void JumpNext2()
        {
            Create(s_lines);
            _operations.Setup(x => x.JumpNext(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            _operations.Verify();
        }

        [Test]
        public void JumpNext3()
        {
            Create(s_lines);
            _operations.Setup(x => x.JumpNext(1)).Verifiable();
            _mode.Process(KeyInputUtil.TabKey);
            _operations.Verify();
        }

        [Test]
        public void JumpPrevious1()
        {
            Create(s_lines);
            _operations.Setup(x => x.JumpPrevious(1)).Verifiable();
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _operations.Verify();
        }

        [Test]
        public void JumpPrevious2()
        {
            Create(s_lines);
            _operations.Setup(x => x.JumpPrevious(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _operations.Verify();
        }

        [Test]
        public void Append1()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveCaretForAppend()).Verifiable();
            var ret = _mode.Process('a');
            Assert.IsTrue(ret.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, ret.AsSwitchMode().Item);
            _operations.Verify();
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
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap));
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
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap));
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
        public void Command3()
        {
            Create("again");
            _operations.Setup(x => x.MoveCaretUp(1));
            _mode.Process('k');
            Assert.AreEqual(string.Empty, _mode.Command);
        }

        [Test]
        public void Command4()
        {
            Create(s_lines);
            _mode.Process('2');
            Assert.AreEqual("2", _mode.Command);
        }

        [Test]
        public void Command5()
        {
            Create(s_lines);
            _mode.Process("2d");
            Assert.AreEqual("2d", _mode.Command);
        }

        [Test]
        public void Commands1()
        {
            Create("foo");
            var all = _modeRaw.Commands.ToList();
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

        [Test]
        public void RepeatLastChange1()
        {
            Create("foo");
            _operations.Setup(x => x.Beep()).Verifiable();
            _changeTracker.SetupGet(x => x.LastChange).Returns(FSharpOption<RepeatableChange>.None).Verifiable();
            _mode.Process('.');
            _changeTracker.Verify();
            _operations.Verify();
        }

        [Test]
        public void RepeatLastChange2()
        {
            Create("");
            _changeTracker.SetupGet(x => x.LastChange).Returns(FSharpOption.Create(RepeatableChange.NewTextChange(TextChange.NewInsert("h")))).Verifiable();
            _operations.Setup(x => x.InsertText("h", 1)).Verifiable();
            _mode.Process('.');
            _operations.Verify();
            _changeTracker.Verify();
        }

        [Test]
        public void RepeatLastChange3()
        {
            Create("");
            _changeTracker.SetupGet(x => x.LastChange).Returns(FSharpOption.Create(RepeatableChange.NewTextChange(TextChange.NewInsert("h")))).Verifiable();
            _operations.Setup(x => x.InsertText("h", 3)).Verifiable();
            _mode.Process("3.");
            _operations.Verify();
            _changeTracker.Verify();
        }

        [Test]
        public void RepeatLastChange4()
        {
            Create("");
            var didRun = false;
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateSimpleCommand("d", (x, y) => { didRun = true; }),
                    _map.GetRegister(RegisterName.Unnamed),
                    1);
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process(".");
            _operations.Verify();
            _changeTracker.Verify();
            Assert.IsTrue(didRun);
        }

        [Test]
        public void RepeatLastChange5()
        {
            Create("");
            var didRun = false;
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateSimpleCommand("c", (x, y) => { didRun = true; }),
                    _map.GetRegister(RegisterName.Unnamed),
                    1);
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process(".");
            _operations.Verify();
            _changeTracker.Verify();
            Assert.IsTrue(didRun);
        }

        [Test, Description("No new count should use last count")]
        public void RepeatLastChange6()
        {
            Create("");
            var didRun = false;
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateSimpleCommand("c", (x, y) =>
                    {
                        Assert.AreEqual(2, x.Value);
                        didRun = true;
                    }),
                    _map.GetRegister(RegisterName.Unnamed),
                    2);
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process(".");
            _operations.Verify();
            _changeTracker.Verify();
            Assert.IsTrue(didRun);
        }

        [Test, Description("New Count sohuld replace old count")]
        public void RepeatLastChange7()
        {
            Create("");
            var didRun = false;
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateSimpleCommand("c", (x, y) =>
                    {
                        Assert.AreEqual(4, x.Value);
                        didRun = true;
                    }),
                    _map.GetRegister(RegisterName.Unnamed),
                    2);
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process("4.");
            _operations.Verify();
            _changeTracker.Verify();
            Assert.IsTrue(didRun);
        }

        [Test, Description("Executing . should not clear the last command")]
        public void RepeatLastChange8()
        {
            Create("");
            var runCount = 0;
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateSimpleCommand("c", (x, y) => { runCount++; }),
                    _map.GetRegister(RegisterName.Unnamed));
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process(".");
            _mode.Process(".");
            _mode.Process(".");
            Assert.AreEqual(3, runCount);
            _operations.Verify();
            _changeTracker.Verify();
        }

        [Test, Description("Guard against a possible stack overflow with a recursive . repeat")]
        public void RepeatLastChange9()
        {
            Create("");
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateSimpleCommand("c", (x, y) => { _modeRaw.RepeatLastChange(FSharpOption.Create(42), _map.GetRegister(RegisterName.Unnamed)); }),
                    _map.GetRegister(RegisterName.Unnamed));
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_RecursiveRepeatDetected)).Verifiable();
            _mode.Process(".");
            _changeTracker.Verify();
            _statusUtil.Verify();
        }

        [Test]
        [Description("Repeat with a motion")]
        public void RepeatLastChange10()
        {
            Create("foobar");
            var didRunCommand = false;
            var didRunMotion = false;
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateMotionCommand("c", (x, y, motionData) => { didRunCommand = true; }),
                    _map.GetRegister(RegisterName.Unnamed),
                    1,
                    VimUtil.CreateMotionRunData(
                        VimUtil.CreateSimpleMotion("w", () => null),
                        null,
                        () =>
                        {
                            didRunMotion = true;
                            return CreateMotionData();
                        }));
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process(".");
            _operations.Verify();
            _changeTracker.Verify();
            Assert.IsTrue(didRunCommand);
            Assert.IsTrue(didRunMotion);
        }

        [Test]
        public void RepeatLastChange11()
        {
            Create("");
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateVisualCommand(name: "c"),
                    _map.GetRegister(RegisterName.Unnamed),
                    2);
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _statusUtil
                .Setup(x => x.OnError(Resources.NormalMode_RepeatNotSupportedOnCommand("c")))
                .Verifiable();
            _mode.Process(".");
            _changeTracker.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void RepeatLastChange12()
        {
            Create("here again", "and again");
            var didRun = false;
            var span = VimUtil.CreateVisualSpanSingle(_view.GetLineRange(1).Extent);
            var data =
                VimUtil.CreateCommandRunData(
                    VimUtil.CreateVisualCommand(
                        "c",
                        CommandFlags.None,
                        VisualKind.Line,
                        (_, __, spanArg) =>
                        {
                            didRun = true;
                            Assert.AreEqual(span, spanArg);
                            return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                        }),
                    _map.GetRegister(RegisterName.Unnamed),
                    1,
                    visualRunData: VimUtil.CreateVisualSpanSingle(_view.GetLineRange(0).Extent));
            _visualSpanCalculator.Setup(x => x.CalculateForTextView(
                It.IsAny<ITextView>(),
                It.IsAny<VisualSpan>())).Returns(span).Verifiable();
            _changeTracker
                .SetupGet(x => x.LastChange)
                .Returns(FSharpOption.Create(RepeatableChange.NewCommandChange(data)))
                .Verifiable();
            _mode.Process(".");
            _changeTracker.Verify();
            _visualSpanCalculator.Verify();
            Assert.IsTrue(didRun);
        }

        [Test]
        [Description("Verify certain commands are not actually repeatable")]
        public void RepeatLastChange13()
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
            Assert.IsTrue(res.IsProcessed);
        }

        [Test]
        public void OneTimeCommand1()
        {
            Create(string.Empty);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.OnEnter(ModeArgument.NewOneTimeCommand(ModeKind.Insert));
            var res = _mode.Process("h");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void OneTimeCommand2()
        {
            Create(string.Empty);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.OnEnter(ModeArgument.NewOneTimeCommand(ModeKind.Command));
            var res = _mode.Process("h");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Command, res.AsSwitchMode().Item);
        }

        [Test]
        public void ReplaceMode1()
        {
            Create(string.Empty);
            var res = _mode.Process("R");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Replace, res.AsSwitchMode().Item);
        }

        [Test]
        [Description("Make sure it doesn't pass the flag")]
        public void Substitute1()
        {
            Create("foo bar");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.Confirm));
            _operations.Setup(x => x.Substitute("a", "b", _view.GetLineRange(0, 0), SubstituteFlags.None));
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
            _operations.Setup(x => x.Substitute("a", "b", SnapshotLineRangeUtil.CreateForSnapshot(_view.TextSnapshot), SubstituteFlags.Confirm));
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

        [Test]
        public void Handle_Q()
        {
            Create("foo bar");
            _operations.Setup(x => x.Close(false)).Verifiable();
            _mode.Process("Q");
            _operations.Verify();
        }

        #endregion

        #region Visual Mode

        [Test]
        public void VisualMode1()
        {
            Create(s_lines);
            var res = _mode.Process('v');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.VisualCharacter, res.AsSwitchMode().Item);
        }

        [Test]
        public void VisualMode2()
        {
            Create(s_lines);
            var res = _mode.Process('V');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.VisualLine, res.AsSwitchMode().Item);
        }

        [Test]
        public void VisualMode3()
        {
            Create(s_lines);
            var res = _mode.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.VisualBlock, res.AsSwitchMode().Item);
        }

        [Test]
        public void ShiftI_1()
        {
            Create(s_lines);
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            var res = _mode.Process('I');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _editorOperations.Verify();
        }

        [Test]
        public void gt_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToNextTab(1)).Verifiable();
            _mode.Process("gt");
            _operations.Verify();
        }

        [Test]
        public void gt_2()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToNextTab(2)).Verifiable();
            _mode.Process("2gt");
            _operations.Verify();
        }

        [Test]
        public void CPageDown_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToNextTab(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageDown, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void CPageDown_2()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToNextTab(2)).Verifiable();
            _mode.Process("2");
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageDown, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void gT_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToPreviousTab(1)).Verifiable();
            _mode.Process("gT");
            _operations.Verify();
        }

        [Test]
        public void gT_2()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToPreviousTab(2)).Verifiable();
            _mode.Process("2gT");
            _operations.Verify();
        }

        [Test]
        public void CPageUp_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToPreviousTab(1)).Verifiable();
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageUp, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void CPageUp_2()
        {
            Create(s_lines);
            _operations.Setup(x => x.GoToPreviousTab(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.PageUp, KeyModifiers.Control));
            _operations.Verify();
        }

        [Test]
        public void FormatMotion1()
        {
            Create("foo", "bar");
            _host.Setup(x => x.FormatLines(_view, _view.GetLineRange(0, 0)));
            _mode.Process("==");
        }

        [Test]
        public void FormatMotion2()
        {
            Create("foo", "bar");
            _host.Setup(x => x.FormatLines(_view, _view.GetLineRange(0, 1)));
            _mode.Process("2==");
        }

        #endregion

        #region Folding

        [Test]
        public void Fold_zo()
        {
            Create(s_lines);
            _operations.Setup(x => x.OpenFold(_view.GetCaretLine().Extent, 1)).Verifiable();
            _mode.Process("zo");
            _operations.Verify();
        }

        [Test]
        public void Fold_zo_2()
        {
            Create(s_lines);
            _operations.Setup(x => x.OpenFold(_view.GetCaretLine().Extent, 3)).Verifiable();
            _mode.Process("3zo");
            _operations.Verify();
        }

        [Test]
        public void Fold_zc_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.CloseFold(_view.GetCaretLine().Extent, 1)).Verifiable();
            _mode.Process("zc");
            _operations.Verify();
        }

        [Test]
        public void Fold_zc_2()
        {
            Create(s_lines);
            _operations.Setup(x => x.CloseFold(_view.GetCaretLine().Extent, 3)).Verifiable();
            _mode.Process("3zc");
            _operations.Verify();
        }

        [Test]
        public void Fold_zO_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.OpenAllFolds(_view.GetCaretLine().Extent)).Verifiable();
            _mode.Process("zO");
            _operations.Verify();
        }

        [Test]
        public void Fold_zC_1()
        {
            Create(s_lines);
            _operations.Setup(x => x.CloseAllFolds(_view.GetCaretLine().Extent)).Verifiable();
            _mode.Process("zC");
            _operations.Verify();
        }

        [Test]
        public void Fold_zf_1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _foldManager.Setup(x => x.CreateFold(_view.TextBuffer.GetSpan(0, 4))).Verifiable();
            _mode.Process("zfw");
            _foldManager.Verify();
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
        public void MoveViewUp1()
        {
            Create(string.Empty);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-k>"));
            _host.Verify(x => x.MoveViewUp(_view));
        }

        [Test]
        public void MoveViewUp2()
        {
            Create(string.Empty);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("k"));
            _host.Verify(x => x.MoveViewUp(_view));
        }

        [Test]
        public void MoveViewDown1()
        {
            Create(string.Empty);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-j>"));
            _host.Verify(x => x.MoveViewDown(_view));
        }

        [Test]
        public void MoveViewDown2()
        {
            Create(string.Empty);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
            _mode.Process(KeyNotationUtil.StringToKeyInput("j"));
            _host.Verify(x => x.MoveViewDown(_view));
        }

        #endregion
    }
}
