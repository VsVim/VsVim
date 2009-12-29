#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type IVimHost =
    abstract UpdateStatus : string -> unit
    abstract Beep : unit -> unit
    abstract OpenFile : string -> unit
    abstract Undo : ITextBuffer -> int -> unit
    abstract GoToDefinition : unit -> bool

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionWindowActive : ITextView -> bool

    /// Dismiss any completion windows on the given ITextView
    abstract DismissCompletionWindow : ITextView -> unit

