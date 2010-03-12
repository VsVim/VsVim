#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalModeResult = 
    | SwitchMode of ModeKind
    | OperatorPending of (KeyInput -> int -> Register -> NormalModeResult)
    | NeedMoreInput of (KeyInput -> int -> Register -> NormalModeResult)
    | CountComplete of int * KeyInput
    | RegisterComplete of Register
    | Complete 

