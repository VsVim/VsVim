#light

namespace Vim.Modes.Command
open Vim

type internal CommandMode =
    interface ICommandMode
    new : (IVimBuffer * IOperations ) -> CommandMode
    
