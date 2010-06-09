using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim;
using EnvDTE;
using Moq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Microsoft.VisualStudio.Text;
using VsVim.Properties;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using VimCore.Test.Utils;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Shell;

namespace VsVimTest
{
    [TestFixture]
    public class VsVimHostTest
    {
        private VsVimHost _hostRaw;
        private IVimHost _host;
        private MockFactory _factory;
        private Mock<IVsEditorAdaptersFactoryService> _editorAdaptersFactoryService;
        private Mock<ITextBufferUndoManagerProvider> _undoManagerProvider;
        private Mock<_DTE> _dte;
        private Mock<IVsTextManager> _textManager;
        private Mock<StatusBar> _statusBar;

        private void Create()
        {
            _factory = new MockFactory(MockBehavior.Strict);
            _undoManagerProvider = _factory.Create<ITextBufferUndoManagerProvider>();
            _editorAdaptersFactoryService = _factory.Create<IVsEditorAdaptersFactoryService>();
            _statusBar = _factory.Create<StatusBar>();
            _dte = _factory.Create<_DTE>();
            _dte.SetupGet(x => x.StatusBar).Returns(_statusBar.Object);
            _textManager = _factory.Create<IVsTextManager>();

            // Several commands check to see if there is an active view.  By default pretend there is none
            IVsTextView notUsed = null;
            _textManager
                .Setup(x => x.GetActiveView(0, null, out notUsed))
                .Returns(VSConstants.S_OK);

            var sp = _factory.Create<SVsServiceProvider>();
            sp.Setup(x => x.GetService(typeof(SVsTextManager))).Returns(_textManager.Object);
            sp.Setup(x => x.GetService(typeof(_DTE))).Returns(_dte.Object);
            _hostRaw = new VsVimHost(_undoManagerProvider.Object, _editorAdaptersFactoryService.Object, sp.Object);
            _host = _hostRaw;
        }


        [TearDown]
        public void TearDown()
        {
            _statusBar = null;
            _dte = null;
            _host = null;
            _hostRaw = null;
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
            Create();
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, String.Empty)).Throws(new Exception());
            Assert.IsFalse(_host.GoToDefinition());
        }

        [Test]
        public void GotoDefinition3()
        {
            Create();
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, String.Empty));
            Assert.IsTrue(_host.GoToDefinition());
        }

        [Test]
        public void GotoDefinition4()
        {
            Create();
            var ct = EditorUtil.GetOrCreateContentType(VsVim.Constants.CPlusPlusContentType, "code");
            var textView = EditorUtil.CreateView(ct, "hello world");
            var mockVsTextView = _factory.Create<IVsTextView>();
            var vsTextView = mockVsTextView.Object;
            _textManager
                .Setup(x => x.GetActiveView(0, null, out vsTextView))
                .Returns(0);
            _editorAdaptersFactoryService
                .Setup(x => x.GetWpfTextView(mockVsTextView.Object))
                .Returns(textView);
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, "hello"));
            Assert.IsTrue(_host.GoToDefinition());
        }

        [Test]
        public void GoToMatch1()
        {
            Create();
            _dte.Setup(x => x.ExecuteCommand(It.IsAny<string>(), It.IsAny<string>()));
            Assert.IsTrue(_host.GoToMatch());
        }

        [Test]
        public void GoToMatch2()
        {
            Create();
            _dte.Setup(x => x.ExecuteCommand(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception());
            Assert.IsFalse(_host.GoToMatch());
        }

        [Test]
        public void NavigateTo1()
        {
            Create();
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var vsBuffer = new Mock<IVsTextBuffer>(MockBehavior.Strict);
            _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer)).Returns(vsBuffer.Object);
            var viewGuid = VSConstants.LOGVIEWID_Code;
            _textManager
                .Setup(x => x.NavigateToLineAndColumn(vsBuffer.Object, ref viewGuid, 0, 2, 0, 2))
                .Returns(0)
                .Verifiable();
            _host.NavigateTo(new VirtualSnapshotPoint(buffer.CurrentSnapshot, 2));
            _textManager.Verify();
        }

        [Test]
        public void GetName1()
        {
            Create();
            var buffer = new Mock<ITextBuffer>();
            _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns((IVsTextBuffer)null);
            Assert.AreEqual("", _host.GetName(buffer.Object));
        }

        [Test]
        public void GetName2()
        {
            Create();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            var vsTextBuffer = (new Mock<IVsTextLines>(MockBehavior.Strict));
            var userData = vsTextBuffer.As<IVsUserData>();
            var moniker = VsVim.Constants.VsUserData_FileNameMoniker;
            object ret = "foo";
            userData.Setup(x => x.GetData(ref moniker, out ret)).Returns(0);
            _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns(vsTextBuffer.Object);
            Assert.AreEqual("foo", _host.GetName(buffer.Object));
        }


    }
}
