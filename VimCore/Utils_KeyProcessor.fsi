#light

namespace Vim.Utils
open Vim

[<Class>]
type KeyProcessor = 
    inherit Microsoft.VisualStudio.Text.Editor.KeyProcessor
    new : IVimBuffer -> KeyProcessor