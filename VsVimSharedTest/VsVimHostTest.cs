using System;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim;
using Vim.UnitTest;
using System.Collections.Generic;

namespace VsVim.UnitTest
{
    public sealed class VsVimHostTest : VimTestBase
    {
        private VsVimHost _hostRaw;
        private IVimHost _host;
        private MockRepository _factory;
        private Mock<IVsAdapter> _adapter;
        private Mock<ITextManager> _textManager;
        private Mock<IVsEditorAdaptersFactoryService> _editorAdaptersFactoryService;
        private Mock<ITextBufferUndoManagerProvider> _undoManagerProvider;
        private Mock<IEditorOperationsFactoryService> _editorOperationsFactoryService;
        private Mock<_DTE> _dte;
        private Mock<IVsUIShell4> _shell;
        private Mock<StatusBar> _statusBar;

        private void Create()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _adapter = _factory.Create<IVsAdapter>();
            _undoManagerProvider = _factory.Create<ITextBufferUndoManagerProvider>();
            _editorAdaptersFactoryService = _factory.Create<IVsEditorAdaptersFactoryService>();
            _editorOperationsFactoryService = _factory.Create<IEditorOperationsFactoryService>();
            _statusBar = _factory.Create<StatusBar>();
            _shell = _factory.Create<IVsUIShell4>();
            _dte = _factory.Create<_DTE>();
            _dte.SetupGet(x => x.StatusBar).Returns(_statusBar.Object);
            _textManager = _factory.Create<ITextManager>();
            _textManager.SetupGet(x => x.TextViews).Returns(new List<ITextView>());

            var sp = _factory.Create<SVsServiceProvider>();
            sp.Setup(x => x.GetService(typeof(_DTE))).Returns(_dte.Object);
            sp.Setup(x => x.GetService(typeof(SVsUIShell))).Returns(_shell.Object);
            sp.Setup(x => x.GetService(typeof(IVsExtensibility))).Returns(_factory.Create<IVsExtensibility>().Object);
            _hostRaw = new VsVimHost(
                _adapter.Object,
                _factory.Create<ITextBufferFactoryService>().Object,
                _factory.Create<ITextEditorFactoryService>().Object,
                _factory.Create<ITextDocumentFactoryService>().Object,
                _undoManagerProvider.Object,
                _editorAdaptersFactoryService.Object,
                _editorOperationsFactoryService.Object,
                WordUtilFactory,
                _textManager.Object,
                _factory.Create<ISharedServiceFactory>(MockBehavior.Loose).Object,
                sp.Object);
            _host = _hostRaw;
        }

        [Fact]
        public void GotoDefinition1()
        {
            Create();
            var textView = CreateTextView("");
            _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
            Assert.False(_host.GoToDefinition());
        }

        [Fact]
        public void GotoDefinition2()
        {
            Create();
            var textView = CreateTextView("");
            _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, String.Empty)).Throws(new Exception());
            Assert.False(_host.GoToDefinition());
        }

        [Fact]
        public void GotoDefinition3()
        {
            Create();
            var textView = CreateTextView("");
            _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, String.Empty));
            Assert.True(_host.GoToDefinition());
        }

        /// <summary>
        /// The C++ implementation of the goto definition command requires that the word which 
        /// it should target be passed along as an argument to the command
        /// </summary>
        [Fact]
        public void GotoDefinition_CPlusPlus()
        {
            Create();
            var ct = GetOrCreateContentType(VsVim.Constants.CPlusPlusContentType, "code");
            var textView = CreateTextView(ct, "hello world");
            var wordUtil = WordUtilFactory.GetWordUtil(textView.TextBuffer);
            _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, "hello"));
            Assert.True(_host.GoToDefinition());
        }

        /// <summary>
        /// For most languages the word which is targeted should not be included in the 
        /// command
        /// </summary>
        [Fact]
        public void GotoDefinition_Normal()
        {
            Create();
            var ct = GetOrCreateContentType("csharp", "code");
            var textView = CreateTextView(ct, "hello world");
            _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
            _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, ""));
            Assert.True(_host.GoToDefinition());
        }

        [Fact]
        public void NavigateTo1()
        {
            Create();
            var buffer = CreateTextBuffer("foo", "bar");
            var point = new VirtualSnapshotPoint(buffer.CurrentSnapshot, 2);
            _textManager.Setup(x => x.NavigateTo(point)).Returns(true);
            _host.NavigateTo(new VirtualSnapshotPoint(buffer.CurrentSnapshot, 2));
            _textManager.Verify();
        }

        [Fact]
        public void GetName1()
        {
            Create();
            var buffer = new Mock<ITextBuffer>();
            _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns((IVsTextBuffer)null);
            Assert.Equal("", _host.GetName(buffer.Object));
        }

        [Fact]
        public void GetName2()
        {
            Create();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            var vsTextBuffer = (new Mock<IVsTextLines>(MockBehavior.Strict));
            var userData = vsTextBuffer.As<IVsUserData>();
            var moniker = VsVim.Constants.VsUserDataFileNameMoniker;
            object ret = "foo";
            userData.Setup(x => x.GetData(ref moniker, out ret)).Returns(0);
            _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns(vsTextBuffer.Object);
            Assert.Equal("foo", _host.GetName(buffer.Object));
        }

        public object VimUtil { get; set; }
    }
}
