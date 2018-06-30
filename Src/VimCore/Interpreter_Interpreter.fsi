#light
namespace Vim.Interpreter
open Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

[<Class>]
type internal VimInterpreterFactory =
    interface IVimInterpreterFactory

[<Class>]
type ScriptGlobal = 
    new: string * IVimBuffer * IStatusUtil -> ScriptGlobal

    member Name: string
    member VimBuffer: IVimBuffer
    member StatusUtil: IStatusUtil

 