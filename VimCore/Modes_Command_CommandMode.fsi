#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Vim.Interpreter

type internal CommandMode =
    interface ICommandMode
    new : IVimBuffer * ICommonOperations * Interpreter -> CommandMode
    
