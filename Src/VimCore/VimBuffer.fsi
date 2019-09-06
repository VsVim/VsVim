#light

namespace Vim

open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal VimBufferData = 
    new: vimTextBuffer: IVimTextBuffer * textView: ITextView * windowSettings: IVimWindowSettings * jumpList: IJumpList * statusUtil: IStatusUtil * selectionUtil: ISelectionUtil * caretRegisterMap: ICaretRegisterMap -> VimBufferData

    interface IVimBufferData

type internal VimBuffer =

    new: vimBufferData: IVimBufferData * incrementalSearch: IIncrementalSearch * motionUtil: IMotionUtil * textStructureNavigator: ITextStructureNavigator * windowSettings: IVimWindowSettings * commandutil: ICommandUtil -> VimBuffer

    member AddMode: mode: IMode -> unit

    interface IVimBuffer

    interface IVimBufferInternal
