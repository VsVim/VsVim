using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.FSharp.Core;
using Moq;
using MockFactory = VimCoreTest.Utils.MockObjectFactory;
using Vim.Modes.Normal;
using Vim.Modes;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest
{
    [TestFixture]
    public class NormalModeTest
    {
        private Vim.Modes.Normal.NormalMode _modeRaw;
        private IMode _mode;
        private IWpfTextView _view;
        private IRegisterMap _map;
        private Mock<IVimBuffer> _bufferData;
        private MockBlockCaret _blockCaret;
        private Mock<IOperations> _operations;
        private Mock<IEditorOperations> _editorOperations;
        private Mock<ISearchReplace> _searchReplace;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private Mock<IJumpList> _jumpList;

        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        public void CreateBuffer(params string[] lines)
        {
            CreateBuffer(new FakeVimHost(), lines);
        }

        public void CreateBuffer(IVimHost host, params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map = new RegisterMap();
            _blockCaret = new MockBlockCaret();
            _editorOperations = new Mock<IEditorOperations>();
            _searchReplace = new Mock<ISearchReplace>(MockBehavior.Strict);
            _incrementalSearch = new Mock<IIncrementalSearch>(MockBehavior.Strict);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _bufferData = MockFactory.CreateVimBuffer(
                _view,
                "test",
                MockFactory.CreateVim(_map, host : host).Object,
                _blockCaret,
                _editorOperations.Object,
                _jumpList.Object);
            _operations = new Mock<IOperations>(MockBehavior.Strict);
            _modeRaw = new Vim.Modes.Normal.NormalMode(Tuple.Create(_bufferData.Object, _operations.Object, _searchReplace.Object, _incrementalSearch.Object));
            _mode = _modeRaw;
            _mode.OnEnter();
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
            CreateBuffer(s_lines);
            Assert.AreEqual(ModeKind.Normal, _mode.ModeKind);
        }

        [Test, Description("Let enter go straight back to the editor in the default case")]
        public void EnterProcessing()
        {
            CreateBuffer(s_lines);
            var can = _mode.CanProcess(InputUtil.KeyToKeyInput(Key.Enter));
            Assert.IsTrue(can);
        }

        #region CanProcess

        [Test, Description("Can process basic commands")]
        public void CanProcess1()
        {
            CreateBuffer(s_lines);
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('u')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('h')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('j')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('i')));
        }

        [Test, Description("Cannot process invalid commands")]
        public void CanProcess2()
        {
            CreateBuffer(s_lines);
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('U')));
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('Z')));
        }

        [Test, Description("Must be able to process numbers")]
        public void CanProcess3()
        {
            CreateBuffer(s_lines);
            foreach (var cur in Enumerable.Range(1, 8))
            {
                var c = char.Parse(cur.ToString());
                var ki = InputUtil.CharToKeyInput(c);
                Assert.IsTrue(_mode.CanProcess(ki));
            }
        }

        [Test, Description("When in a need more state, process everything")]
        public void CanProcess4()
        {
            CreateBuffer(s_lines);
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap));
            _mode.Process(InputUtil.CharToKeyInput('/'));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('U')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('Z')));
        }


        #endregion

        #region Movement

        [Test, Description("Enter should move down on line")]
        public void Enter1()
        {
            CreateBuffer(s_lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process(Key.Enter);
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Enter at end of file should beep ")]
        public void Enter2()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start.Add(2));
            _mode.Process(Key.Enter);
            Assert.AreEqual(1, host.BeepCount);
            Assert.AreEqual(last.Start.Add(2), _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void Move_l()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process("l");
            _operations.Verify();
        }

        [Test]
        public void Move_l2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(2)).Verifiable();
            _mode.Process("2l");
            _operations.Verify();
        }

        [Test]
        public void Move_h()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process("h");
            _operations.Verify();
        }

        [Test]
        public void Move_h2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(2)).Verifiable();
            _mode.Process("2h");
            _operations.Verify();
        }

        [Test]
        public void Move_Backspace1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Back));
            _operations.Verify();
        }

        [Test]
        public void Move_Backspace2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(InputUtil.KeyToKeyInput(Key.Back));
            _operations.Verify();
        }

        [Test]
        public void Move_k()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretUp(1)).Verifiable();
            _mode.Process("k");
            _operations.Verify();
        }

        [Test]
        public void Move_j()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            _mode.Process("j");
            _operations.Verify();
        }

        [Test]
        public void Move_LeftArrow1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Left));
            _operations.Verify();
        }

        [Test]
        public void Move_LeftArrow2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(InputUtil.KeyToKeyInput(Key.Left));
            _operations.Verify();
        }

        [Test]
        public void Move_RightArrow1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Right));
            _operations.Verify();
        }

        [Test]
        public void Move_RightArrow2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(InputUtil.KeyToKeyInput(Key.Right));
            _operations.Verify();
        }

        [Test]
        public void Move_UpArrow1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretUp(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Up));
            _operations.Verify();
        }

        [Test]
        public void Move_UpArrow2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretUp(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(InputUtil.KeyToKeyInput(Key.Up));
            _operations.Verify();
        }

        [Test]
        public void Move_DownArrow1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Down));
            _operations.Verify();
        }

        [Test]
        public void Move_DownArrow2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretDown(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(InputUtil.KeyToKeyInput(Key.Down));
            _operations.Verify();
        }

        [Test]
        public void Move_CtrlP1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretUp(1)).Verifiable();
            _mode.Process(new KeyInput('p', Key.P, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void Move_CtrlN1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            _mode.Process(new KeyInput('n', Key.N, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void Move_CtrlH1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process(new KeyInput('h', Key.H, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void Move_SpaceBar1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Space));
            _operations.Verify();
        }

        [Test]
        public void Move_w1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveWordForward(WordKind.NormalWord, 1)).Verifiable();
            _mode.Process('w');
            _operations.Verify();
        }

        [Test]
        public void Move_W1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveWordForward(WordKind.BigWord, 1)).Verifiable();
            _mode.Process('W');
            _operations.Verify();
        }

        [Test]
        public void Move_b1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveWordBackward(WordKind.NormalWord, 1)).Verifiable();
            _mode.Process('b');
            _operations.Verify();
        }

        [Test]
        public void Move_B1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveWordBackward(WordKind.BigWord, 1)).Verifiable();
            _mode.Process('B');
            _operations.Verify();
        }

        [Test]
        public void Move_0()
        {
            CreateBuffer("foo bar baz");
            _editorOperations.Setup(x => x.MoveToStartOfLine(false)).Verifiable();
            _view.MoveCaretTo(3);
            _mode.Process('0');
            _editorOperations.Verify();
        }

        [Test]
        public void Move_Shift6_1()
        {
            CreateBuffer("foo bar");
            _view.MoveCaretTo(3);
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process('^');
            _editorOperations.Verify();
        }

        [Test]
        public void Move_Shift6_2()
        {
            CreateBuffer("   foo bar");
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process('^');
            _editorOperations.Verify();

        }

        [Test]
        public void Move_Shift4_1()
        {
            CreateBuffer("foo", "bar");
            _editorOperations.Setup(x => x.MoveToEndOfLine(false)).Verifiable();
            _mode.Process('$');
            _editorOperations.Verify();
        }

        [Test]
        public void Move_gUnderscore_1()
        {
            CreateBuffer("foo bar ");
            _editorOperations.Setup(x => x.MoveToLastNonWhiteSpaceCharacter(false)).Verifiable();
            _mode.Process("g_");
            _editorOperations.Verify();
        }

        #endregion

        #region Scroll

        [Test]
        public void ScrollUp1()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.Scroll(ScrollDirection.Up, 1)).Verifiable();
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.U, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test, Description("Don't break at line 0")]
        public void ScrollUp2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.Scroll(ScrollDirection.Up, 2)).Verifiable();
            _mode.Process('2');
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.U, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void ScrollDown1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.Scroll(ScrollDirection.Down, 1)).Verifiable();
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.D, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void Scroll_zEnter()
        {
            CreateBuffer("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineTop()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z");
            _mode.Process(Key.Enter);
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zt()
        {
            CreateBuffer("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineTop()).Verifiable();
            _mode.Process("zt");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zPeriod()
        {
            CreateBuffer("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineCenter()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z.");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zz()
        {
            CreateBuffer("foo", "bar");
            _editorOperations.Setup(x => x.ScrollLineCenter()).Verifiable();
            _mode.Process("z.");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zDash()
        {
            CreateBuffer(String.Empty);
            _editorOperations.Setup(x => x.ScrollLineBottom()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z-");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zb()
        {
            CreateBuffer(String.Empty);
            _editorOperations.Setup(x => x.ScrollLineBottom()).Verifiable();
            _editorOperations.Setup(x => x.MoveToStartOfLineAfterWhiteSpace(false)).Verifiable();
            _mode.Process("z-");
            _editorOperations.Verify();
        }

        [Test]
        public void Scroll_zInvalid()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, String.Empty);
            _mode.Process("z;");
            Assert.IsTrue(host.BeepCount > 0);
        }

        #endregion

        #region Motion

        [Test, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion1()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _mode.Process("d@");
            Assert.AreNotEqual(String.Empty, host.Status);
        }

        [Test, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion2()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _mode.Process("d@aoeuaoeu");
            Assert.AreNotEqual(String.Empty, host.Status);
        }

        [Test, Description("Enter must cancel an invalid motion")]
        public void BadMotion3()
        {
            CreateBuffer(s_lines);
            _mode.Process("d@");
            var res = _mode.Process(Key.I);
            Assert.IsTrue(res.IsProcessed);
            _mode.Process(Key.Enter);
            res = _mode.Process(Key.I);
            Assert.IsTrue(res.IsSwitchMode);
        }

        [Test, Description("Canceled motion should reset the status")]
        public void BadMotion4()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _mode.Process("dzzz");
            Assert.IsFalse(String.IsNullOrEmpty(host.Status));
            _mode.Process(Key.Escape);
            Assert.IsTrue(String.IsNullOrEmpty(host.Status));
        }

        [Test, Description("Completed motion should reset the status")]
        public void BadMotion5()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _operations.Setup(x => x.Yank(
                It.IsAny<SnapshotSpan>(),
                It.IsAny<MotionKind>(),
                It.IsAny<OperationKind>(),
                It.IsAny<Register>())).Verifiable();
            _mode.Process("yaw");
            Assert.IsTrue(String.IsNullOrEmpty(host.Status));
            _operations.Verify();
        }

        [Test]
        public void Motion_l()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(1)).Verifiable();
            _mode.Process("l");
            _operations.Verify();
        }

        [Test]
        public void Motion_2l()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.MoveCaretRight(2)).Verifiable();
            _mode.Process("2l");
            _operations.Verify();
        }

        [Test]
        public void Motion_50l()
        {
            CreateBuffer(s_lines);
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
            CreateBuffer("how is", "foo");
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
            CreateBuffer("foo", "bar");
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _operations.Setup(x => x.InsertLineBelow()).Returns<ITextSnapshotLine>(null).Verifiable();
            _mode.Process('o');
            _operations.Verify();
        }

        [Test]
        public void Edit_O_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.InsertLineAbove()).Returns<ITextSnapshotLine>(null).Verifiable();
            _mode.Process('O');
            _operations.Verify();
        }

        [Test]
        public void Edit_O_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.InsertLineAbove()).Returns<ITextSnapshotLine>(null).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _mode.Process("O");
            _operations.Verify();
        }

        [Test]
        public void Edit_O_3()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.InsertLineAbove()).Returns<ITextSnapshotLine>(null).Verifiable();
            var res = _mode.Process('O');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().item);
        }

        [Test]
        public void Edit_X_1()
        {
            CreateBuffer("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.Setup(x => x.DeleteCharacterBeforeCursor(1, It.IsAny<Register>())).Verifiable();
            _mode.Process("X");
            _operations.Verify();
        }

        [Test, Description("Don't delete past the current line")]
        public void Edit_X_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _operations.Setup(x => x.DeleteCharacterBeforeCursor(1, It.IsAny<Register>())).Verifiable();
            _mode.Process("X");
            _operations.Verify();
        }

        [Test]
        public void Edit_2X_1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            _operations.Setup(x => x.DeleteCharacterBeforeCursor(2, It.IsAny<Register>())).Verifiable();
            _mode.Process("2X");
            _operations.Verify();
        }

        [Test]
        public void Edit_2X_2()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.DeleteCharacterBeforeCursor(2, It.IsAny<Register>())).Verifiable();
            _mode.Process("2X");
            _operations.Verify();
        }

        [Test]
        public void Edit_r_1()
        {
            CreateBuffer("foo");
            var ki = InputUtil.CharToKeyInput('b');
            _operations.Setup(x => x.ReplaceChar(ki, 1)).Returns(true).Verifiable();
            _mode.Process("rb");
            _operations.Verify();
        }

        [Test]
        public void Edit_r_2()
        {
            CreateBuffer("foo");
            var ki = InputUtil.CharToKeyInput('b');
            _operations.Setup(x => x.ReplaceChar(ki, 2)).Returns(true).Verifiable();
            _mode.Process("2rb");
            _operations.Verify();
        }

        [Test]
        public void Edit_r_3()
        {
            CreateBuffer("foo");
            var ki = InputUtil.KeyToKeyInput(Key.LineFeed);
            _operations.Setup(x => x.ReplaceChar(ki, 1)).Returns(true).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("r");
            _mode.Process(InputUtil.KeyToKeyInput(Key.LineFeed));
            _operations.Verify();
        }

        public void Edit_r_4()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, "food");
            _operations.Setup(x => x.ReplaceChar(It.IsAny<KeyInput>(), 200)).Returns(false).Verifiable();
            _mode.Process("200ru");
            Assert.IsTrue(host.BeepCount > 0);
            _operations.Verify();
        }

        [Test, Description("block caret should be hidden for the duration of the r command")]
        public void Edit_r_5()
        {
            CreateBuffer("foo");
            _blockCaret.HideCount = 0;
            _blockCaret.ShowCount = 0;
            _mode.Process('r');
            Assert.AreEqual(1, _blockCaret.HideCount);
            _operations.Setup(x => x.ReplaceChar(It.IsAny<KeyInput>(), 1)).Returns(true).Verifiable();
            _mode.Process('u');
            Assert.AreEqual(1, _blockCaret.ShowCount);
            _operations.Verify();
        }

        [Test]
        public void Edit_x_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.DeleteCharacterAtCursor(1, It.IsAny<Register>())).Verifiable();
            _mode.Process("x");
            _operations.Verify();
        }

        [Test]
        public void Edit_2x()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.DeleteCharacterAtCursor(2, It.IsAny<Register>())).Verifiable();
            _mode.Process("2x");
            _operations.Verify();
        }

        [Test]
        public void Edit_x_2()
        {
            CreateBuffer("foo");
            var reg = _map.GetRegister('c');
            _operations.Setup(x => x.DeleteCharacterAtCursor(1, reg)).Verifiable();
            _mode.Process("\"cx");
            _operations.Verify();
        }

        [Test]
        public void Edit_Del_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.DeleteCharacterAtCursor(1, _map.DefaultRegister)).Verifiable();
            _mode.Process(Key.Delete);
            _operations.Verify();
        }

        [Test]
        public void Edit_c_1()
        {
            CreateBuffer("foo bar");
            _operations
                .Setup(x => x.DeleteSpan(new SnapshotSpan(_view.TextSnapshot, 0, 4), MotionKind.Exclusive, OperationKind.CharacterWise, _map.DefaultRegister))
                .Returns(_view.TextSnapshot)
                .Verifiable();
            var res = _mode.Process("cw");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_c_2()
        {
            CreateBuffer("foo bar");
            var reg = _map.GetRegister('c');
            _operations
                .Setup(x => x.DeleteSpan(new SnapshotSpan(_view.TextSnapshot, 0, 4), MotionKind.Exclusive, OperationKind.CharacterWise, reg))
                .Returns(_view.TextSnapshot)
                .Verifiable();
            var res = _mode.Process("\"ccw");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_cc_1()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations
                .Setup(x => x.DeleteSpan(_view.GetLineSpan(0, 0), MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister))
                .Returns(_view.TextSnapshot)
                .Verifiable();
            var res = _mode.Process("cc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_cc_2()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations
                .Setup(x => x.DeleteSpan(_view.GetLineSpan(0, 1), MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister))
                .Returns(_view.TextSnapshot)
                .Verifiable();
            var res = _mode.Process("2cc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_C_1()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteLinesFromCursor(1, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process("C");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_C_2()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteLinesFromCursor(1, _map.GetRegister('b'))).Verifiable();
            var res = _mode.Process("\"bC");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test, Description("Delete from the cursor")]
        public void Edit_C_3()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteLinesFromCursor(2, _map.GetRegister('b'))).Verifiable();
            var res = _mode.Process("\"b2C");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_s_1()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.DeleteCharacterAtCursor(1, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process("s");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_s_2()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.DeleteCharacterAtCursor(2, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process("2s");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_s_3()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.DeleteCharacterAtCursor(1, _map.GetRegister('c'))).Verifiable();
            var res = _mode.Process("\"cs");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_S_1()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteLines(1, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process("S");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_S_2()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteLines(2, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process("2S");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        [Test]
        public void Edit_S_3()
        {
            CreateBuffer("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteLines(300, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process("300S");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Verify();
        }

        #endregion

        #region Yank

        [Test]
        public void Yank_yw()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.Yank(
                _view.TextSnapshot.GetLineFromLineNumber(0).Extent,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yw");
            _operations.Verify();
        }

        [Test, Description("Yanks in the middle of the word should only get a partial")]
        public void Yank_yw_2()
        {
            CreateBuffer("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 1, 3),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yw");
            _operations.Verify();
        }

        [Test, Description("Yank word should go to the start of the next word including spaces")]
        public void Yank_yw_3()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0, 4),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yw");
            _operations.Verify();
        }

        [Test, Description("Non-default register")]
        public void Yank_yw_4()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0, 4),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.GetRegister('c'))).Verifiable();
            _mode.Process("\"cyw");
            _operations.Verify();
        }

        [Test]
        public void Yank_2yw()
        {
            CreateBuffer("foo bar baz");
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0, 8),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("2yw");
            _operations.Verify();
        }

        [Test]
        public void Yank_3yw()
        {
            CreateBuffer("foo bar baz joe");
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0, 12),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("3yw");
            _operations.Verify();
        }

        [Test]
        public void Yank_yaw()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0, 4),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yaw");
            _operations.Verify();
        }

        [Test]
        public void Yank_y2w()
        {
            CreateBuffer("foo bar baz");
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0, 8),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("y2w");
            _operations.Verify();
        }

        [Test]
        public void Yank_yaw_2()
        {
            CreateBuffer("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.Setup(x => x.Yank(
                new SnapshotSpan(_view.TextSnapshot, 0,4),
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yaw");
            _operations.Verify();
        }

        [Test, Description("A yy should grab the end of line including line break information")]
        public void Yank_yy_1()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.Yank(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yy");
            _operations.Verify();
        }

        [Test, Description("yy should yank the entire line even if the cursor is not at the start")]
        public void Yank_yy_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.Setup(x => x.Yank(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                _map.DefaultRegister)).Verifiable();
            _mode.Process("yy");
            _operations.Verify();
        }

        [Test]
        public void Yank_Y_1()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.YankLines(1, _map.DefaultRegister)).Verifiable();
            _mode.Process("Y");
            _operations.Verify();
        }

        [Test]
        public void Yank_Y_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.YankLines(1, _map.GetRegister('c'))).Verifiable();
            _mode.Process("\"cY");
            _operations.Verify();
        }

        [Test]
        public void Yank_Y_3()
        {
            CreateBuffer("foo", "bar", "jazz");
            _operations.Setup(x => x.YankLines(2, _map.DefaultRegister)).Verifiable();
            _mode.Process("2Y");
            _operations.Verify();
        }

        #endregion

        #region Paste

        [Test]
        public void Paste_p()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process('p');
            _operations.Verify();
        }

        [Test, Description("Paste from a non-default register")]
        public void Paste_p_2()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister('j').UpdateValue("hey");
            _mode.Process("\"jp");
            _operations.Verify();
        }

        [Test, Description("Pasting a linewise motion should occur on the next line")]
        public void Paste_p_3()
        {
            CreateBuffer("foo", "bar");
            var data = "baz" + Environment.NewLine;
            _operations.Setup(x => x.PasteAfterCursor(data, 1, OperationKind.LineWise, false)).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map.DefaultRegister.UpdateValue(new RegisterValue(data, MotionKind.Inclusive, OperationKind.LineWise));
            _mode.Process("p");
            _operations.Verify();
        }

        [Test]
        public void Paste_2p()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteAfterCursor("hey", 2, OperationKind.CharacterWise, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("2p");
            _operations.Verify();
        }

        [Test]
        public void Paste_P()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 1, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process('P');
            _operations.Verify();
        }

        [Test]
        public void Paste_2P()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 2, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("2P");
            _operations.Verify();
        }

        [Test]
        public void Paste_gp_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("gp");
            _operations.Verify();
        }

        [Test]
        public void Paste_gp_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _map.GetRegister('c').UpdateValue("hey");
            _mode.Process("\"cgp");
            _operations.Verify();
        }

        [Test]
        public void Paste_gP_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 1, true)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("gP");
            _operations.Verify();
        }

        [Test]
        public void Paste_gP_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.PasteBeforeCursor("hey", 1, true)).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("gP");
            _operations.Verify();
        }

        #endregion

        #region Delete

        [Test, Description("Make sure a dd is a linewise action")]
        public void Delete_dd_1()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.DeleteSpan(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Inclusive,
                OperationKind.LineWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            _mode.Process("dd");
            _operations.Verify();
        }

        [Test, Description("Make sure that it deletes the entire line regardless of where the caret is")]
        public void Delete_dd_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.Setup(x => x.DeleteSpan(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Inclusive,
                OperationKind.LineWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            _mode.Process("dd");
            _operations.Verify();
        }

        [Test]
        public void Delete_dw_1()
        {
            CreateBuffer("foo bar baz");
            _operations.Setup(x => x.DeleteSpan(
                new SnapshotSpan(_view.TextSnapshot, 0, 4),
                MotionKind._unique_Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            _mode.Process("dw");
            _operations.Verify();
        }

        [Test, Description("Delete at the end of the line shouldn't delete newline")]
        public void Delete_dw_2()
        {
            CreateBuffer("foo bar", "baz");
            var point = new SnapshotPoint(_view.TextSnapshot, 4);
            _view.Caret.MoveTo(point);
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            var span = new SnapshotSpan(point, _view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.Setup(x => x.DeleteSpan(
                span,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            _mode.Process("dw");
            _operations.Verify();
        }


        #endregion

        #region Regressions

        [Test, Description("Don't re-enter insert mode on every keystroke once you've left")]
        public void Regression_InsertMode()
        {
            CreateBuffer(s_lines);
            var res = _mode.Process(InputUtil.KeyToKeyInput(Key.I));
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            res = _mode.Process(InputUtil.KeyToKeyInput(Key.H));
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
        }

        [Test, Description("j past the end of the buffer")]
        public void Regression_DownPastBufferEnd()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.MoveCaretDown(1)).Verifiable();
            var res = _mode.Process(Key.J);
            Assert.IsTrue(res.IsProcessed);
            res = _mode.Process(Key.J);
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
        }

        #endregion

        #region Incremental Search

        [Test]
        public void IncrementalSearch1()
        {
            CreateBuffer("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            _incrementalSearch.Verify();
        }

        [Test]
        public void IncrementalSearch2()
        {
            CreateBuffer("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.BackwardWithWrap)).Verifiable();
            _mode.Process('?');
            _incrementalSearch.Verify();
        }

        [Test]
        public void IncrementalSearch3()
        {
            CreateBuffer("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _mode.Process('/');
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchResult.SearchComplete).Verifiable();
            _mode.Process('b');
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        [Test, Description("Make sure any key goes to incremental search")]
        public void IncrementalSearch4()
        {
            CreateBuffer("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            var ki = InputUtil.KeyToKeyInput(Key.DbeRoman);
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchResult.SearchComplete).Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _mode.Process(ki);
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        [Test, Description("After a true return incremental search should be completed")]
        public void IncrementalSearch5()
        {
            CreateBuffer("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            var ki = InputUtil.KeyToKeyInput(Key.DbeRoman);
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchResult.SearchComplete).Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _mode.Process(ki);
            _mode.Process(InputUtil.KeyToKeyInput(Key.DbeAlphanumeric));
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        [Test, Description("Cancel should not add to the jump list")]
        public void IncrementalSearch6()
        {
            CreateBuffer("foo bar");
            _incrementalSearch.Setup(x => x.Begin(SearchKind.ForwardWithWrap)).Verifiable();
            _mode.Process('/');
            _incrementalSearch.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(SearchResult.SearchCanceled).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.DbeRoman));
            _incrementalSearch.Verify();
            _jumpList.Verify();
        }

        #endregion

        #region Next / Previous Word

        [Test]
        public void NextWord1()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfWordAtCursor(true, 1)).Verifiable();
            _mode.Process("*");
            _operations.Verify();
        }

        [Test, Description("No matches should have no effect")]
        public void NextWord2()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfWordAtCursor(true, 4)).Verifiable();
            _mode.Process("4*");
            _operations.Verify();
        }

        [Test]
        public void PreviousWord1()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.MoveToPreviousOccuranceOfWordAtCursor(true, 1)).Verifiable();
            _mode.Process("#");
            _operations.Verify();
        }

        [Test]
        public void PreviousWord2()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.MoveToPreviousOccuranceOfWordAtCursor(true, 4)).Verifiable();
            _mode.Process("4#");
            _operations.Verify();
        }

        [Test]
        public void NextPartialWord1()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.MoveToNextOccuranceOfPartialWordAtCursor(1)).Verifiable();
            _mode.Process("g*");
            _operations.Verify();
        }

        [Test]
        public void PreviousPartialWord1()
        {
            CreateBuffer("foo bar");
            _operations.Setup(x => x.MoveToPreviousOccuranceOfPartialWordAtCursor(1)).Verifiable();
            _mode.Process("g#");
            _operations.Verify();
        }

        #endregion

        #region Shift

        [Test]
        public void ShiftRight1()
        {
            CreateBuffer("foo");
            _operations
                .Setup(x => x.ShiftRight(_view.TextSnapshot.GetLineFromLineNumber(0).Extent, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process(">>");
            _operations.Verify();
        }

        [Test, Description("With a count")]
        public void ShiftRight2()
        {
            CreateBuffer("foo", "bar");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).End);
            _operations
                .Setup(x => x.ShiftRight(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("2>>");
            _operations.Verify();
        }

        [Test, Description("With a motion")]
        public void ShiftRight3()
        {
            CreateBuffer("foo", "bar");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).End);
            _operations
                .Setup(x => x.ShiftRight(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process(">j");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft1()
        {
            CreateBuffer("foo");
            _operations
                .Setup(x => x.ShiftLeft(_view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("<<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft2()
        {
            CreateBuffer(" foo");
            _operations
                .Setup(x => x.ShiftLeft(_view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("<<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft3()
        {
            CreateBuffer("     foo");
            _operations
                .Setup(x => x.ShiftLeft(_view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("<<");
            _operations.Verify();
        }

        [Test, Description("With a count")]
        public void ShiftLeft4()
        {
            CreateBuffer("     foo", "     bar");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).End);
            _operations
                .Setup(x => x.ShiftLeft(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("2<<");
            _operations.Verify();
        }

        #endregion

        #region Misc

        [Test]
        public void Register1()
        {
            CreateBuffer("foo");
            Assert.AreEqual('_', _modeRaw.Register.Name);
            _mode.Process("\"c");
            Assert.AreEqual('c', _modeRaw.Register.Name);
        }

        [Test]
        public void Undo1()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, "foo");
            Assert.AreEqual(0, host.UndoCount);
            _mode.Process("u");
            Assert.AreEqual(1, host.UndoCount);
        }

        [Test]
        public void Undo2()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, "foo");
            _mode.Process("2u");
            Assert.AreEqual(2, host.UndoCount);
        }

        [Test]
        public void Redo1()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, "foo");
            _mode.Process(new KeyInput('r', Key.R, ModifierKeys.Control));
            Assert.AreEqual(1, host.RedoCount);
        }

        [Test]
        public void Redo2()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, "bar");
            _mode.Process('2');
            _mode.Process(new KeyInput('r', Key.R, ModifierKeys.Control));
            Assert.AreEqual(2, host.RedoCount);
        }

        [Test]
        public void Join1()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.JoinAtCaret(1)).Verifiable();
            _mode.Process("J");
            _operations.Verify();
        }

        [Test]
        public void Join2()
        {
            CreateBuffer("foo", "  bar", "baz");
            _operations.Setup(x => x.JoinAtCaret(2)).Verifiable();
            _mode.Process("2J");
            _operations.Verify();
        }

        [Test]
        public void Join3()
        {
            CreateBuffer("foo", "  bar", "baz");
            _operations.Setup(x => x.JoinAtCaret(3)).Verifiable();
            _mode.Process("3J");
            _operations.Verify();
        }

        [Test]
        public void Join4()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.Join(
                _view.Caret.Position.BufferPosition,
                JoinKind.KeepEmptySpaces,
                1))
                .Returns(true)
                .Verifiable();
            _mode.Process("gJ");
            _operations.Verify();
        }

        [Test]
        public void GoToDefinition1()
        {
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            CreateBuffer("foo");
            _operations.Setup(x => x.GoToDefinitionWrapper()).Verifiable();
            _mode.Process(def);
            _operations.Verify();
        }

        [Test]
        public void GoToDefinition2()
        {
            CreateBuffer(s_lines);
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            Assert.IsTrue(_mode.CanProcess(def));
            Assert.IsTrue(_mode.Commands.Contains(def));
        }

        [Test]
        public void Mark1()
        {
            CreateBuffer(s_lines);
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('m')));
            Assert.IsTrue(_mode.Commands.Contains(InputUtil.CharToKeyInput('m')));
        }

        [Test, Description("Once we are in mark mode we can process anything")]
        public void Mark2()
        {
            CreateBuffer(s_lines);
            _mode.Process(InputUtil.CharToKeyInput('m'));
            Assert.IsTrue(_mode.CanProcess(new KeyInput('c', Key.C, ModifierKeys.Control)));
        }

        [Test]
        public void Mark3()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.SetMark(_bufferData.Object, _view.Caret.Position.BufferPosition, 'a')).Returns(Result._unique_Succeeded).Verifiable();
            _mode.Process(InputUtil.CharToKeyInput('m'));
            _mode.Process(InputUtil.CharToKeyInput('a'));
            _operations.Verify();
        }

        [Test, Description("Bad mark should beep")]
        public void Mark4()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _operations.Setup(x => x.SetMark(_bufferData.Object, _view.Caret.Position.BufferPosition, ';')).Returns(Result.NewFailed("foo")).Verifiable();
            _mode.Process(InputUtil.CharToKeyInput('m'));
            _mode.Process(InputUtil.CharToKeyInput(';'));
            Assert.IsTrue(host.BeepCount > 0);
            _operations.Verify();
        }

        [Test]
        public void JumpToMark1()
        {
            CreateBuffer(s_lines);
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('\'')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('`')));
            Assert.IsTrue(_mode.Commands.Contains(InputUtil.CharToKeyInput('\'')));
            Assert.IsTrue(_mode.Commands.Contains(InputUtil.CharToKeyInput('`')));
        }

        [Test]
        public void JumpToMark2()
        {
            CreateBuffer("foobar");
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
            CreateBuffer("foobar");
            _operations
                .Setup(x => x.JumpToMark('a', _bufferData.Object.MarkMap))
                .Returns(Result._unique_Succeeded)
                .Verifiable();
            _mode.Process('`');
            _mode.Process('a');
            _operations.Verify();
        }

        [Test, Description("OnLeave should kill the block caret")]
        public void OnLeave1()
        {
            CreateBuffer(s_lines);
            _blockCaret.HideCount = 0;
            _mode.OnLeave();
            Assert.AreEqual(1, _blockCaret.HideCount);
        }

        [Test]
        public void JumpNext1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.JumpNext(1)).Verifiable();
            _mode.Process(new KeyInput('i', Key.I, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void JumpNext2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.JumpNext(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(new KeyInput('i', Key.I, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void JumpNext3()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.JumpNext(1)).Verifiable();
            _mode.Process(InputUtil.KeyToKeyInput(Key.Tab));
            _operations.Verify();

        }

        [Test]
        public void JumpPrevious1()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.JumpPrevious(1)).Verifiable();
            _mode.Process(new KeyInput('o', Key.O, ModifierKeys.Control));
            _operations.Verify();
        }

        [Test]
        public void JumpPrevious2()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.JumpPrevious(2)).Verifiable();
            _mode.Process('2');
            _mode.Process(new KeyInput('o', Key.O, ModifierKeys.Control));
            _operations.Verify();
        }

        #endregion

        #region Visual Mode

        [Test]
        public void VisualMode1()
        {
            CreateBuffer(s_lines);
            var res = _mode.Process('v');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.VisualCharacter, res.AsSwitchMode().Item);
        }

        [Test]
        public void VisualMode2()
        {
            CreateBuffer(s_lines);
            var res = _mode.Process('V');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.VisualLine, res.AsSwitchMode().Item);
        }

        [Test]
        public void VisualMode3()
        {
            CreateBuffer(s_lines);
            var res = _mode.Process(new KeyInput('q', Key.Q, ModifierKeys.Control));
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.VisualBlock, res.AsSwitchMode().Item);
        }

        #endregion

    }
}
