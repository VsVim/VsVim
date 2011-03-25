#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes

type internal CommandMode =
    interface ICommandMode
    new : IVimBuffer * ICommandProcessor * ICommonOperations -> CommandMode
    
