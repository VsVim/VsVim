#light

namespace Vim.Modes.Insert
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Vim

type internal InsertMode =
    interface IInsertMode
    // KTODO: delete the WordUtil parameter here and get it from IVimBuffer
    new: IVimBuffer * ICommonOperations * IDisplayWindowBroker * IEditorOptions * IUndoRedoOperations * ITextChangeTracker * IInsertUtil * IMotionUtil * ICommandUtil * IMotionCapture * bool * IKeyboardDevice * IMouseDevice * WordUtil * IWordCompletionSessionFactoryService -> InsertMode
