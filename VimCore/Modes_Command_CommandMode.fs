#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions
open Vim.RegexUtil

type CommandMode( _data : IVimBufferData ) = 
    let mutable _command : System.String = System.String.Empty

    /// Reverse list of the inputted commands
    let mutable _input : list<KeyInput> = []

    member x.BadMessage = sprintf "Cannot run \"%s\"" _command

    member x.SkipWhitespace (cmd:KeyInput list) =
        if cmd |> List.isEmpty then cmd
        else 
            let head = cmd |> List.head 
            if System.Char.IsWhiteSpace head.Char then x.SkipWhitespace (cmd |> List.tail)
            else cmd
        
    member x.SkipPast (suffix:seq<char>) (cmd:list<KeyInput>) =
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

    /// Parse the register out of the stream.  Will return default if no register is 
    /// specified
    member x.SkipRegister (cmd:KeyInput list) =
        let map = _data.RegisterMap
        match cmd |> List.isEmpty with
        | true -> (map.DefaultRegister,cmd)
        | false -> 
            let head = cmd |> List.head
            match head.IsDigit,map.IsRegisterName (head.Char) with
            | true,_ -> (map.DefaultRegister, cmd)
            | false,true -> (map.GetRegister(head.Char), cmd |> List.tail)
            | false,false -> (map.DefaultRegister, cmd)

    member x.TryParseNext (cmd:KeyInput List) next = 
        if cmd |> List.isEmpty then 
            _data.VimHost.UpdateStatus("Invalid Command String")
        else next (cmd |> List.head) (cmd |> List.tail) 

    /// Parse out the :join command
    member x.ParseJoin (rest:KeyInput list) (range:Range option) =
        let rest = rest |> x.SkipPast "oin" |> x.SkipWhitespace
        let hasBang,rest = x.SkipBang rest        
        let kind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        let rest = x.SkipWhitespace rest
        let count,rest = RangeUtil.ParseNumber rest
        let span = 
            match range with 
            | Some(range) -> Some(RangeUtil.GetSnapshotSpan range)
            | None -> None
        Util.Join _data.TextView span kind count |> ignore

    /// Parse out the :edit commnad
    member x.ParseEdit (rest:KeyInput list) = 
        let _,rest = 
            rest 
            |> x.SkipWhitespace 
            |> x.SkipPast "dit"
            |> x.SkipBang
        let name = 
            rest 
                |> Seq.ofList
                |> Seq.map (fun i -> i.Char)
                |> StringUtil.OfCharSeq 
        Util.EditFile _data.VimHost name

    /// Parse out the Yank command
    member x.ParseYank (rest:KeyInput list) (range: Range option)=
        let reg,rest = 
            rest 
            |> x.SkipWhitespace
            |> x.SkipPast "ank"
            |> x.SkipWhitespace
            |> x.SkipRegister
        let count,rest = RangeUtil.ParseNumber rest

        // Calculate the span to yank
        let range = 
            match range with 
            | Some(range) -> range
            | None -> RangeUtil.RangeForCurrentLine _data.TextView
        
        // Apply the count if present
        let range = 
            match count with             
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range

        let span = RangeUtil.GetSnapshotSpan range
        Modes.ModeUtil.Yank span MotionKind.Exclusive OperationKind.LineWise reg

    /// Parse the Put command
    member x.ParsePut (rest:KeyInput list) (range: Range option) =
        let bang,rest =
            rest
            |> x.SkipWhitespace
            |> x.SkipPast "t"
            |> x.SkipBang
        let reg,rest = 
            rest
            |> x.SkipWhitespace
            |> x.SkipRegister
        
        // Figure out the line number
        let line = 
            match range with 
            | None -> (ViewUtil.GetCaretPoint _data.TextView).GetContainingLine()
            | Some(range) ->
                match range with 
                | Range.SingleLine(line) -> line
                | Range.RawSpan(span) -> span.End.GetContainingLine()
                | Range.Lines(tss,_,endLine) -> tss.GetLineFromLineNumber(endLine)

        Util.Put _data.VimHost _data.TextView reg.StringValue line (not bang)

    /// Parse the < command
    member x.ParseShiftLeft (rest:KeyInput list) (range: Range option) =
        let count,rest =  rest  |> x.SkipWhitespace |> RangeUtil.ParseNumber

        let range = 
            match range with 
            | Some(range) -> range
            | None -> RangeUtil.RangeForCurrentLine _data.TextView
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        BufferUtil.ShiftLeft span _data.Settings.ShiftWidth |> ignore

    member x.ParseShiftRight (rest:KeyInput list) (range: Range option) =
        let count,rest = rest |> x.SkipWhitespace |> RangeUtil.ParseNumber

        let range =
            match range with
            | Some(range) -> range
            | None -> RangeUtil.RangeForCurrentLine _data.TextView
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        BufferUtil.ShiftRight span _data.Settings.ShiftWidth |> ignore

    member x.ParsePChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'u' -> x.ParsePut rest range
        | _ -> _data.VimHost.UpdateStatus(x.BadMessage)

    member x.ParseCommand (current:KeyInput) (rest:KeyInput list) (range:Range option) =
        match current.Char with
        | 'j' -> x.ParseJoin rest range
        | 'e' -> x.ParseEdit rest 
        | '$' -> Util.JumpToLastLine _data
        | 'y' -> x.ParseYank rest range
        | 'p' -> 
            let next head tail = x.ParsePChar head tail range
            x.TryParseNext rest next
        | '<' -> x.ParseShiftLeft rest range
        | '>' -> x.ParseShiftRight rest range
        | _ -> _data.VimHost.UpdateStatus(x.BadMessage)
    
    member x.ParseInput (originalInputs : KeyInput list) =
        let withRange (range:Range option) (inputs:KeyInput list) =
            let next head tail = x.ParseCommand head tail range
            x.TryParseNext inputs next
        let point = ViewUtil.GetCaretPoint _data.TextView
        match RangeUtil.ParseRange point _data.MarkMap originalInputs with
        | Succeeded(range, inputs) -> 
            if inputs |> List.isEmpty then
                match range with 
                | SingleLine(line) ->  _data.TextView.Caret.MoveTo(line.Start) |> ignore
                | _ -> _data.VimHost.UpdateStatus("Invalid Command String")
            else
                withRange (Some(range)) inputs
        | NoRange -> withRange None originalInputs
        | Failed(msg) -> 
            _data.VimHost.UpdateStatus(msg)
            ()

    interface IMode with 
        member x.Commands = Seq.empty
        member x.ModeKind = ModeKind.Command
        member x.CanProcess ki = true
        member x.Process ki = 
            match ki.Key with 
                | Key.Enter ->
                    _data.VimHost.UpdateStatus(System.String.Empty)
                    x.ParseInput (List.rev _input)
                    SwitchMode ModeKind.Normal
                | Key.Escape ->
                    _data.VimHost.UpdateStatus(System.String.Empty)
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
        member x.OnLeave () = ()


