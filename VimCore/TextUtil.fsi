#light

namespace Vim
open Microsoft.VisualStudio.Text

module internal TextUtil = 

    /// Is this a word character for the specified WordKind
    val IsWordChar : WordKind -> char -> bool

    /// Get the spans of word values in the given string in the provided direction
    val GetWordSpans : WordKind -> Path -> string -> Span seq

    val FindCurrentWordSpan : WordKind -> string -> int -> option<Span>
    val FindFullWordSpan : WordKind -> string -> int -> option<Span>
    val FindPreviousWordSpan : WordKind -> string -> int -> option<Span>
    val FindNextWordSpan : WordKind -> string -> int -> option<Span>

    // TODO: Consider deleting as they are no longer used.  Make sure to consider keeping
    // any tests which test some underlying functionality
    val FindFullWord : WordKind -> string -> int -> string
    val FindNextWord : WordKind -> string -> int -> string
    val FindCurrentWord : WordKind -> string -> int -> string
    val FindPreviousWord : WordKind -> string -> int -> string

