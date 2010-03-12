#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

module internal MotionUtil =
    val CharLeft : SnapshotPoint -> int -> SnapshotSpan 
    val CharRight : SnapshotPoint -> int -> SnapshotSpan
    val CharUp : SnapshotPoint -> int -> SnapshotSpan
    val CharDown : SnapshotPoint -> int -> SnapshotSpan
    val LineUp : SnapshotPoint -> int -> SnapshotSpan
    val LineDown : SnapshotPoint -> int -> SnapshotSpan
