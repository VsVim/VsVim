#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

module ModeUtil =
    val Join : ITextView -> SnapshotPoint -> JoinKind -> int -> bool
    val GoToDefinition : ITextView -> IVimHost -> Result
    val SetMark : MarkMap -> SnapshotPoint -> char -> Result
    val JumpToMark : MarkMap -> ITextView -> char -> Result
    val Yank : SnapshotSpan -> MotionKind -> OperationKind -> Register -> unit
    val PasteAfter : SnapshotPoint -> string -> OperationKind -> SnapshotSpan
    val PasteBefore : SnapshotPoint -> string -> SnapshotSpan
    val DeleteSpan: SnapshotSpan -> MotionKind -> OperationKind -> Register -> ITextSnapshot
    val MoveCaretLeft : ITextView -> IEditorOperations -> count : int -> unit
    val MoveCaretRight : ITextView -> IEditorOperations -> count : int -> unit
    val MoveCaretUp : ITextView -> IEditorOperations -> count : int -> unit
    val MoveCaretDown : ITextView -> IEditorOperations -> count : int -> unit
