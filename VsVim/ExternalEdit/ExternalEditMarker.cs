using Microsoft.VisualStudio.Text;

namespace VsVim.ExternalEdit
{
    internal struct ExternalEditMarker
    {
        internal readonly ExternalEditKind ExternalEditKind;
        internal readonly SnapshotSpan Span;

        internal ExternalEditMarker(
            ExternalEditKind kind,
            SnapshotSpan span)
        {
            ExternalEditKind = kind;
            Span = span;
        }
    }
}
