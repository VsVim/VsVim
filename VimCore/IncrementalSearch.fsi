#light

namespace VimCore
open Microsoft.VisualStudio.Text

type internal IncrementalSearch =
    new : string -> IncrementalSearch
    new : string * SearchKind -> IncrementalSearch
    new : string * SearchKind * SearchOptions -> IncrementalSearch
    member Pattern : string
    member SearchKind : SearchKind
    member FindNextMatch: SnapshotPoint -> option<SnapshotSpan>



