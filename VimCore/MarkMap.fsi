#light

namespace VimCore
open Microsoft.VisualStudio.Text

[<Sealed>]
type MarkMap =
    static member IsLocalMark : char -> bool
    new : unit -> MarkMap
    member GetLocalMark : ITextBuffer -> char -> VirtualSnapshotPoint option
    member SetMark : SnapshotPoint -> char -> unit
    member DeleteMark : ITextBuffer -> char -> bool
    member DeleteAllMarks : unit -> unit

