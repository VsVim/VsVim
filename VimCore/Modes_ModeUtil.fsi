#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal JoinKind = 
    | RemoveEmptySpaces
    | KeepEmptySpaces

module internal ModeUtil =
    type Result = 
        | Succeeded
        | Failed of string

    val Join : ITextView -> SnapshotPoint -> JoinKind -> int -> bool
    val GoToDefinition : ITextView -> IVimHost -> Result
    val SetMark : MarkMap -> SnapshotPoint -> char -> Result
    val JumpToMark : MarkMap -> ITextView -> char -> Result
    val Yank : SnapshotSpan -> MotionKind -> OperationKind -> Register -> unit
    val PasteAfter : SnapshotPoint -> string -> OperationKind -> SnapshotSpan
    val PasteBefore : SnapshotPoint -> string -> SnapshotSpan
    val DeleteSpan: SnapshotSpan -> MotionKind -> OperationKind -> Register -> ITextSnapshot
