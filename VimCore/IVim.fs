#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Vim instance.  Global for a group of buffers
type IVim =
    abstract Host : IVimHost
    abstract Data : IVimData
    abstract Buffers : seq<IVimBuffer>
    abstract CreateBuffer : IWpfTextView -> bufferName:string -> IVimBuffer

