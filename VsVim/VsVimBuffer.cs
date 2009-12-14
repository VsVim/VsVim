using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.UI.Undo;
using Microsoft.VisualStudio.OLE.Interop;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.FSharp.Control;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace VsVim
{
    internal sealed class VsVimBuffer
    {
        private readonly IWpfTextView _view;
        private readonly VsVimHost _host;
        private readonly IVimBuffer _buffer;
        private readonly VsCommandFilter _filter;

        internal IVimBuffer VimBuffer
        {
            get { return _buffer; }
        }

        internal VsVimHost VsVimHost
        {
            get { return _host; }
        }

        internal VsVimBuffer(IVim vim, IWpfTextView view, IVsTextView shimView, IVsTextLines lines, IUndoHistoryRegistry undoHistory, IEditorFormatMap map)
        {
            _view = view;

            var sp = ((IObjectWithSite)shimView).GetServiceProvider();
            _host = new VsVimHost(sp, undoHistory);
            _buffer = vim.CreateBuffer(_host, _view, lines.GetFileName(), new BlockCursor(view,HostFactory.BlockAdornmentLayer, map));

            _filter = new VsCommandFilter(_buffer, shimView);
        }

        internal void Close()
        {
            _buffer.Close();
        }
    }
}

