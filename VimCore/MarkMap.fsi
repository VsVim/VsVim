#light

namespace Vim
open Microsoft.VisualStudio.Text

[<Sealed>]
type MarkMap =
    static member IsLocalMark : char -> bool
    new : unit -> MarkMap
    member GetLocalMark : ITextBuffer -> char -> VirtualSnapshotPoint option
    member GetMark : ITextBuffer -> char -> VirtualSnapshotPoint option
    member SetMark : SnapshotPoint -> char -> unit
    member DeleteMark : ITextBuffer -> char -> bool
    member DeleteAllMarks : unit -> unit
    member DeleteAllMarksForBuffer : ITextBuffer -> unit

