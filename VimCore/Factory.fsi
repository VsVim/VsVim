#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module Factory =
    val CreateVim : IVimHost -> IVim
    val CreateVimBuffer : IVimHost -> IWpfTextView -> string -> IBlockCaret -> IVimBuffer