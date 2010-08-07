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
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Microsoft.VisualStudio.OLE.Interop;
using IServiceProvider = System.IServiceProvider;

namespace VsVim.Implementation
{
    [Export(typeof(ITextManager))]
    internal sealed class TextManager : ITextManager
    {
        private readonly IVsAdapter _adapter;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IVsTextManager _textManager;
        private readonly RunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;

        public IEnumerable<ITextBuffer> TextBuffers
        {
            get
            {
                var list = new List<ITextBuffer>();
                foreach (var item in _table)
                {
                    ITextBuffer buffer;
                    if (_adapter.TryGetTextBufferForDocCookie(item.DocCookie, out buffer))
                    {
                        list.Add(buffer);
                    }
                }
                return list;
            }
        }

        public IEnumerable<ITextView> TextViews
        {
            get { return TextBuffers.Select(x => GetTextViews(x)).SelectMany(x => x); }
        }

        public ITextView ActiveTextView
        {
            get
            {
                IVsTextView vsTextView;
                IWpfTextView textView = null;
                ErrorHandler.ThrowOnFailure(_textManager.GetActiveView(0, null, out vsTextView));
                textView = _editorAdaptersFactoryService.GetWpfTextView(vsTextView);
                if (textView == null)
                {
                    throw new InvalidOperationException();
                }
                return textView;
            }
        }

        [ImportingConstructor]
        internal TextManager(
            IVsAdapter adapter,
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _adapter = adapter;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = new RunningDocumentTable(_serviceProvider);
        }

        public bool NavigateTo(VirtualSnapshotPoint point)
        {
            var tuple = SnapshotPointUtil.GetLineColumn(point.Position);
            var line = tuple.Item1;
            var column = tuple.Item2;
            var vsBuffer = _editorAdaptersFactoryService.GetBufferAdapter(point.Position.Snapshot.TextBuffer);
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

        public void Save(ITextView textView)
        {
            var vsTextView = _editorAdaptersFactoryService.GetViewAdapter(textView);
            VsShellUtilities.SaveFileIfDirty(vsTextView);
        }

        public bool Close(ITextView textView, bool checkDirty)
        {
            IVsWindowFrame frame;
            if (!_adapter.TryGetContainingWindowFrame(textView, out frame))
            {
                return false;
            }

            var value = checkDirty
                ? __FRAMECLOSE.FRAMECLOSE_PromptSave
                : __FRAMECLOSE.FRAMECLOSE_NoSave;
            return ErrorHandler.Succeeded(frame.CloseFrame((uint)value));
        }

        public bool SplitView(ITextView textView)
        {
            IVsCodeWindow codeWindow;
            if (_adapter.TryGetCodeWindow(textView, out codeWindow))
            {
                var target = codeWindow as IOleCommandTarget;
                if (target != null)
                {
                    var group = VSConstants.GUID_VSStandardCommandSet97;
                    var cmdId = VSConstants.VSStd97CmdID.Split;
                    var result = target.Exec(ref group, (uint)cmdId, (uint)OLECMDEXECOPT.OLECMDEXECOPT_DODEFAULT, IntPtr.Zero, IntPtr.Zero);
                    return VSConstants.S_OK == result;
                }
            }

            return false;
        }

        private IEnumerable<IWpfTextView> GetTextViews(ITextBuffer textBuffer)
        {
            return _adapter.GetTextViews(textBuffer)
                .Select(x => _editorAdaptersFactoryService.GetWpfTextView(x))
                .Where(x => x != null);
        }
    }
}
