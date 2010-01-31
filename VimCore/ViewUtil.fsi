#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module internal ViewUtil =
    val GetCaretPoint : ITextView -> SnapshotPoint
    val MoveCaretToPosition : ITextView -> int -> CaretPosition
    val MoveCaretToPoint : ITextView -> SnapshotPoint -> CaretPosition
    val MoveCaretToVirtualPoint : ITextView -> VirtualSnapshotPoint -> CaretPosition

