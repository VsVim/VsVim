#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module internal ViewUtil =
    val VimLineToViewLine : int -> int
    val GetLineNumber : ITextView -> int
    val GetLineText : ITextView -> int -> string
    val CurrentPosition : ITextView -> int
    val GetCaretPoint : ITextView -> SnapshotPoint
    val MoveCaretToPosition : ITextView -> int -> CaretPosition
    val MoveCaretToPoint : ITextView -> SnapshotPoint -> CaretPosition
    val MoveCaretToVirtualPoint : ITextView -> VirtualSnapshotPoint -> CaretPosition
    val MoveCaretToEndOfLine : ITextView -> CaretPosition
    val MoveCaretToBeginingOfLine : ITextView -> CaretPosition
    val MoveToLastLineStart : IWpfTextView -> SnapshotPoint
    val MoveToLineStart : IWpfTextView -> ITextSnapshotLine -> SnapshotPoint
    val InsertNewLineAfter : ITextView -> int -> bool
    val FindCurrentFullWord : ITextView -> WordKind -> string
    val FindNextWordStart : ITextView -> WordKind -> int
    val FindPreviousWordStart : ITextView -> WordKind -> int
    val SelectSpan : ITextView -> Span -> unit
    val ClearSelection : ITextView -> unit

