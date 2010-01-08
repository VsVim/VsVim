#light

namespace Vim
open Microsoft.VisualStudio.Text

type SearchReplaceFlags = 
    | None = 0x0
    | IgnoreCase = 0x1

type SearchData = {
    Pattern : string;
    Kind: SearchKind;
    Flags : SearchReplaceFlags;
}

type ISearchReplace =

    /// Find the next match for the given SearchData starting at the given SnapshotPoint
    abstract FindNextMatch : SearchData -> SnapshotPoint -> option<SnapshotSpan>

    /// Find the next instance of the passed in word.  If no match is found, the SnapshotSpan of 
    /// the original word will be returned
    abstract FindNextWord : word : SnapshotSpan -> SearchKind -> ignoreCase : bool -> SnapshotSpan