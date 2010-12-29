using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.ExternalEdit
{
    internal interface IExternalEditorAdapter
    {
        ExternalEditMarker? TryCreateExternalEditMarker(IVsTextLineMarker marker, ITextSnapshot snapshot);
        ExternalEditMarker? TryCreateExternalEditMarker(ITag tag, SnapshotSpan tagSpan);
    }
}
