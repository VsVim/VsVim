using System;
using System.Linq;
using EnvDTE;
using Vim.EditorHost;
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
using Vim.UI.Wpf;

namespace Vim.VisualStudio.UnitTest
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
        private Mock<IVimApplicationSettings> _vimApplicationSettings;
        private Mock<_DTE> _dte;
        private Mock<IVsUIShell> _uiVSShell;
        private Mock<IVsShell> _vsShell;
        private Mock<StatusBar> _statusBar;
        private Mock<IExtensionAdapterBroker> _extensionAdapterBroker;
        private Mock<ICommandDispatcher> _commandDispatcher;
        private Mock<IClipboardDevice> _clipboardDevice;

        private void Create()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _adapter = _factory.Create<IVsAdapter>();
            _adapter.Setup(x => x.IsWatchWindowView(It.IsAny<ITextView>())).Returns(false);
            _adapter.Setup(x => x.IsTextEditorView(It.IsAny<ITextView>())).Returns(true);
            _undoManagerProvider = _factory.Create<ITextBufferUndoManagerProvider>();
            _editorAdaptersFactoryService = _factory.Create<IVsEditorAdaptersFactoryService>();
            _editorOperationsFactoryService = _factory.Create<IEditorOperationsFactoryService>();
            _statusBar = _factory.Create<StatusBar>();
            _uiVSShell = _factory.Create<IVsUIShell>(MockBehavior.Strict);
            _vsShell = _factory.Create<IVsShell>(MockBehavior.Loose);
            _dte = _factory.Create<_DTE>();
            _dte.SetupGet(x => x.StatusBar).Returns(_statusBar.Object);
            _textManager = _factory.Create<ITextManager>();
            _textManager.Setup(x => x.GetDocumentTextViews(DocumentLoad.RespectLazy)).Returns(new List<ITextView>());
            _vimApplicationSettings = _factory.Create<IVimApplicationSettings>(MockBehavior.Loose);
            _extensionAdapterBroker = _factory.Create<IExtensionAdapterBroker>(MockBehavior.Loose);
            _commandDispatcher = _factory.Create<ICommandDispatcher>();
            _clipboardDevice = _factory.Create<IClipboardDevice>(MockBehavior.Loose);

            var vsMonitorSelection = _factory.Create<IVsMonitorSelection>();
            uint selectionCookie = 42;
            vsMonitorSelection.Setup(x => x.AdviseSelectionEvents(It.IsAny<IVsSelectionEvents>(), out selectionCookie)).Returns(VSConstants.S_OK);

            var vsRunningDocumentTable = _factory.Create<IVsRunningDocumentTable>();
            uint runningDocumentTableCookie = 86;
            vsRunningDocumentTable.Setup(x => x.AdviseRunningDocTableEvents(It.IsAny<IVsRunningDocTableEvents3>(), out runningDocumentTableCookie)).Returns(VSConstants.S_OK);

            var sp = _factory.Create<SVsServiceProvider>();
            sp.Setup(x => x.GetService(typeof(_DTE))).Returns(_dte.Object);
            sp.Setup(x => x.GetService(typeof(SVsUIShell))).Returns(_uiVSShell.Object);
            sp.Setup(x => x.GetService(typeof(SVsShell))).Returns(_vsShell.Object);
            sp.Setup(x => x.GetService(typeof(IVsExtensibility))).Returns(_factory.Create<IVsExtensibility>().Object);
            sp.Setup(x => x.GetService(typeof(SVsShellMonitorSelection))).Returns(vsMonitorSelection.Object);
            sp.Setup(x => x.GetService(typeof(SVsRunningDocumentTable))).Returns(vsRunningDocumentTable.Object);
            _hostRaw = new VsVimHost(
                _adapter.Object,
                _factory.Create<ITextBufferFactoryService>().Object,
                _factory.Create<ITextEditorFactoryService>().Object,
                _factory.Create<ITextDocumentFactoryService>().Object,
                _undoManagerProvider.Object,
                _editorAdaptersFactoryService.Object,
                _editorOperationsFactoryService.Object,
                _factory.Create<ISmartIndentationService>().Object,
                _textManager.Object,
                _factory.Create<ISharedServiceFactory>(MockBehavior.Loose).Object,
                _vimApplicationSettings.Object,
                _extensionAdapterBroker.Object,
                ProtectedOperations,
                _factory.Create<IMarkDisplayUtil>(MockBehavior.Loose).Object,
                _factory.Create<IControlCharUtil>(MockBehavior.Loose).Object,
                _commandDispatcher.Object,
                sp.Object,
                _clipboardDevice.Object);
            _host = _hostRaw;
        }

        public abstract class GoToDefinitionTest : VsVimHostTest
        {
            public sealed class NormalTest : GoToDefinitionTest
            {
                [WpfFact]
                public void GotoDefinition1()
                {
                    Create();
                    var textView = CreateTextView("");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    Assert.False(_host.GoToDefinition());
                }

                [WpfFact]
                public void GotoDefinition2()
                {
                    Create();
                    var textView = CreateTextView("");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(textView, VsVimHost.CommandNameGoToDefinition, string.Empty, false))
                        .Throws(new Exception())
                        .Verifiable();
                    Assert.False(_host.GoToDefinition());
                    _commandDispatcher.Verify();
                }

                [WpfFact]
                public void GotoDefinition3()
                {
                    Create();
                    var textView = CreateTextView("");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(textView, VsVimHost.CommandNameGoToDefinition, string.Empty, false))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// Make sure that go to local declaration is translated into
                /// go to defintion in a non-C++ language
                /// </summary>
                [WpfFact]
                public void GoToLocalDeclaration()
                {
                    Create();
                    var textView = CreateTextView("hello world");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(textView, VsVimHost.CommandNameGoToDefinition, "hello", false))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToLocalDeclaration(textView, "hello"));
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// Make sure that go to global declaration is translated into
                /// go to defintion in a non-C++ language
                /// </summary>
                [WpfFact]
                public void GoToGlobalDeclaration()
                {
                    Create();
                    var textView = CreateTextView("hello world");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(textView, VsVimHost.CommandNameGoToDefinition, "hello", false))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToGlobalDeclaration(textView, "hello"));
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// For most languages the word which is targeted should not be included in the 
                /// command
                /// </summary>
                [WpfFact]
                public void Normal()
                {
                    Create();
                    var ct = GetOrCreateContentType("csharp", "code");
                    var textView = CreateTextView(ct, "hello world");
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(textView);
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(textView, VsVimHost.CommandNameGoToDefinition, "", false))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _commandDispatcher.Verify();
                }
            }

            public sealed class CPlusPlusTest : GoToDefinitionTest
            {
                private ITextView _textView;

                private void CreateWithText(params string[] lines)
                {
                    Create();

                    var contentType = GetOrCreateContentType(VsVimConstants.CPlusPlusContentType, "code");
                    _textView = CreateTextView(contentType, lines);
                    _textManager.SetupGet(x => x.ActiveTextViewOptional).Returns(_textView);
                }

                /// <summary>
                /// The C++ implementation needs 'Edit.GoToDefinition' to
                /// have a null argument and needs for it to be posted
                /// </summary>
                [WpfFact]
                public void GoToDefinition()
                {
                    CreateWithText("hello world");
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(_textView, VsVimHost.CommandNameGoToDefinition, null, true))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToDefinition());
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// The C++ implementation needs 'Edit.GoToDeclaration' to
                /// have a null argument and needs for it to be posted
                /// </summary>
                [WpfFact]
                public void GoToLocalDeclaration()
                {
                    CreateWithText("hello world");
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(_textView, VsVimHost.CommandNameGoToDeclaration, null, true))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToLocalDeclaration(_textView, "hello"));
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// The C++ implementation needs 'Edit.GoToDeclaration' to
                /// have a null argument and needs for it to be posted
                /// </summary>
                [WpfFact]
                public void GoToGlobalDeclaration()
                {
                    CreateWithText("hello world");
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(_textView, VsVimHost.CommandNameGoToDeclaration, null, true))
                        .Returns(true)
                        .Verifiable();
                    Assert.True(_host.GoToGlobalDeclaration(_textView, "hello"));
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// When go to definition is executed as a host command, it
                /// should use the same dispatching logic as if go to
                /// definition were called directly
                /// </summary>
                [WpfFact]
                public void GoToDefinition_HostCommand()
                {
                    CreateWithText("hello world");
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(_textView, VsVimHost.CommandNameGoToDefinition, null, true))
                        .Returns(true)
                        .Verifiable();
                    _host.RunHostCommand(_textView, VsVimHost.CommandNameGoToDefinition, "");
                    _commandDispatcher.Verify();
                }

                /// <summary>
                /// When go to declaration is executed as a host command, it
                /// should use the same dispatching logic as if go to
                /// local/global declaration were called directly
                /// </summary>
                [WpfFact]
                public void GoToDeclaration_HostCommand()
                {
                    CreateWithText("hello world");
                    _commandDispatcher
                        .Setup(x => x.ExecuteCommand(_textView, VsVimHost.CommandNameGoToDeclaration, null, true))
                        .Returns(true)
                        .Verifiable();
                    _host.RunHostCommand(_textView, VsVimHost.CommandNameGoToDeclaration, "");
                    _commandDispatcher.Verify();
                }
            }
        }

        public sealed class NavigateToTest : VsVimHostTest
        {
            [WpfFact]
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

            private ITextView CreateWithMajorRoles()
            {
                return CreateWithRoles(
                    PredefinedTextViewRoles.Analyzable,
                    PredefinedTextViewRoles.Document,
                    PredefinedTextViewRoles.Editable,
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Structured,
                    PredefinedTextViewRoles.Zoomable,
                    PredefinedTextViewRoles.Debuggable,
                    PredefinedTextViewRoles.PrimaryDocument);
            }

            /// <summary>
            /// Don't create IVimBuffer instances for interactive windows.  This would cause the NuGet
            /// window to have instances of vim created inside of it 
            /// </summary>
            [WpfFact]
            public void Interactive()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.Interactive);
                Assert.False(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void EmbeddedPeekTextView()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, VsVimConstants.TextViewRoleEmbeddedPeekTextView);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void StandardDocument()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.Document, PredefinedTextViewRoles.Structured, PredefinedTextViewRoles.Zoomable, PredefinedTextViewRoles.Debuggable);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void StandardPrimaryDocument()
            {
                var textView = CreateWithRoles(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.PrimaryDocument, PredefinedTextViewRoles.Structured, PredefinedTextViewRoles.Zoomable, PredefinedTextViewRoles.Debuggable);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void StandardCSharpDocument()
            {
                var textView = CreateWithMajorRoles();
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void StandardCSharpEmbeddedTextView()
            {
                var textView = CreateWithRoles(
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Editable,
                    VsVimConstants.TextViewRoleEmbeddedPeekTextView,
                    PredefinedTextViewRoles.Analyzable,
                    PredefinedTextViewRoles.Zoomable);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void NuGetManagerConsole()
            {
                var textView = CreateWithRoles(
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Editable,
                    PredefinedTextViewRoles.Analyzable,
                    PredefinedTextViewRoles.Zoomable);
                Assert.False(_host.ShouldCreateVimBuffer(textView));
            }

            /// <summary>
            /// Allow hosts like R# to opt out of creating the <see cref="IVimBuffer>"/>
            /// Issue 1498
            /// </summary>
            [WpfFact]
            public void ExtensionReject()
            {
                var textView = CreateWithMajorRoles();
                _extensionAdapterBroker.Setup(x => x.ShouldCreateVimBuffer(textView)).Returns(false);
                Assert.False(_host.ShouldCreateVimBuffer(textView));
            }

            [WpfFact]
            public void ExtensionAccept()
            {
                var textView = CreateWithMajorRoles();
                _extensionAdapterBroker.Setup(x => x.ShouldCreateVimBuffer(textView)).Returns(true);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }

            /// <summary>
            /// Default behavior should occur when the extension ignores the <see cref="ITextView"/>
            /// </summary>
            [WpfFact]
            public void ExtensionIgnore()
            {
                var textView = CreateWithMajorRoles();
                _extensionAdapterBroker.Setup(x => x.ShouldCreateVimBuffer(textView)).Returns((bool?)null);
                Assert.True(_host.ShouldCreateVimBuffer(textView));
            }
        }

        public sealed class MiscTest : VsVimHostTest
        {
            [WpfFact]
            public void GetName1()
            {
                Create();
                var buffer = new Mock<ITextBuffer>();
                _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns((IVsTextBuffer)null);
                Assert.Equal("", _host.GetName(buffer.Object));
            }

            [WpfFact]
            public void GetName2()
            {
                Create();
                var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
                var vsTextBuffer = (new Mock<IVsTextLines>(MockBehavior.Strict));
                var userData = vsTextBuffer.As<IVsUserData>();
                var moniker = VsVimConstants.VsUserDataFileNameMoniker;
                object ret = "foo";
                userData.Setup(x => x.GetData(ref moniker, out ret)).Returns(0);
                _editorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns(vsTextBuffer.Object);
                Assert.Equal("foo", _host.GetName(buffer.Object));
            }

            /// <summary>
            /// Settings shouldn't be automatically synchronized for new IVimBuffer instances in the
            /// code base.  They are custom handled by HostFactory
            /// </summary>
            [WpfFact]
            public void AutoSynchronizeSettings()
            {
                Create();
                Assert.False(_host.AutoSynchronizeSettings);
            }

            [WpfFact]
            public void DefaultSettingsTiedToApplicationSettings()
            {
                Create();
                foreach (var cur in Enum.GetValues(typeof(DefaultSettings)).Cast<DefaultSettings>())
                {
                    _vimApplicationSettings.SetupGet(x => x.DefaultSettings).Returns(cur);
                    Assert.Equal(cur, _hostRaw.DefaultSettings);
                }
            }
        }
    }
}
