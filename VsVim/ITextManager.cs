using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    public interface ITextManager
    {
        /// <summary>
        /// Set of active ITextBuffers
        /// </summary>
        IEnumerable<ITextBuffer> TextBuffers { get; }

        /// <summary>
        /// Set of all active IWpfTextViews
        /// </summary>
        IEnumerable<IWpfTextView> TextViews { get; }

        /// <summary>
        /// Get and return the IWpfTextView for the active document
        /// </summary>
        /// <returns></returns>
        Tuple<bool, IWpfTextView> TryGetActiveTextView();

        /// <summary>
        /// Navigate Visual Studio to the given point
        /// </summary>
        bool NavigateTo(VirtualSnapshotPoint point);
    }
}
