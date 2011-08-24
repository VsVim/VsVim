#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal VisualMode =
    interface IVisualMode
    new : VimBufferData * ICommonOperations * IMotionUtil * ModeKind * ICommandRunner * IMotionCapture * ISelectionTracker -> VisualMode
