#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal VisualMode =
    interface IMode
    new : (IVimBufferData * ICommonOperations * ModeKind ) -> VisualMode
