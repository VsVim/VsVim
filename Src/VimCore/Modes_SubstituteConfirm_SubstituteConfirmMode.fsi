#light

namespace Vim.Modes.SubstituteConfirm
open Vim
open Vim.Modes

type internal SubstituteConfirmMode =
    new : IVimBufferData * ICommonOperations -> SubstituteConfirmMode

    interface ISubstituteConfirmMode

