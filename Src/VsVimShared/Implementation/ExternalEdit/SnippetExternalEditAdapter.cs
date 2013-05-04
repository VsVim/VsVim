using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.Implementation.ExternalEdit
{
    [Export(typeof(IExternalEditAdapter))]
    internal sealed class SnippetExternalEditAdapter : IExternalEditAdapter
    {
        public bool IsExternalEditMarker(IVsTextLineMarker marker)
        {
            var result = marker.GetMarkerType();
            if (result.IsError)
            {
                return false;
            }

            // Predefined markers aren't a concern
            var value = (int)result.Value;
            if (value <= (int)MARKERTYPE.DEF_MARKER_COUNT)
            {
                return false;
            }

            switch ((int)result.Value)
            {
                case 15:
                case 16:
                case 26:
                    // Details
                    //  15: Snippet marker for inactive span
                    //  16: Snippet marker for active span
                    //  26: Tracks comment insertion for a snippet don' for cursor placement
                    return true;
                case 25:
                    // Kind currently unknown.  
                    // Used at least for brace matching
                    return false;
                default:
                    return false;
            }
        }

        #region IExternalEditAdapter

        bool IExternalEditAdapter.IsInterested(ITextView textView, out ITagger<ITag> tagger)
        {
            tagger = null;
            return true;
        }

        bool IExternalEditAdapter.IsExternalEditMarker(IVsTextLineMarker marker)
        {
            return IsExternalEditMarker(marker);
        }

        bool IExternalEditAdapter.IsExternalEditTag(ITag tag)
        {
            return false;
        }

        #endregion
    }
}
