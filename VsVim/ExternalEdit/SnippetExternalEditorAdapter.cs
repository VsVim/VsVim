using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.ExternalEdit
{
    internal sealed class SnippetExternalEditorAdapter : IExternalEditorAdapter
    {
        public bool TryCreateExternalEditMarker(IVsTextLineMarker marker, ITextSnapshot snapshot, out ExternalEditMarker editMarker)
        {
            editMarker = new ExternalEditMarker();
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

            // Get the SnapshotSpan for the marker
            var span = marker.GetCurrentSpan(snapshot);
            if (span.IsError)
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
                    //  26: Tracks comment insertion for a snippet
                    editMarker = new ExternalEditMarker(ExternalEditKind.Snippet, span.Value);
                    return true;
                case 25:
                    // Kind currently unknown.  
                    return false;
                default:
                    // TODO: Should remove this after development completes
                    return false;
            }
        }


        public bool TryCreateExternalEditMarker(ITag tag, SnapshotSpan tagSpan, out ExternalEditMarker editMarker)
        {
            editMarker = new ExternalEditMarker();
            return false;
        }
    }
}
