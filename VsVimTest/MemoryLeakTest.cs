
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
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
                _serviceMap[typeof(_DTE)] = _factory.Create<_DTE>().Object;
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
                return _factory.Create<IVsTextLines>().Object;
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

        private void RunGarbageCollector()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        [Test]
        [Description("Take VsVim out of the equation and make sure the test is functioning properly")]
        public void LeakTest_TextViewOnly()
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
        public void LeakTest_VimWpfDoesntHoldBuffer()
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
        public void LeakTest_VsVimfDoesntHoldBuffer()
        {
            var list = EditorUtil.GetEditorCatalog();
            list.Add(new AssemblyCatalog(typeof(Vim.IVim).Assembly));
            list.Add(new AssemblyCatalog(typeof(Vim.UI.Wpf.KeyProcessor).Assembly));
            list.Add(new AssemblyCatalog(typeof(VsVim.VsCommandTarget).Assembly));
            list.Add(new TypeCatalog(
                typeof(VsVim.UnitTest.MemoryLeakTest.ServiceProvider),
                typeof(VsVim.UnitTest.MemoryLeakTest.VsEditorAdaptersFactoryService)));

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
    }
}
