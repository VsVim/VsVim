#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimBufferData = 
    new : IVimTextBuffer * ITextView * IVimWindowSettings * IJumpList * IStatusUtil * IWordUtil -> VimBufferData

    interface IVimBufferData

type internal VimBuffer =

    new : IVimBufferData * IIncrementalSearch * IMotionUtil * ITextStructureNavigator * IVimWindowSettings * ICommandUtil -> VimBuffer

    member AddMode : IMode -> unit

    member RaiseErrorMessage : string -> unit

    member RaiseWarningMessage : string -> unit

    member RaiseStatusMessage : string -> unit

    interface IVimBuffer

