#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type ICompletionWindowBroker =

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionWindowActive : ITextView -> bool

    /// Dismiss any completion windows on the given ITextView
    abstract DismissCompletionWindow : ITextView -> unit
