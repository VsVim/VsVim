#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions
open Vim.RegexUtil

type CommandAction = KeyInput list -> Range option -> bool -> unit

/// Type which is responsible for executing command mode commands
type internal CommandProcessor 
    ( 
        _data : IVimBuffer, 
        _operations : IOperations ) as this = 

    let mutable _command : System.String = System.String.Empty

    /// Last substitute operation that occured
    let mutable _lastSubstitute : (string * string * SubstituteFlags) = ("","",SubstituteFlags.None)

    /// List of supported commands.  The bool value on the lambda is whether or not there was a 
    /// bang following the command
    let mutable _commandList : (string * CommandAction) list = List.empty

    do
        let normalSeq = seq {
            yield ("edit", this.ProcessEdit)
            yield ("delete", this.ProcessDelete)
            yield ("join", this.ProcessJoin)
            yield ("marks", this.ProcessMarks)
            yield ("put", this.ProcessPut)
            yield ("set", this.ProcessSet)
            yield ("source", this.ProcessSource)
            yield ("substitute", this.ProcessSubstitute)
            yield ("s", this.ProcessSubstitute)
            yield ("su", this.ProcessSubstitute)
            yield ("redo", this.ProcessRedo)
            yield ("undo", this.ProcessUndo)
            yield ("yank", this.ProcessYank)
            yield ("<", this.ProcessShiftLeft)
            yield (">", this.ProcessShiftRight)
            yield ("$", fun _ _ _ -> _data.EditorOperations.MoveToEndOfDocument(false))
        }

        let mapClearSeq = seq {
            yield ("mapc", [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Command; KeyRemapMode.OperatorPending]);
            yield ("nmapc", [KeyRemapMode.Normal]);
            yield ("vmapc", [KeyRemapMode.Visual; KeyRemapMode.Select]);
            yield ("xmapc", [KeyRemapMode.Visual]);
            yield ("smapc", [KeyRemapMode.Select]);
            yield ("omapc", [KeyRemapMode.OperatorPending]);
            yield ("mapc!", [KeyRemapMode.Insert; KeyRemapMode.Command]);
            yield ("imapc", [KeyRemapMode.Insert]);
            yield ("cmapc", [KeyRemapMode.Command]);
        }
        let mapClearSeq = 
            mapClearSeq 
            |> Seq.map (fun (name,modes) -> (name, fun _ _ hasBang -> this.ProcessKeyMapClear modes hasBang))


        let unmapSeq = seq {
            yield ("unmap", [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            yield ("nunmap", [KeyRemapMode.Normal])
            yield ("vunmap", [KeyRemapMode.Visual;KeyRemapMode.Select])
            yield ("xunmap", [KeyRemapMode.Visual])
            yield ("sunmap", [KeyRemapMode.Select])
            yield ("ounmap", [KeyRemapMode.OperatorPending])
            yield ("iunmap", [KeyRemapMode.Insert])
            yield ("lunmap", [KeyRemapMode.Language])
            yield ("cunmap", [KeyRemapMode.Command])
        }
        let unmapSeq= 
            unmapSeq
            |> Seq.map (fun (name,modes) -> (name,(fun rest _ hasBang -> this.ProcessKeyUnmap name modes hasBang rest)))

        let remapSeq = seq {
            yield ("map", true, [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            yield ("nmap", true, [KeyRemapMode.Normal])
            yield ("vmap", true, [KeyRemapMode.Visual;KeyRemapMode.Select])
            yield ("xmap", true, [KeyRemapMode.Visual])
            yield ("smap", true, [KeyRemapMode.Select])
            yield ("omap", true, [KeyRemapMode.OperatorPending])
            yield ("imap", true, [KeyRemapMode.Insert])
            yield ("lmap", true, [KeyRemapMode.Language])
            yield ("cmap", true, [KeyRemapMode.Command])
            yield ("noremap", false, [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            yield ("nnoremap", false, [KeyRemapMode.Normal])
            yield ("vnoremap", false, [KeyRemapMode.Visual;KeyRemapMode.Select])
            yield ("xnoremap", false, [KeyRemapMode.Visual])
            yield ("snoremap", false, [KeyRemapMode.Select])
            yield ("onoremap", false, [KeyRemapMode.OperatorPending])
            yield ("inoremap", false, [KeyRemapMode.Insert])
            yield ("lnoremap", false, [KeyRemapMode.Language])
            yield ("cnoremap", false, [KeyRemapMode.Command])
        }

        let remapSeq = 
            remapSeq 
            |> Seq.map (fun (name,allowRemap,modes) -> (name,(fun rest _ hasBang -> this.ProcessKeyMap name allowRemap modes hasBang rest)))

        _commandList <- 
            normalSeq 
            |> Seq.append remapSeq
            |> Seq.append mapClearSeq
            |> Seq.append unmapSeq
            |> List.ofSeq

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

    /// Process the :join command
    member private x.ProcessJoin (rest:KeyInput list) (range:Range option) hasBang =
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
    member private x.ProcessEdit (rest:KeyInput list) _ hasBang = 
        let name = 
            rest 
                |> x.SkipWhitespace
                |> Seq.ofList
                |> Seq.map (fun i -> i.Char)
                |> StringUtil.OfCharSeq 
        if System.String.IsNullOrEmpty name then _data.VimHost.ShowOpenFileDialog()
        else _operations.EditFile name

    /// Parse out the Yank command
    member private x.ProcessYank (rest:KeyInput list) (range: Range option) _ =
        let reg,rest = rest |> x.SkipRegister
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
    member private x.ProcessPut (rest:KeyInput list) (range: Range option) bang =
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
    member private x.ProcessShiftLeft (rest:KeyInput list) (range: Range option) _ =
        let count,rest =  rest  |> x.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        _operations.ShiftLeft span _data.Settings.GlobalSettings.ShiftWidth |> ignore

    member private x.ProcessShiftRight (rest:KeyInput list) (range: Range option) _ =
        let count,rest = rest |> x.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        _operations.ShiftRight span _data.Settings.GlobalSettings.ShiftWidth |> ignore

    /// Implements the :delete command
    member private x.ProcessDelete (rest:KeyInput list) (range:Range option) _ =
        let reg,rest = rest |> x.SkipRegister
        let count,rest = rest |> x.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLine _data.TextView range
        let range = 
            match count with
            | Some(count) -> RangeUtil.ApplyCount range count
            | None -> range
        let span = RangeUtil.GetSnapshotSpan range
        _operations.DeleteSpan span MotionKind.Exclusive OperationKind.LineWise reg |> ignore

    member private x.ProcessUndo rest _ _ =
        match Seq.isEmpty rest with
        | true -> _data.VimHost.Undo _data.TextBuffer 1
        | false -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ProcessRedo rest _ _ =
        match Seq.isEmpty rest with
        | true -> _data.VimHost.Redo _data.TextBuffer 1
        | false -> _data.VimHost.UpdateStatus x.BadMessage

    member private x.ProcessMarks rest _ _ =
        match Seq.isEmpty rest with
        | true -> _operations.PrintMarks _data.MarkMap
        | false -> _data.VimHost.UpdateStatus x.BadMessage

    /// Parse out the :set command
    member private x.ProcessSet (rest:KeyInput list) _ _=
        let rest,data = rest |> x.SkipWhitespace |> x.SkipNonWhitespace
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
    member private x.ProcessSource (rest:KeyInput list) _ bang =
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

    member private x.ProcessSubstitute(rest:KeyInput list) (range:Range option) _ =

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
    
    member private x.ProcessKeyMapClear modes hasBang =
        let modes = 
            if hasBang then [KeyRemapMode.Insert; KeyRemapMode.Command]
            else modes
        _operations.ClearKeyMapModes modes

    member private x.ProcessKeyUnmap (name:string) (modes: KeyRemapMode list) (hasBang:bool) (rest: KeyInput list) = 
        let modes = 
            if hasBang then [KeyRemapMode.Insert; KeyRemapMode.Command]
            else modes
        let rest,lhs = rest |> x.SkipNonWhitespace
        _operations.UnmapKeys lhs modes
        
    member private x.ProcessKeyMap (name:string) (allowRemap:bool) (modes: KeyRemapMode list) (hasBang:bool) (rest: KeyInput list) = 
        let modes = 
            if hasBang then [KeyRemapMode.Insert; KeyRemapMode.Command]
            else modes
        let withKeys lhs rhs _ = _operations.RemapKeys lhs rhs modes allowRemap 
        x.ParseKeys rest withKeys (fun() -> _data.VimHost.UpdateStatus x.BadMessage)

    member private x.ParseCommand (rest:KeyInput list) (range:Range option) = 

        let isCommandNameChar c = System.Char.IsLetter c

        /// Find the single command which fits the passed in set of key strokes
        let rec findCommand (current:KeyInput) (rest:KeyInput list) (commands : (string * CommandAction) seq) index = 

            let found = 
                commands 
                |> Seq.filter (fun (name,_) -> index < name.Length && name.Chars(index) = current.Char)

            match found |> Seq.length with
            | 0 -> None
            | 1 -> 
                let name,action = found |> Seq.head

                // We found a single command with the prefix.  Make sure any remaining input keys
                // match the remainder of the name
                let rec correctNameCheck index (current:KeyInput) (rest:KeyInput list) = 
                    if index = name.Length then Some(action,current :: rest)
                    elif not (isCommandNameChar current.Char) then Some(action, current :: rest)
                    elif name.Chars(index) <> current.Char then None
                    else ListUtil.tryProcessHead rest (fun head tail -> correctNameCheck (index+1) head tail) (fun () -> Some(action,List.empty))
                
                match rest |> ListUtil.tryHead with
                | Some(head,tail) -> correctNameCheck (index+1) head tail
                | None -> Some(action,List.empty)

            | _ -> 

                // Need to be careful here for exact matches.  Certain commands such as substitute can be
                // executed even though they have an ambiguous prefix.  "s" for instance is a prefix for 
                // set or substitute but substitute has priority here.  This is established by adding the 
                // exact name s into the command table.  So if we reach the end of the input to process
                // a command against and we have an exact match it wins
                let exactMatch = found |> Seq.tryFind (fun (name,action) -> name.Length = (index+1))
                match exactMatch,(rest |> ListUtil.tryHead) with
                | Some(name,action),None -> Some(action,List.empty)
                | Some(name,action),Some(head,tail) ->
                    if isCommandNameChar head.Char then findCommand head tail found (index+1)
                    else Some(action,head :: tail)
                | None,Some(head,tail) -> findCommand head tail found (index+1)
                | None,None -> None

        if rest |> List.isEmpty then _data.VimHost.UpdateStatus (x.BadMessage)
        else 
            let head,tail = rest |> ListUtil.divide
            match findCommand head tail _commandList 0 with
            | None -> _data.VimHost.UpdateStatus x.BadMessage
            | Some(action,rest) -> 
                let hasBang,rest = rest |> x.SkipBang
                let rest = rest |> x.SkipWhitespace
                action rest range hasBang
    
    member private x.ParseInput (originalInputs : KeyInput list) =
        let withRange (range:Range option) (inputs:KeyInput list) = x.ParseCommand inputs range
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

            


