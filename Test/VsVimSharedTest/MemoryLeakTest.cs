using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Threading;
using EditorUtils;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using Vim.UnitTest;
using Vim.UnitTest.Exports;
using Vim.VisualStudio.UnitTest.Mock;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    /// <summary>
    /// At least a cursory attempt at getting memory leak detection into a unit test.  By 
    /// no means a thorough example because I can't accurately simulate Visual Studio 
    /// integration without starting Visual Studio.  But this should at least help me catch
    /// a portion of them. 
    /// </summary>
    public sealed class MemoryLeakTest : IDisposable
    {
        #region Exports

        /// <summary>
        /// This smooths out the nonsense type equality problems that come with having NoPia
        /// enabled on only some of the assemblies.  
        /// </summary>
        private sealed class TypeEqualityComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                return
                    x.FullName == y.FullName &&
                    x.GUID == y.GUID;
            }

            public int GetHashCode(Type obj)
            {
                return obj != null ? obj.GUID.GetHashCode() : 0;
            }
        }

        [Export(typeof(SVsServiceProvider))]
        private sealed class ServiceProvider : SVsServiceProvider
        {
            private MockRepository _factory = new MockRepository(MockBehavior.Loose);
            private Dictionary<Type, object> _serviceMap = new Dictionary<Type, object>(new TypeEqualityComparer());

            public ServiceProvider()
            {
                _serviceMap[typeof(SVsShell)] = _factory.Create<IVsShell>().Object;
                _serviceMap[typeof(SVsTextManager)] = _factory.Create<IVsTextManager>().Object;
                _serviceMap[typeof(SVsRunningDocumentTable)] = _factory.Create<IVsRunningDocumentTable>().Object;
                _serviceMap[typeof(SVsUIShell)] = _factory.Create<IVsUIShell>().As<IVsUIShell4>().Object;
                _serviceMap[typeof(SVsShellMonitorSelection)] = _factory.Create<IVsMonitorSelection>().Object;
                _serviceMap[typeof(IVsExtensibility)] = _factory.Create<IVsExtensibility>().Object;
                var dte = MockObjectFactory.CreateDteWithCommands();
                _serviceMap[typeof(_DTE)] = dte.Object;
                _serviceMap[typeof(SDTE)] = dte.Object;
                _serviceMap[typeof(SVsSettingsManager)] = CreateSettingsManager().Object;
                _serviceMap[typeof(SVsFindManager)] = _factory.Create<IVsFindManager>().Object;
            }

            private Mock<IVsSettingsManager> CreateSettingsManager()
            {
                var settingsManager = _factory.Create<IVsSettingsManager>();

                var writableSettingsStore = _factory.Create<IVsWritableSettingsStore>();
                var local = writableSettingsStore.Object;
                settingsManager.Setup(x => x.GetWritableSettingsStore(It.IsAny<uint>(), out local)).Returns(VSConstants.S_OK);

                return settingsManager;
            }

            public object GetService(Type serviceType)
            {
                return _serviceMap[serviceType];
            }
        }

        [Export(typeof(IVsEditorAdaptersFactoryService))]
        private sealed class VsEditorAdaptersFactoryService : IVsEditorAdaptersFactoryService
        {
            private MockRepository _factory = new MockRepository(MockBehavior.Loose);
            public IVsCodeWindow CreateVsCodeWindowAdapter(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider)
            {
                throw new NotImplementedException();
            }

            public IVsTextBuffer CreateVsTextBufferAdapter(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider, Microsoft.VisualStudio.Utilities.IContentType contentType)
            {
                throw new NotImplementedException();
            }

            public IVsTextBuffer CreateVsTextBufferAdapter(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider)
            {
                throw new NotImplementedException();
            }

            public IVsTextBuffer CreateVsTextBufferAdapterForSecondaryBuffer(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider, Microsoft.VisualStudio.Text.ITextBuffer secondaryBuffer)
            {
                throw new NotImplementedException();
            }

            public IVsTextBufferCoordinator CreateVsTextBufferCoordinatorAdapter()
            {
                throw new NotImplementedException();
            }

            public IVsTextView CreateVsTextViewAdapter(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider, ITextViewRoleSet roles)
            {
                throw new NotImplementedException();
            }

            public IVsTextView CreateVsTextViewAdapter(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider)
            {
                throw new NotImplementedException();
            }

            public IVsTextBuffer GetBufferAdapter(ITextBuffer textBuffer)
            {
                var lines = _factory.Create<IVsTextLines>();
                IVsEnumLineMarkers markers;
                lines
                    .Setup(x => x.EnumMarkers(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<uint>(), out markers))
                    .Returns(VSConstants.E_FAIL);
                return lines.Object;
            }

            public Microsoft.VisualStudio.Text.ITextBuffer GetDataBuffer(IVsTextBuffer bufferAdapter)
            {
                throw new NotImplementedException();
            }

            public Microsoft.VisualStudio.Text.ITextBuffer GetDocumentBuffer(IVsTextBuffer bufferAdapter)
            {
                throw new NotImplementedException();
            }

            public IVsTextView GetViewAdapter(ITextView textView)
            {
                throw new NotImplementedException();
            }

            public IWpfTextView GetWpfTextView(IVsTextView viewAdapter)
            {
                throw new NotImplementedException();
            }

            public IWpfTextViewHost GetWpfTextViewHost(IVsTextView viewAdapter)
            {
                throw new NotImplementedException();
            }

            public void SetDataBuffer(IVsTextBuffer bufferAdapter, Microsoft.VisualStudio.Text.ITextBuffer dataBuffer)
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        private readonly VimEditorHost _vimEditorHost;
        private readonly TestableSynchronizationContext _synchronizationContext;

        public MemoryLeakTest()
        {
            _vimEditorHost = CreateVimEditorHost();
            _synchronizationContext = new TestableSynchronizationContext();
            _synchronizationContext.Install();
        }

        public void Dispose()
        {
            _synchronizationContext.RunAll();
            _synchronizationContext.Uninstall();
        }

        private void RunGarbageCollector()
        {
            for (var i = 0; i < 15; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);
                GC.Collect();
            }
        }

        private void ClearHistory(ITextBuffer textBuffer)
        {
            IBasicUndoHistory basicUndoHistory;
            if (_vimEditorHost.BasicUndoHistoryRegistry.TryGetBasicUndoHistory(textBuffer, out basicUndoHistory))
            {
                basicUndoHistory.Clear();
            }
        }

        private static VimEditorHost CreateVimEditorHost()
        {
            var editorHostFactory = new EditorHostFactory();
            editorHostFactory.Add(new AssemblyCatalog(typeof(Vim.IVim).Assembly));
            editorHostFactory.Add(new AssemblyCatalog(typeof(Vim.UI.Wpf.VimKeyProcessor).Assembly));
            editorHostFactory.Add(new AssemblyCatalog(typeof(VsCommandTarget).Assembly));
            editorHostFactory.Add(new AssemblyCatalog(typeof(ISharedService).Assembly));
            editorHostFactory.Add(new TypeCatalog(
                typeof(Vim.VisualStudio.UnitTest.MemoryLeakTest.ServiceProvider),
                typeof(Vim.VisualStudio.UnitTest.MemoryLeakTest.VsEditorAdaptersFactoryService),
                typeof(VimErrorDetector)));

            return new VimEditorHost(editorHostFactory.CreateCompositionContainer());
        }

        private IVimBuffer CreateVimBuffer()
        {
            var factory = _vimEditorHost.CompositionContainer.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            var vimBuffer = _vimEditorHost.Vim.GetOrCreateVimBuffer(textView);
            Assert.NotNull(vimBuffer);

            // Do one round of DoEvents since several services queue up actions to 
            // take immediately after the IVimBuffer is created
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
            }

            return vimBuffer;
        }

        /// <summary>
        /// Make sure that we respect the host policy on whether or not an IVimBuffer should be created for a given
        /// ITextView
        ///
        /// This test is here because it's one of the few places where we load every component in every assembly into
        /// our MEF container.  This gives us the best chance of catching a random new component which accidentally
        /// introduces a new IVimBuffer against the host policy
        /// </summary>
        [Fact]
        public void RespectHostCreationPolicy()
        {
            var container = _vimEditorHost.CompositionContainer;
            var vsVimHost = container.GetExportedValue<VsVimHost>();
            vsVimHost.DisableVimBufferCreation = true;
            try
            {
                var factory = container.GetExportedValue<ITextEditorFactoryService>();
                var textView = factory.CreateTextView();
                var vim = container.GetExportedValue<IVim>();
                IVimBuffer vimBuffer;
                Assert.False(vim.TryGetVimBuffer(textView, out vimBuffer));
            }
            finally
            {
                vsVimHost.DisableVimBufferCreation = false;
            }
        }

        /// <summary>
        /// Run a sanity check which just tests the ability for an ITextView to be created
        /// and closed without leaking memory that doesn't involve the creation of an 
        /// IVimBuffer
        /// 
        /// TODO: This actually creates an IVimBuffer instance.  Right now IVim will essentially
        /// create an IVimBuffer for every ITextView created hence one is created here.  Need
        /// to fix this so we have a base case to judge the memory leak tests by
        /// </summary>
        [Fact]
        public void TextViewOnly()
        {
            var container = _vimEditorHost.CompositionContainer;
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();
            var weakReference = new WeakReference(textView);
            textView.Close();
            textView = null;

            RunGarbageCollector();
            Assert.Null(weakReference.Target);
        }

        /// <summary>
        /// Run a sanity check which just tests the ability for an ITextViewHost to be created
        /// and closed without leaking memory that doesn't involve the creation of an
        /// IVimBuffer
        /// </summary>
        [Fact]
        public void TextViewHostOnly()
        {
            var container = _vimEditorHost.CompositionContainer;
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();
            var textViewHost = factory.CreateTextViewHost(textView, setFocus: true);
            var weakReference = new WeakReference(textViewHost);
            textViewHost.Close();
            textView = null;
            textViewHost = null;

            RunGarbageCollector();
            Assert.Null(weakReference.Target);
        }

        [Fact]
        public void VimWpfDoesntHoldBuffer()
        {
            var container = _vimEditorHost.CompositionContainer;
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            var vim = container.GetExport<IVim>().Value;
            var vimBuffer = vim.GetOrCreateVimBuffer(textView);
            Assert.NotNull(vimBuffer);

            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(textView);

            // Clean up 
            ClearHistory(textView.TextBuffer);
            textView.Close();
            textView = null;
            Assert.True(vimBuffer.IsClosed);
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        [Fact]
        public void VsVimDoesntHoldBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        [Fact]
        public void SetGlobalMarkAndClose()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.MarkMap.SetMark(Mark.OfChar('a').Value, vimBuffer.VimBufferData, 0, 0);
            vimBuffer.MarkMap.SetMark(Mark.OfChar('A').Value, vimBuffer.VimBufferData, 0, 0);
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);
            var localSettings = vimBuffer.LocalSettings;

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
            Assert.NotNull(localSettings);
        }

        /// <summary>
        /// Change tracking is currently IVimBuffer specific.  Want to make sure it's
        /// not indirectly holding onto an IVimBuffer reference
        /// </summary>
        [Fact]
        public void ChangeTrackerDoesntHoldTheBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.TextBuffer.SetText("hello world");
            vimBuffer.Process("dw");
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            ClearHistory(vimBuffer.TextBuffer);
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        /// <summary>
        /// Make sure the caching which comes with searching doesn't hold onto the buffer
        /// </summary>
        [Fact]
        public void SearchCacheDoesntHoldTheBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.TextBuffer.SetText("hello world");
            vimBuffer.Process("/world", enter: true);
            var weakTextBuffer = new WeakReference(vimBuffer.TextBuffer);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakTextBuffer.Target);
        }
    }
}
