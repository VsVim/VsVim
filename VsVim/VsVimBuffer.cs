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
        private readonly IVimBuffer _buffer;
        private readonly VsCommandFilter _filter;

        internal IVimBuffer VimBuffer
        {
            get { return _buffer; }
        }

        internal VsVimBuffer(IVim vim, IWpfTextView view, IVsTextView shimView, IVsTextLines lines, IUndoHistoryRegistry undoHistory, IEditorFormatMap map)
        {
            _view = view;
            _buffer = vim.CreateBuffer( _view, lines.GetFileName(), new BlockCursor(view,HostFactory.BlockAdornmentLayerName, map));
            _filter = new VsCommandFilter(_buffer, shimView);
        }

        internal void Close()
        {
            _buffer.Close();
        }
    }
}

