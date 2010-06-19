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

namespace VsVim.Implementation
{
    [Export(typeof(ITextManager))]
    internal sealed class TextManager : ITextManager
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IVsTextManager _textManager;
        private readonly IVsUIShell _uiShell;
        private readonly RunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;

        public IEnumerable<ITextBuffer> TextBuffers
        {
            get
            {
                return _table
                    .Select(x => GetTextBufferForDocCookie(x.DocCookie))
                    .Where(x => x.Item1)
                    .Select(x => x.Item2);
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
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = new RunningDocumentTable(_serviceProvider);
            _uiShell = _serviceProvider.GetService<SVsUIShell, IVsUIShell>();
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

        public void Close(ITextView textView, bool checkDirty)
        {
            var frame = GetContainingWindowFrame(textView);
            var value = checkDirty
                ? __FRAMECLOSE.FRAMECLOSE_PromptSave
                : __FRAMECLOSE.FRAMECLOSE_NoSave;
            ErrorHandler.ThrowOnFailure(frame.CloseFrame((uint)value));
        }

        private IVsWindowFrame GetContainingWindowFrame(ITextView textView)
        {
            var targetView = _editorAdaptersFactoryService.GetViewAdapter(textView);
            foreach ( var frame in _uiShell.GetDocumentWindowFrames())
            {
                var codeWindow = frame.GetCodeWindow();
                IVsTextView vsTextView;
                if (ErrorHandler.Succeeded(codeWindow.GetPrimaryView(out vsTextView)) && NativeMethods.IsSameComObject(vsTextView, targetView))
                {
                    return frame;
                }

                if (ErrorHandler.Succeeded(codeWindow.GetSecondaryView(out vsTextView)) && NativeMethods.IsSameComObject(vsTextView, targetView))
                {
                    return frame;
                }
            }

            throw new InvalidOperationException();
        }

        private IEnumerable<IWpfTextView> GetTextViews(ITextBuffer textBuffer)
        {
            return GetVsTextViews(textBuffer)
                .Select(x => _editorAdaptersFactoryService.GetWpfTextView(x))
                .Where(x => x != null);
        }

        private IEnumerable<IVsTextView> GetVsTextViews(ITextBuffer textBuffer)
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

        private Tuple<bool, ITextBuffer> GetTextBufferForDocCookie(uint cookie)
        {
            var info = _table.GetDocumentInfo(cookie);
            var obj = info.DocData;
            var vsTextLines = obj as IVsTextLines;
            var vsTextBufferProvider = obj as IVsTextBufferProvider;
            ITextBuffer buffer = null;
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

            return Tuple.Create(buffer != null, buffer);
        }
    }
}
