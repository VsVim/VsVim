using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.Implementation
{
    [Export(typeof(IVsAdapter))]
    internal sealed class VsAdapter : IVsAdapter
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly IIncrementalSearchFactoryService _incrementalSearchFactoryService;
        private readonly IVsTextManager _textManager;
        private readonly IVsUIShell _uiShell;
        private readonly RunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsMonitorSelection _monitorSelection;

        public bool InDebugMode
        {
            get
            {
                var result = _monitorSelection.IsCmdUIContextActive(VSConstants.UICONTEXT_Debugging);
                return result.IsSuccess && result.Value;
            }
        }

        public bool InAutomationFunction
        {
            get { return VsShellUtilities.IsInAutomationFunction(_serviceProvider); }
        }

        public IVsEditorAdaptersFactoryService EditorAdapter
        {
            get { return _editorAdaptersFactoryService; }
        }

        [ImportingConstructor]
        internal VsAdapter(
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            IEditorOptionsFactoryService editorOptionsFactoryService,
            IIncrementalSearchFactoryService incrementalSearchFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _incrementalSearchFactoryService = incrementalSearchFactoryService;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _editorOptionsFactoryService = editorOptionsFactoryService;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = new RunningDocumentTable(_serviceProvider);
            _uiShell = _serviceProvider.GetService<SVsUIShell, IVsUIShell>();
            _monitorSelection = _serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();
        }

        public Result<IVsTextLines> GetTextLines(ITextBuffer textBuffer)
        {
            var lines = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer) as IVsTextLines;
            return lines != null
               ? Result.CreateSuccess(lines)
               : Result.Error;
        }

        public bool TryGetCodeWindow(ITextView textView, out IVsCodeWindow codeWindow)
        {
            codeWindow = null;

            IVsWindowFrame frame;
            return TryGetContainingWindowFrame(textView, out frame)
                && frame.TryGetCodeWindow(out codeWindow);
        }

        public bool TryGetContainingWindowFrame(ITextView textView, out IVsWindowFrame windowFrame)
        {
            var targetView = _editorAdaptersFactoryService.GetViewAdapter(textView);
            return TryGetContainingWindowFrame(targetView, out windowFrame);
        }

        public bool TryGetContainingWindowFrame(IVsTextView textView, out IVsWindowFrame windowFrame)
        {
            foreach (var frame in _uiShell.GetDocumentWindowFrames())
            {
                IVsCodeWindow codeWindow;
                if (frame.TryGetCodeWindow(out codeWindow))
                {
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

        public bool IsVenusView(IVsTextView vsTextView)
        {
            var textView = _editorAdaptersFactoryService.GetWpfTextView(vsTextView);
            if (textView == null)
            {
                return false;
            }

            var vsTextLines = _editorAdaptersFactoryService.GetBufferAdapter(textView.TextBuffer);
            if (vsTextLines == null)
            {
                return false;
            }

            Guid id;
            return ErrorHandler.Succeeded(vsTextLines.GetLanguageServiceID(out id))
                && id == VSConstants.CLSID_HtmlLanguageService;
        }

        public bool IsReadOnly(ITextBuffer textBuffer)
        {
            var editorOptions = _editorOptionsFactoryService.GetOptions(textBuffer);
            if (editorOptions != null
                && EditorOptionsUtil.GetOptionValueOrDefault(editorOptions, DefaultTextViewOptions.ViewProhibitUserInputId, false))
            {
                return true;
            }

            var textLines = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer);
            uint flags;
            if (ErrorHandler.Succeeded(textLines.GetStateFlags(out flags))
                && 0 != (flags & (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY))
            {
                return true;
            }

            return false;
        }

        public bool IsIncrementalSearchActive(ITextView textView)
        {
            var search = _incrementalSearchFactoryService.GetIncrementalSearch(textView);
            return search != null && search.IsActive;
        }
    }
}
