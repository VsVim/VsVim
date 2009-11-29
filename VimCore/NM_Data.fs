#light

namespace VimCore.Modes.Normal
open VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type NormalModeData = {
    VimBufferData : IVimBufferData;
    Register : Register;
    Count : int;
    LastSearch : option<IncrementalSearch>;
    RunFunc : NormalModeData -> KeyInput -> NormalModeResult;
    WaitingForMoreInput:bool;
} 
    
and NormalModeResult = 
    | SwitchMode of ModeKind
    | NeedMore of NormalModeData
    | NeedMore2 of (NormalModeData -> KeyInput -> NormalModeResult)
    | SearchComplete of IncrementalSearch
    | CountComplete of int * KeyInput
    | RegisterComplete of Register
    | Complete 
