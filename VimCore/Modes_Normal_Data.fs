#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type internal NormalModeResult = 
    | SwitchMode of ModeKind
    | OperatorPending of (KeyInput -> int -> Register -> NormalModeResult)
    | NeedMoreInput of (KeyInput -> int -> Register -> NormalModeResult)
    | CountComplete of int * KeyInput
    | RegisterComplete of Register
    | Complete 

