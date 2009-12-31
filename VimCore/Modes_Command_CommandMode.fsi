#light

namespace Vim.Modes.Command
open Vim

type internal CommandMode =
    interface IMode
    new : (IVimBuffer * IOperations ) -> CommandMode
    
