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
using Vim.UI.Wpf;
using Vim.UnitTest;
using VsVim.UnitTest.Mock;
using Xunit;

namespace VsVim.UnitTest
{
    /// <summary>
    /// At least a cursory attempt at getting memory leak detection into a unit test.  By 
    /// no means a thorough example because I can't accurately simulate Visual Studio 
    /// integration without starting Visual Studio.  But this should at least help me catch
    /// a portion of them. 
    /// </summary>
    public sealed class MemoryLeakTest : VimTestBase
    {
        #region Exports

        [Export(typeof(SVsServiceProvider))]
        private sealed class ServiceProvider : SVsServiceProvider
        {
            private MockRepository _factory = new MockRepository(MockBehavior.Loose);
            private Dictionary<Type, object> _serviceMap = new Dictionary<Type, object>();

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

        [Export(typeof(ILegacySettings))]
        private sealed class LegacySettings : ILegacySettings
        {
            public bool HaveUpdatedKeyBindings { get; set; }
            public bool IgnoredConflictingKeyBinding { get; set; }
            public ReadOnlyCollection<CommandBindingSetting> RemovedBindings  
            {
                get { return new ReadOnlyCollection<CommandBindingSetting>(new CommandBindingSetting[] {}); }
                set { }
            }

            public void Save()
            {

            }
        }

        #endregion

        /// <summary>
        /// This is the CompositionContainer specifically for the memory leak test.  This
        /// has several custom types inserted which are intended to enhance the memory leak
        /// diagnostics.  This is the only CompositionContainer which should be used in this
        /// test
        /// </summary>
        [ThreadStatic]
        private static CompositionContainer _compositionContainer;

        private void RunGarbageCollector()
        {
            for (var i = 0; i < 15; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        protected override CompositionContainer GetOrCreateCompositionContainer()
        {
            if (_compositionContainer == null)
            {
                var list = GetEditorUtilsCatalog();
                list.Add(new AssemblyCatalog(typeof(Vim.IVim).Assembly));
                list.Add(new AssemblyCatalog(typeof(Vim.UI.Wpf.VimKeyProcessor).Assembly));
                list.Add(new AssemblyCatalog(typeof(VsVim.VsCommandTarget).Assembly));
                list.Add(new TypeCatalog(
                    typeof(Vim.UnitTest.Exports.VimErrorDetector),
                    typeof(VsVim.UnitTest.MemoryLeakTest.ServiceProvider),
                    typeof(VsVim.UnitTest.MemoryLeakTest.VsEditorAdaptersFactoryService),
                    typeof(VsVim.UnitTest.MemoryLeakTest.LegacySettings)));

                var catalog = new AggregateCatalog(list.ToArray());
                _compositionContainer = new CompositionContainer(catalog);
            }

            return _compositionContainer;
        }

        private IVimBuffer CreateVimBuffer()
        {
            var container = GetOrCreateCompositionContainer();
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            var vimBuffer = Vim.GetOrCreateVimBuffer(textView);
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
        /// Run a sanity check which just tests the ability for an ITextView to be created
        /// and closed without leaking memory that doesn't involve the creation of an 
        /// IVimBuffer
        /// </summary>
        [Fact]
        public void TextViewOnly()
        {
            var container = GetOrCreateCompositionContainer();
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
            var container = GetOrCreateCompositionContainer();
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
            var container = GetOrCreateCompositionContainer();
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            var vim = container.GetExport<IVim>().Value;
            var vimBuffer = vim.GetOrCreateVimBuffer(textView);
            Assert.NotNull(vimBuffer);

            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(textView);

            // Clean up 
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
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

    }
}
