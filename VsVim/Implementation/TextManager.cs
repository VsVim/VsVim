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
                textView = _adapter.EditorAdapter.GetWpfTextView(vsTextView);
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
            SVsServiceProvider serviceProvider)
        {
            _adapter = adapter;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = new RunningDocumentTable(_serviceProvider);
        }

        public bool NavigateTo(VirtualSnapshotPoint point)
        {
            var tuple = SnapshotPointUtil.GetLineColumn(point.Position);
            var line = tuple.Item1;
            var column = tuple.Item2;
            var vsBuffer = _adapter.EditorAdapter.GetBufferAdapter(point.Position.Snapshot.TextBuffer);
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
            var vsTextView = _adapter.EditorAdapter.GetViewAdapter(textView);
            VsShellUtilities.SaveFileIfDirty(vsTextView);
        }

        public bool CloseBuffer(ITextView textView, bool checkDirty)
        {
            IVsWindowFrame frame;
            if (!_adapter.TryGetContainingWindowFrame(textView, out frame))
            {
                return false;
            }

            var value = checkDirty
                ? __FRAMECLOSE.FRAMECLOSE_PromptSave
                : __FRAMECLOSE.FRAMECLOSE_SaveIfDirty;
            return ErrorHandler.Succeeded(frame.CloseFrame((uint)value));
        }

        public bool CloseView(ITextView textView, bool checkDirty)
        {
            IVsCodeWindow codeWindow;
            if (_adapter.TryGetCodeWindow(textView, out codeWindow) )
            {
                return codeWindow.IsSplit()
                    ? SendSplit(codeWindow)
                    : CloseBuffer(textView, checkDirty);
            }

            return false;
        }

        public bool SplitView(ITextView textView)
        {
            IVsCodeWindow codeWindow;
            if (_adapter.TryGetCodeWindow(textView, out codeWindow))
            {
                return SendSplit(codeWindow);
            }

            return false;
        }

        public bool MoveViewUp(ITextView textView)
        {
            var vsView = _adapter.EditorAdapter.GetViewAdapter(textView);
            IVsTextView otherVsView;
            IVsCodeWindow codeWindow;
            if (vsView == null
                || !_adapter.TryGetCodeWindow(textView, out codeWindow)
                || !codeWindow.TryGetSecondaryView(out otherVsView))
            {
                return false;
            }

            var otherTextView = _adapter.EditorAdapter.GetWpfTextView(otherVsView);
            if (otherTextView == null || otherTextView == textView)
            {
                return false;
            }

            return otherTextView.VisualElement.Focus();
        }

        public bool MoveViewDown(ITextView textView)
        {
            var vsView = _adapter.EditorAdapter.GetViewAdapter(textView);
            IVsTextView otherVsView;
            IVsCodeWindow codeWindow;
            if (vsView == null
                || !_adapter.TryGetCodeWindow(textView, out codeWindow)
                || !codeWindow.TryGetPrimaryView(out otherVsView))
            {
                return false;
            }
            
            var otherTextView = _adapter.EditorAdapter.GetWpfTextView(otherVsView);
            if ( otherTextView == null || otherTextView == textView)
            {
                return false;
            }

            return otherTextView.VisualElement.Focus();
        }

        /// <summary>
        /// Send the split command.  This is really a toggle command that will split
        /// and unsplit the window
        /// </summary>
        private bool SendSplit(IVsCodeWindow codeWindow)
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

        private IEnumerable<IWpfTextView> GetTextViews(ITextBuffer textBuffer)
        {
            return _adapter.GetTextViews(textBuffer)
                .Select(x => _adapter.EditorAdapter.GetWpfTextView(x))
                .Where(x => x != null);
        }
    }
}
