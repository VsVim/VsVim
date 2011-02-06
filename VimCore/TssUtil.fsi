#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

module TssUtil =

    /// Get the spans of all Words starting at the given point and searching the 
    /// spans with the specified Kind
    val GetWordSpans : SnapshotPoint -> WordKind -> SearchKind -> seq<SnapshotSpan>

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
    val FindNextWordStart : SnapshotPoint -> int -> WordKind -> SnapshotPoint

    /// Find and return the SnapshotPoint representing the first non-whitespace character on
    /// the given ITextSnapshotLine
    val FindFirstNonWhiteSpaceCharacter : ITextSnapshotLine -> SnapshotPoint

    /// Find and return the SnapshotPoint representing the last non-whitespace character on
    /// the given ITextSnapshotLine
    val FindLastNonWhiteSpaceCharacter : ITextSnapshotLine -> SnapshotPoint

    /// This function is mainly a backing for the "b" command mode command.  It is really
    /// used to find the position of the start of the current or previous word.  Unless we 
    /// are currently at the start of a word, in which case it should go back to the previous
    /// one
    val FindPreviousWordStart : SnapshotPoint -> int -> WordKind -> SnapshotPoint

    /// Find the next "count" occurrence of the given "char" on the specified line
    val FindNextOccurranceOfCharOnLine : SnapshotPoint -> char -> int -> SnapshotPoint option

    /// Find the point just before the "count" occurrence of the "char" on the specified line
    val FindTillNextOccurranceOfCharOnLine : SnapshotPoint -> char -> int -> SnapshotPoint option

    /// Find the next "count" ocurrance of the given "char" on the specified line
    val FindPreviousOccurranceOfCharOnLine : SnapshotPoint -> char -> int -> SnapshotPoint option

    /// Find the point just before next "count" ocurrance of the given "char" on the specified line
    val FindTillPreviousOccurranceOfCharOnLine : SnapshotPoint -> char -> int -> SnapshotPoint option

    /// Find the indent position for the given line.  
    val FindIndentPosition : ITextSnapshotLine -> tabSize:int -> int

    /// Get the reverse character span.  This will search backwards count items until the 
    /// count is satisfied or the begining of the line is reached
    val GetReverseCharacterSpan : SnapshotPoint -> int -> SnapshotSpan

    /// Create an ITextStructureNavigator instance for the given WordKind with the provided 
    /// base implementation to fall back on
    val CreateTextStructureNavigator : WordKind -> ITextStructureNavigator -> ITextStructureNavigator


    
