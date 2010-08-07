using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;

namespace VsVim.Implementation
{
    [Export(typeof(IVsAdapter))]
    internal sealed class VsAdapter : IVsAdapter
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IVsTextManager _textManager;
        private readonly IVsUIShell _uiShell;
        private readonly RunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        internal VsAdapter(
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = new RunningDocumentTable(_serviceProvider);
            _uiShell = _serviceProvider.GetService<SVsUIShell, IVsUIShell>();
        }

        public bool TryGetCodeWindow(ITextView textView, out IVsCodeWindow codeWindow)
        {
            IVsWindowFrame frame;
            if (TryGetContainingWindowFrame(textView, out frame))
            {
                codeWindow = frame.GetCodeWindow();
                return true;
            }

            codeWindow = null;
            return false;
        }

        public bool TryGetContainingWindowFrame(ITextView textView, out IVsWindowFrame windowFrame)
        {
            var targetView = _editorAdaptersFactoryService.GetViewAdapter(textView);
            return TryGetContainingWindowFrame(targetView, out windowFrame);
        }

        public bool TryGetContainingWindowFrame(IVsTextView textView, out IVsWindowFrame windowFrame)
        {
            foreach ( var frame in _uiShell.GetDocumentWindowFrames())
            {
                var codeWindow = frame.GetCodeWindow();
                IVsTextView vsTextView;
                if (ErrorHandler.Succeeded(codeWindow.GetPrimaryView(out vsTextView)) && NativeMethods.IsSameComObject(vsTextView, textView))
                {
                    windowFrame = frame;
                    return true;
                }

                if (ErrorHandler.Succeeded(codeWindow.GetSecondaryView(out vsTextView)) && NativeMethods.IsSameComObject(vsTextView, textView))
                {
                    windowFrame = frame;
                    return true;
                }
            }

            windowFrame = null;
            return false;
        }

        public IEnumerable<IVsTextView> GetTextViews(ITextBuffer textBuffer)
        {
            var vsTextBuffer = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer);
            if (vsTextBuffer == null)
            {
                return Enumerable.Empty<IVsTextView>();
            }

            IVsEnumTextViews vsEnum;
            ErrorHandler.ThrowOnFailure(_textManager.EnumViews(vsTextBuffer, out vsEnum));

            var list = new List<IVsTextView>();
            var done = false;
            var array = new IVsTextView[1];
            do
            {
                uint found = 0;
                var hr = vsEnum.Next((uint)array.Length, array, ref found);
                ErrorHandler.ThrowOnFailure(hr);
                if (VSConstants.S_OK == hr && array[0] != null)
                {
                    list.Add(array[0]);
                }
                else
                {
                    done = true;
                }
            }
            while (!done);

            return list;
        }

        public bool TryGetTextBufferForDocCookie(uint cookie, out ITextBuffer buffer)
        {
            var info = _table.GetDocumentInfo(cookie);
            var obj = info.DocData;
            var vsTextLines = obj as IVsTextLines;
            var vsTextBufferProvider = obj as IVsTextBufferProvider;
            if (vsTextLines != null)
            {
                buffer = _editorAdaptersFactoryService.GetDataBuffer(vsTextLines);
            }
            else if (vsTextBufferProvider != null
                && ErrorHandler.Succeeded(vsTextBufferProvider.GetTextBuffer(out vsTextLines))
                && vsTextLines != null)
            {
                buffer = _editorAdaptersFactoryService.GetDataBuffer(vsTextLines);
            }
            else
            {
                buffer = null;
            }

            return buffer != null;
        }

    }
}
