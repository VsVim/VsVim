#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type IVimFactoryService =
    abstract CreateVim : IVimHost -> IVim
    abstract CreateVimBuffer : IVimHost -> IWpfTextView -> IEditorOperations -> string -> IBlockCaret -> ICompletionWindowBroker -> IVimBuffer
    abstract CreateKeyProcessor : IVimBuffer -> KeyProcessor
