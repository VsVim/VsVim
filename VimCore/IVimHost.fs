#light

namespace VimCore
open Microsoft.VisualStudio.Text

type IVimHost =
    abstract UpdateStatus : string -> unit
    abstract Beep : unit -> unit
    abstract OpenFile : string -> unit
    abstract Undo : ITextBuffer -> int -> unit
    abstract GoToDefinition : unit -> bool

