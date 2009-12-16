#light

namespace Vim
open Microsoft.VisualStudio.Text

type IncrementalSearch =
    new : string -> IncrementalSearch
    new : string * SearchKind -> IncrementalSearch
    new : string * SearchKind * SearchOptions -> IncrementalSearch
    member Pattern : string
    member SearchKind : SearchKind
    member FindNextMatch: SnapshotPoint -> option<SnapshotSpan>



