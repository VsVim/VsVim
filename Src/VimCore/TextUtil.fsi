#light

namespace Vim
open Microsoft.VisualStudio.Text

module TextUtil = 

    /// Is this a word character for the specified WordKind
    val IsWordChar: WordKind -> char -> bool

    /// Get the spans of word values in the given string in the provided direction
    val GetWordSpans: wordKind: WordKind -> searchPath: SearchPath -> text: string -> Span seq

    /// Get the word that occurs prior to the passed in index. If the index is the beginning of a
    /// word then that word will not be returned but instead the one occuring before that word will
    /// be returned
    val GetPreviousWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option

    /// Get the word that occurs after, or on, the passed in index. If the index is inside a word
    /// then that word will be returned.
    val GetNextWordSpan: wordKind: WordKind -> text: string -> index: int -> Span option

