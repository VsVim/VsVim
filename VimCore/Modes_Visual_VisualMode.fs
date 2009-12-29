#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal VisualMode
    (
        _bufferData : IVimBufferData,
        _operations : ICommonOperations,
        _kind : ModeKind ) = 
    do 
        match _kind with 
        | ModeKind.VisualBlock -> ()
        | ModeKind.VisualCharacter -> ()
        | ModeKind.VisualLineWise -> ()
        | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"
        
    interface IMode with
        member x.Commands = Seq.empty
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = false
        member x.Process (ki : KeyInput) =  ProcessResult.ProcessNotHandled
        member x.OnEnter () = ()
        member x.OnLeave () = ()


