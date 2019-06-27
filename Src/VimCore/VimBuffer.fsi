#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimBufferData = 
    new: IVimTextBuffer * ITextView * IVimWindowSettings * IJumpList * IStatusUtil * ICaretRegisterMap -> VimBufferData

    interface IVimBufferData

type internal VimBuffer =

    new: IVimBufferData * IIncrementalSearch * IMotionUtil * ITextStructureNavigator * IVimWindowSettings * ICommandUtil -> VimBuffer

    member AddMode: IMode -> unit

    interface IVimBuffer

    interface IVimBufferInternal

