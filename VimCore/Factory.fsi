#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

module Factory =
    val CreateVim : IVimHost -> IVim
    val CreateVimBuffer : IVimHost -> IWpfTextView -> IEditorOperations -> string -> IBlockCaret -> IVimBuffer