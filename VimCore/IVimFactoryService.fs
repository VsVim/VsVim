#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// MEF component which can spin up Vi components
type IVimFactoryService =
    abstract CreateVim : IVimHost -> IVim
    abstract CreateVimBuffer : IVimHost -> IWpfTextView -> string -> IBlockCaret -> IVimBuffer
    abstract CreateKeyProcessor : IVimBuffer -> KeyProcessor
