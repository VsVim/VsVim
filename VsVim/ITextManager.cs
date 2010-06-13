using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    public interface ITextManager
    {
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
