#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type ICompletionWindowBroker =
    abstract IsCompletionWindowActive : ITextView -> bool
    abstract DismissCompletionWindow : ITextView -> unit
