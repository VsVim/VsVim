#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module Factory =
    val CreateVim : unit -> IVim
    val CreateVimBuffer : IVimHost -> IWpfTextView -> string -> IBlockCaret -> IVimBuffer