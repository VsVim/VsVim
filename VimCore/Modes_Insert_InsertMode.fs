#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type internal InsertMode
    ( 
        _data : IVimBufferData, 
        _operations : Modes.ICommonOperations ) =
    let _commands = [
        InputUtil.KeyToKeyInput(Key.Escape);
        KeyInput('d', Key.D, ModifierKeys.Control); ]

    /// Process the CTRL-D combination and do a shift left
    member this.ShiftLeft() =
        let caret = ViewUtil.GetCaretPoint _data.TextView
        let line = caret.GetContainingLine()
        _operations.ShiftLeft line.Extent (_data.Settings.ShiftWidth) |> ignore

    interface IMode with 
        member x.Commands = _commands |> Seq.ofList
        member x.ModeKind = ModeKind.Insert
        member x.CanProcess (ki:KeyInput) = 
            match _commands |> List.tryFind (fun d -> d = ki) with
            | Some _ -> true
            | None -> false
        member x.Process (ki : KeyInput) = 
            match ki.Key,ki.ModifierKeys with
            | Key.Escape,_ -> ProcessResult.SwitchMode ModeKind.Normal
            | Key.D,ModifierKeys.Control -> 
                x.ShiftLeft()
                ProcessResult.Processed
            | _ -> 
                Processed
        member x.OnEnter () = ()
        member x.OnLeave () = ()
