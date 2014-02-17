#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type UndoRedoOperations =
    interface IUndoRedoOperations
    new : IStatusUtil * ITextUndoHistory option * IEditorOperationsFactoryService -> UndoRedoOperations

