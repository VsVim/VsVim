#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Tagging

/// MEF component which can spin up Vi components
type IVimFactoryService =
    abstract Vim : IVim
    abstract CreateKeyProcessor : IVimBuffer -> KeyProcessor
    abstract CreateMouseProcessor : IVimBuffer -> IMouseProcessor

type IBlockCaretFactoryService =
    abstract CreateBlockCaret : IWpfTextView -> IBlockCaret

/// Used to determine if a completion window is active for a given view
type ICompletionWindowBroker =

    /// TextView this broker is associated with
    abstract TextView : ITextView 

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionWindowActive : bool

    /// Dismiss any completion windows on the given ITextView
    abstract DismissCompletionWindow : unit -> unit

type ICompletionWindowBrokerFactoryService =
    abstract CreateCompletionWindowBroker : ITextView -> ICompletionWindowBroker
