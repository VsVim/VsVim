using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.VisualStudio.Text.Editor;
using VimCore;
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
        private readonly IWpfTextView m_view;
        private readonly VsVimHost m_host;
        private readonly IVimBuffer m_buffer;
        private readonly VsCommandFilter m_filter;

        internal IVimBuffer VimBuffer
        {
            get { return m_buffer; }
        }

        internal VsVimHost VsVimHost
        {
            get { return m_host; }
        }

        internal VsVimBuffer(IVim vim, IWpfTextView view, IVsTextView shimView, IVsTextLines lines, IUndoHistoryRegistry undoHistory, IEditorFormatMap map)
        {
            m_view = view;

            var sp = ((IObjectWithSite)shimView).GetServiceProvider();
            m_host = new VsVimHost(sp, undoHistory);
            m_buffer = vim.CreateBuffer(m_host, m_view, lines.GetFileName(), new BlockCursor(view,HostFactory.BlockAdornmentLayer, map));

            m_filter = new VsCommandFilter(m_buffer, shimView);
        }

        internal void Close()
        {
        }
    }
}

