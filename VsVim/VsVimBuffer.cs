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
using Microsoft.VisualStudio.Text.Operations;

namespace VsVim
{
    /// <summary>
    /// Wrapper class maintaining the components surrounding an IVimBuffer hosted inside of 
    /// Visual Studio
    /// </summary>
    internal sealed class VsVimBuffer
    {
        private readonly IVimBuffer _buffer;

        internal IVimBuffer VimBuffer
        {
            get { return _buffer; }
        }

        /// <summary>
        /// VsCommandFilter used on the associated view.  This will not be set until after 
        /// the first time the underlying IVsTextView shim is created.  This happens on 
        /// first focus
        /// </summary>
        internal VsCommandFilter VsCommandFilter { get; set; }

        internal VsVimBuffer(
            IVim vim, 
            IWpfTextView view, 
            string fileName,
            IUndoHistoryRegistry undoHistory, 
            IEditorFormatMap map)
        {
            _buffer = vim.CreateBuffer(
                view,
                fileName,
                new BlockCursor(view, HostFactory.BlockAdornmentLayerName, map));
        }

        internal void Close()
        {
            _buffer.Close();
        }
    }
}

