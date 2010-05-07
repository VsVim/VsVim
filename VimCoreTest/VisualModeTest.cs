using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.Modes.Visual;
using Moq;
using Microsoft.VisualStudio.Text.Operations;
using Vim.Modes;
using VimCore.Test.Utils;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using VimCore.Test.Mock;
using Microsoft.FSharp.Core;

namespace VimCore.Test
{
    [TestFixture]
    public class VisualModeTest
    {
        private Mock<IWpfTextView> _view;
        private Mock<ITextCaret> _caret;
        private Mock<ITextSelection> _selection;
        private ITextBuffer _buffer;
        private Mock<IVimBuffer> _bufferData;
        private VisualMode _modeRaw;
        private IMode _mode;
        private IRegisterMap _map;
        private Mock<IOperations> _operations;
        private Mock<ISelectionTracker> _tracker;

        public void Create(params string[] lines)
        {
            Create2(lines: lines);
        }

        public void Create2(
            ModeKind kind=ModeKind.VisualCharacter, 
            params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
            _caret = new Mock<ITextCaret>(MockBehavior.Strict);
            _view = new Mock<IWpfTextView>(MockBehavior.Strict);
            _selection = new Mock<ITextSelection>(MockBehavior.Strict);
            _view.SetupGet(x => x.Caret).Returns(_caret.Object);
            _view.SetupGet(x => x.Selection).Returns(_selection.Object);
            _view.SetupGet(x => x.TextBuffer).Returns(_buffer);
            _view.SetupGet(x => x.TextSnapshot).Returns(() => _buffer.CurrentSnapshot);
            _map = new RegisterMap();
            _tracker = new Mock<ISelectionTracker>(MockBehavior.Strict);
            _tracker.Setup(x => x.Start());
            _operations = new Mock<IOperations>(MockBehavior.Strict);
            _operations.SetupGet(x => x.SelectionTracker).Returns(_tracker.Object);
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view.Object,
                "test",
                MockObjectFactory.CreateVim(_map).Object);
            var capture = new MotionCapture(_view.Object, new MotionUtil(_view.Object, _bufferData.Object.Settings.GlobalSettings));
            var runner = new CommandRunner(Tuple.Create((ITextView)_view.Object, _map, (IMotionCapture)capture, (new Mock<IStatusUtil>()).Object));
            _modeRaw = new Vim.Modes.Visual.VisualMode(_bufferData.Object, _operations.Object, kind, runner, capture);
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        public void SetupApplyAsSingleEdit()
        {
            _operations
                .Setup(x => x.ApplyAsSingleEdit(
                                It.IsAny<FSharpOption<string>>(),
                                It.IsAny<IEnumerable<SnapshotSpan>>(),
                                It.IsAny<FSharpFunc<SnapshotSpan, Unit>>()))
                .Callback<FSharpOption<string>,IEnumerable<SnapshotSpan>, FSharpFunc<SnapshotSpan,Unit>>((unused, spans, func) =>
                {
                    foreach (var span in spans)
                    {
                        func.Invoke(span);
                    }
                })
                .Verifiable();
        }

        [Test,Description("Movement commands")]
        public void Commands1()
        {
            Create("foo");
            var list = new KeyInput[] {
                InputUtil.CharToKeyInput('h'),
                InputUtil.CharToKeyInput('j'),
                InputUtil.CharToKeyInput('k'),
                InputUtil.CharToKeyInput('l'),
                InputUtil.VimKeyToKeyInput(VimKey.LeftKey),
                InputUtil.VimKeyToKeyInput(VimKey.RightKey),
                InputUtil.VimKeyToKeyInput(VimKey.UpKey),
                InputUtil.VimKeyToKeyInput(VimKey.DownKey),
                InputUtil.VimKeyToKeyInput(VimKey.BackKey) };
            var commands = _mode.CommandNames.ToList();
            foreach (var item in list)
            {
                var name = CommandName.NewOneKeyInput(item);
                Assert.Contains(name, commands);
            }
        }

        [Test]
        public void Process1()
        {
            Create("foo");
            var res = _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test, Description("Escape should always escape even if we're processing an inner key sequence")]
        public void Process2()
        {
            Create("foo");
            _mode.Process('g');
            var res = _mode.Process(VimKey.EscapeKey);
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test]
        public void OnLeave1()
        {
            _tracker.Setup(x => x.Stop()).Verifiable();
            _mode.OnLeave();
            _tracker.Verify();
        }

        [Test]
        public void InExplicitMove1()
        {
            Create("foo");
            _modeRaw.BeginExplicitMove();
            Assert.IsTrue(_modeRaw.InExplicitMove);
        }

        [Test]
        public void InExplicitMove2()
        {
            Create("");
            Assert.IsFalse(_modeRaw.InExplicitMove);
            _modeRaw.BeginExplicitMove();
            _modeRaw.BeginExplicitMove();
            _modeRaw.EndExplicitMove();
            _modeRaw.EndExplicitMove();
            Assert.IsFalse(_modeRaw.InExplicitMove);
        }

        [Test,Description("Must handle arbitrary input to prevent changes but don't list it as a command")]
        public void PreventInput1()
        {
            Create(lines:"foo");
            var input = InputUtil.CharToKeyInput(',');
            _operations.Setup(x => x.Beep()).Verifiable();
            Assert.IsFalse(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == input.Char));
            Assert.IsTrue(_mode.CanProcess(input));
            var ret = _mode.Process(input);
            Assert.IsTrue(ret.IsProcessed);
            _operations.Verify();
        }

        #region Movement

        public void MoveLeft1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.Process('h');
            _operations.Verify();
        }

        public void MoveWordLeft1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.MoveWordForward(WordKind.NormalWord,1)).Verifiable();
            _mode.Process('w');
            _operations.Verify();
        }

        public void MoveDollar1()
        {
            Create("foo", "bar");
            var editOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            editOpts.Setup(x => x.MoveToEndOfLine(false)).Verifiable();
            _operations.Setup(x => x.EditorOperations).Returns(editOpts.Object);
            _mode.Process('$');
            editOpts.Verify();
        }

        public void MoveEnter1()
        {
            Create("foo bar");
            _operations.Setup(x => x.MoveCaretDownToFirstNonWhitespaceCharacter(1)).Verifiable();
            _mode.Process(VimKey.EnterKey);
            _operations.Verify();
        }

        #endregion

        #region Operations

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            Assert.IsTrue(_mode.Process('y').IsSwitchPreviousMode);
            _operations.Verify();
            _tracker.Verify();
        }

        [Test, Description("Yank should go back to normal mode")]
        public void Yank2()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process('y');
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.GetRegister('c'))).Verifiable();
            _mode.Process("\"cy");
            _operations.Verify();
        }

        [Test]
        public void YankLines1()
        {
            Create("foo","bar");
            var tss = _buffer.CurrentSnapshot;
            var line = tss.GetLineFromLineNumber(0);
            _selection.SetupGet(x => x.Start).Returns(new VirtualSnapshotPoint(line.Start)).Verifiable();
            _selection.SetupGet(x => x.End).Returns(new VirtualSnapshotPoint(line.End)).Verifiable();
            _operations.Setup(x => x.Yank(line.ExtentIncludingLineBreak, MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister)).Verifiable();
            Assert.IsTrue(_mode.Process('Y').IsSwitchPreviousMode);
            _selection.Verify();
            _operations.Verify();
        }

        [Test, Description("Yank in visual line mode should always be a linewise yank")]
        public void YankLines2()
        {
            Create2(ModeKind.VisualLine, null, "foo", "bar");
            var tss = _buffer.CurrentSnapshot;
            var line = tss.GetLineFromLineNumber(0);
            _tracker.Setup(x => x.SelectedText).Returns("foo" + Environment.NewLine).Verifiable();
            _operations.Setup(x => x.YankText("foo" + Environment.NewLine, MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister)).Verifiable();
            _mode.Process('y');
            _tracker.Verify();
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection1()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("d");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection2()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.GetRegister('c')))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("\"cd");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection3()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process("x");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection4()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            _mode.Process(VimKey.DeleteKey);
            _operations.Verify();
        }

        [Test]
        public void Join1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.JoinSelection(JoinKind.RemoveEmptySpaces)).Returns(true).Verifiable();
            _mode.Process('J');
            _operations.Verify();
        }

        [Test]
        public void Join2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.JoinSelection(JoinKind.RemoveEmptySpaces)).Returns(true).Verifiable();
            _mode.Process('J');
            _operations.Verify();
        }

        [Test]
        public void Join3()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.JoinSelection(JoinKind.KeepEmptySpaces)).Returns(true).Verifiable();
            _mode.Process("gJ");
            _operations.Verify();
        }

        [Test]
        public void Change1()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process('c');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change2()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.GetRegister('b')))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process("\"bc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change3()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process('s');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change4()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelectedLines(_map.DefaultRegister))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process('S');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change5()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelectedLines(_map.DefaultRegister))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process('C');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void ChangeCase1()
        {
            Create("foo bar", "baz");
            var span = _buffer.GetSpan(0,3);
            _operations
                .Setup(x => x.ChangeLetterCase(span))
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(span))
                .Verifiable();
            _mode.Process('~');
            _selection.Verify();
            _operations.Verify();
        }

        [Test]
        public void ChangeCase2()
        {
            Create("foo bar baz");
            SetupApplyAsSingleEdit();
            var spans = new SnapshotSpan[] { _buffer.GetSpan(0, 2), _buffer.GetSpan(3, 2) };
            var count = 0;
            _operations
                .Setup(x => x.ChangeLetterCase(It.IsAny<SnapshotSpan>()))
                .Callback(() => {count++;})
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(spans))
                .Verifiable();
            _mode.Process('~');
            _selection.Verify();
            _operations.Verify();
            Assert.AreEqual(count, 2);
        }

        #endregion
    }
}
