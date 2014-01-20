namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal SelectMode =
    interface ISelectMode
    new : IVimBufferData * ICommonOperations * IMotionUtil * VisualKind * ICommandRunner * IMotionCapture * IUndoRedoOperations * ISelectionTracker -> SelectMode

