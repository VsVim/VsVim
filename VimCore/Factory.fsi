#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module Factory =
    val CreateVimBuffer : IVimHost -> IWpfTextView -> string -> IVimBuffer
