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
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;

namespace VimCoreTest
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
        private Mock<IEditorOperations> _editOpts;
        private Mock<IOperations> _operations;
        private Mock<ISelectionTracker> _tracker;

        public void Create(params string[] lines)
        {
            Create2(lines: lines);
        }

        public void Create2(
            ModeKind kind=ModeKind.VisualCharacter, 
            IVimHost host= null,
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
            _editOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _tracker = new Mock<ISelectionTracker>(MockBehavior.Strict);
            _tracker.Setup(x => x.Start());
            _operations = new Mock<IOperations>(MockBehavior.Strict);
            _operations.SetupGet(x => x.SelectionTracker).Returns(_tracker.Object);
            host = host ?? new FakeVimHost();
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view.Object,
                "test",
                MockObjectFactory.CreateVim(_map,host:host).Object,
                _editOpts.Object);
            _modeRaw = new Vim.Modes.Visual.VisualMode(Tuple.Create<IVimBuffer, IOperations, ModeKind>(_bufferData.Object, _operations.Object, kind));
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        [Test, Description("Escape is used to exit visual mode")]
        public void Commands1()
        {
            Create("foo");
            Assert.IsTrue(_mode.Commands.Contains(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey)));
        }

        [Test,Description("Movement commands")]
        public void Commands2()
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
            var commands = _mode.Commands.ToList();
            foreach (var item in list)
            {
                Assert.Contains(item, commands); 
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

        [Test]
        public void Banner1()
        {
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.UpdateStatus(Resources.VisualMode_Banner)).Verifiable();
            Create2(kind: ModeKind.VisualCharacter, host: host.Object, lines: "foo");
            host.Verify();
        }

        [Test]
        public void Banner2()
        {
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.UpdateStatus(Resources.VisualMode_Banner)).Verifiable();
            Create2(kind: ModeKind.VisualCharacter, host: host.Object, lines: "foo");
            host.Setup(x => x.UpdateStatus(String.Empty)).Verifiable();
            _tracker.Setup(x => x.Stop()).Verifiable();
            _mode.OnLeave();
            host.Verify();
            _tracker.Verify();
        }

        [Test,Description("Must handle arbitrary input to prevent changes but don't list it as a command")]
        public void PreventInput1()
        {
            var host = new FakeVimHost();
            Create2(host:host,lines:"foo");
            var input = InputUtil.CharToKeyInput(',');
            Assert.IsFalse(_mode.Commands.Any(x => x.Char == input.Char));
            Assert.IsTrue(_mode.CanProcess(input));
            var ret = _mode.Process(input);
            Assert.IsTrue(ret.IsProcessed);
            Assert.AreEqual(1, host.BeepCount);
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


        #endregion
    }
}
