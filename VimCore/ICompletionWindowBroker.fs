#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

/// Used to determine if a completion window is active for a given view
type ICompletionWindowBroker =

    /// TextView this broker is associated with
    abstract TextView : ITextView 

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionWindowActive : bool

    /// Dismiss any completion windows on the given ITextView
    abstract DismissCompletionWindow : unit -> unit
