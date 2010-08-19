#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalMode =
    interface INormalMode
    new: IVimBuffer* IOperations * IIncrementalSearch * IStatusUtil * IDisplayWindowBroker * ICommandRunner * IMotionCapture * IVisualSpanCalculator -> NormalMode
    member IncrementalSearch : IIncrementalSearch

