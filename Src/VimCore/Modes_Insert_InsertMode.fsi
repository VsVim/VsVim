#light

namespace Vim.Modes.Insert
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Vim

// HACK: need to make sure that visual studio integration handles this in insert mode correctly
type internal InsertMode =
    interface IInsertMode
    interface IProvisionalTextMode
    new : IVimBuffer * ICommonOperations * IDisplayWindowBroker * IEditorOptions * IUndoRedoOperations * ITextChangeTracker * IInsertUtil * IMotionUtil * ICommandUtil * IMotionCapture * bool * IKeyboardDevice * IMouseDevice * IWordUtil * IWordCompletionSessionFactoryService -> InsertMode
