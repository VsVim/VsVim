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
    /// bang following the command.  The two strings represent the full and short match name
    /// of the command.  String.Empty represents no shorten'd command available
    let mutable _commandList : (string * string * CommandAction) list = List.empty

    do
        let normalSeq = seq {
            yield ("edit", "e", this.ProcessEdit)
            yield ("delete","d", this.ProcessDelete)
            yield ("join", "j", this.ProcessJoin)
            yield ("marks", "", this.ProcessMarks)
            yield ("put", "pu", this.ProcessPut)
            yield ("set", "se", this.ProcessSet)
            yield ("source","so", this.ProcessSource)
            yield ("substitute", "s", this.ProcessSubstitute)
            yield ("redo", "red", this.ProcessRedo)
            yield ("undo", "u", this.ProcessUndo)
            yield ("yank", "y", this.ProcessYank)
            yield ("<", "", this.ProcessShiftLeft)
            yield (">", "", this.ProcessShiftRight)
            yield ("$", "", fun _ _ _ -> _data.EditorOperations.MoveToEndOfDocument(false))
        }

        let mapClearSeq = seq {
            yield ("mapclear", "mapc", [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Command; KeyRemapMode.OperatorPending]);
            yield ("nmapclear", "nmapc", [KeyRemapMode.Normal]);
            yield ("vmapclear", "vmapc", [KeyRemapMode.Visual; KeyRemapMode.Select]);
            yield ("xmapclear", "xmapc", [KeyRemapMode.Visual]);
            yield ("smapclear", "smapc", [KeyRemapMode.Select]);
            yield ("omapclear", "omapc", [KeyRemapMode.OperatorPending]);
            yield ("imapclear", "imapc", [KeyRemapMode.Insert]);
            yield ("cmapclear", "cmapc", [KeyRemapMode.Command]);
        }
        let mapClearSeq = 
            mapClearSeq 
            |> Seq.map (fun (name,short,modes) -> (name, short, fun _ _ hasBang -> this.ProcessKeyMapClear modes hasBang))


        let unmapSeq = seq {
            yield ("unmap", "unm", [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            yield ("nunmap", "nun", [KeyRemapMode.Normal])
            yield ("vunmap", "vu", [KeyRemapMode.Visual;KeyRemapMode.Select])
            yield ("xunmap", "xu", [KeyRemapMode.Visual])
            yield ("sunmap", "sunm", [KeyRemapMode.Select])
            yield ("ounmap", "ou", [KeyRemapMode.OperatorPending])
            yield ("iunmap", "iu", [KeyRemapMode.Insert])
            yield ("lunmap", "lu", [KeyRemapMode.Language])
            yield ("cunmap", "cu", [KeyRemapMode.Command])
        }
        let unmapSeq= 
            unmapSeq
            |> Seq.map (fun (name,short, modes) -> (name,short,(fun rest _ hasBang -> this.ProcessKeyUnmap name modes hasBang rest)))

        let remapSeq = seq {
            yield ("map", "", true, [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            yield ("nmap", "nm", true, [KeyRemapMode.Normal])
            yield ("vmap", "vm", true, [KeyRemapMode.Visual;KeyRemapMode.Select])
            yield ("xmap", "xm", true, [KeyRemapMode.Visual])
            yield ("smap", "", true, [KeyRemapMode.Select])
            yield ("omap", "om", true, [KeyRemapMode.OperatorPending])
            yield ("imap", "im", true, [KeyRemapMode.Insert])
            yield ("lmap", "lm", true, [KeyRemapMode.Language])
            yield ("cmap", "cm", true, [KeyRemapMode.Command])
            yield ("noremap", "no", false, [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            yield ("nnoremap", "nn", false, [KeyRemapMode.Normal])
            yield ("vnoremap", "vn", false, [KeyRemapMode.Visual;KeyRemapMode.Select])
            yield ("xnoremap", "xn", false, [KeyRemapMode.Visual])
            yield ("snoremap", "snor", false, [KeyRemapMode.Select])
            yield ("onoremap", "ono", false, [KeyRemapMode.OperatorPending])
            yield ("inoremap", "ino", false, [KeyRemapMode.Insert])
            yield ("lnoremap", "ln", false, [KeyRemapMode.Language])
            yield ("cnoremap", "cno", false, [KeyRemapMode.Command])
        }

        let remapSeq = 
            remapSeq 
            |> Seq.map (fun (name,short,allowRemap,modes) -> (name,short,(fun rest _ hasBang -> this.ProcessKeyMap name allowRemap modes hasBang rest)))

        _commandList <- 
            normalSeq 
            |> Seq.append remapSeq
            |> Seq.append mapClearSeq
            |> Seq.append unmapSeq
            |> List.ofSeq

#if DEBUG
        // Make sure there are no duplicates
        let set = new System.Collections.Generic.HashSet<string>()
        for name in _commandList |> Seq.map (fun (name,_,_) -> name) do
            if not (set.Add(name)) then failwith (sprintf "Duplicate command name %s" name)
        let set = new System.Collections.Generic.HashSet<string>()
        for name in _commandList |> Seq.map (fun (_,short,_) -> short) do
            if name <> "" && not (set.Add(name)) then failwith (sprintf "Duplicate command short name %s" name)

#endif

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

        let isCommandNameChar c = 
            (not (System.Char.IsWhiteSpace(c))) 
            && c <> '!'
            && c <> '/'

        // Get the name of the command
        let commandName = 
            rest 
            |> Seq.map (fun ki -> ki.Char)
            |> Seq.takeWhile isCommandNameChar
            |> StringUtil.OfCharSeq

        // Look for commands with that name
        let command =

            // First look for the exact match
            let found =
                _commandList
                |> Seq.tryFind (fun (name,shortName,_) -> name = commandName || shortName = commandName)
            match found with
            | Some(data) -> Some(data)
            | None ->
                
                // No exact name matches look for a prefix match on the full command name
                let found =
                    _commandList
                    |> Seq.filter (fun (name,_,_) -> name.StartsWith(commandName, System.StringComparison.Ordinal))
                match found |> Seq.length with
                | 1 -> found |> Seq.head |> Some
                | _ -> None


        match command with
        | None -> _data.VimHost.UpdateStatus x.BadMessage
        | Some(name,shortName,action) ->
            let rest = rest |> ListUtil.skip commandName.Length 
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

            


