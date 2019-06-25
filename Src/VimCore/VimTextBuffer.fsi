
#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimTextBuffer =

    new: textBuffer: ITextBuffer * localSettings: IVimLocalSettings * localAbbreviationMap: IVimLocalAbbreviationMap * bufferTrackingService: IBufferTrackingService * undoRedoOperations: IUndoRedoOperations * wordUtil: WordUtil * vim: IVim -> VimTextBuffer

    interface IVimTextBuffer
