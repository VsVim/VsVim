#light

namespace Vim
open Vim

[<Class>]
type internal KeyProcessor = 
    inherit Microsoft.VisualStudio.Text.Editor.KeyProcessor
    new : IVimBuffer -> KeyProcessor