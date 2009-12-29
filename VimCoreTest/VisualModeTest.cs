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
        private IWpfTextView _view;
        private IVimBufferData _bufferData;
        private VisualMode _modeRaw;
        private IMode _mode;
        private FakeVimHost _host;
        private IRegisterMap _map;
        private Mock<IEditorOperations> _editOpts;
        private Mock<ICommonOperations> _operations;

        public void Create(params string[] lines)
        {
            Create(ModeKind.VisualBlock, lines);
        }

        public void Create(ModeKind kind, params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map = new RegisterMap();
            _host = new FakeVimHost();
            _editOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _operations = new Mock<ICommonOperations>(MockBehavior.Strict);
            _bufferData = MockObjectFactory.CreateVimBufferData(
                _view,
                "test",
                _host,
                MockObjectFactory.CreateVimData(_map).Object,
                MockObjectFactory.CreateBlockCaret().Object,
                _editOpts.Object);
            _modeRaw = new Vim.Modes.Visual.VisualMode(Tuple.Create<IVimBufferData, ICommonOperations, ModeKind>(_bufferData, _operations.Object, kind));
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ModeKind1()
        {
            var data = new Mock<IVimBufferData>();
            var operations = new Mock<ICommonOperations>();
            new VisualMode(Tuple.Create<IVimBufferData, ICommonOperations, ModeKind>(data.Object, operations.Object, ModeKind.Insert));
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
            _editOpts.Setup(x => x.ResetSelection()).Verifiable();
            _mode.OnLeave();
            _editOpts.Verify();
        }

        #region Selection

        [Test]
        public void Character1()
        {
            Create(ModeKind.VisualCharacter, "foo", "bar");
            _mode.Process('l');
            Assert.AreEqual("fo", _view.Selection.GetSpan().GetText());
        }

        [Test]
        public void Character2()
        {
            Create(ModeKind.VisualCharacter, "foo", "bar");
            _mode.Process('l');
            _mode.Process('l');
            Assert.AreEqual("foo", _view.Selection.GetSpan().GetText());
        }

        [Test]
        public void Character3()
        {
            Create(ModeKind.VisualCharacter, "foo");
            Assert.AreEqual("f", _view.Selection.GetSpan().GetText());
        }

        #endregion

        #region Operations

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 3);
            _view.Selection.Select(span, false);
            _operations.Setup(x => x.Yank(span, MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            _mode.Process('y');
            _operations.Verify();
        }

        [Test, Description("Yank should go back to normal mode")]
        public void Yank2()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 3);
            _view.Selection.Select(span, false);
            _operations.Setup(x => x.Yank(span, MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process('y');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().Item);
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 3);
            _view.Selection.Select(span, false);
            _operations.Setup(x => x.Yank(span, MotionKind.Inclusive, OperationKind.CharacterWise, _map.GetRegister('c'))).Verifiable();
            _mode.Process("\"cy");
            _operations.Verify();
        }

        #endregion
    }
}
