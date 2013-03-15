using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    internal interface IExternalEditAdapter
    {
        /// <summary>
        /// Is the external editor interested in this ITextView.  Can return a particular
        /// ITagger implementation that it's interested in in the ITextView
        /// </summary>
        bool IsInterested(ITextView textView, out ITagger<ITag> tagger);

        /// <summary>
        /// Does this IVsTextLineMarker represent an external edit 
        /// </summary>
        bool IsExternalEditMarker(IVsTextLineMarker marker);

        /// <summary>
        /// Does this ITag represent an external edit
        /// </summary>
        bool IsExternalEditTag(ITag tag);
    }
}
