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
    | Complete 

module internal NormalModeUtil =
    
    /// Create the appropriate FindOptions value
    let CreateFindOptions kind (settings:IVimGlobalSettings) =
        let options = if not settings.IgnoreCase then FindOptions.MatchCase else FindOptions.None
        let options = if SearchKindUtil.IsBackward kind then options ||| FindOptions.SearchReverse else options
        options
        
