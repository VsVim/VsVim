#light

namespace Vim
open Microsoft.VisualStudio.Text

module TextUtil = 

    /// Is this a word character for the specified WordKind
    val IsWordChar: WordKind -> char -> bool

    /// Get the spans of word values in the given string in the provided direction
    val GetWordSpans: wordKind: WordKind -> searchPath: SearchPath -> text: string -> Span seq

    val GetCurrentWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option
    val GetFullWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option
    val GetPreviousWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option
    val GetNextWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option

