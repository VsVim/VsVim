#light

namespace VimCore.Modes.Command
open VimCore
open VimCore.Modes.Common
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions
open VimCore.RegexUtil

type CommandMode( _data : IVimBufferData ) = 
    let mutable _command : System.String = System.String.Empty

    /// Reverse list of the inputted commands
    let mutable _input : list<KeyInput> = []

   // Actually process the completed command
    member x.ProcessCommand (cmd:string) (range:SnapshotSpan option)= 
        let d = _data
        let host = d.VimHost
        match cmd with
            | Match2 "^e\s(.*)$" (_,file) -> Util.EditFile host file
            | Match2 "^(\d+)$" (_,lineNum) -> Util.JumpToLineNumber d lineNum
            | Match1 "^\$$" _ -> Util.JumpToLastLine d
            | Match1 "^j" _ -> Util.Join d.TextView range JoinKind.RemoveEmptySpaces None |> ignore
            | Match1 "^join" _ -> Util.Join d.TextView range JoinKind.RemoveEmptySpaces None |> ignore
            | _ -> host.UpdateStatus("Cannot run \"" + cmd)

    member x.ParseInput (originalInputs : KeyInput list) =
        let withRange (range:SnapshotSpan option) (inputs:KeyInput list) =
            let builder = new System.Text.StringBuilder()
            inputs |> List.iter (fun x -> builder.Append(x.Char) |> ignore)
            x.ProcessCommand (builder.ToString()) range
        let point = ViewUtil.GetCaretPoint _data.TextView
        match RangeUtil.ParseRange point _data.MarkMap originalInputs with
        | ValidRange(span, inputs) -> withRange (Some(span)) inputs
        | NoRange(inputs) -> withRange None inputs
        | Invalid(msg,_) -> 
            _data.VimHost.UpdateStatus(msg)
            ()

    interface IMode with 
        member x.Commands = Seq.empty
        member x.ModeKind = ModeKind.Command
        member x.CanProcess ki = true
        member x.Process ki = 
            match ki.Key with 
                | Key.Enter ->
                    x.ParseInput (List.rev _input)
                    SwitchMode ModeKind.Normal
                | Key.Escape ->
                    SwitchMode ModeKind.Normal
                | _ -> 
                    let c = ki.Char
                    _command <-_command + (c.ToString())
                    _data.VimHost.UpdateStatus(":" + _command)
                    _input <- ki :: _input
                    Processed
                    
        member x.OnEnter () =
            _command <- System.String.Empty
            _data.VimHost.UpdateStatus(":")
        member x.OnLeave () = 
            _data.VimHost.UpdateStatus(System.String.Empty)


