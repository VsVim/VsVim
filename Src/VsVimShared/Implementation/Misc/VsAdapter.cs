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
using System.Diagnostics;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IVsAdapter))]
    internal sealed class VsAdapter : IVsAdapter
    {
        private static readonly object s_findUIAdornmentLayerKey = new object();

        /// <summary>
        /// The watch window will stash this GUID inside the IVsUserData (off IVsTextBuffer). It 
        /// allows us to identify this window
        /// </summary>
        private static readonly Guid s_watchWindowGuid = new Guid(0x944E3FE0, 0xCD33, 0x44A2, 0x93, 0x1C, 0x1D, 0x7F, 0x84, 0x2C, 0x9D, 0x5);

        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly IIncrementalSearchFactoryService _incrementalSearchFactoryService;
        private readonly IVsFindManager _vsFindManager;
        private readonly IVsTextManager _textManager;
        private readonly IVsUIShell _uiShell;
        private readonly RunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsMonitorSelection _monitorSelection;
        private readonly IExtensionAdapterBroker _extensionAdapterBroker;

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
            IExtensionAdapterBroker extensionAdapterBroker,
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
            _vsFindManager = _serviceProvider.GetService<SVsFindManager, IVsFindManager>();
            _extensionAdapterBroker = extensionAdapterBroker;
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
                if (frameTextBuffer is IProjectionBuffer frameProjectionBuffer && frameProjectionBuffer.SourceBuffers.Contains(textBuffer))
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

            if (ErrorHandler.Failed(_textManager.EnumViews(vsTextBuffer, out IVsEnumTextViews vsEnum)))
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
                _table.FindDocument(textDocument.FilePath, out uint docCookie);
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

            return ErrorHandler.Succeeded(vsTextLines.GetLanguageServiceID(out Guid id))
                && id == VSConstants.CLSID_HtmlLanguageService;
        }

        internal bool IsWatchWindowView(ITextView textView)
        {
            var vsTextBuffer = _editorAdaptersFactoryService.GetBufferAdapter(textView.TextDataModel.DocumentBuffer);
            if (vsTextBuffer == null)
            {
                return false;
            }

            var vsUserData = vsTextBuffer as IVsUserData;
            if (vsUserData == null)
            {
                return false;
            }

            var key = s_watchWindowGuid;
            if (!ErrorHandler.Succeeded(vsUserData.GetData(ref key, out object value)) || value == null)
            {
                return false;
            }

            // The intent of the debugger appears to be for using true / false here.  But a native
            // code bug can cause this to become 1 instead of true.  
            return value.Equals(true) || value.Equals(1);
        }

        internal bool IsTextEditorView(ITextView textView)
        {
            // Our fonts and colors won't work unless this view is in the "Text Editor"
            // fonts and colors category.
            var GUID_EditPropCategory_View_MasterSettings =
                new Guid("{D1756E7C-B7FD-49a8-B48E-87B14A55655A}"); // see {VSIP}/Common/Inc/textmgr.h
            var textEditorGuid =
                new Guid("{A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0}"); // Text editor category

            var vsTextView = _editorAdaptersFactoryService.GetViewAdapter(textView);
            if (vsTextView is IVsTextEditorPropertyCategoryContainer categoryContainer)
            {
                var guid = GUID_EditPropCategory_View_MasterSettings;
                if (categoryContainer.GetPropertyCategory(ref guid, out IVsTextEditorPropertyContainer propertyContainer) == VSConstants.S_OK)
                {
                    if (propertyContainer.GetProperty(VSEDITPROPID.VSEDITPROPID_ViewGeneral_FontCategory, out object property) == VSConstants.S_OK)
                    {
                        if (property is Guid propertyGuid)
                        {
                            if (propertyGuid == textEditorGuid)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
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

            if (ErrorHandler.Succeeded(textLines.GetStateFlags(out uint flags))
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

            if (IsIncrementalSearchActiveScreenScrape(textView))
            {
                return true;
            }

            return _extensionAdapterBroker.IsIncrementalSearchActive(textView) ?? false;
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
                    if (IsFindManagerIncrementalSearchActive())
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
        /// is active or not is to use IVsFindManager as an IVsUIDataSource (once again thanks Ryan!)
        /// </summary>
        internal bool IsFindManagerIncrementalSearchActive()
        {
            try
            {
                var dataSource = (IVsUIDataSource)_vsFindManager;
                if (ErrorHandler.Failed(dataSource.GetValue("IsIncrementalSearchActive", out IVsUIObject uiObj)) ||
                    ErrorHandler.Failed(uiObj.get_Data(out object obj)) ||
                    !(obj is bool))
                {
                    return false;
                }

                return (bool)obj;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OpenFile(string filePath)
        {
            try
            {
                var dte = _serviceProvider.GetService<SDTE, _DTE>();
                dte.ItemOperations.OpenFile(filePath, EnvDTE.Constants.vsViewKindTextView);
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        private bool TryGetActiveTextView(out IWpfTextView textView)
        {
            var textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager2>();
            if (textManager == null)
            {
                textView = null;
                return false;
            }

            var hr = textManager.GetActiveView2(fMustHaveFocus: 0, pBuffer: null, grfIncludeViewFrameType: (uint)_VIEWFRAMETYPE.vftCodeWindow, ppView: out IVsTextView vsTextView);
            if (ErrorHandler.Failed(hr))
            {
                textView = null;
                return false;
            }

            textView = _editorAdaptersFactoryService.GetWpfTextView(vsTextView);
            return textView != null;
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

        bool IVsAdapter.IsWatchWindowView(ITextView textView)
        {
            return IsWatchWindowView(textView);
        }

        bool IVsAdapter.IsTextEditorView(ITextView textView)
        {
            return IsTextEditorView(textView);
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

        void IVsAdapter.OpenFile(string filePath)
        {
            OpenFile(filePath);
        }

        bool IVsAdapter.TryGetActiveTextView(out IWpfTextView textView)
        {
            return TryGetActiveTextView(out textView);
        }

        #endregion
    }
}
