#light

namespace Vim.Modes.SubstituteConfirm
open Vim
open Vim.Modes

type internal SubstituteConfirmMode =
    new : IVimBuffer * ICommonOperations -> SubstituteConfirmMode

    interface ISubstituteConfirmMode

