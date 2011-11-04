#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

module internal CommonUtil =

    val GetSearchPointAndWrap : Path -> SnapshotPoint -> SnapshotPoint * bool

    val GetSearchPoint : Path -> SnapshotPoint -> SnapshotPoint

    /// Select the given VisualSpan in the ITextView.
    val Select : ITextView -> VisualSpan -> unit

    /// Select the given VisualSelection in the ITextView and place the caret in the correct
    /// position
    val SelectAndUpdateCaret : ITextView -> VisualSelection -> unit

    val RaiseSearchResultMessage : IStatusUtil -> SearchResult -> unit

