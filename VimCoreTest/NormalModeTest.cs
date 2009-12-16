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

namespace VimCoreTest
{
    [TestFixture]
    public class NormalModeTest
    {
        private Vim.Modes.Normal.NormalMode _modeRaw;
        private IMode _mode;
        private IWpfTextView _view;
        private IRegisterMap _map;
        private FakeVimHost _host;
        private VimBufferData _bufferData;
        private MockBlockCaret _blockCaret;
        private Mock<IOperations> _operations;

        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        public void CreateBuffer(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _host = new FakeVimHost();
            _map = new RegisterMap();
            _blockCaret = new MockBlockCaret();
            _bufferData = MockFactory.CreateVimBufferData(
                _view,
                "test",
                _host,
                MockFactory.CreateVimData(_map).Object,
                _blockCaret);
            _operations = new Mock<IOperations>(MockBehavior.Strict);
            _modeRaw = new Vim.Modes.Normal.NormalMode(Tuple.Create((IVimBufferData)_bufferData, _operations.Object));
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
            CreateBuffer(s_lines);
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start.Add(2));
            _mode.Process(Key.Enter);
            Assert.AreEqual(1, _host.BeepCount);
            Assert.AreEqual(last.Start.Add(2), _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void Move_l()
        {
            CreateBuffer(s_lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process("l");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Move_l2()
        {
            CreateBuffer(s_lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process("2l");
            Assert.AreEqual(2, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Move_h()
        {
            CreateBuffer(s_lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("h");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test,Description("Make sure that we clear the selection on a motion")]
        public void Move_h2()
        {
            CreateBuffer(s_lines);
            var start = new SnapshotPoint(_view.TextSnapshot, 1);
            _view.Caret.MoveTo(start);
            _view.Selection.Select(new SnapshotSpan(start, 5), false);
            _mode.Process("h");
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
        }

        [Test]
        public void Move_k()
        {
            CreateBuffer(s_lines);
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _mode.Process("k");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        [Test]
        public void Move_j()
        {
            CreateBuffer(s_lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process("j");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        #endregion

        #region Scroll

        [Test]
        public void ScrollUp1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).End);
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.U, ModifierKeys.Control));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        [Test, Description("Don't break at line 0")]
        public void ScrollUp2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.U, ModifierKeys.Control));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        [Test]
        public void ScrollDown1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.D, ModifierKeys.Control));
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        #endregion

        #region Motion

        [Test, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion1()
        {
            CreateBuffer(s_lines);
            _mode.Process("d@");
            Assert.AreNotEqual(String.Empty, _host.Status);
        }

        [Test, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion2()
        {
            CreateBuffer(s_lines);
            _mode.Process("d@aoeuaoeu");
            Assert.AreNotEqual(String.Empty, _host.Status);
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
            CreateBuffer(s_lines);
            _mode.Process("dzzz");
            Assert.IsFalse(String.IsNullOrEmpty(_host.Status));
            _mode.Process(Key.Escape);
            Assert.IsTrue(String.IsNullOrEmpty(_host.Status));
        }

        [Test, Description("Completed motion should reset the status")]
        public void BadMotion5()
        {
            CreateBuffer(s_lines);
            _mode.Process("yaw");
            Assert.IsTrue(String.IsNullOrEmpty(_host.Status));
        }

        [Test]
        public void Motion_l()
        {
            CreateBuffer(s_lines);
            _mode.Process("l");
            var point = _view.Caret.Position.BufferPosition;
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start.Add(1), point);
        }

        [Test]
        public void Motion_2l()
        {
            CreateBuffer(s_lines);
            _mode.Process("2l");
            var point = _view.Caret.Position.BufferPosition;
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start.Add(2), point);
        }

        [Test, Description("Don't crash moving off the end of the buffer")]
        public void Motion_50l()
        {
            CreateBuffer(s_lines);
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _mode.Process("50l");
            var point = _view.Caret.Position.BufferPosition;
            Assert.AreEqual(line.End, point);
        }

        #endregion

        #region Edits

        [Test]
        public void Edit_o_1()
        {
            CreateBuffer("how is", "foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            var res = _mode.Process('o');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            Assert.AreEqual(3, _view.TextSnapshot.Lines.Count());
        }

        [Test, Description("Use o at end of buffer")]
        public void Edit_o_2()
        {
            CreateBuffer("foo", "bar");
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _mode.Process('o');
        }

        [Test, Description("Make sure o will indent if the previous line was indented")]
        public void Edit_o_3()
        {
            CreateBuffer("  foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process('o');
            var point = _view.Caret.Position.VirtualBufferPosition;
            Assert.IsTrue(point.IsInVirtualSpace);
            Assert.AreEqual(2, point.VirtualSpaces);
        }

        [Test]
        public void Edit_O_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.InsertLineAbove()).Verifiable();
            _mode.Process('O');
            _operations.Verify();
        }

        [Test]
        public void Edit_O_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.InsertLineAbove()).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _mode.Process("O");
            _operations.Verify();
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
            CreateBuffer("food");
            _operations.Setup(x => x.ReplaceChar(It.IsAny<KeyInput>(), 200)).Returns(false).Verifiable();
            _mode.Process("200ru");
            Assert.IsTrue(_host.BeepCount > 0);
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

        #endregion

        #region Yank

        [Test]
        public void Yank_yw()
        {
            CreateBuffer("foo");
            _mode.Process("yw");
            Assert.AreEqual("foo", _map.DefaultRegister.StringValue);
        }

        [Test, Description("Yanks in the middle of the word should only get a partial")]
        public void Yank_yw_2()
        {
            CreateBuffer("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("yw");
            Assert.AreEqual("oo ", _map.DefaultRegister.StringValue);
        }

        [Test, Description("Yank word should go to the start of the next word including spaces")]
        public void Yank_yw_3()
        {
            CreateBuffer("foo bar");
            _mode.Process("yw");
            Assert.AreEqual("foo ", _map.DefaultRegister.StringValue);
        }

        [Test, Description("Non-default register")]
        public void Yank_yw_4()
        {
            CreateBuffer("foo bar");
            _mode.Process("\"cyw");
            Assert.AreEqual("foo ", _map.GetRegister('c').StringValue);
        }

        [Test]
        public void Yank_2yw()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("2yw");
            Assert.AreEqual("foo bar ", _map.DefaultRegister.StringValue);
        }

        [Test]
        public void Yank_3yw()
        {
            CreateBuffer("foo bar baz joe");
            _mode.Process("3yw");
            Assert.AreEqual("foo bar baz ", _map.DefaultRegister.StringValue);
        }

        [Test]
        public void Yank_yaw()
        {
            CreateBuffer("foo bar");
            _mode.Process("yaw");
            Assert.AreEqual("foo ", _map.DefaultRegister.StringValue);
        }

        [Test]
        public void Yank_y2w()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("y2w");
            Assert.AreEqual("foo bar ", _map.DefaultRegister.StringValue);
        }

        [Test]
        public void Yank_yaw_2()
        {
            CreateBuffer("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("yaw");
            Assert.AreEqual("foo ", _map.DefaultRegister.StringValue);
        }

        [Test, Description("A yy should grab the end of line including line break information")]
        public void Yank_yy_1()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("yy");
            Assert.AreEqual("foo" + Environment.NewLine, _map.DefaultRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, _map.DefaultRegister.Value.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, _map.DefaultRegister.Value.MotionKind);
        }

        [Test, Description("yy should yank the entire line even if the cursor is not at the start")]
        public void Yank_yy_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("yy");
            Assert.AreEqual("foo" + Environment.NewLine, _map.DefaultRegister.StringValue);
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
            CreateBuffer("foo", "bar","jazz");
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
            _operations.Setup(x => x.PasteAfter("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process('p');
            _operations.Verify();
        }

        [Test, Description("Paste from a non-default register")]
        public void Paste_p_2()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteAfter("hey", 1, OperationKind.CharacterWise, false)).Verifiable();
            _map.GetRegister('j').UpdateValue("hey");
            _mode.Process("\"jp");
            _operations.Verify();
        }

        [Test, Description("Pasting a linewise motion should occur on the next line")]
        public void Paste_p_3()
        {
            CreateBuffer("foo", "bar");
            var data = "baz" + Environment.NewLine;
            _operations.Setup(x => x.PasteAfter(data, 1, OperationKind.LineWise, false)).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map.DefaultRegister.UpdateValue(new RegisterValue(data, MotionKind.Inclusive, OperationKind.LineWise));
            _mode.Process("p");
            _operations.Verify();
        }

        [Test]
        public void Paste_2p()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteAfter("hey", 2, OperationKind.CharacterWise, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("2p");
            _operations.Verify();
        }

        [Test]
        public void Paste_P()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteBefore("hey", 1, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process('P');
            _operations.Verify();
        }

        [Test]
        public void Paste_2P()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteBefore("hey", 2, false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("2P");
            _operations.Verify();
        }

        [Test]
        public void Paste_gp_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteAfter("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("gp");
            _operations.Verify();
        }

        [Test]
        public void Paste_gp_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.PasteAfter("hey", 1, OperationKind.CharacterWise, true)).Verifiable();
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _map.GetRegister('c').UpdateValue("hey");
            _mode.Process("\"cgp");
            _operations.Verify();
        }

        [Test]
        public void Paste_gP_1()
        {
            CreateBuffer("foo");
            _operations.Setup(x => x.PasteBefore("hey", 1, true)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("gP");
            _operations.Verify();
        }

        [Test]
        public void Paste_gP_2()
        {
            CreateBuffer("foo", "bar");
            _operations.Setup(x => x.PasteBefore("hey", 1, true)).Verifiable();
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
            _mode.Process("dd");
            var value = _map.DefaultRegister.Value;
            Assert.AreEqual("foo" + Environment.NewLine, value.Value);
            Assert.AreEqual(MotionKind.Inclusive, value.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, value.OperationKind);
        }

        [Test, Description("Make sure that it deletes the entire line regardless of where the caret is")]
        public void Delete_dd_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("dd");
            var value = _map.DefaultRegister.Value;
            Assert.AreEqual("foo" + Environment.NewLine, value.Value);
            Assert.AreEqual(MotionKind.Inclusive, value.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, value.OperationKind);
        }

        [Test]
        public void Delete_dw_1()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("dw");
            Assert.AreEqual("bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Delete at the end of the line shouldn't delete newline")]
        public void Delete_dw_2()
        {
            CreateBuffer("foo bar","baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            _mode.Process("dw");
            var tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo ", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(1).GetText());
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
            res = _mode.Process(InputUtil.KeyToKeyInput(Key.H));
            Assert.IsTrue(res.IsProcessed);
        }

        [Test, Description("j past the end of the buffer")]
        public void Regression_DownPastBufferEnd()
        {
            CreateBuffer("foo");
            var res = _mode.Process(Key.J);
            Assert.IsTrue(res.IsProcessed);
            res = _mode.Process(Key.J);
            Assert.IsTrue(res.IsProcessed);
        }

        #endregion

        #region Incremental Search

        [Test]
        public void Search1()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(3, sel.Span.Length);
            Assert.AreEqual("bar", sel.GetText());
        }

        [Test]
        public void Search2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/foo");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(3, sel.Length);
            Assert.AreEqual("foo", sel.GetText());
        }

        [Test, Description("Make sure it matches the first occurance")]
        public void Search3()
        {
            CreateBuffer("foo bar bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual("bar", sel.GetText());
        }

        [Test, Description("No match should select nothing")]
        public void Search4()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("/q");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(0, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("A partial match followed by a bad match should go back to start")]
        public void Search5()
        {
            CreateBuffer("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("/bq");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(1, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("Search accross lines")]
        public void Search6()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual("bar", sel.GetText());
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void SearchNext1()
        {
            CreateBuffer("foo bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("Don't start at current position")]
        public void SearchNext2()
        {
            CreateBuffer("bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("Don't skip the current word just the current letter")]
        public void SearchNext3()
        {
            CreateBuffer("bbar, baz");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Counted next")]
        public void SearchNext4()
        {
            CreateBuffer(" bar bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("3n");
            Assert.AreEqual(9, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Make sure enter sets the search")]
        public void SearchNext5()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("/ba");
            _mode.Process(Key.Enter);
            _mode.Process("n");
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void SearchReverse1()
        {
            CreateBuffer("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _mode.Process("?bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual("bar", sel.GetText());
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Change nothing on invalid searh")]
        public void SearchReverse2()
        {
            CreateBuffer("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _mode.Process("?invalid");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(line.End, sel.Start);
            Assert.AreEqual(0, sel.Length);
            Assert.AreEqual(line.End, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void SearchNextReverse1()
        {
            CreateBuffer("bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar", SearchKind.Backward));
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _mode.Process("n");
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
        }

        [Test]
        public void SearchStatus1()
        {
            CreateBuffer(s_lines);
            _mode.Process("/");
            Assert.AreEqual("/", _host.Status);
        }

        [Test]
        public void SearchStatus2()
        {
            CreateBuffer(s_lines);
            _mode.Process("/zzz");
            Assert.AreEqual("/zzz", _host.Status);
        }

        [Test]
        public void SearchBackspace1()
        {
            CreateBuffer("foo bar");
            _mode.Process("/fooh");
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
            _mode.Process(Key.Back);
            Assert.AreEqual(3, _view.Selection.SelectedSpans.Single().Length);
            Assert.AreEqual("foo", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test]
        public void SearchBackspace2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bb");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _mode.Process(Key.Back);
            Assert.AreEqual("b", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test, Description("Completely exit from the search")]
        public void SearchBackspace3()
        {
            CreateBuffer("foo bar");
            _mode.Process("/b");
            _mode.Process(Key.Back);
            _mode.Process(Key.Back);
            var res = _mode.Process('i');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        #endregion

        #region Next / Previous Word

        [Test]
        public void NextWord1()
        {
            CreateBuffer(" ");
            _mode.Process("*");
            Assert.AreEqual("No word under cursor", _host.Status);
        }

        [Test, Description("No matches should have no effect")]
        public void NextWord2()
        {
            CreateBuffer("foo bar");
            _mode.Process("*");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void NextWord3()
        {
            CreateBuffer("foo foo");
            _mode.Process("*");
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void NextWord4()
        {
            CreateBuffer("foo bar", "foo");
            _mode.Process("*");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't start on position 0")]
        public void NextWord5()
        {
            CreateBuffer("foo bar", "foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            _mode.Process("*");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start.Add(4), _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void PreviousWord1()
        {
            CreateBuffer("");
            _mode.Process("#");
            Assert.AreEqual("No word under cursor", _host.Status);
        }

        [Test]
        public void PreviousWord2()
        {
            CreateBuffer("foo bar");
            _mode.Process("#");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void PreviousWord3()
        {
            CreateBuffer("foo bar", "foo");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _mode.Process("#");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void PreviousWord4()
        {
            CreateBuffer("foo bar", "foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start.Add(4));
            _mode.Process("#");
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        #endregion

        #region Shift

        [Test]
        public void ShiftRight1()
        {
            CreateBuffer("foo");
            _mode.Process(">>");
            Assert.AreEqual("    foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("With a count")]
        public void ShiftRight2()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("2>>");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("    foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("    bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("With a motion")]
        public void ShiftRight3()
        {
            CreateBuffer("foo", "bar");
            _mode.Process(">j");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("    foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("    bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Make sure a normal >> doesn't shift 2 lines")]
        public void ShiftRight4()
        {
            CreateBuffer("foo", "bar");
            _mode.Process(">>");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("    foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Don't eat extra whitespace")]
        public void ShiftLeft1()
        {
            CreateBuffer("foo");
            _mode.Process("<<");
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLeft2()
        {
            CreateBuffer(" foo");
            _mode.Process("<<");
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLeft3()
        {
            CreateBuffer("     foo");
            _mode.Process("<<");
            Assert.AreEqual(" foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("With a count")]
        public void ShiftLeft4()
        {
            CreateBuffer("     foo", "     bar");
            _mode.Process("2<<");
            var tss = _view.TextSnapshot;
            Assert.AreEqual(" foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(" bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Make sure a << doesn't shift more than 1 line")]
        public void ShiftLeft5()
        {
            CreateBuffer(" foo", " bar");
            _mode.Process("<<");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(" bar", tss.GetLineFromLineNumber(1).GetText());
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
            CreateBuffer("foo");
            Assert.AreEqual(0, _host.UndoCount);
            _mode.Process("u");
            Assert.AreEqual(1, _host.UndoCount);
        }

        [Test]
        public void Undo2()
        {
            CreateBuffer("foo");
            _mode.Process("2u");
            Assert.AreEqual(2, _host.UndoCount);
        }

        [Test]
        public void Join1()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("J");
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Join2()
        {
            CreateBuffer("foo", "  bar", "baz");
            _mode.Process("2J");
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void Join3()
        {
            CreateBuffer("foo", "  bar", "baz");
            _mode.Process("3J");
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void Join4()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("gJ");
            Assert.AreEqual("foobar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());   
        }

        [Test]
        public void GoToDefinition1()
        {
            CreateBuffer(s_lines);
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            CreateBuffer("foo");
            _mode.Process(def);
            Assert.AreEqual(1, _host.GoToDefinitionCount);
        }

        [Test, Description("When it fails, the status should be updated")]
        public void GoToDefinition2()
        {
            CreateBuffer("foo");
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            _host.GoToDefinitionReturn = false;
            _mode.Process(def);
            Assert.AreEqual(1, _host.GoToDefinitionCount);
            Assert.IsTrue(_host.Status.Contains("foo"));
        }

        [Test]
        public void GoToDefinition3()
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
            _operations.Setup(x => x.SetMark('a', _bufferData._vimData.MarkMap)).Returns(Result._unique_Succeeded).Verifiable();
            _mode.Process(InputUtil.CharToKeyInput('m'));
            _mode.Process(InputUtil.CharToKeyInput('a'));
            _operations.Verify();
        }

        [Test, Description("Bad mark should beep")]
        public void Mark4()
        {
            CreateBuffer(s_lines);
            _operations.Setup(x => x.SetMark(';', _bufferData._vimData.MarkMap)).Returns(Result.NewFailed("foo")).Verifiable();
            _mode.Process(InputUtil.CharToKeyInput('m'));
            _mode.Process(InputUtil.CharToKeyInput(';'));
            Assert.IsTrue(_host.BeepCount > 0);
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
                .Setup(x => x.JumpToMark('a', _bufferData._vimData.MarkMap))
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
                .Setup(x => x.JumpToMark('a', _bufferData._vimData.MarkMap))
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
        
        #endregion

    }
}
