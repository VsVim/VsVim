using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.Text.Projection;
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
        private readonly IPowerToolsUtil _powerToolsUtil;

        internal bool InDebugMode
        {
            get
            {
                var result = _monitorSelection.IsCmdUIContextActive(VSConstants.UICONTEXT_Debugging);
                return result.IsSuccess && result.Value;
            }
        }

        internal bool InAutomationFunction
        {
            get { return VsShellUtilities.IsInAutomationFunction(_serviceProvider); }
        }

        internal KeyboardDevice KeyboardDevice
        {
            get { return InputManager.Current.PrimaryKeyboardDevice; }
        }

        internal IServiceProvider ServiceProvider
        {
            get { return _serviceProvider; }
        }

        internal IVsEditorAdaptersFactoryService EditorAdapter
        {
            get { return _editorAdaptersFactoryService; }
        }

        [ImportingConstructor]
        internal VsAdapter(
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            IEditorOptionsFactoryService editorOptionsFactoryService,
            IIncrementalSearchFactoryService incrementalSearchFactoryService,
            IPowerToolsUtil powerToolsUtil,
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
            _powerToolsUtil = powerToolsUtil;
        }

        internal Result<IVsTextLines> GetTextLines(ITextBuffer textBuffer)
        {
            var lines = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer) as IVsTextLines;
            return lines != null
               ? Result.CreateSuccess(lines)
               : Result.Error;
        }

        internal Result<IVsCodeWindow> GetCodeWindow(ITextView textView)
        {
            var result = GetContainingWindowFrame(textView);
            if (result.IsError)
            {
                return Result.CreateError(result.HResult);
            }

            return result.Value.GetCodeWindow();
        }

        internal Result<IVsWindowFrame> GetContainingWindowFrame(ITextView textView)
        {
            var vsTextView = _editorAdaptersFactoryService.GetViewAdapter(textView);
            if (vsTextView == null)
            {
                return Result.Error;
            }

            return vsTextView.GetWindowFrame();
        }

        internal Result<List<IVsWindowFrame>> GetWindowFrames()
        {
            return _uiShell.GetDocumentWindowFrames();
        }

        internal Result<List<IVsWindowFrame>> GetContainingWindowFrames(ITextBuffer textBuffer)
        {
            var vsTextLines = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer);
            if (vsTextLines == null)
            {
                return Result.Error;
            }

            var frameList = _uiShell.GetDocumentWindowFrames();
            if (frameList.IsError)
            {
                return Result.CreateError(frameList.HResult);
            }

            var list = new List<IVsWindowFrame>();
            foreach (var frame in frameList.Value)
            {
                var result = frame.GetTextBuffer(_editorAdaptersFactoryService);
                if (result.IsError)
                {
                    continue;
                }

                var frameTextBuffer = result.Value;
                if (frameTextBuffer == textBuffer)
                {
                    list.Add(frame);
                    continue;
                }

                // Still need to account for the case where the window is backed by a projection buffer
                // and this ITextBuffer is in the graph
                var frameProjectionBuffer = frameTextBuffer as IProjectionBuffer;
                if (frameProjectionBuffer != null && frameProjectionBuffer.SourceBuffers.Contains(textBuffer))
                {
                    list.Add(frame);
                }
            }

            return Result.CreateSuccess(list);
        }

        internal Result<IVsPersistDocData> GetPersistDocData(ITextBuffer textBuffer)
        {
            var vsTextBuffer = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer);
            if (vsTextBuffer == null)
            {
                return Result.Error;
            }

            try
            {
                return Result.CreateSuccess((IVsPersistDocData)vsTextBuffer);
            }
            catch (Exception e)
            {
                return Result.CreateError(e);
            }
        }

        internal IEnumerable<IVsTextView> GetTextViews(ITextBuffer textBuffer)
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

        internal Result<ITextBuffer> GetTextBufferForDocCookie(uint cookie)
        {
            var info = _table.GetDocumentInfo(cookie);
            var obj = info.DocData;
            var vsTextLines = obj as IVsTextLines;
            var vsTextBufferProvider = obj as IVsTextBufferProvider;

            ITextBuffer textBuffer;
            if (vsTextLines != null)
            {
                textBuffer = _editorAdaptersFactoryService.GetDataBuffer(vsTextLines);
            }
            else if (vsTextBufferProvider != null
                && ErrorHandler.Succeeded(vsTextBufferProvider.GetTextBuffer(out vsTextLines))
                && vsTextLines != null)
            {
                textBuffer = _editorAdaptersFactoryService.GetDataBuffer(vsTextLines);
            }
            else
            {
                textBuffer = null;
            }

            return Result.CreateSuccessNonNull(textBuffer);
        }

        internal Result<uint> GetDocCookie(ITextDocument textDocument)
        {
            try
            {
                uint docCookie;
                _table.FindDocument(textDocument.FilePath, out docCookie);
                return docCookie;
            }
            catch (Exception ex)
            {
                return Result.CreateError(ex);
            }
        }

        internal bool IsVenusView(IVsTextView vsTextView)
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

        internal bool IsReadOnly(ITextBuffer textBuffer)
        {
            var editorOptions = _editorOptionsFactoryService.GetOptions(textBuffer);
            if (editorOptions != null
                && EditorOptionsUtil.GetOptionValueOrDefault(editorOptions, DefaultTextViewOptions.ViewProhibitUserInputId, false))
            {
                return true;
            }

            var textLines = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer);
            if (textLines == null)
            {
                return false;
            }

            uint flags;
            if (ErrorHandler.Succeeded(textLines.GetStateFlags(out flags))
                && 0 != (flags & (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY))
            {
                return true;
            }

            return false;
        }

        internal bool IsIncrementalSearchActive(ITextView textView)
        {
            var search = _incrementalSearchFactoryService.GetIncrementalSearch(textView);
            if (search != null && search.IsActive)
            {
                return true;
            }

            return _powerToolsUtil.IsQuickFindActive;
        }

        #region IVsAdapter

        bool IVsAdapter.InAutomationFunction
        {
            get { return InAutomationFunction; }
        }

        bool IVsAdapter.InDebugMode
        {
            get { return InDebugMode; }
        }

        IServiceProvider IVsAdapter.ServiceProvider
        {
            get { return ServiceProvider; }
        }

        IVsEditorAdaptersFactoryService IVsAdapter.EditorAdapter
        {
            get { return EditorAdapter; }
        }

        KeyboardDevice IVsAdapter.KeyboardDevice
        {
            get { return KeyboardDevice; }
        }

        Result<IVsTextLines> IVsAdapter.GetTextLines(ITextBuffer textBuffer)
        {
            return GetTextLines(textBuffer);
        }

        IEnumerable<IVsTextView> IVsAdapter.GetTextViews(ITextBuffer textBuffer)
        {
            return GetTextViews(textBuffer);
        }

        bool IVsAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return IsIncrementalSearchActive(textView);
        }

        bool IVsAdapter.IsVenusView(IVsTextView textView)
        {
            return IsVenusView(textView);
        }

        bool IVsAdapter.IsReadOnly(ITextBuffer textBuffer)
        {
            return IsReadOnly(textBuffer);
        }

        Result<List<IVsWindowFrame>> IVsAdapter.GetWindowFrames()
        {
            return GetWindowFrames();
        }

        Result<List<IVsWindowFrame>> IVsAdapter.GetContainingWindowFrames(ITextBuffer textBuffer)
        {
            return GetContainingWindowFrames(textBuffer);
        }

        Result<IVsPersistDocData> IVsAdapter.GetPersistDocData(ITextBuffer textBuffer)
        {
            return GetPersistDocData(textBuffer);
        }

        Result<IVsCodeWindow> IVsAdapter.GetCodeWindow(ITextView textView)
        {
            return GetCodeWindow(textView);
        }

        Result<IVsWindowFrame> IVsAdapter.GetContainingWindowFrame(ITextView textView)
        {
            return GetContainingWindowFrame(textView);
        }

        Result<uint> IVsAdapter.GetDocCookie(ITextDocument textDocument)
        {
            return GetDocCookie(textDocument);
        }

        Result<ITextBuffer> IVsAdapter.GetTextBufferForDocCookie(uint cookie)
        {
            return GetTextBufferForDocCookie(cookie);
        }

        #endregion
    }
}
