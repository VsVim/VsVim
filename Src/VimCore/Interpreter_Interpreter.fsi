#light
namespace Vim.Interpreter
open EditorUtils
open Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

[<Class>]
type internal VimInterpreterFactory =
    interface IVimInterpreterFactory

