#light

namespace Vim.Modes.Insert
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Vim

type internal InsertMode =
    interface IMode
    new : IVimBuffer * Modes.ICommonOperations * IDisplayWindowBroker * IEditorOptions * IUndoRedoOperations * ITextChangeTracker * bool -> InsertMode
