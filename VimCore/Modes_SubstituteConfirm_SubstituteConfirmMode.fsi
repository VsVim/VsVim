#light

namespace Vim.Modes.SubstituteConfirm
open Vim
open Vim.Modes

type internal SubstituteConfirmMode =
    new : VimBufferData * ICommonOperations -> SubstituteConfirmMode

    interface ISubstituteConfirmMode

