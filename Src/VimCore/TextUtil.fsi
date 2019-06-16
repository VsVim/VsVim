#light

namespace Vim
open Microsoft.VisualStudio.Text

module TextUtil = 

    /// Is this a word character for the specified WordKind
    val IsWordChar: WordKind -> char -> bool

    /// Get the spans of word values in the given string in the provided direction
    val GetWordSpans: wordKind: WordKind -> searchPath: SearchPath -> text: string -> Span seq

    val FindCurrentWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option
    val FindFullWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option
    val FindPreviousWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option
    val FindNextWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option

