#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalMode =
    interface INormalMode
    new: IVimBuffer* ICommonOperations * IStatusUtil * IDisplayWindowBroker * ICommandRunner * IMotionCapture -> NormalMode

