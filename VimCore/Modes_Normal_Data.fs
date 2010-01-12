#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type internal NormalModeData = {
    VimBufferData : IVimBuffer;
    Register : Register;
    Count : int;
    RunFunc : NormalModeData -> KeyInput -> NormalModeResult;
    WaitingForMoreInput:bool;
} 
    
and internal NormalModeResult = 
    | SwitchMode of ModeKind
    | NeedMore of NormalModeData
    | NeedMore2 of (NormalModeData -> KeyInput -> NormalModeResult)
    | CountComplete of int * KeyInput
    | RegisterComplete of Register
    | Complete 

