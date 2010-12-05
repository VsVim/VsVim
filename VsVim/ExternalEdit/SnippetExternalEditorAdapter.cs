using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.ExternalEdit
{
    internal sealed class SnippetExternalEditorAdapter : IExternalEditorAdapter
    {
        public ExternalEditMarker? TryCreateExternalEditMarker(IVsTextLineMarker marker, ITextSnapshot snapshot)
        {
            var result = marker.GetMarkerType();
            if (result.IsError)
            {
                return null;
            }

            // Predefined markers aren't a concern
            var value = (int)result.Value;
            if (value <= (int)MARKERTYPE.DEF_MARKER_COUNT)
            {
                return null;
            }

            // Get the SnapshotSpan for the marker
            var span = marker.GetCurrentSpan(snapshot);
            if (span.IsError)
            {
                return null;
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
                    return new ExternalEditMarker(ExternalEditKind.Snippet, span.Value);
                case 25:
                    // Kind currently unknown.  
                    return null;
                default:
                    return null;
            }
        }


        public ExternalEditMarker? TryCreateExternalEditMarker(ITag tag, SnapshotSpan tagSpan)
        {
            return null;
        }
    }
}
