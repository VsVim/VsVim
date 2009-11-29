#light

namespace VimCore
open System.Windows.Input
open Microsoft.VisualStudio.Text

module TextUtil = 
    val FindCurrentWordSpan : WordKind -> string -> int -> option<Span>
    val FindCurrentWord : WordKind -> string -> int -> string
    val FindFullWordSpan : WordKind -> string -> int -> option<Span>
    val FindFullWord : WordKind -> string -> int -> string
    val FindPreviousWordSpan : WordKind -> string -> int -> option<Span>
    val FindPreviousWord : WordKind -> string -> int -> string
    val FindNextWordSpan : WordKind -> string -> int -> option<Span>
    val FindNextWord : WordKind -> string -> int -> string

