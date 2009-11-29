#light

namespace VimCore
open Microsoft.VisualStudio.Text

module TssUtil =
    val GetLines : SnapshotPoint -> SearchKind -> seq<ITextSnapshotLine>
    val GetPoints : ITextSnapshotLine -> seq<SnapshotPoint>
    val GetSpans : SnapshotPoint -> SearchKind -> seq<SnapshotSpan>
    val GetLineRangeSpan : SnapshotPoint -> int -> SnapshotSpan
    val GetLineRangeSpanIncludingLineBreak : SnapshotPoint -> int -> SnapshotSpan
    val VimLineToTssLine : int -> int
    val FindCurrentWordSpan : SnapshotPoint -> WordKind -> option<SnapshotSpan>
    val FindCurrentFullWordSpan : SnapshotPoint -> WordKind -> option<SnapshotSpan>
    val FindNextWordSpan : SnapshotPoint -> WordKind -> SnapshotSpan
    val FindPreviousWordSpan : SnapshotPoint -> WordKind -> SnapshotSpan
    val FindAnyWordSpan : SnapshotSpan -> WordKind -> option<SnapshotSpan>
    val FindAnyWordSpanReverse : SnapshotSpan -> WordKind -> option<SnapshotSpan>
    val FindNextWordPosition : SnapshotPoint -> WordKind -> SnapshotPoint
    val FindPreviousWordPosition : SnapshotPoint -> WordKind -> SnapshotPoint
    val SearchDirection: SearchKind -> 'a -> 'a -> 'a
    val FindIndentPosition : ITextSnapshotLine -> int
    val GetReverseCharacterSpan : SnapshotPoint -> int -> SnapshotSpan
    val GetCharacterSpan : SnapshotPoint -> SnapshotSpan
    val GetLastLine : ITextSnapshot -> ITextSnapshotLine
    val GetStartPoint : ITextSnapshot -> SnapshotPoint
    val GetEndPoint : ITextSnapshot -> SnapshotPoint 
    val GetNextPoint : SnapshotPoint -> SnapshotPoint
    val GetNextPointWithWrap : SnapshotPoint -> SnapshotPoint 
    val GetPreviousPointWithWrap : SnapshotPoint -> SnapshotPoint
    
     