#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal NormalModeResult = 
    | SwitchMode of ModeKind
    | OperatorPending of (KeyInput -> int -> Register -> NormalModeResult)
    | NeedMoreInput of (KeyInput -> int -> Register -> NormalModeResult)
    | NeedMoreInput2 of (KeyInput -> int option -> Register -> NormalModeResult)
    | CountComplete of int * KeyInput
    | RegisterComplete of Register
    | CompleteRepeatable of int * Register
    | Complete 
    | CompleteNotCommand 

        
