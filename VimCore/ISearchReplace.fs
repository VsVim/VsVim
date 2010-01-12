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

    /// Finds the next instance of the word at the passed in point.  If no word exists at that
    /// point or no other instance can be found, None will be returned
    abstract FindNextWord : SnapshotPoint -> WordKind -> SearchKind -> ignoreCase : bool -> SnapshotSpan option