using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using IServiceProvider = System.IServiceProvider;

namespace VsVim.Implementation.Misc
{
    [Export(typeof(ITextManager))]
    internal sealed class TextManager : ITextManager
    {
        private readonly IVsAdapter _vsAdapter;
        private readonly IVsTextManager _textManager;
        private readonly IVsRunningDocumentTable _runningDocumentTable;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly ITextBufferFactoryService _textBufferFactoryService;
        private readonly ISharedService _sharedService;

        internal ITextView ActiveTextViewOptional
        {
            get
            {
                IVsTextView vsTextView;
                IWpfTextView textView = null;
                try
                {
                    ErrorHandler.ThrowOnFailure(_textManager.GetActiveView(0, null, out vsTextView));
                    textView = _vsAdapter.EditorAdapter.GetWpfTextView(vsTextView);
                }
                catch
                {
                    // Both ThrowOnFailure and GetWpfTextView can throw an exception.  The latter will
                    // throw even if a non-null value is passed into it 
                    textView = null;
                }
                return textView;
            }
        }

        [ImportingConstructor]
        internal TextManager(
            IVsAdapter adapter,
            ITextDocumentFactoryService textDocumentFactoryService,
            ITextBufferFactoryService textBufferFactoryService,
            ISharedServiceFactory sharedServiceFactory,
            SVsServiceProvider serviceProvider) : this(adapter, textDocumentFactoryService, textBufferFactoryService, sharedServiceFactory.Create(), serviceProvider)
        {

        }

        internal TextManager(
            IVsAdapter adapter,
            ITextDocumentFactoryService textDocumentFactoryService,
            ITextBufferFactoryService textBufferFactoryService,
            ISharedService sharedService,
            SVsServiceProvider serviceProvider)
        {
            _vsAdapter = adapter;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _textDocumentFactoryService = textDocumentFactoryService;
            _textBufferFactoryService = textBufferFactoryService;
            _runningDocumentTable = _serviceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable>();
            _sharedService = sharedService;
        }

        private IEnumerable<ITextBuffer> GetDocumentTextBuffers(DocumentLoad documentLoad)
        {
            var list = new List<ITextBuffer>();
            foreach (var docCookie in _runningDocumentTable.GetRunningDocumentCookies())
            {
                if (documentLoad == DocumentLoad.RespectLazy && _sharedService.IsLazyLoaded(docCookie))
                {
                    continue;
                }

                ITextBuffer buffer;
                if (_vsAdapter.GetTextBufferForDocCookie(docCookie).TryGetValue(out buffer))
                {
                    list.Add(buffer);
                }
            }

            return list;
        }

        private IEnumerable<ITextView> GetDocumentTextViews(DocumentLoad documentLoad)
        {
            var list = new List<ITextView>();
            foreach (var textBuffer in GetDocumentTextBuffers(documentLoad))
            {
                list.AddRange(GetTextViews(textBuffer));
            }

            return list;
        }

        internal bool NavigateTo(VirtualSnapshotPoint point)
        {
            var tuple = SnapshotPointUtil.GetLineColumn(point.Position);
            var line = tuple.Item1;
            var column = tuple.Item2;
            var vsBuffer = _vsAdapter.EditorAdapter.GetBufferAdapter(point.Position.Snapshot.TextBuffer);
            var viewGuid = VSConstants.LOGVIEWID_Code;
            var hr = _textManager.NavigateToLineAndColumn(
                vsBuffer,
                ref viewGuid,
                line,
                column,
                line,
                column);
            return ErrorHandler.Succeeded(hr);
        }

        internal Result Save(ITextBuffer textBuffer)
        {
            // In order to save the ITextBuffer we need to get a document cookie for it.  The only way I'm
            // aware of is to use the path moniker which is available for the accompanying ITextDocment 
            // value.  
            //
            // In many types of files (.cs, .vb, .cpp) there is usually a 1-1 mapping between ITextBuffer 
            // and the ITextDocument.  But in any file type where an IProjectionBuffer is common (.js, 
            // .aspx, etc ...) this mapping breaks down.  To get it back we must visit all of the 
            // source buffers for a projection and individually save them
            var result = Result.Success;
            foreach (var sourceBuffer in textBuffer.GetSourceBuffersRecursive())
            {
                // The inert buffer doesn't need to be saved.  It's used as a fake buffer by web applications
                // in order to render projected content
                if (sourceBuffer.ContentType == _textBufferFactoryService.InertContentType)
                {
                    continue;
                }

                var sourceResult = SaveCore(sourceBuffer);
                if (sourceResult.IsError)
                {
                    result = sourceResult;
                }
            }

            return result;
        }

        internal Result SaveCore(ITextBuffer textBuffer)
        {
            ITextDocument textDocument;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out textDocument))
            {
                return Result.Error;
            }

            try
            {
                var docCookie = _vsAdapter.GetDocCookie(textDocument).Value;
                var runningDocumentTable = _serviceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable>();
                ErrorHandler.ThrowOnFailure(runningDocumentTable.SaveDocuments((uint)__VSRDTSAVEOPTIONS.RDTSAVEOPT_ForceSave, null, 0, docCookie));
                return Result.Success;
            }
            catch (Exception e)
            {
                return Result.CreateError(e);
            }
        }

        internal bool CloseView(ITextView textView)
        {
            IVsCodeWindow vsCodeWindow;
            if (!_vsAdapter.GetCodeWindow(textView).TryGetValue(out vsCodeWindow))
            {
                return false;
            }

            if (vsCodeWindow.IsSplit())
            {
                return SendSplit(vsCodeWindow);
            }

            IVsWindowFrame vsWindowFrame;
            if (!_vsAdapter.GetContainingWindowFrame(textView).TryGetValue(out vsWindowFrame))
            {
                return false;
            }

            // It's possible for IVsWindowFrame elements to nest within each other.  When closing we want to 
            // close the actual tab in the editor so get the top most item
            vsWindowFrame = vsWindowFrame.GetTopMost();

            var value = __FRAMECLOSE.FRAMECLOSE_NoSave;
            return ErrorHandler.Succeeded(vsWindowFrame.CloseFrame((uint)value));
        }

        internal bool SplitView(ITextView textView)
        {
            IVsCodeWindow codeWindow;
            if (_vsAdapter.GetCodeWindow(textView).TryGetValue(out codeWindow))
            {
                return SendSplit(codeWindow);
            }

            return false;
        }

        internal bool MoveViewUp(ITextView textView)
        {
            try
            {
                var vsCodeWindow = _vsAdapter.GetCodeWindow(textView).Value;
                var vsTextView = vsCodeWindow.GetSecondaryView().Value;
                return ErrorHandler.Succeeded(vsTextView.SendExplicitFocus());
            }
            catch
            {
                return false;
            }
        }

        internal bool MoveViewDown(ITextView textView)
        {
            try
            {
                var vsCodeWindow = _vsAdapter.GetCodeWindow(textView).Value;
                var vsTextView = vsCodeWindow.GetPrimaryView().Value;
                return ErrorHandler.Succeeded(vsTextView.SendExplicitFocus());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Send the split command.  This is really a toggle command that will split
        /// and unsplit the window
        /// </summary>
        private static bool SendSplit(IVsCodeWindow codeWindow)
        {
            var target = codeWindow as IOleCommandTarget;
            if (target != null)
            {
                var group = VSConstants.GUID_VSStandardCommandSet97;
                var cmdId = VSConstants.VSStd97CmdID.Split;
                var result = target.Exec(ref group, (uint)cmdId, (uint)OLECMDEXECOPT.OLECMDEXECOPT_DODEFAULT, IntPtr.Zero, IntPtr.Zero);
                return VSConstants.S_OK == result;
            }
            return false;
        }

        internal IEnumerable<ITextView> GetTextViews(ITextBuffer textBuffer)
        {
            return _vsAdapter.GetTextViews(textBuffer)
                .Select(x => _vsAdapter.EditorAdapter.GetWpfTextView(x))
                .Where(x => x != null);
        }

        #region ITextManager

        ITextView ITextManager.ActiveTextViewOptional
        {
            get { return ActiveTextViewOptional; }
        }

        IEnumerable<ITextView> ITextManager.GetDocumentTextViews(ITextBuffer textBuffer)
        {
            return GetTextViews(textBuffer);
        }

        IEnumerable<ITextBuffer> ITextManager.GetDocumentTextBuffers(DocumentLoad documentLoad)
        {
            return GetDocumentTextBuffers(documentLoad);
        }

        IEnumerable<ITextView> ITextManager.GetDocumentTextViews(DocumentLoad documentLoad)
        {
            return GetDocumentTextViews(documentLoad);
        }

        bool ITextManager.NavigateTo(VirtualSnapshotPoint point)
        {
            return NavigateTo(point);
        }

        Result ITextManager.Save(ITextBuffer textBuffer)
        {
            return Save(textBuffer);
        }

        bool ITextManager.CloseView(ITextView textView)
        {
            return CloseView(textView);
        }

        bool ITextManager.SplitView(ITextView textView)
        {
            return SplitView(textView);
        }

        bool ITextManager.MoveViewUp(ITextView textView)
        {
            return MoveViewUp(textView);
        }

        bool ITextManager.MoveViewDown(ITextView textView)
        {
            return MoveViewDown(textView);
        }

        #endregion
    }
}
