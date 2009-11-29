using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    [TestClass]
    public class NormalModeTest
    {
        private VimCore.Modes.Normal.NormalMode _modeRaw;
        private IMode _mode;
        private IWpfTextView _view;
        private IRegisterMap _map;
        private FakeVimHost _host;
        private VimBufferData _bufferData;

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
            _bufferData = MockFactory.CreateVimBufferData(
                _view,
                "test",
                _host,
                MockFactory.CreateVimData(_map).Object);
            _modeRaw = new VimCore.Modes.Normal.NormalMode(_bufferData);
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        [TestInitialize]
        public void TestInit()
        {
            CreateBuffer(s_lines);
        }

        [TestMethod]
        public void ModeKindTest()
        {
            Assert.AreEqual(ModeKind.Normal, _mode.ModeKind);
        }

        [TestMethod, Description("Let enter go straight back to the editor in the default case")]
        public void EnterProcessing()
        {
            var can = _mode.CanProcess(InputUtil.KeyToKeyInput(Key.Enter));
            Assert.IsTrue(can);
        }

        #region CanProcess

        [TestMethod, Description("Can process basic commands")]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('u')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('h')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('j')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('i')));
        }

        [TestMethod, Description("Cannot process invalid commands")]
        public void CanProcess2()
        {
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('U')));
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('Z')));
        }

        [TestMethod, Description("Must be able to process numbers")]
        public void CanProcess3()
        {
            foreach (var cur in Enumerable.Range(1, 8))
            {
                var c = char.Parse(cur.ToString());
                var ki = InputUtil.CharToKeyInput(c);
                Assert.IsTrue(_mode.CanProcess(ki));
            }
        }

        [TestMethod, Description("When in a need more state, process everything")]
        public void CanProcess4()
        {
            _mode.Process(InputUtil.CharToKeyInput('/'));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('U')));
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('Z')));
        }

        #endregion

        #region Movement

        [TestMethod, Description("Enter should move down on line")]
        public void Enter1()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process(Key.Enter);
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [TestMethod, Description("Enter at end of file should beep ")]
        public void Enter2()
        {
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start.Add(2));
            _mode.Process(Key.Enter);
            Assert.AreEqual(1, _host.BeepCount);
            Assert.AreEqual(last.Start.Add(2), _view.Caret.Position.BufferPosition);
        }

        [TestMethod]
        public void Move_l()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process("l");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void Move_l2()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process("2l");
            Assert.AreEqual(2, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void Move_h()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("h");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod,Description("Make sure that we clear the selection on a motion")]
        public void Move_h2()
        {
            var start = new SnapshotPoint(_view.TextSnapshot, 1);
            _view.Caret.MoveTo(start);
            _view.Selection.Select(new SnapshotSpan(start, 5), false);
            _mode.Process("h");
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
        }

        [TestMethod]
        public void Move_k()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _mode.Process("k");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        [TestMethod]
        public void Move_j()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process("j");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        #endregion

        #region Scroll

        [TestMethod]
        public void ScrollUp1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).End);
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.U, ModifierKeys.Control));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        [TestMethod, Description("Don't break at line 0")]
        public void ScrollUp2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.U, ModifierKeys.Control));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        [TestMethod]
        public void ScrollDown1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _mode.Process(InputUtil.KeyAndModifierToKeyInput(Key.D, ModifierKeys.Control));
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
        }

        #endregion

        #region Motion

        [TestMethod, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion1()
        {
            _mode.Process("d@");
            Assert.AreNotEqual(String.Empty, _host.Status);
        }

        [TestMethod, Description("Typing in invalid motion should produce a warning")]
        public void BadMotion2()
        {
            _mode.Process("d@aoeuaoeu");
            Assert.AreNotEqual(String.Empty, _host.Status);
        }

        [TestMethod, Description("Enter must cancel an invalid motion")]
        public void BadMotion3()
        {
            _mode.Process("d@");
            var res = _mode.Process(Key.I);
            Assert.IsTrue(res.IsProcessed);
            _mode.Process(Key.Enter);
            res = _mode.Process(Key.I);
            Assert.IsTrue(res.IsSwitchMode);  
        }

        [TestMethod, Description("Canceled motion should reset the status")]
        public void BadMotion4()
        {
            _mode.Process("dzzz");
            Assert.IsFalse(String.IsNullOrEmpty(_host.Status));
            _mode.Process(Key.Escape);
            Assert.IsTrue(String.IsNullOrEmpty(_host.Status));
        }

        [TestMethod, Description("Completed motion should reset the status")]
        public void BadMotion5()
        {
            _mode.Process("yaw");
            Assert.IsTrue(String.IsNullOrEmpty(_host.Status));
        }

        [TestMethod]
        public void Motion_l()
        {
            _mode.Process("l");
            var point = _view.Caret.Position.BufferPosition;
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start.Add(1), point);
        }

        [TestMethod]
        public void Motion_2l()
        {
            _mode.Process("2l");
            var point = _view.Caret.Position.BufferPosition;
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start.Add(2), point);
        }

        [TestMethod, Description("Don't crash moving off the end of the buffer")]
        public void Motion_50l()
        {
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _mode.Process("50l");
            var point = _view.Caret.Position.BufferPosition;
            Assert.AreEqual(line.End, point);
        }

        #endregion

        #region Edits

        [TestMethod]
        public void Edit_o_1()
        {
            CreateBuffer("how is", "foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            var res = _mode.Process('o');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            Assert.AreEqual(3, _view.TextSnapshot.Lines.Count());
        }

        [TestMethod, Description("Use o at end of buffer")]
        public void Edit_o_2()
        {
            CreateBuffer("foo", "bar");
            var line = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(line.Start);
            _mode.Process('o');
        }

        [TestMethod, Description("Make sure o will indent if the previous line was indented")]
        public void Edit_o_3()
        {
            CreateBuffer("  foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _mode.Process('o');
            var point = _view.Caret.Position.VirtualBufferPosition;
            Assert.IsTrue(point.IsInVirtualSpace);
            Assert.AreEqual(2, point.VirtualSpaces);
        }

        [TestMethod]
        public void Edit_X_1()
        {
            CreateBuffer("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("X");
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("Don't delete past the current line")]
        public void Edit_X_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _mode.Process("X");
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void Edit_2X_1()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            _mode.Process("2X");
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void Edit_2X_2()
        {
            CreateBuffer("foo");
            _mode.Process("2X");
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        #endregion

        #region Yank

        [TestMethod]
        public void Yank_yw()
        {
            CreateBuffer("foo");
            _mode.Process("yw");
            Assert.AreEqual("foo", _map.DefaultRegister.StringValue);
        }

        [TestMethod, Description("Yanks in the middle of the word should only get a partial")]
        public void Yank_yw_2()
        {
            CreateBuffer("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("yw");
            Assert.AreEqual("oo ", _map.DefaultRegister.StringValue);
        }

        [TestMethod, Description("Yank word should go to the start of the next word including spaces")]
        public void Yank_yw_3()
        {
            CreateBuffer("foo bar");
            _mode.Process("yw");
            Assert.AreEqual("foo ", _map.DefaultRegister.StringValue);
        }

        [TestMethod, Description("Non-default register")]
        public void Yank_yw_4()
        {
            CreateBuffer("foo bar");
            _mode.Process("\"cyw");
            Assert.AreEqual("foo ", _map.GetRegister('c').StringValue);
        }

        [TestMethod]
        public void Yank_2yw()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("2yw");
            Assert.AreEqual("foo bar ", _map.DefaultRegister.StringValue);
        }

        [TestMethod]
        public void Yank_3yw()
        {
            CreateBuffer("foo bar baz joe");
            _mode.Process("3yw");
            Assert.AreEqual("foo bar baz ", _map.DefaultRegister.StringValue);
        }

        [TestMethod]
        public void Yank_yaw()
        {
            CreateBuffer("foo bar");
            _mode.Process("yaw");
            Assert.AreEqual("foo ", _map.DefaultRegister.StringValue);
        }

        [TestMethod]
        public void Yank_y2w()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("y2w");
            Assert.AreEqual("foo bar ", _map.DefaultRegister.StringValue);
        }

        [TestMethod]
        public void Yank_yaw_2()
        {
            CreateBuffer("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("yaw");
            Assert.AreEqual("foo ", _map.DefaultRegister.StringValue);
        }

        [TestMethod, Description("A yy should grab the end of line including line break information")]
        public void Yank_yy_1()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("yy");
            Assert.AreEqual("foo" + Environment.NewLine, _map.DefaultRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, _map.DefaultRegister.Value.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, _map.DefaultRegister.Value.MotionKind);
        }

        [TestMethod, Description("yy should yank the entire line even if the cursor is not at the start")]
        public void Yank_yy_2()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("yy");
            Assert.AreEqual("foo" + Environment.NewLine, _map.DefaultRegister.StringValue);
        }

        #endregion

        #region Paste

        [TestMethod]
        public void Paste_p()
        {
            CreateBuffer("foo bar");
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process('p');
            Assert.AreEqual("fheyoo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("Paste from a non-default register")]
        public void Paste_p_2()
        {
            CreateBuffer("foo");
            _map.GetRegister('j').UpdateValue("hey");
            _mode.Process("\"jp");
            Assert.AreEqual("fheyoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("Paste at end of buffer shouldn't crash")]
        public void Paste_p_3()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(TssUtil.GetEndPoint(_view.TextSnapshot));
            _map.DefaultRegister.UpdateValue("hello");
            _mode.Process("p");
            Assert.AreEqual("barhello", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod]
        public void Paste_p_4()
        {
            CreateBuffer("foo", String.Empty);
            _view.Caret.MoveTo(TssUtil.GetEndPoint(_view.TextSnapshot));
            _map.DefaultRegister.UpdateValue("bar");
            _mode.Process("p");
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod, Description("Pasting a linewise motion should occur on the next line")]
        public void Paste_p_5()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map.DefaultRegister.UpdateValue(new RegisterValue("baz" + Environment.NewLine, MotionKind.Inclusive, OperationKind.LineWise));
            _mode.Process("p");
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("baz", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod, Description("Pasting a linewise motion should move the caret to the start of the next line")]
        public void Paste_p_6()
        {
            CreateBuffer("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map.DefaultRegister.UpdateValue(new RegisterValue("baz" + Environment.NewLine, MotionKind.Inclusive, OperationKind.LineWise));
            _mode.Process("p");
            var pos = _view.Caret.Position.BufferPosition;
            Assert.AreEqual(pos, _view.TextSnapshot.GetLineFromLineNumber(1).Start);
        }

        [TestMethod]
        public void Paste_2p()
        {
            CreateBuffer("foo");
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("2p");
            Assert.AreEqual("fheyheyoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void Paste_P()
        {
            CreateBuffer("foo");
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process('P');
            Assert.AreEqual("heyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void Paste_2P()
        {
            CreateBuffer("foo");
            _map.DefaultRegister.UpdateValue("hey");
            _mode.Process("2P");
            Assert.AreEqual("heyheyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        #endregion

        #region Delete

        [TestMethod, Description("Make sure a dd is a linewise action")]
        public void Delete_dd_1()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("dd");
            var value = _map.DefaultRegister.Value;
            Assert.AreEqual("foo" + Environment.NewLine, value.Value);
            Assert.AreEqual(MotionKind.Inclusive, value.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, value.OperationKind);
        }

        [TestMethod, Description("Make sure that it deletes the entire line regardless of where the caret is")]
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
        

        #endregion

        #region Regressions

        [TestMethod, Description("Don't re-enter insert mode on every keystroke once you've left")]
        public void Regression_InsertMode()
        {
            var res = _mode.Process(InputUtil.KeyToKeyInput(Key.I));
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            res = _mode.Process(InputUtil.KeyToKeyInput(Key.H));
            Assert.IsTrue(res.IsProcessed);
        }

        [TestMethod, Description("j past the end of the buffer")]
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

        [TestMethod]
        public void Search1()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(3, sel.Span.Length);
            Assert.AreEqual("bar", sel.GetText());
        }

        [TestMethod]
        public void Search2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/foo");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(3, sel.Length);
            Assert.AreEqual("foo", sel.GetText());
        }

        [TestMethod, Description("Make sure it matches the first occurance")]
        public void Search3()
        {
            CreateBuffer("foo bar bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual("bar", sel.GetText());
        }

        [TestMethod, Description("No match should select nothing")]
        public void Search4()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("/q");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(0, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [TestMethod, Description("A partial match followed by a bad match should go back to start")]
        public void Search5()
        {
            CreateBuffer("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("/bq");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(1, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [TestMethod, Description("Search accross lines")]
        public void Search6()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual("bar", sel.GetText());
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [TestMethod]
        public void SearchNext1()
        {
            CreateBuffer("foo bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [TestMethod, Description("Don't start at current position")]
        public void SearchNext2()
        {
            CreateBuffer("bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [TestMethod, Description("Don't skip the current word just the current letter")]
        public void SearchNext3()
        {
            CreateBuffer("bbar, baz");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Counted next")]
        public void SearchNext4()
        {
            CreateBuffer(" bar bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("3n");
            Assert.AreEqual(9, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Make sure enter sets the search")]
        public void SearchNext5()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("/ba");
            _mode.Process(Key.Enter);
            _mode.Process("n");
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
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

        [TestMethod, Description("Change nothing on invalid searh")]
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

        [TestMethod]
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

        [TestMethod]
        public void SearchStatus1()
        {
            _mode.Process("/");
            Assert.AreEqual("/", _host.Status);
        }

        [TestMethod]
        public void SearchStatus2()
        {
            _mode.Process("/zzz");
            Assert.AreEqual("/zzz", _host.Status);
        }

        [TestMethod]
        public void SearchBackspace1()
        {
            CreateBuffer("foo bar");
            _mode.Process("/fooh");
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
            _mode.Process(Key.Back);
            Assert.AreEqual(3, _view.Selection.SelectedSpans.Single().Length);
            Assert.AreEqual("foo", _view.Selection.SelectedSpans.Single().GetText());
        }

        [TestMethod]
        public void SearchBackspace2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bb");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _mode.Process(Key.Back);
            Assert.AreEqual("b", _view.Selection.SelectedSpans.Single().GetText());
        }

        [TestMethod, Description("Completely exit from the search")]
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

        [TestMethod]
        public void NextWord1()
        {
            CreateBuffer(" ");
            _mode.Process("*");
            Assert.AreEqual("No word under cursor", _host.Status);
        }

        [TestMethod, Description("No matches should have no effect")]
        public void NextWord2()
        {
            CreateBuffer("foo bar");
            _mode.Process("*");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void NextWord3()
        {
            CreateBuffer("foo foo");
            _mode.Process("*");
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void NextWord4()
        {
            CreateBuffer("foo bar", "foo");
            _mode.Process("*");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [TestMethod, Description("Don't start on position 0")]
        public void NextWord5()
        {
            CreateBuffer("foo bar", "foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            _mode.Process("*");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start.Add(4), _view.Caret.Position.BufferPosition);
        }

        [TestMethod]
        public void PreviousWord1()
        {
            CreateBuffer("");
            _mode.Process("#");
            Assert.AreEqual("No word under cursor", _host.Status);
        }

        [TestMethod]
        public void PreviousWord2()
        {
            CreateBuffer("foo bar");
            _mode.Process("#");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void PreviousWord3()
        {
            CreateBuffer("foo bar", "foo");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _mode.Process("#");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
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

        [TestMethod]
        public void ShiftRight1()
        {
            CreateBuffer("foo");
            _mode.Process(">>");
            Assert.AreEqual("    foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("With a count")]
        public void ShiftRight2()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("2>>");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("    foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("    bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod, Description("With a motion")]
        public void ShiftRight3()
        {
            CreateBuffer("foo", "bar");
            _mode.Process(">j");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("    foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("    bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod, Description("Make sure a normal >> doesn't shift 2 lines")]
        public void ShiftRight4()
        {
            CreateBuffer("foo", "bar");
            _mode.Process(">>");
            var tss = _view.TextSnapshot;
            Assert.AreEqual("    foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod, Description("Don't eat extra whitespace")]
        public void ShiftLeft1()
        {
            CreateBuffer("foo");
            _mode.Process("<<");
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void ShiftLeft2()
        {
            CreateBuffer(" foo");
            _mode.Process("<<");
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void ShiftLeft3()
        {
            CreateBuffer("     foo");
            _mode.Process("<<");
            Assert.AreEqual(" foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("With a count")]
        public void ShiftLeft4()
        {
            CreateBuffer("     foo", "     bar");
            _mode.Process("2<<");
            var tss = _view.TextSnapshot;
            Assert.AreEqual(" foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(" bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [TestMethod, Description("Make sure a << doesn't shift more than 1 line")]
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

        [TestMethod]
        public void Register1()
        {
            CreateBuffer("foo");
            Assert.AreEqual('_', _modeRaw.Register.Name);
            _mode.Process("\"c");
            Assert.AreEqual('c', _modeRaw.Register.Name);
        }

        [TestMethod]
        public void Undo1()
        {
            CreateBuffer("foo");
            Assert.AreEqual(0, _host.UndoCount);
            _mode.Process("u");
            Assert.AreEqual(1, _host.UndoCount);
        }

        [TestMethod]
        public void Undo2()
        {
            CreateBuffer("foo");
            _mode.Process("2u");
            Assert.AreEqual(2, _host.UndoCount);
        }

        [TestMethod]
        public void Join1()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("J");
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void Join2()
        {
            CreateBuffer("foo", "  bar", "baz");
            _mode.Process("2J");
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void Join3()
        {
            CreateBuffer("foo", "  bar", "baz");
            _mode.Process("3J");
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
        public void GoToDefinition1()
        {
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            CreateBuffer("foo");
            _mode.Process(def);
            Assert.AreEqual(1, _host.GoToDefinitionCount);
        }

        [TestMethod, Description("When it fails, the status should be updated")]
        public void GoToDefinition2()
        {
            CreateBuffer("foo");
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            _host.GoToDefinitionReturn = false;
            _mode.Process(def);
            Assert.AreEqual(1, _host.GoToDefinitionCount);
            Assert.IsTrue(_host.Status.Contains("foo"));
        }

        [TestMethod]
        public void GoToDefinition3()
        {
            var def = new KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
            Assert.IsTrue(_mode.CanProcess(def));
            Assert.IsTrue(_mode.Commands.Contains(def));
        }

        [TestMethod]
        public void Mark1()
        {
            Assert.IsTrue(_mode.CanProcess(InputUtil.CharToKeyInput('m')));
            Assert.IsTrue(_mode.Commands.Contains(InputUtil.CharToKeyInput('m')));
        }

        [TestMethod, Description("Once we are in mark mode we can process anything")]
        public void Mark2()
        {
            _mode.Process(InputUtil.CharToKeyInput('m'));
            Assert.IsTrue(_mode.CanProcess(new KeyInput('c', Key.C, ModifierKeys.Control)));
        }

        [TestMethod]
        public void Mark3()
        {
            _mode.Process(InputUtil.CharToKeyInput('m'));
            _mode.Process(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(_bufferData._vimData.MarkMap.GetLocalMark(_view.TextBuffer, 'a').IsSome());
        }

        [TestMethod, Description("Bad mark should beep")]
        public void Mark4()
        {
            _mode.Process(InputUtil.CharToKeyInput('m'));
            _mode.Process(InputUtil.CharToKeyInput(';'));
            Assert.IsTrue(_host.BeepCount > 0);
        }
        
        #endregion

    }
}
