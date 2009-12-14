#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

/// Vim instance.  Global for a group of buffers
type IVim =
    abstract Data : IVimData
    abstract Buffers : seq<IVimBuffer>
    abstract CreateBuffer : IVimHost -> IWpfTextView -> string -> IBlockCaret -> IVimBuffer

