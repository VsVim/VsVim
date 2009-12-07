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

    member x.BadMessage = sprintf "Cannot run \"%s\"" _command

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

    member x.SkipWhitespace (cmd:KeyInput list) =
        if cmd |> List.isEmpty then cmd
        else 
            let head = cmd |> List.head 
            if System.Char.IsWhiteSpace head.Char then x.SkipWhitespace (cmd |> List.tail)
            else cmd
        
    member x.SkipPast (cmd:list<KeyInput>) (suffix:seq<char>) = 
        let cmd = x.SkipWhitespace cmd |> Seq.ofList
        let rec inner (cmd:seq<KeyInput>) (suffix:seq<char>) = 
            if suffix |> Seq.isEmpty then cmd
            else if cmd |> Seq.isEmpty then cmd
            else 
                let left = cmd |> Seq.head 
                let right = suffix |> Seq.head
                if left.Char = right then inner (cmd |> Seq.skip 1) (suffix |> Seq.skip 1)
                else cmd
        inner cmd suffix |> List.ofSeq

    /// Try and skip the ! operator
    member x.SkipBang (cmd:KeyInput list) =
        match cmd |> List.isEmpty with
        | true -> (false,cmd)
        | false ->
            let head = cmd |> List.head 
            if head.Char = '!' then (true, cmd |> List.tail)
            else (false,cmd)

    member x.TryParse (cmd:KeyInput List) (range:SnapshotSpan option) next = 
        if cmd |> List.isEmpty then 
            _data.VimHost.UpdateStatus("Invalid Command String:")
        else next (cmd |> List.head) (cmd |> List.tail) range

    /// Parse out the :join command
    member x.ParseJoin (rest:KeyInput list) (range:SnapshotSpan option) =
        let rest = x.SkipPast rest "oin" |> x.SkipWhitespace
        let hasBang,rest = x.SkipBang rest        
        let kind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        let rest = x.SkipWhitespace rest
        let count,rest = RangeUtil.ParseNumber rest
        Util.Join _data.TextView range kind count |> ignore

    /// Parse out the :edit commnad
    member x.ParseEdit (rest:KeyInput list) = 
        let rest = x.SkipWhitespace rest
        let rest = x.SkipPast rest "dit"
        let _,rest = x.SkipBang rest
        let name = 
            rest 
                |> Seq.ofList
                |> Seq.map (fun i -> i.Char)
                |> StringUtil.OfCharSeq 
        Util.EditFile _data.VimHost name

    member x.ParseCommand (current:KeyInput) (rest:KeyInput list) (range:SnapshotSpan option) =
        if current.IsDigit then
            let number = (current :: rest) |> Seq.ofList |> Seq.map (fun x -> x.Char) |> StringUtil.OfCharSeq
            Util.JumpToLineNumber _data number
        else
            match current.Char with
            | 'j' -> x.ParseJoin rest range
            | 'e' -> x.ParseEdit rest 
            | '$' -> Util.JumpToLastLine _data
            | _ -> _data.VimHost.UpdateStatus(x.BadMessage)
    
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


