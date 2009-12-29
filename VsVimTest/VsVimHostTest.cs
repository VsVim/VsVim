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

namespace VsVimTest
{
    [TestFixture]
    public class VsVimHostTest
    {
        private IVimHost Create(
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = null,
            _DTE dte = null,
            IUndoHistoryRegistry undoRegistry = null,
            ICompletionBroker broker = null)
        {
            return CreateRaw(sp, dte, undoRegistry, broker);
        }

        private VsVimHost CreateRaw(
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = null,
            _DTE dte = null,
            IUndoHistoryRegistry undoRegistry = null,
            ICompletionBroker broker = null)
        {
            sp = sp ?? (new Mock<Microsoft.VisualStudio.OLE.Interop.IServiceProvider>(MockBehavior.Strict)).Object;
            dte = dte ?? (new Mock<_DTE>(MockBehavior.Strict)).Object;
            undoRegistry = undoRegistry ?? (new Mock<IUndoHistoryRegistry>(MockBehavior.Strict)).Object;
            broker = broker ?? (new Mock<ICompletionBroker>(MockBehavior.Strict)).Object;
            return new VsVimHost(sp, undoRegistry, broker, dte);
        }

        [Test]
        public void IsCompletionWindowActive1()
        {
            var view = new Mock<ITextView>(MockBehavior.Strict);
            var broker = new Mock<ICompletionBroker>(MockBehavior.Strict);
            broker
                .Setup(x => x.IsCompletionActive(view.Object))
                .Returns(false)
                .Verifiable();
            var host = Create(broker : broker.Object);
            Assert.IsFalse(host.IsCompletionWindowActive(view.Object));
            broker.Verify();
        }

        [Test]
        public void IsCompletionWindowActive2()
        {
            var view = new Mock<ITextView>(MockBehavior.Strict);
            var broker = new Mock<ICompletionBroker>(MockBehavior.Strict);
            broker
                .Setup(x => x.IsCompletionActive(view.Object))
                .Returns(true)
                .Verifiable();
            var host = Create(broker: broker.Object);
            Assert.IsTrue(host.IsCompletionWindowActive(view.Object));
            broker.Verify();
        }

        [Test]
        public void DismissCompletionWindow1()
        {
            var view = new Mock<ITextView>(MockBehavior.Strict);
            var broker = new Mock<ICompletionBroker>(MockBehavior.Strict);
            broker
                .Setup(x => x.DismissAllSessions(view.Object))
                .Verifiable();
            var host = Create(broker: broker.Object);
            host.DismissCompletionWindow(view.Object);
            broker.Verify();
        }
    }
}
