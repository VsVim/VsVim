using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
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
                    if (_adapter.GetTextBufferForDocCookie(item.DocCookie).TryGetValue(out buffer))
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

        public Result Save(ITextBuffer textBuffer)
        {
            try
            {
                var docData = _adapter.GetPersistDocData(textBuffer).Value;
                string unusedNewDocumentName;
                int saveCancelled;
                ErrorHandler.ThrowOnFailure(docData.SaveDocData(VSSAVEFLAGS.VSSAVE_Save, out unusedNewDocumentName, out saveCancelled));
                if (saveCancelled != 0)
                {
                    return Result.Error;
                }

                return Result.Success;
            }
            catch (Exception e)
            {
                return Result.CreateError(e);
            }
        }

        public bool CloseView(ITextView textView, bool checkDirty)
        {
            IVsCodeWindow vsCodeWindow;
            if (!_adapter.GetCodeWindow(textView).TryGetValue(out vsCodeWindow))
            {
                return false;
            }

            if (vsCodeWindow.IsSplit())
            {
                return SendSplit(vsCodeWindow);
            }

            IVsWindowFrame vsWindowFrame;
            if (!_adapter.GetContainingWindowFrame(textView).TryGetValue(out vsWindowFrame))
            {
                return false;
            }
            var value = checkDirty
                ? __FRAMECLOSE.FRAMECLOSE_PromptSave
                : __FRAMECLOSE.FRAMECLOSE_SaveIfDirty;
            return ErrorHandler.Succeeded(vsWindowFrame.CloseFrame((uint)value));
        }

        public bool SplitView(ITextView textView)
        {
            IVsCodeWindow codeWindow;
            if (_adapter.GetCodeWindow(textView).TryGetValue(out codeWindow))
            {
                return SendSplit(codeWindow);
            }

            return false;
        }

        public bool MoveViewUp(ITextView textView)
        {
            try
            {
                var vsCodeWindow = _adapter.GetCodeWindow(textView).Value;
                var vsTextView = vsCodeWindow.GetSecondaryView().Value;
                return ErrorHandler.Succeeded(vsTextView.SendExplicitFocus());
            }
            catch
            {
                return false;
            }
        }

        public bool MoveViewDown(ITextView textView)
        {
            try
            {
                var vsCodeWindow = _adapter.GetCodeWindow(textView).Value;
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

        public IEnumerable<ITextView> GetTextViews(ITextBuffer textBuffer)
        {
            return _adapter.GetTextViews(textBuffer)
                .Select(x => _adapter.EditorAdapter.GetWpfTextView(x))
                .Where(x => x != null);
        }

        #region ITextManager

        IEnumerable<ITextBuffer> ITextManager.TextBuffers
        {
            get { return TextBuffers; }
        }

        IEnumerable<ITextView> ITextManager.TextViews
        {
            get { return TextViews; }
        }

        ITextView ITextManager.ActiveTextViewOptional
        {
            get { return ActiveTextView; }
        }

        IEnumerable<ITextView> ITextManager.GetTextViews(ITextBuffer textBuffer)
        {
            return GetTextViews(textBuffer);
        }

        bool ITextManager.NavigateTo(VirtualSnapshotPoint point)
        {
            return NavigateTo(point);
        }

        Result ITextManager.Save(ITextBuffer textBuffer)
        {
            return Save(textBuffer);
        }

        bool ITextManager.CloseView(ITextView textView, bool checkDirty)
        {
            return CloseView(textView, checkDirty);
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
