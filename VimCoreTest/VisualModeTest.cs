using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.Modes.Visual;
using Moq;
using Vim.Modes.Normal;
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
        private Mock<ICommonOperations> _operations;
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
            _operations = new Mock<ICommonOperations>(MockBehavior.Strict);
            _tracker = new Mock<ISelectionTracker>(MockBehavior.Strict);
            _tracker.Setup(x => x.Start());
            host = host ?? new FakeVimHost();
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view.Object,
                "test",
                MockObjectFactory.CreateVim(_map,host:host).Object,
                MockObjectFactory.CreateBlockCaret().Object,
                _editOpts.Object);
            _modeRaw = new Vim.Modes.Visual.VisualMode(Tuple.Create<IVimBuffer, ICommonOperations, ISelectionTracker, ModeKind>(_bufferData.Object, _operations.Object, _tracker.Object, kind));
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        [Test, Description("Escape is used to exit visual mode")]
        public void Commands1()
        {
            Create("foo");
            Assert.IsTrue(_mode.Commands.Contains(InputUtil.KeyToKeyInput(Key.Escape)));
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
                InputUtil.KeyToKeyInput(Key.Left),
                InputUtil.KeyToKeyInput(Key.Right),
                InputUtil.KeyToKeyInput(Key.Up),
                InputUtil.KeyToKeyInput(Key.Down),
                InputUtil.KeyToKeyInput(Key.Back) };
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
            var res = _mode.Process(InputUtil.KeyToChar(Key.Escape));
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().item);
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
            Create2(ModeKind.VisualCharacter, host.Object, "foo");
            host.Verify();
        }

        [Test]
        public void Banner2()
        {
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.UpdateStatus(Resources.VisualMode_Banner)).Verifiable();
            Create2(ModeKind.VisualCharacter, host.Object, "foo");
            host.Setup(x => x.UpdateStatus(String.Empty)).Verifiable();
            _tracker.Setup(x => x.Stop()).Verifiable();
            _mode.OnLeave();
            host.Verify();
            _tracker.Verify();
        }

        #region Operations

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            _mode.Process('y');
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
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().Item);
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
            var span = tss.GetLineFromLineNumber(0).Extent;
            _selection.SetupGet(x => x.Start).Returns(new VirtualSnapshotPoint(span.Start)).Verifiable();
            _selection.SetupGet(x => x.End).Returns(new VirtualSnapshotPoint(span.End)).Verifiable();
            _operations.Setup(x => x.Yank(span, MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister)).Verifiable();
            _mode.Process("Y");
            _selection.Verify();
            _operations.Verify();
        }

        #endregion
    }
}
