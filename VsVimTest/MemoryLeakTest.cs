
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UI.Wpf;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
namespace VsVim.UnitTest
{
    /// <summary>
    /// At least a cursory attempt at getting memory leak detection into a unit test.  By 
    /// no means a thorough example because I can't accurately simulate Visual Studio 
    /// integration without starting Visual Studio.  But this should at least help me catch
    /// a portion of them. 
    /// </summary>
    [TestFixture]
    public sealed class MemoryLeakTest
    {
        #region Exports

        [Export(typeof(IExtensionErrorHandler))]
        private sealed class ErrorHandler : IExtensionErrorHandler
        {
            public void HandleError(object sender, Exception exception)
            {
                _errorInService = exception.ToString();
            }
        }

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

        #endregion

        /// <summary>
        /// Due to the way the MEF components are spun up errors like a missing service will
        /// result in an exception which is swallowed by the editor layer.  To counter this
        /// we provide an IExtensionErrorHandler to log any errors that occur on startup.
        /// </summary>
        private static string _errorInService;

        /// <summary>
        /// This field is used to hold the IVim instance if it's created.  This is essential
        /// to ensuring a leak doesn't occur by a Vim global service such as the change tracker
        /// holding ont an IVimBuffer after it's closed
        /// </summary>
        private IVim _vim;

        [SetUp]
        public void SetUp()
        {
            _errorInService = null;
        }

        [TearDown]
        public void TearDown()
        {
            Assert.IsNull(_errorInService, _errorInService);
            _vim = null;
        }

        private void RunGarbageCollector()
        {
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private IVimBuffer CreateVimBuffer()
        {
            var list = EditorUtil.GetEditorCatalog();
            list.Add(new AssemblyCatalog(typeof(Vim.IVim).Assembly));
            list.Add(new AssemblyCatalog(typeof(Vim.UI.Wpf.KeyProcessor).Assembly));
            list.Add(new AssemblyCatalog(typeof(VsVim.VsCommandTarget).Assembly));
            list.Add(new TypeCatalog(
                typeof(VsVim.UnitTest.MemoryLeakTest.ServiceProvider),
                typeof(VsVim.UnitTest.MemoryLeakTest.VsEditorAdaptersFactoryService),
                typeof(VsVim.UnitTest.MemoryLeakTest.ErrorHandler)));

            var catalog = new AggregateCatalog(list.ToArray());
            var container = new CompositionContainer(catalog);
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            _vim = container.GetExport<IVim>().Value;
            var vimBuffer = _vim.GetOrCreateBuffer(textView);
            Assert.IsNotNull(vimBuffer);

            // Do one round of DoEvents since several services queue up actions to 
            // take immediately after the IVimBuffer is created
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
            }

            return vimBuffer;
        }

        [Test]
        [Description("Take VsVim out of the equation and make sure the test is functioning properly")]
        public void TextViewOnly()
        {
            var list = EditorUtil.GetEditorCatalog();
            var catalog = new AggregateCatalog(list.ToArray());
            var container = new CompositionContainer(catalog);
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();
            var weakReference = new WeakReference(textView);
            textView.Close();
            textView = null;

            RunGarbageCollector();
            Assert.IsNull(weakReference.Target);
        }

        [Test]
        public void VimWpfDoesntHoldBuffer()
        {
            var list = EditorUtil.GetEditorCatalog();
            list.Add(new TypeCatalog(typeof(Vim.UnitTest.Exports.VimHost)));
            list.Add(new AssemblyCatalog(typeof(Vim.IVim).Assembly));
            list.Add(new AssemblyCatalog(typeof(Vim.UI.Wpf.KeyProcessor).Assembly));
            var catalog = new AggregateCatalog(list.ToArray());
            var container = new CompositionContainer(catalog);
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            var vim = container.GetExport<IVim>().Value;
            var vimBuffer = vim.GetOrCreateBuffer(textView);
            Assert.IsNotNull(vimBuffer);

            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(textView);

            // Clean up 
            textView.Close();
            textView = null;
            vimBuffer.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.IsNull(weakVimBuffer.Target);
            Assert.IsNull(weakTextView.Target);
        }

        [Test]
        public void VsVimDoesntHoldBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.IsNull(weakVimBuffer.Target);
            Assert.IsNull(weakTextView.Target);
        }

        [Test]
        public void SetGlobalMarkAndClose()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.MarkMap.SetMark(vimBuffer.TextSnapshot.GetPoint(0), 'a');
            vimBuffer.MarkMap.SetMark(vimBuffer.TextSnapshot.GetPoint(0), 'A');
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.IsNull(weakVimBuffer.Target);
            Assert.IsNull(weakTextView.Target);
        }

        /// <summary>
        /// Change tracking is currently IVimBuffer specific.  Want to make sure it's
        /// not indirectly holding onto an IVimBuffer reference
        /// </summary>
        [Test]
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
            Assert.IsNull(weakVimBuffer.Target);
            Assert.IsNull(weakTextView.Target);
        }

    }
}
