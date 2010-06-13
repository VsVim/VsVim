using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
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
        private readonly IVsRunningDocumentTable _table;
        private readonly IServiceProvider _serviceProvider;

        public IEnumerable<ITextBuffer> TextBuffers
        {
            get
            {
                return GetRunningDocumentCookies()
                    .Select(x => GetTextBufferForDocCookie(x))
                    .Where(x => x.Item1)
                    .Select(x => x.Item2);
            }
        }

        public IEnumerable<IWpfTextView> TextViews
        {
            get { return TextBuffers.Select(x => GetTextViews(x)).SelectMany(x => x);  }
        }

        [ImportingConstructor]
        internal TextManager(
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _serviceProvider = serviceProvider;
            _textManager = _serviceProvider.GetService<SVsTextManager, IVsTextManager>();
            _table = _serviceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable>();
        }

        public Tuple<bool, IWpfTextView> TryGetActiveTextView()
        {
            IVsTextView vsTextView;
            IWpfTextView textView = null;
            if (ErrorHandler.Succeeded(_textManager.GetActiveView(0, null, out vsTextView)) && vsTextView != null)
            {
                textView = _editorAdaptersFactoryService.GetWpfTextView(vsTextView);
            }

            return Tuple.Create(textView != null, textView);
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

        private IEnumerable<IWpfTextView> GetTextViews(ITextBuffer textBuffer)
        {
            var vsTextBuffer = _editorAdaptersFactoryService.GetBufferAdapter(textBuffer);
            if (vsTextBuffer == null)
            {
                return Enumerable.Empty<IWpfTextView>();
            }

            IVsEnumTextViews vsEnum;
            ErrorHandler.ThrowOnFailure(_textManager.EnumViews(vsTextBuffer, out vsEnum));

            var list = new List<IWpfTextView>();
            var done = false;
            var array = new IVsTextView[1];
            do
            {
                uint found = 0;
                var hr = vsEnum.Next((uint)array.Length, array, ref found);
                ErrorHandler.ThrowOnFailure(hr);
                if (VSConstants.S_OK == hr && array[0] != null)
                {
                    var textView = _editorAdaptersFactoryService.GetWpfTextView(array[0]);
                    if (textView != null)
                    {
                        list.Add(textView);
                    }
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
            uint rdtFlags, readLocks, editLocks, itemId;
            string document;
            IVsHierarchy hierarchy;
            var docData = IntPtr.Zero;
            var hr = _table.GetDocumentInfo(
                cookie,
                out rdtFlags,
                out readLocks,
                out editLocks,
                out document,
                out hierarchy,
                out itemId,
                out docData);
            ITextBuffer buffer = null;
            if (ErrorHandler.Failed(hr))
            {
                return Tuple.Create(false, buffer);
            }

            var obj = Marshal.GetObjectForIUnknown(docData);
            try
            {
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
            }
            finally
            {
                Marshal.Release(docData);
            }

            return Tuple.Create(buffer != null, buffer);
        }

        private IEnumerable<uint> GetRunningDocumentCookies()
        {
            IEnumRunningDocuments enumDocs;
            ErrorHandler.ThrowOnFailure(_table.GetRunningDocumentsEnum(out enumDocs));
            var done = false;
            var array = new uint[1];
            var list = new List<uint>();
            do
            {
                uint found = 0;
                var hr = enumDocs.Next(1, array, out found);
                ErrorHandler.ThrowOnFailure(hr);
                if (found == 0 || VSConstants.S_FALSE == hr)
                {
                    done = true;
                }
                else
                {
                    list.Add(array[0]);
                }
            } while (!done);
            return list;
        }
    }
}
