using System;
using EnvDTE;
using EditorUtils;
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
using Microsoft.VisualStudio;

namespace VsVim.UnitTest
{
    public abstract class VsVimHostTest : VimTestBase
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
            _textManager.Setup(x => x.GetDocumentTextViews(DocumentLoad.RespectLazy)).Returns(new List<ITextView>());

            var vsMonitorSelection = _factory.Create<IVsMonitorSelection>();
            uint cookie = 42;
            vsMonitorSelection.Setup(x => x.AdviseSelectionEvents(It.IsAny<IVsSelectionEvents>(), out cookie)).Returns(VSConstants.S_OK);

            var sp = _factory.Create<SVsServiceProvider>();
            sp.Setup(x => x.GetService(typeof(_DTE))).Returns(_dte.Object);
            sp.Setup(x => x.GetService(typeof(SVsUIShell))).Returns(_shell.Object);
            sp.Setup(x => x.GetService(typeof(IVsExtensibility))).Returns(_factory.Create<IVsExtensibility>().Object);
            sp.Setup(x => x.GetService(typeof(SVsShellMonitorSelection))).Returns(vsMonitorSelection.Object);
            _hostRaw = new VsVimHost(
                _adapter.Object,
                _factory.Create<ITextBufferFactoryService>().Object,
                _factory.Create<ITextEditorFactoryService>().Object,
                _factory.Create<ITextDocumentFactoryService>().Object,
                _undoManagerProvider.Object,
                _editorAdaptersFactoryService.Object,
                _editorOperationsFactoryService.Object,
                _textManager.Object,
                _factory.Create<ISharedServiceFactory>(MockBehavior.Loose).Object,
                sp.Object);
            _host = _hostRaw;
        }

        public abstract class GoToDefinitionTest : VsVimHostTest
        {
            public sealed class NormalTest : GoToDefinitionTest
            {
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
                /// For most languages the word which is targeted should not be included in the 
                /// command
                /// </summary>
                [Fact]
                public void Normal()
                {
                    Create();
                    var ct = GetOrCreateContentType("csharp", "code");
                    var textView = CreateTextView(ct, "hello world");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, ""));
                    Assert.True(_host.GoToDefinition());
                }
            }

            public sealed class CPlusPlusTest : GoToDefinitionTest
            {
                private ITextView _textView;

                private void CreateWithText(params string[] lines)
                {
                    Create();

                    var contentType = GetOrCreateContentType(VsVim.Constants.CPlusPlusContentType, "code");
                    _textView = CreateTextView(contentType, lines);
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(_textView);
                }

                /// <summary>
                /// The C++ implementation of the goto definition command requires that the word which 
                /// it should target be passed along as an argument to the command
                /// </summary>
                [Fact]
                public void Simple()
                {
                    CreateWithText("hello world");
                    _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, "hello")).Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _dte.Verify();
                }

                [Fact]
                public void MiddleOfIdentifier()
                {
                    CreateWithText("cat; dog");
                    _textView.MoveCaretTo(1);
                    _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, "cat")).Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _dte.Verify();
                }

                [Fact]
                public void MiddleOfLongIdentifier()
                {
                    CreateWithText("big_cat; dog");
                    _textView.MoveCaretTo(1);
                    _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, "big_cat")).Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _dte.Verify();
                }

                /// <summary>
                /// The code should pass valid C++ identifiers to the GoToDefinition command.  It should not be 
                /// using a full vim word (:help WORD) as it can include many non-legal C++ identifiers
                /// </summary>
                [Fact]
                public void Issue1122()
                {
                    CreateWithText("cat; dog");
                    _dte.Setup(x => x.ExecuteCommand(VsVimHost.CommandNameGoToDefinition, "cat")).Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _dte.Verify();
                }
            }
        }

        public sealed class NavigateToTest : VsVimHostTest
        {
            [Fact]
            public void Simple()
            {
                Create();
                var buffer = CreateTextBuffer("foo", "bar");
                var point = new VirtualSnapshotPoint(buffer.CurrentSnapshot, 2);
                _textManager.Setup(x => x.NavigateTo(point)).Returns(true);
                _host.NavigateTo(new VirtualSnapshotPoint(buffer.CurrentSnapshot, 2));
                _textManager.Verify();
            }
        }

        public sealed class SholudCreateVimBufferTest : VsVimHostTest
        {
            private ITextView CreateWithRoles(params string[] textViewRoles)
            {
                Create();
                return TextEditorFactoryService.CreateTextView(
                    CreateTextBuffer(),
                    TextEditorFactoryService.CreateTextViewRoleSet(textViewRoles));
            }

            /// <summary>
            /// Don't create IVimBuffer instances for interactive windows.  This would cause the NuGet
            /// window to have instances of vim created inside of it 
            /// </summary>
            [Fact]
            public void Interactive()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.Interactive);
                Assert.False(_host.ShouldCreateVimBuffer(textView));
            }

            [Fact]
            public void EmbeddedPeekTextView()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, Constants.TextViewRoleEmbeddedPeekTextView);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [Fact]
            public void StandardDocument()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.Document, PredefinedTextViewRoles.Structured, PredefinedTextViewRoles.Zoomable, PredefinedTextViewRoles.Debuggable);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [Fact]
            public void StandardPrimaryDocument()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.PrimaryDocument, PredefinedTextViewRoles.Structured, PredefinedTextViewRoles.Zoomable, PredefinedTextViewRoles.Debuggable);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [Fact]
            public void StandardCSharpDocument()
            {
                var textView = CreateWithRoles(
                    PredefinedTextViewRoles.Analyzable,
                    PredefinedTextViewRoles.Document,
                    PredefinedTextViewRoles.Editable,
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Structured,
                    PredefinedTextViewRoles.Zoomable,
                    PredefinedTextViewRoles.Debuggable,
                    PredefinedTextViewRoles.PrimaryDocument);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [Fact]
            public void StandardCSharpEmbeddedTextView()
            {
                var textView = CreateWithRoles(
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Editable,
                    Constants.TextViewRoleEmbeddedPeekTextView,
                    PredefinedTextViewRoles.Analyzable,
                    PredefinedTextViewRoles.Zoomable);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [Fact]
            public void NuGetManagerConsole()
            {
                var textView = CreateWithRoles(
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Editable,
                    PredefinedTextViewRoles.Analyzable,
                    PredefinedTextViewRoles.Zoomable);
                Assert.False(_host.ShouldCreateVimBuffer(textView));
            }
        }

        public sealed class MiscTest : VsVimHostTest
        {
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

            /// <summary>
            /// Settings shouldn't be automatically synchronized for new IVimBuffer instances in the
            /// code base.  They are custom handled by HostFactory
            /// </summary>
            [Fact]
            public void AutoSynchronizeSettings()
            {
                Create();
                Assert.False(_host.AutoSynchronizeSettings);
            }
        }
    }
}
