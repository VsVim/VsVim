#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

module internal CommonUtil =

    val GetSearchPointAndWrap : Path -> SnapshotPoint -> SnapshotPoint * bool

    val GetSearchPoint : Path -> SnapshotPoint -> SnapshotPoint

    val RaiseSearchResultMessage : IStatusUtil -> SearchResult -> unit