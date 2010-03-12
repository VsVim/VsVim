#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions
open Vim.RegexUtil

type internal CommandMode
    ( 
        _data : IVimBuffer, 
        _processor : ICommandProcessor ) =

    let mutable _command = System.String.Empty

    /// Reverse list of the inputted commands
    let mutable _input : list<KeyInput> = []

    /// Currently queued up command string
    member x.Command = _command

    interface ICommandMode with 
        member x.VimBuffer = _data 
        member x.Commands = Seq.empty
        member x.ModeKind = ModeKind.Command
        member x.CanProcess ki = true
        member x.Process ki = 
            match ki.Key with 
                | EnterKey ->
                    _data.VimHost.UpdateStatus(System.String.Empty)
                    let command = _input |> List.rev |> Seq.map (fun ki -> ki.Char) |> List.ofSeq
                    _processor.RunCommand command
                    _input <- List.empty
                    _command <- System.String.Empty
                    SwitchMode ModeKind.Normal
                | EscapeKey ->
                    _data.VimHost.UpdateStatus(System.String.Empty)
                    _input <- List.empty
                    _command <- System.String.Empty
                    SwitchMode ModeKind.Normal
                | BackKey -> 
                    if not (List.isEmpty _input) then 
                        _input <- List.tail _input
                        _command <- _command.Substring(0, (_command.Length - 1))
                    Processed
                | _ -> 
                    let c = ki.Char
                    _command <-_command + (c.ToString())
                    _data.VimHost.UpdateStatus(":" + _command)
                    _input <- ki :: _input
                    Processed

        member x.OnEnter () =
            _command <- System.String.Empty
            _data.VimHost.UpdateStatus(":")
            _data.TextView.Caret.IsHidden <- true
        member x.OnLeave () = 
            _data.TextView.Caret.IsHidden <- false

        member x.RunCommand command = 
            _processor.RunCommand (command |> List.ofSeq)
            


