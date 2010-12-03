using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.ExternalEdit
{
    internal interface IExternalEditorAdapter
    {
        bool TryCreateExternalEditMarker(IVsTextLineMarker marker, ITextSnapshot snapshot, out ExternalEditMarker editMarker);
        bool TryCreateExternalEditMarker(ITag tag, SnapshotSpan tagSpan, out ExternalEditMarker editMarker);
    }
}
