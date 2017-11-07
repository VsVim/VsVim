﻿using Vim.EditorHost;
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
using Vim.VisualStudio.Implementation.Misc;
using Xunit;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Threading;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class HostFactoryTest : VimTestBase
    {
        private readonly IVimGlobalSettings _globalSettings;
        private readonly HostFactory _hostFactory;
        private readonly MockRepository _mockFactory;
        private readonly Mock<IVsEditorAdaptersFactoryService> _vsEditorAdaptersFactoryService;
        private readonly Mock<IEditorToSettingsSynchronizer> _synchronizer;
        private readonly Mock<IVimApplicationSettings> _vimApplicationSettings;

        protected HostFactoryTest()
        {
            _globalSettings = Vim.GlobalSettings;
            _mockFactory = new MockRepository(MockBehavior.Strict);
            _synchronizer = _mockFactory.Create<IEditorToSettingsSynchronizer>(MockBehavior.Strict);
            _synchronizer.Setup(x => x.SyncSetting(It.IsAny<SettingSyncData>()));
            _vsEditorAdaptersFactoryService = _mockFactory.Create<IVsEditorAdaptersFactoryService>();
            _vimApplicationSettings = _mockFactory.Create<IVimApplicationSettings>(MockBehavior.Loose);

            var vsAdapter = _mockFactory.Create<IVsAdapter>();
            vsAdapter.SetupGet(x => x.EditorAdapter).Returns(_vsEditorAdaptersFactoryService.Object);

            _hostFactory = new HostFactory(
                Vim,
                _vsEditorAdaptersFactoryService.Object,
                _mockFactory.Create<IDisplayWindowBrokerFactoryService>(MockBehavior.Loose).Object,
                _mockFactory.Create<ITextManager>(MockBehavior.Loose).Object,
                vsAdapter.Object,
                ProtectedOperations,
                new VimBufferCoordinatorFactory(Vim),
                _mockFactory.Create<IKeyUtil>(MockBehavior.Loose).Object,
                _synchronizer.Object,
                _vimApplicationSettings.Object,
                new Lazy<ICommandTargetFactory, IOrderable>[] { });
        }

        private void InvalidateSynchronizer()
        {
            _synchronizer.Setup(x => x.StartSynchronizing(It.IsAny<IVimBuffer>(), It.IsAny<SettingSyncSource>())).Throws(new Exception());
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
                _vimApplicationSettings.SetupGet(x => x.UseEditorDefaults).Returns(true);
            }

            private void SetupVsTextView()
            {
                _vsTextView = _mockFactory.Create<IVsTextView>(MockBehavior.Loose);
                _vsEditorAdaptersFactoryService.Setup(x => x.GetWpfTextView(_vsTextView.Object)).Returns((IWpfTextView)_textView);
                _vsEditorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(_textView.TextBuffer)).Returns((IVsTextBuffer)null);
            }

            /// <summary>
            /// If we only see an ITextView instance, and no IVsTextView, then the settings should
            /// be synchronized once the post sets up 
            /// </summary>
            [WpfFact]
            public void TextViewOnlyNoVimRc()
            {
                VimRcState = VimRcState.LoadFailed;
                RaiseTextViewCreated(_textView);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer, SettingSyncSource.Editor)).Verifiable();
                TestableSynchronizationContext.RunAll();
                Dispatcher.CurrentDispatcher.DoEvents();
                _synchronizer.Verify();
            }

            [WpfFact]
            public void TextViewOnlyUseVim()
            {
                _vimApplicationSettings.SetupGet(x => x.UseEditorDefaults).Returns(false);
                VimRcState = VimRcState.NewLoadSucceeded(new VimRcPath(VimRcKind.VimRc, "test"), new string[] { });
                RaiseTextViewCreated(_textView);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer, SettingSyncSource.Vim)).Verifiable();
                TestableSynchronizationContext.RunAll();
                Dispatcher.CurrentDispatcher.DoEvents();
                _synchronizer.Verify();
            }

            [WpfFact]
            public void TextViewOnlyWithVimRcAndEditorDefaults()
            {
                VimRcState = VimRcState.NewLoadSucceeded(new VimRcPath(VimRcKind.VimRc, "test"), new string[] { });
                _vimApplicationSettings.SetupGet(x => x.UseEditorDefaults).Returns(true);
                RaiseTextViewCreated(_textView);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer, SettingSyncSource.Editor)).Verifiable();
                TestableSynchronizationContext.RunAll();
                Dispatcher.CurrentDispatcher.DoEvents();
                _synchronizer.Verify();
            }

            /// <summary>
            /// The settings sync code needs to account for the case that the IVimBuffer is closed before
            /// the post runs
            /// </summary>
            [WpfFact]
            public void CloseBeforePost()
            {
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);
                _textView.Close();
                TestableSynchronizationContext.RunAll();
            }

            /// <summary>
            /// When the IVsTextView is created we should immediately synchronize settings as the load 
            /// process is complete at this point
            /// </summary>
            [WpfFact]
            public void BothViewsUseEditor()
            {
                _vimApplicationSettings.SetupGet(x => x.UseEditorDefaults).Returns(true);
                SetupVsTextView();
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer, SettingSyncSource.Editor)).Verifiable();
                RaiseVsTextViewCreated(_vsTextView.Object);
                _synchronizer.Verify();
                InvalidateSynchronizer();
                TestableSynchronizationContext.RunAll();
            }

            [WpfFact]
            public void BothViewsUseVim()
            {
                _vimApplicationSettings.SetupGet(x => x.UseEditorDefaults).Returns(false);
                VimRcState = VimRcState.NewLoadSucceeded(new VimRcPath(VimRcKind.VimRc, "test"), new string[] { });
                SetupVsTextView();
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer, SettingSyncSource.Vim)).Verifiable();
                RaiseVsTextViewCreated(_vsTextView.Object);
                _synchronizer.Verify();
                InvalidateSynchronizer();
                TestableSynchronizationContext.RunAll();
            }

            [WpfFact]
            public void BothViewsWithVimRcAndEditorDefaults()
            {
                VimRcState = VimRcState.NewLoadSucceeded(new VimRcPath(VimRcKind.VimRc, "test"), new string[] { });
                _vimApplicationSettings.SetupGet(x => x.UseEditorDefaults).Returns(true);
                SetupVsTextView();
                RaiseTextViewCreated(_textView);
                RaiseVimBufferCreated(_vimBuffer);

                _synchronizer.Setup(x => x.StartSynchronizing(_vimBuffer, SettingSyncSource.Editor)).Verifiable();
                RaiseVsTextViewCreated(_vsTextView.Object);
                _synchronizer.Verify();
                InvalidateSynchronizer();
                TestableSynchronizationContext.RunAll();
            }
        }
    }
}
