namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities



type internal VimTextBuffer =
    new: textBuffer:ITextBuffer * localAbbreviationMap:IVimLocalAbbreviationMap * localKeyMap:IVimLocalKeyMap * localSettings:IVimLocalSettings * bufferTrackingService:IBufferTrackingService * undoRedoOperations:IUndoRedoOperations * wordUtil:WordUtil * vim:IVim
         -> VimTextBuffer

    interface IVimTextBuffer
