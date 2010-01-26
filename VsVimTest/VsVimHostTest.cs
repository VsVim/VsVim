using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim;
using EnvDTE;
using Microsoft.VisualStudio.UI.Undo;
using Moq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Microsoft.VisualStudio.Text;
using VsVim.Properties;

namespace VsVimTest
{
    [TestFixture]
    public class VsVimHostTest
    {
        private VsVimHost _hostRaw;
        private IVimHost _host;
        private Mock<IUndoHistoryRegistry> _undoRegistry;
        private Mock<_DTE> _dte;
        private Mock<StatusBar> _statusBar;

        private void Create()
        {
            _undoRegistry = new Mock<IUndoHistoryRegistry>(MockBehavior.Strict);
            _hostRaw = new VsVimHost(_undoRegistry.Object);
            _host = _hostRaw;
        }

        private void CreateDte()
        {
            _statusBar = new Mock<StatusBar>(MockBehavior.Strict);
            _dte = new Mock<_DTE>(MockBehavior.Strict);
            _dte.SetupGet(x => x.StatusBar).Returns(_statusBar.Object);
            _hostRaw.DTE = _dte.Object;
        }

        private void CreateBoth()
        {
            Create();
            CreateDte();
        }

        [TearDown]
        public void TearDown()
        {
            _statusBar = null;
            _dte = null;
            _host = null;
            _hostRaw = null;
        }

        [Test, Description("Don't crash id _dte is not set")]
        public void UpdateStatus1()
        {
            Create();
            _host.UpdateStatus("foo");
        }

        [Test]
        public void UpdateStatus2()
        {
            CreateBoth();
            _statusBar.SetupSet(x => x.Text).Verifiable();
            _host.UpdateStatus("foo");
            _statusBar.Verify();
        }

        [Test]
        public void Undo1()
        {
            CreateBoth();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            UndoHistory temp = null;
            _undoRegistry.Setup(x => x.TryGetHistory(buffer.Object, out temp)).Returns(false).Verifiable();
            _statusBar
                .SetupSet(x => x.Text)
                .Callback(msg => Assert.AreEqual(Resources.VimHost_NoUndoRedoSupport, msg))
                .Verifiable();
            _host.Undo(buffer.Object, 1);
            _undoRegistry.Verify();
            _statusBar.Verify();
        }

        [Test]
        public void Undo2()
        {
            CreateBoth();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            var history = new Mock<UndoHistory>(MockBehavior.Strict);
            var temp = history.Object;
            _undoRegistry.Setup(x => x.TryGetHistory(buffer.Object, out temp)).Returns(true).Verifiable();
            history.SetupGet(x => x.CanUndo).Throws(new NotSupportedException());
            _statusBar
                .SetupSet(x => x.Text)
                .Callback(msg => Assert.AreEqual(Resources.VimHost_CannotUndo, msg))
                .Verifiable();
            _host.Undo(buffer.Object, 1);
            _undoRegistry.Verify();
            _statusBar.Verify();
        }

        [Test]
        public void Redo1()
        {
            CreateBoth();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            UndoHistory temp = null;
            _undoRegistry.Setup(x => x.TryGetHistory(buffer.Object, out temp)).Returns(false).Verifiable();
            _statusBar
                .SetupSet(x => x.Text)
                .Callback(msg => Assert.AreEqual(Resources.VimHost_NoUndoRedoSupport, msg))
                .Verifiable();
            _host.Redo(buffer.Object, 1);
            _undoRegistry.Verify();
            _statusBar.Verify();
        }

        [Test]
        public void Redo2()
        {
            CreateBoth();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            var history = new Mock<UndoHistory>(MockBehavior.Strict);
            var temp = history.Object;
            _undoRegistry.Setup(x => x.TryGetHistory(buffer.Object, out temp)).Returns(true).Verifiable();
            history.SetupGet(x => x.CanRedo).Throws(new NotSupportedException());
            _statusBar
                .SetupSet(x => x.Text)
                .Callback(msg => Assert.AreEqual(Resources.VimHost_CannotRedo, msg))
                .Verifiable();
            _host.Redo(buffer.Object, 1);
            _undoRegistry.Verify();
            _statusBar.Verify();
        }

        [Test]
        public void GotoDefinition1()
        {
            Create();
            Assert.IsFalse(_host.GoToDefinition());
        }

        [Test]
        public void GotoDefinition2()
        {
            CreateBoth();
            _dte.Setup(x => x.ExecuteCommand(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception());
            Assert.IsFalse(_host.GoToDefinition());
        }

        [Test]
        public void GotoDefinition3()
        {
            CreateBoth();
            _dte.Setup(x => x.ExecuteCommand(It.IsAny<string>(), It.IsAny<string>()));
            Assert.IsTrue(_host.GoToDefinition());
        }

    }
}
