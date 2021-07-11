using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;

namespace VsVimSharedTest.Implementation
{
    [Export(typeof(IVsEditorAdaptersFactoryService))]
    [Export(typeof(TestableEditorAdaptersFactoryService))]
    internal sealed class TestableEditorAdaptersFactoryService : IVsEditorAdaptersFactoryService
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
            return null;
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
}
