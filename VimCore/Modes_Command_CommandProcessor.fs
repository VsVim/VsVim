#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions
open Vim.RegexUtil

/// Type which is responsible for executing command mode commands
type internal CommandProcessor
    ( 
        _data : IVimBuffer, 
        _operations : IOperations ) = 

    let mutable _command : System.String = System.String.Empty

    /// Last substitute operation that occured
    let mutable _lastSubstitute : (string * string * SubstituteFlags) = ("","",SubstituteFlags.None)

    member private x.BadMessage = Resources.CommandMode_CannotRun _command

    member private x.SkipWhitespace (cmd:KeyInput list) =
        let inner (head:KeyInput) tail = 
            if System.Char.IsWhiteSpace head.Char then x.SkipWhitespace (cmd |> List.tail)
            else cmd
        ListUtil.tryProcessHead cmd inner (fun () -> cmd)
        
    member private x.SkipPast (suffix:seq<char>) (cmd:list<KeyInput>) =
        let cmd = cmd |> Seq.ofList
        let rec inner (cmd:seq<KeyInput>) (suffix:seq<char>) = 
            if suffix |> Seq.isEmpty then cmd
            else if cmd |> Seq.isEmpty then cmd
            else 
                let left = cmd |> Seq.head 
                let right = suffix |> Seq.head
                if left.Char = right then inner (cmd |> Seq.skip 1) (suffix |> Seq.skip 1)
                else cmd
        inner cmd suffix |> List.ofSeq

    /// Skip past non-whitespace characters and return the string and next input
    member private x.SkipNonWhitespace (cmd:KeyInput list) =
        let rec inner (cmd:KeyInput list) (data:char list) =
            let withHead (headKey:KeyInput) rest = 
                if System.Char.IsWhiteSpace headKey.Char then (cmd,data)
                else inner rest ([headKey.Char] @ data)
            ListUtil.tryProcessHead cmd withHead (fun () -> (cmd,data))
        let rest,data = inner cmd List.empty
        rest,(data |> List.rev |> StringUtil.OfCharSeq)

    /// Try and skip the ! operator
    member private x.SkipBang (cmd:KeyInput list) =
        let inner (head:KeyInput) tail = 
            if head.Char = '!' then (true, tail)
            else (false,cmd)
        ListUtil.tryProcessHead cmd inner (fun () -> (false,cmd))

    /// Parse the register out of the stream.  Will return default if no register is 
    /// specified
    member private x.SkipRegister (cmd:KeyInput list) =
        let map = _data.RegisterMap
        let inner (head:KeyInput) tail =
            match head.IsDigit,map.IsRegisterName (head.Char) with
            | true,_ -> (map.DefaultRegister, cmd)
            | false,true -> (map.GetRegister(head.Char), tail)
            | false,false -> (map.DefaultRegister, cmd)
        ListUtil.tryProcessHead cmd inner (fun () -> (map.DefaultRegister, cmd))

    /// Parse out the keys for a key remap command
    member private x.ParseKeys (rest:KeyInput list) found notFound =
        let rest,left = rest |> x.SkipWhitespace |> x.SkipNonWhitespace
        let rest,right = rest |> x.SkipWhitespace |> x.SkipNonWhitespace 
        if System.String.IsNullOrEmpty(left) || System.String.IsNullOrEmpty(right) then
            notFound()
        else
            found left right rest

    /// Parse out the :join command
    member private x.ParseJoin (rest:KeyInput list) (range:Range option) =
        let rest = rest |> x.SkipPast "oin" |> x.SkipWhitespace
        let hasBang,rest = x.SkipBang rest        
        let kind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        let rest = x.SkipWhitespace rest
        let count,rest = RangeUtil.ParseNumber rest
        let span = 
            match range with 
            | Some(range) -> Some(RangeUtil.GetSnapshotSpan range)
            | None -> None
            
        let range = span
        let range = 
            match range with 
            | Some(s) -> s
            | None -> 
                let point = ViewUtil.GetCaretPoint _data.TextView
                SnapshotSpan(point,0)

        match count with 
        | Some(c) -> _operations.Join range.End kind c |> ignore
        | None -> 
            let startLine = range.Start.GetContainingLine().LineNumber
            let endLine = range.End.GetContainingLine().LineNumber
            let count = endLine - startLine
            _operations.Join range.Start kind count |> ignore

    /// Parse out the :edit commnad
    member private x.ParseEdit (rest:KeyInput list) = 
        let _,rest = 
            rest 
            |> x.SkipWhitespace 
            |> x.SkipPast "dit"
            |> x.SkipBang
        let name = 
            rest 
                |> x.SkipWhitespace
                |> Seq.ofList
                |> Seq.map (fun i -> i.Char)
                |> StringUtil.OfCharSeq 
        if System.String.IsNullOrEmpty name then _data.VimHost.ShowOpenFileDialog()
        else _operations.EditFile name

    /// Parse out the Yank command
    member private x.ParseYank (rest:KeyInput list) (range: Range option)=
        let reg,rest = 
            rest 
            |> x.SkipWhitespace
            |> x.SkipPast "ank"
            |> x.SkipWhitespace
            |> x.SkipRegister
        let count,rest = RangeUtil.ParseNumber rest

        // Calculate the span to yank
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        
        // Apply the count if present
        let range = 
            match count with             
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range

        let span = RangeUtil.GetSnapshotSpan range
        _operations.Yank span MotionKind.Exclusive OperationKind.LineWise reg

    /// Parse the Put command
    member private x.ParsePut (rest:KeyInput list) (range: Range option) =
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

        _operations.Put reg.StringValue line (not bang)

    /// Parse the < command
    member private x.ParseShiftLeft (rest:KeyInput list) (range: Range option) =
        let count,rest =  rest  |> x.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        _operations.ShiftLeft span _data.Settings.GlobalSettings.ShiftWidth |> ignore

    member private x.ParseShiftRight (rest:KeyInput list) (range: Range option) =
        let count,rest = rest |> x.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        _operations.ShiftRight span _data.Settings.GlobalSettings.ShiftWidth |> ignore

    /// Implements the :delete command
    member private x.ParseDelete (rest:KeyInput list) (range:Range option) =
        let reg,rest =
            rest
            |> x.SkipWhitespace
            |> x.SkipPast "elete"
            |> x.SkipWhitespace
            |> x.SkipRegister
        let count,rest = rest |> x.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        _operations.DeleteSpan span MotionKind.Exclusive OperationKind.LineWise reg |> ignore

    member private x.ParseUndo rest =
        let rest = rest |> x.SkipPast "ndo" |> x.SkipWhitespace 
        match Seq.isEmpty rest with
        | true -> _data.VimHost.Undo _data.TextBuffer 1
        | false -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseRedo rest =
        let rest = rest |> x.SkipPast "edo" |> x.SkipWhitespace 
        match Seq.isEmpty rest with
        | true -> _data.VimHost.Redo _data.TextBuffer 1
        | false -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseMarks rest =
        let rest = rest |> x.SkipPast "ks"
        match Seq.isEmpty rest with
        | true -> _operations.PrintMarks _data.MarkMap
        | false -> _data.VimHost.UpdateStatus x.BadMessage

    /// Parse out the :set command
    member private x.ParseSet (rest:KeyInput list) =
        let rest,data = rest |> x.SkipPast "t" |> x.SkipWhitespace |> x.SkipNonWhitespace
        if System.String.IsNullOrEmpty(data) then _operations.PrintModifiedSettings()
        else
            match data with
            | Match1("^all$") _ -> _operations.PrintAllSettings()
            | Match2("^(\w+)\?$") (_,name) -> _operations.PrintSetting name
            | Match2("^no(\w+)$") (_,name) -> _operations.ResetSetting name
            | Match2("^(\w+)\!$") (_,name) -> _operations.InvertSetting name
            | Match2("^inv(\w+)$") (_,name) -> _operations.InvertSetting name
            | Match3("^(\w+):(\w+)$") (_,name,value) -> _operations.SetSettingValue name value
            | Match3("^(\w+)=(\w+)$") (_,name,value) -> _operations.SetSettingValue name value
            | Match2("^(\w+)$") (_,name) -> _operations.OperateSetting(name)
            | _ -> ()

    /// Used to parse out the :source command.  List is pointing past the o in :source
    member private x.ParseSource (rest:KeyInput list) =
        let bang,rest = rest |> x.SkipPast "urce" |> x.SkipBang
        let rest = rest |> x.SkipWhitespace
        let file = rest |> Seq.map (fun ki -> ki.Char) |> StringUtil.OfCharSeq
        if bang then _data.VimHost.UpdateStatus Resources.CommandMode_NotSupported_SourceNormal
        else
            match Utils.ReadAllLines file with
            | None -> _data.VimHost.UpdateStatus (Resources.CommandMode_CouldNotOpenFile file)
            | Some(_,lines) ->
                lines 
                |> Seq.map (fun command -> command |> Seq.map InputUtil.CharToKeyInput |> List.ofSeq)
                |> Seq.iter x.RunCommand

    member private x.ParseSubstitute (rest:KeyInput list) (range:Range option) =

        // Used to parse out the flags on the :s command
        let rec parseFlags (rest:KeyInput seq) =
            let charToOption c = 
                match c with 
                | 'c' -> SubstituteFlags.Confirm
                | '&' -> SubstituteFlags.UsePrevious
                | 'e' -> SubstituteFlags.SuppressError
                | 'g' -> SubstituteFlags.ReplaceAll
                | 'i' -> SubstituteFlags.IgnoreCase
                | 'I' -> SubstituteFlags.OrdinalCase
                | 'n' -> SubstituteFlags.ReportOnly
                | _ -> SubstituteFlags.Invalid
            rest |> Seq.map (fun ki -> ki.Char) |> Seq.fold (fun f c -> (charToOption c) ||| f) SubstituteFlags.None 

        let doParse rest badParse goodParse =
            let parseOne (rest: KeyInput seq) notFound found = 
                let prefix = rest |> Seq.takeWhile (fun ki -> ki.Char = '/' ) 
                if Seq.length prefix <> 1 then notFound()
                else
                    let rest = rest |> Seq.skip 1
                    let data = rest |> Seq.map (fun ki -> ki.Char) |> Seq.takeWhile (fun c -> c <> '/' ) |> StringUtil.OfCharSeq
                    found data (rest |> Seq.skip data.Length)
            parseOne rest (fun () -> badParse() ) (fun search rest -> 
                parseOne rest (fun () -> badParse()) (fun replace rest ->  
                    let rest = rest |> Seq.skipWhile (fun ki -> ki.Char = '/')
                    let flagsInput = rest |> Seq.takeWhile (fun ki -> not (System.Char.IsWhiteSpace ki.Char))
                    let flags = parseFlags flagsInput
                    let flags = 
                        if Utils.IsFlagSet flags SubstituteFlags.UsePrevious then
                            let _,_,prev = _lastSubstitute
                            (Utils.UnsetFlag flags SubstituteFlags.UsePrevious) ||| prev
                        else
                            flags
                    if Utils.IsFlagSet flags SubstituteFlags.Invalid then badParse()
                    else goodParse search replace flags ))

        let range = RangeUtil.RangeOrCurrentLine _data.TextView range |> RangeUtil.GetSnapshotSpan
        let rest = 
            rest 
            |> x.SkipWhitespace
            |> x.SkipPast "bstitute"
        if List.isEmpty rest then
            let search,replace,flags = _lastSubstitute
            _operations.Substitute search replace range flags
        else 
            let badParse () = _data.VimHost.UpdateStatus Resources.CommandMode_InvalidCommand
            let goodParse search replace flags = 
                if Utils.IsFlagSet flags SubstituteFlags.Confirm then
                    _data.VimHost.UpdateStatus Resources.CommandMode_NotSupported_SubstituteConfirm
                else
                    _operations.Substitute search replace range flags
                    _lastSubstitute <- (search,replace,flags)
            doParse rest badParse goodParse    

    member private x.ParseKeyRemap (rest: KeyInput list) (expected:string) modes allowRemap =
        let rest = rest |> x.SkipPast expected 
        x.ParseKeys rest (fun lhs rhs _ -> _operations.RemapKeys lhs rhs modes allowRemap |> ignore) (fun() -> _data.VimHost.UpdateStatus x.BadMessage)

    member private x.ParseOChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.OperatorPending] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParsePChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'u' -> x.ParsePut rest range
        | _ -> _data.VimHost.UpdateStatus(x.BadMessage)

    member private x.ParseNChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'o' -> x.ParseKeyRemap rest "remap" [KeyRemapMode.Normal ; KeyRemapMode.Visual ; KeyRemapMode.OperatorPending] false
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Normal] true
        | _ -> _data.VimHost.UpdateStatus(x.BadMessage)

    member private x.ParseSChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'e' -> x.ParseSet rest 
        | 'o' -> x.ParseSource rest
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Select] true
        | _ -> x.ParseSubstitute ([current] @ rest) range

    member private x.ParseMAChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'r' -> x.ParseMarks rest
        | 'p' -> x.ParseKeyRemap rest "" [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.OperatorPending] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseCChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Command] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseLChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Language] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseIChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Insert] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseMChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        let parseNext nextFunc = 
            let next head tail = nextFunc head tail range
            ListUtil.tryProcessHead rest next (fun () -> _data.VimHost.UpdateStatus x.BadMessage)
        match current.Char with
        | 'a' -> parseNext x.ParseMAChar
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseVChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Visual; KeyRemapMode.Select] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseXChar (current:KeyInput) (rest: KeyInput list) (range:Range option) =
        match current.Char with
        | 'm' -> x.ParseKeyRemap rest "ap" [KeyRemapMode.Visual] true
        | _ -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ParseCommand (current:KeyInput) (rest:KeyInput list) (range:Range option) =
        let parseNext nextFunc = 
            let next head tail = nextFunc head tail range
            ListUtil.tryProcessHead rest next (fun () -> _data.VimHost.UpdateStatus x.BadMessage)
        match current.Char with
        | 'c' -> parseNext x.ParseCChar
        | 'd' -> x.ParseDelete rest range
        | 'e' -> x.ParseEdit rest 
        | 'j' -> x.ParseJoin rest range
        | 'l' -> parseNext x.ParseLChar 
        | 'i' -> parseNext x.ParseIChar 
        | 'm' -> parseNext x.ParseMChar
        | 'n' -> parseNext x.ParseNChar
        | 'o' -> parseNext x.ParseOChar 
        | 'p' -> parseNext x.ParsePChar
        | 'r' -> x.ParseRedo rest 
        | 's' -> parseNext x.ParseSChar
        | 'u' -> x.ParseUndo rest 
        | 'v' -> parseNext x.ParseVChar
        | 'x' -> parseNext x.ParseXChar
        | 'y' -> x.ParseYank rest range
        | '$' -> _data.EditorOperations.MoveToEndOfDocument(false);
        | '<' -> x.ParseShiftLeft rest range
        | '>' -> x.ParseShiftRight rest range
        | _ -> _data.VimHost.UpdateStatus(x.BadMessage)
    
    member private x.ParseInput (originalInputs : KeyInput list) =
        let withRange (range:Range option) (inputs:KeyInput list) =
            let next head tail = x.ParseCommand head tail range
            ListUtil.tryProcessHead inputs next (fun () -> _data.VimHost.UpdateStatus x.BadMessage)
        let point = ViewUtil.GetCaretPoint _data.TextView
        match RangeUtil.ParseRange point _data.MarkMap originalInputs with
        | ParseRangeResult.Succeeded(range, inputs) -> 
            if inputs |> List.isEmpty then
                match range with 
                | SingleLine(line) -> _data.EditorOperations.GotoLine(line.LineNumber) |> ignore
                | _ -> _data.VimHost.UpdateStatus("Invalid Command String")
            else
                withRange (Some(range)) inputs
        | NoRange -> withRange None originalInputs
        | ParseRangeResult.Failed(msg) -> 
            _data.VimHost.UpdateStatus(msg)
            ()

    /// Run the specified command.  This funtion can be called recursively
    member x.RunCommand (input: KeyInput list)=
        let prev = _command
        try
            // Strip off the preceeding :
            let input = 
                match ListUtil.tryHead input with
                | None -> input
                | Some(head,tail) when head.Char = ':' -> tail
                | _ -> input

            _command <- input |> Seq.map (fun ki -> ki.Char) |> StringUtil.OfCharSeq
            x.ParseInput input
        finally
            _command <- prev

    interface ICommandProcessor with
        member x.RunCommand input = x.RunCommand input

            


