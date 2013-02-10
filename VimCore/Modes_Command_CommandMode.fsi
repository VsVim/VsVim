#light

namespace Vim.Modes.Command
open Vim
open Vim.Interpreter

type internal CommandMode =
    interface ICommandMode
    new : IVimBuffer * ICommonOperations -> CommandMode
    
