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
    }
}
