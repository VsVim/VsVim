
#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimTextBuffer =

    new : ITextBuffer * IVimLocalSettings * ITextStructureNavigator * IBufferTrackingService * IUndoRedoOperations * IVim -> VimTextBuffer

    interface IVimTextBuffer
