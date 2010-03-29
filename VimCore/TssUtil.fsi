#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

module internal TssUtil =
    val GetLines : SnapshotPoint -> SearchKind -> seq<ITextSnapshotLine>

    /// Get the SnapshotSpan of an ITextSnapshotLine
    val GetLineExtent : ITextSnapshotLine -> SnapshotSpan

    /// Get the SnapshotSpan of an ITextSnapshotLine including the line break
    val GetLineExtentIncludingLineBreak : ITextSnapshotLine -> SnapshotSpan

    /// Get the points on the particular line in order 
    val GetPoints : ITextSnapshotLine -> seq<SnapshotPoint>

    /// Get the points on the particular line including the line break
    val GetPointsIncludingLineBreak : ITextSnapshotLine -> seq<SnapshotPoint>

    /// Get the ITextSnapshotLine containing the specified SnapshotPoint
    val GetContainingLine : SnapshotPoint -> ITextSnapshotLine

    /// Start searching the snapshot at the given point and return the buffer as a 
    /// sequence of SnapshotSpans.  One will be returned per line in the buffer.  The
    /// only exception is the start line which will be divided at the given start
    /// point
    val GetSpans : SnapshotPoint -> SearchKind -> seq<SnapshotSpan>

    /// Get the spans of all Words starting at the given point and searching the 
    /// spans with the specified Kind
    val GetWordSpans : SnapshotPoint -> WordKind -> SearchKind -> seq<SnapshotSpan>
    
    /// Get the line number back if it's valid and if not the last line in the snapshot
    val GetValidLineNumberOrLast : ITextSnapshot -> int -> int

    /// Get a valid line for the specified number if it's valid and the last line if it's
    /// not
    val GetValidLineOrLast : ITextSnapshot -> int -> ITextSnapshotLine

    /// Get the line range passed in.  If the count of lines exceeds the amount of lines remaining
    /// in the buffer, the span will be truncated to the final line
    val GetLineRangeSpan : SnapshotPoint -> int -> SnapshotSpan

    /// Functions exactly line GetLineRangeSpan except it will include the final line up until
    /// the end of the line break
    val GetLineRangeSpanIncludingLineBreak : SnapshotPoint -> int -> SnapshotSpan

    /// Vim is fairly odd in that it considers the top line of the file to be both line numbers
    /// 1 and 0.  The next line is 2.  The editor is a zero based index though so we need
    /// to take that into account
    val VimLineToTssLine : int -> int

    /// Find the span of the word at the given point
    val FindCurrentWordSpan : SnapshotPoint -> WordKind -> option<SnapshotSpan>

    /// Find the full span of the word at the given point
    val FindCurrentFullWordSpan : SnapshotPoint -> WordKind -> option<SnapshotSpan>

    /// Find the next word span starting at the specified point.  This will not wrap around the buffer 
    /// looking for word spans
    val FindNextWordSpan : SnapshotPoint -> WordKind -> SnapshotSpan

    /// This function is mainly a backing for the "b" command mode command.  It is really
    /// used to find the position of the start of the current or previous word.  Unless we 
    /// are currently at the start of a word, in which case it should go back to the previous
    /// one        
    val FindPreviousWordSpan : SnapshotPoint -> WordKind -> SnapshotSpan

    /// Find any word span in the specified range.  If a span is returned, it will be a subset
    /// of the original span. 
    val FindAnyWordSpan : SnapshotSpan -> WordKind -> SearchKind -> option<SnapshotSpan>

    /// Find the start of the next word from the specified point.  If the cursor is currently
    /// on a word then this word will not be considered.  If there are no more words GetEndPoint
    /// will be returned
    val FindNextWordPosition : SnapshotPoint -> WordKind -> SnapshotPoint

    /// Find and return the SnapshotPoint representing the first non-whitespace character on
    /// the given ITextSnapshotLine
    val FindFirstNonWhitespaceCharacter : ITextSnapshotLine -> SnapshotPoint

    /// This function is mainly a backing for the "b" command mode command.  It is really
    /// used to find the position of the start of the current or previous word.  Unless we 
    /// are currently at the start of a word, in which case it should go back to the previous
    /// one
    val FindPreviousWordPosition : SnapshotPoint -> WordKind -> SnapshotPoint
    val SearchDirection: SearchKind -> 'a -> 'a -> 'a
    val FindIndentPosition : ITextSnapshotLine -> int

    /// Get the reverse character span.  This will search backwards count items until the 
    /// count is satisfied or the begining of the line is reached
    val GetReverseCharacterSpan : SnapshotPoint -> int -> SnapshotSpan

    // Get the span of the character which is pointed to by the point.  Normally this is a 
    // trivial operation.  The only difficulty if the Point exists on an empty line.  In that
    // case it is the extent of the line
    val GetCharacterSpan : SnapshotPoint -> SnapshotSpan

    val GetLastLine : ITextSnapshot -> ITextSnapshotLine
    val GetStartPoint : ITextSnapshot -> SnapshotPoint
    val GetEndPoint : ITextSnapshot -> SnapshotPoint 

    /// Get the next point in the buffer without wrap.  Will throw if you run off the end of 
    /// the ITextSnapshot
    val GetNextPoint : SnapshotPoint -> SnapshotPoint

    /// Get the next point in the buffer with wrap
    val GetNextPointWithWrap : SnapshotPoint -> SnapshotPoint 

    /// Get the previous point in the buffer with wrap
    val GetPreviousPointWithWrap : SnapshotPoint -> SnapshotPoint
        
    /// Create an ITextStructureNavigator instance for the given WordKind with the provided 
    /// base implementation to fall back on
    val CreateTextStructureNavigator : WordKind -> ITextStructureNavigator -> ITextStructureNavigator

    /// Get the line and column information for a given SnapshotPoint
    val GetLineColumn : SnapshotPoint -> (int * int)

    /// Map the specified tracking span to the given ITextSnapshot.  If the span cannot be mapped
    /// due to incompatible changes in the buffer, None will be returned
    val SafeGetTrackingSpan : ITrackingSpan -> ITextSnapshot -> SnapshotSpan option


    
