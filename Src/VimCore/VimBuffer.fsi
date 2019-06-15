#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimBufferData = 
    // KTODO: delete WordUtil and grab it from the IVimTextBuffer instead
    new: IVimTextBuffer * ITextView * IVimWindowSettings * IJumpList * IStatusUtil * WordUtil -> VimBufferData

    interface IVimBufferData

type internal VimBuffer =

    new: IVimBufferData * IIncrementalSearch * IMotionUtil * ITextStructureNavigator * IVimWindowSettings * ICommandUtil -> VimBuffer

    member AddMode: IMode -> unit

    interface IVimBuffer

    interface IVimBufferInternal

