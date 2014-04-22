using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using EnvDTE;
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

namespace VsVim.Implementation.Misc
{
    [Export(typeof(IVsAdapter))]
    internal sealed class VsAdapter : IVsAdapter
    {
        private static readonly object s_findUIAdornmentLayerKey = new object();

        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly IIncrementalSearchFactoryService _incrementalSearchFactoryService;
        private readonly IVsTextManager _textManager;
        private readonly IVsUIShell _uiShell;
        private readonly RunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsMonitorSelection _monitorSelection;
        private readonly IPowerToolsUtil _powerToolsUtil;
        private readonly VisualStudioVersion _visualStudioVersion;

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
            SVsServiceProvider vsServiceProvider)
        {
            _incrementalSearchFactoryService = incrementalSearchFactoryService;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _editorOptionsFactoryService = editorOptionsFactoryService;
            _serviceProvider = vsServiceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = new RunningDocumentTable(_serviceProvider);
            _uiShell = _serviceProvider.GetService<SVsUIShell, IVsUIShell>();
            _monitorSelection = _serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();
            _powerToolsUtil = powerToolsUtil;
            _visualStudioVersion = vsServiceProvider.GetVisualStudioVersion();
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
            // There is a bug in some of the implementations of ITextView, SimpleTextView in 
            // particular, which cause it to throw an exception if we query COM interfaces 
            // that it implements.  If it is closed there is no reason to go any further
            if (textView.IsClosed)
            {
                return Result.Error;
            }

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
            if (ErrorHandler.Failed(_textManager.EnumViews(vsTextBuffer, out vsEnum)))
            {
                // When run as a result of navigation for NavigateTo this method can fail.  The reason
                // isn't understood but it can fail so we must handle it.  
                return Enumerable.Empty<IVsTextView>();
            }

            var list = new List<IVsTextView>();
            var done = false;
            var array = new IVsTextView[1];
            do
            {
                uint found = 0;
                var hr = vsEnum.Next((uint)array.Length, array, ref found);
                if (ErrorHandler.Failed(hr))
                {
                    return Enumerable.Empty<IVsTextView>();
                }

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
            var textView = _editorAdaptersFactoryService.GetWpfTextViewNoThrow(vsTextView);
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

        internal bool IsReadOnly(ITextView textView)
        {
            var editorOptions = textView.Options;
            if (editorOptions != null
                && EditorOptionsUtil.GetOptionValueOrDefault(editorOptions, DefaultTextViewOptions.ViewProhibitUserInputId, false))
            {
                return true;
            }

            return IsReadOnly(textView.TextBuffer);
        }

        internal bool IsReadOnly(ITextBuffer textBuffer)
        {
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

            if (_visualStudioVersion != VisualStudioVersion.Vs2010)
            {
                if (IsIncrementalSearchActiveScreenScrape(textView) ||
                    IsSimpleTextViewIncrementalSearchActive(textView))
                {
                    return true;
                }
            }

            return _powerToolsUtil.IsQuickFindActive;
        }

        /// <summary>
        /// Visual Studio 2012 introduced a new form of incremental search in the find / replace UI
        /// implementation.  It doesn't implement the IIncrementalSearch interface and doesn't expose
        /// whether or not it's active via any public API.
        ///
        /// The best way to find if it's active is to look for the adornment itself and see if it or
        /// it's descendant has focus.  Because Visual Studio is using WPF hosted in a HWND it doesn't
        /// actually have keyboard focus, just normal focus
        /// </summary>
        internal bool IsIncrementalSearchActiveScreenScrape(ITextView textView)
        {
            var wpfTextView = textView as IWpfTextView;
            if (wpfTextView == null)
            {
                return false;
            }

            var adornmentLayer = wpfTextView.GetAdornmentLayerNoThrow("FindUIAdornmentLayer", s_findUIAdornmentLayerKey);
            if (adornmentLayer == null)
            {
                return false;
            }

            foreach (var element in adornmentLayer.Elements)
            {
                // If the adornment is visible and has keyboard focus then consider it active.  
                var adornment = element.Adornment;
                if (adornment.Visibility == Visibility.Visible && adornment.GetType().Name == "FindUI")
                {
                    // The Ctrl+F or find replace UI will set the keyboard focus within
                    if (adornment.IsKeyboardFocusWithin)
                    {
                        return true;
                    }

                    // The Ctrl+I value will not set keyboard focus.  Have to use reflection to look 
                    // into the view to detect this case 
                    if (IsSimpleTextViewIncrementalSearchActive(textView))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Starting in Vs2012 or Vs2013 (unclear) Visual Studio moved incremental search to the same
        /// FindUI dialog but does not use the IsKeyboardFocusWithin property.  Instead they use an 
        /// IOleCommandTarget to drive the UI.  Only reasonable way I've found to query whether this
        /// is active or not is to use reflection (yay)
        /// </summary>
        internal bool IsSimpleTextViewIncrementalSearchActive(ITextView textView)
        {
            try
            {
                var vsTextView = _editorAdaptersFactoryService.GetViewAdapter(textView);
                if (vsTextView == null)
                {
                    return false;
                }

                var type = vsTextView.GetType();
                if (type.Name != "VsTextViewAdapter")
                {
                    return false;
                }

                var propertyInfo = type.GetProperty("IsIncrementalSearchInProgress");
                var value = (bool)propertyInfo.GetValue(vsTextView, null);
                return value;
            }
            catch (Exception)
            {
                return false;
            }
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

        bool IVsAdapter.IsReadOnly(ITextView textView)
        {
            return IsReadOnly(textView);
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
