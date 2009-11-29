#light

namespace VimCore.Modes.Common
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module internal Operations =
    val Join : ITextView -> int -> bool
    
