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

