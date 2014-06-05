using EditorUtils;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Vim.UI.Wpf;
using Vim.UnitTest;
using VsVim.Implementation.Misc;
using Xunit;
using Microsoft.VisualStudio.Utilities;

namespace VsVim.UnitTest
{
    public abstract class HostFactoryTest : VimTestBase
    {
        private readonly IVimGlobalSettings _globalSettings;
        private readonly TestableProtectedOperations _protectedOperations;
        private readonly HostFactory _hostFactory;
        private readonly MockRepository _mockFactory;
        private readonly Mock<IVsEditorAdaptersFactoryService> _vsEditorAdaptersFactoryService;
        private readonly Mock<IEditorToSettingsSynchronizer> _synchronizer;

        protected HostFactoryTest()
        {
            _globalSettings = Vim.GlobalSettings;
            _protectedOperations = new TestableProtectedOperations();
            _mockFactory = new MockRepository(MockBehavior.Strict);
            _synchronizer = _mockFactory.Create<IEditorToSettingsSynchronizer>(MockBehavior.Strict);
            _vsEditorAdaptersFactoryService = _mockFactory.Create<IVsEditorAdaptersFactoryService>();

            var vsAdapter = _mockFactory.Create<IVsAdapter>();
            vsAdapter.SetupGet(x => x.EditorAdapter).Returns(_vsEditorAdaptersFactoryService.Object);

            _hostFactory = new HostFactory(
                Vim,
                _vsEditorAdaptersFactoryService.Object,
                _mockFactory.Create<IDisplayWindowBrokerFactoryService>(MockBehavior.Loose).Object,
                _mockFactory.Create<ITextManager>(MockBehavior.Loose).Object,
                vsAdapter.Object,
                _protectedOperations,
                new VimBufferCoordinatorFactory(Vim),
                _mockFactory.Create<IKeyUtil>(MockBehavior.Loose).Object,
                _synchronizer.Object,
                new Lazy<ICommandTargetFactory, IOrderable>[] { });
        }

        private void InvalidateSynchronizer()
        {
            _synchronizer.Setup(x => x.StartSynchronizing(It.IsAny<IVimBuffer>())).Throws(new Exception());
            _synchronizer.Setup(x => x.CopyEditorToVimSettings(It.IsAny<IVimBuffer>())).Throws(new Exception());
            _synchronizer.Setup(x => x.CopyVimToEditorSettings(It.IsAny<IVimBuffer>())).Throws(new Exception());
        }

        private void RaiseTextViewCreated(ITextView textView)
        {
            var listener = (IWpfTextViewCreationListener)_hostFactory;
            listener.TextViewCreated((IWpfTextView)textView);
        }

        private void RaiseVimBufferCreated(IVimBuffer vimBuffer)
        {
            var listener = (IVimBufferCreationListener)_hostFactory;
            listener.VimBufferCreated(vimBuffer);
        }

        private void RaiseVsTextViewCreated(IVsTextView vsTextView)
        {
            var listener = (IVsTextViewCreationListener)_hostFactory;
            listener.VsTextViewCreated(vsTextView);
        }

        public sealed class SettingsTest : HostFactoryTest
        {
            private readonly IVimBuffer _vimBuffer;
            private readonly ITextView _textView;
            private Mock<IVsTextView> _vsTextView;

            public SettingsTest()
            {
                _vimBuffer = CreateVimBuffer("hello world");
                _textView = _vimBuffer.TextView;
            }

            private void SetupVsTextView()
            {
                _vsTextView = _mockFactory.Create<IVsTextView>(MockBehavior.Loose);
                _vsEditorAdaptersFactoryService.Setup(x => x.GetWpfTextView(_vsTextView.Object)).Returns((IWpfTextView)_textView);
                _vsEditorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(_textView.TextBuffer)).Returns((IVsTextBuffer)null);
            }

            /// <summary>
            /// Any creation of an ITextView should cause us to post to the queue to eventually
            /// begin setting synchronization
            /// </summary>
            [Fact]
            public void TextViewCreatedShouldPost()
            {
                RaiseTextViewCreated(_textView);
                Assert.Equal(1, _protectedOperations.PostedActionCount);
            }

            /// <summary>
            /// If we only see an ITextView instance, and no IVsTextView, then the settings should
            /// be synchronized once the post sets up 
            /// </summary>
            [Fact]
            public void TextViewOnlyNoVimRc()
            {
                VimRcState = VimRcState.LoadFailed;
                RaiseTextViewCreated(_textView);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer)).Verifiable();
                _synchronizer.Setup(x => x.CopyEditorToVimSettings(_vimBuffer)).Verifiable();
                _protectedOperations.RunAll();
                _synchronizer.Verify();
            }

            [Fact]
            public void TextViewOnlyWithVimRc()
            {
                VimRcState = VimRcState.NewLoadSucceeded("test");
                RaiseTextViewCreated(_textView);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer)).Verifiable();
                _synchronizer.Setup(x => x.CopyVimToEditorSettings(_vimBuffer)).Verifiable();
                _protectedOperations.RunAll();
                _synchronizer.Verify();
            }

            [Fact]
            public void TextViewOnlyWithVimRcAndEditorDefaults()
            {
                VimRcState = VimRcState.NewLoadSucceeded("test");
                _globalSettings.UseEditorDefaults = true;                
                RaiseTextViewCreated(_textView);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer)).Verifiable();
                _synchronizer.Setup(x => x.CopyEditorToVimSettings(_vimBuffer)).Verifiable();
                _protectedOperations.RunAll();
                _synchronizer.Verify();
            }

            /// <summary>
            /// The settings sync code needs to account for the case that the IVimBuffer is closed before
            /// the post runs
            /// </summary>
            [Fact]
            public void CloseBeforePost()
            {
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);
                _textView.Close();
                _protectedOperations.RunAll();
            }

            /// <summary>
            /// When the IVsTextView is created we should immediately synchronize settings as the load 
            /// process is complete at this point
            /// </summary>
            [Fact]
            public void BothViewsNoVimRc()
            {
                SetupVsTextView();
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer)).Verifiable();
                _synchronizer.Setup(x => x.CopyEditorToVimSettings(_vimBuffer)).Verifiable();
                RaiseVsTextViewCreated(_vsTextView.Object);
                _synchronizer.Verify();
                InvalidateSynchronizer();
                _protectedOperations.RunAll();
            }

            [Fact]
            public void BothViewsWithVimRc()
            {
                VimRcState = VimRcState.NewLoadSucceeded("test");
                SetupVsTextView();
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer)).Verifiable();
                _synchronizer.Setup(x => x.CopyVimToEditorSettings(_vimBuffer)).Verifiable();
                RaiseVsTextViewCreated(_vsTextView.Object);
                _synchronizer.Verify();
                InvalidateSynchronizer();
                _protectedOperations.RunAll();
            }

            [Fact]
            public void BothViewsWithVimRcAndEditorDefaults()
            {
                VimRcState = VimRcState.NewLoadSucceeded("test");
                _globalSettings.UseEditorDefaults = true;
                SetupVsTextView();
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer)).Verifiable();
                _synchronizer.Setup(x => x.CopyEditorToVimSettings(_vimBuffer)).Verifiable();
                RaiseVsTextViewCreated(_vsTextView.Object);
                _synchronizer.Verify();
                InvalidateSynchronizer();
                _protectedOperations.RunAll();
            }
        }
    }
}
