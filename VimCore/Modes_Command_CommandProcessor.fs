#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions
open Vim.RegexPatternUtil
open Vim.VimHostExtensions

[<System.Flags>]
type internal KeyRemapOptions =
    | None = 0
    | Buffer = 0x1
    | Silent = 0x2
    | Special = 0x4
    | Script = 0x8
    | Expr = 0x10
    | Unique = 0x20

module internal CommandParseUtil = 

    let rec SkipWhitespace (cmd:char list) =
        let inner head tail = 
            if System.Char.IsWhiteSpace head then SkipWhitespace (cmd |> List.tail)
            else cmd
        ListUtil.tryProcessHead cmd inner (fun () -> cmd)
        
    /// Skip past non-whitespace characters and return the string and next input
    let SkipNonWhitespace (cmd:char list) =
        let rec inner (cmd:char list) (data:char list) =
            let withHead headKey rest = 
                if System.Char.IsWhiteSpace headKey then (cmd,data)
                else inner rest (headKey :: data)
            ListUtil.tryProcessHead cmd withHead (fun () -> (cmd,data))
        let rest,data = inner cmd List.empty
        rest,(data |> List.rev |> StringUtil.ofCharSeq)

    /// Try and skip the ! operator
    let SkipBang (cmd:char list) =
        let inner head tail = 
            if head = '!' then (true, tail)
            else (false,cmd)
        ListUtil.tryProcessHead cmd inner (fun () -> (false,cmd))

    /// Parse the register out of the stream.  Will return default if no register is 
    /// specified
    let SkipRegister (map:IRegisterMap) (cmd:char list) =
        let defaultRegister = map.GetRegister RegisterName.Unnamed
        let inner head tail =
            match System.Char.IsDigit(head),RegisterNameUtil.CharToRegister head with
            | true,_ -> (defaultRegister, cmd)
            | false,Some(name)-> (map.GetRegister name, tail)
            | false,None -> (defaultRegister, cmd)
        ListUtil.tryProcessHead cmd inner (fun () -> (defaultRegister, cmd))

    let ParseKeyRemapOptions (rest:char list) =
        let rec inner (orig:char list) options =
            let rest,arg = orig |> SkipNonWhitespace
            match arg with
            | "<buffer>" -> inner rest (options ||| KeyRemapOptions.Buffer)
            | "<silent>" -> inner rest (options ||| KeyRemapOptions.Silent)
            | "<special>" -> inner rest (options ||| KeyRemapOptions.Special)
            | "<script>" -> inner rest (options ||| KeyRemapOptions.Script)
            | "<expr>" -> inner rest (options ||| KeyRemapOptions.Expr)
            | "<unique>" -> inner rest (options ||| KeyRemapOptions.Unique)
            | _ -> (orig |> SkipWhitespace,options)
        inner rest KeyRemapOptions.None

    /// Parse out the keys for a key remap command
    let ParseKeys (rest:char list) found notFound =
        let rest,options = rest |> ParseKeyRemapOptions
        let rest,left = rest |> SkipWhitespace |> SkipNonWhitespace
        let rest,right = rest |> SkipWhitespace |> SkipNonWhitespace 
        if System.String.IsNullOrEmpty(left) || System.String.IsNullOrEmpty(right) then
            notFound()
        else
            found left right rest

    /// Used to parse out the flags for substitute commands.  If successful 
    /// it will pass the flags and remaining characters to the 
    /// goodParse function and will call badParse on an error
    /// the flags.
    let ParseSubstituteFlags previousFlags rest = 

        // Convert the given char to a flag
        let charToFlag c = 
            match c with 
            | 'c' -> Some SubstituteFlags.Confirm
            | 'r' -> Some SubstituteFlags.UsePreviousSearchPattern
            | 'e' -> Some SubstituteFlags.SuppressError
            | 'g' -> Some SubstituteFlags.ReplaceAll
            | 'i' -> Some SubstituteFlags.IgnoreCase
            | 'I' -> Some SubstituteFlags.OrdinalCase
            | 'n' -> Some SubstituteFlags.ReportOnly
            | 'p' -> Some SubstituteFlags.PrintLast
            | 'l' -> Some SubstituteFlags.PrintLastWithList
            | '#' -> Some SubstituteFlags.PrintLastWithNumber
            | '&' -> Some SubstituteFlags.UsePreviousFlags
            | _  -> None

        // Iterate down the characters getting out the flags
        let rec inner rest flags isFirst = 

            match rest with
            | [] -> 
                // Nothing left so we are done
                flags, []
            | head :: tail -> 
                match charToFlag head with
                | None ->
                    // No flag then we're done
                    flags, rest
                | Some flag ->
                    if flag = SubstituteFlags.UsePreviousFlags && isFirst then
                        inner tail previousFlags false
                    elif flag = SubstituteFlags.UsePreviousFlags then
                        // Only valid in the first position.  
                        flags, rest
                    else 
                        let flags = flags ||| flag
                        inner tail flags false

        inner rest SubstituteFlags.None true

type CommandAction = char list -> SnapshotLineRange option -> bool -> RunResult

/// Type which is responsible for executing command mode commands
type internal CommandProcessor 
    ( 
        _buffer : IVimBuffer, 
        _operations : ICommonOperations,
        _commandOperations : IOperations,
        _statusUtil : IStatusUtil,
        _fileSystem : IFileSystem,
        _foldManager : IFoldManager
    ) as this = 

    let _textView = _buffer.TextView
    let _textBuffer = _textView.TextBuffer
    let _host = _buffer.Vim.VimHost
    let _regexFactory = VimRegexFactory(_buffer.LocalSettings.GlobalSettings)
    let _registerMap = _buffer.RegisterMap
    let _vim = _buffer.Vim
    let _searchService = _vim.SearchService
    let _vimData = _buffer.VimData
    let _localSettings = _buffer.LocalSettings

    let mutable _command : System.String = System.String.Empty

    /// List of supported commands.  The bool value on the lambda is whether or not there was a 
    /// bang following the command.  The two strings represent the full and short match name
    /// of the command.  String.Empty represents no shorten'd command available
    ///
    /// This list is calculated on an as needed basis.  Perf Watson reports show that building
    /// this list can be a factor in startup performance.  So now we only build it on demand
    let mutable _commandListStorage : (string * string * CommandAction) list = List.empty

    /// Create the command list for this command processor
    member x.CreateCommandListStorage () =

        // Wrap a unit returning command into one that returns a Complete result
        let wrap func = 
            fun rest range bang -> 
                func rest range bang
                RunResult.Completed

        let normalSeq = seq {
            yield ("close", "clo", this.ProcessClose |> wrap)
            yield ("delete","d", this.ProcessDelete |> wrap)
            yield ("edit", "e", this.ProcessEdit |> wrap)
            yield ("exit", "exi", this.ProcessWriteQuit |> wrap)
            yield ("display","di", this.ProcessRegisters |> wrap) 
            yield ("fold", "fo", this.ProcessFold |> wrap)
            yield ("join", "j", this.ProcessJoin |> wrap)
            yield ("make", "mak", this.ProcessMake |> wrap)
            yield ("marks", "", this.ProcessMarks |> wrap)
            yield ("nohlsearch", "noh", this.ProcessNoHighlightSearch |> wrap)
            yield ("put", "pu", this.ProcessPut |> wrap)
            yield ("quit", "q", this.ProcessQuit |> wrap)
            yield ("qall", "qa", this.ProcessQuitAll |> wrap)
            yield ("quitall", "quita", this.ProcessQuitAll |> wrap)
            yield ("redo", "red", this.ProcessRedo |> wrap)
            yield ("registers", "reg", this.ProcessRegisters |> wrap)
            yield ("retab", "ret", this.ProcessRetab |> wrap)
            yield ("set", "se", this.ProcessSet |> wrap)
            yield ("source","so", this.ProcessSource |> wrap)
            yield ("split", "sp", this.ProcessSplit |> wrap)
            yield ("substitute", "s", this.ProcessSubstitute)
            yield ("smagic", "sm", this.ProcessSubstituteMagic)
            yield ("snomagic", "sno", this.ProcessSubstituteNomagic)
            yield ("tabfirst", "tabfir", this.ProcessTabFirst |> wrap)
            yield ("tablast", "tabl", this.ProcessTabLast |> wrap)
            yield ("tabnext", "tabn", this.ProcessTabNext |> wrap)
            yield ("tabNext", "tabN", this.ProcessTabPrevious |> wrap)
            yield ("tabprevious", "tabp", this.ProcessTabPrevious |> wrap)
            yield ("tabrewind", "tabr", this.ProcessTabFirst |> wrap)
            yield ("undo", "u", this.ProcessUndo |> wrap)
            yield ("write","w", this.ProcessWrite |> wrap)
            yield ("wq", "", this.ProcessWriteQuit |> wrap)
            yield ("wall", "wa", this.ProcessWriteAll |> wrap)
            yield ("xit", "x", this.ProcessWriteQuit |> wrap)
            yield ("yank", "y", this.ProcessYank |> wrap)
            yield ("/", "", this.ProcessSearchPattern Path.Forward |> wrap)
            yield ("?", "", this.ProcessSearchPattern Path.Backward |> wrap)
            yield ("<", "", this.ProcessShiftLeft |> wrap)
            yield (">", "", this.ProcessShiftRight |> wrap)
            yield ("&", "&", this.ProcessSubstituteRepeat SubstituteFlags.None )
            yield ("~", "~", this.ProcessSubstituteWithLastPattern)
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
            |> Seq.map (fun (name,short,modes) -> (name, short, fun _ _ hasBang -> 
                this.ProcessKeyMapClear modes hasBang
                RunResult.Completed))

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
            |> Seq.map (fun (name,short, modes) -> (name,short,(fun rest _ hasBang -> 
                this.ProcessKeyUnmap name modes hasBang rest
                RunResult.Completed)))

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
            |> Seq.map (fun (name,short,allowRemap,modes) -> (name,short,(fun rest _ hasBang -> 
                this.ProcessKeyMap name allowRemap modes hasBang rest
                RunResult.Completed)))

        _commandListStorage <- 
            normalSeq 
            |> Seq.append remapSeq
            |> Seq.append mapClearSeq
            |> Seq.append unmapSeq
            |> List.ofSeq

#if DEBUG
        // Make sure there are no duplicates
        let set = new System.Collections.Generic.HashSet<string>()
        for name in _commandListStorage |> Seq.map (fun (name,_,_) -> name) do
            if not (set.Add(name)) then failwith (sprintf "Duplicate command name %s" name)
        let set = new System.Collections.Generic.HashSet<string>()
        for name in _commandListStorage |> Seq.map (fun (_,short,_) -> short) do
            if name <> "" && not (set.Add(name)) then failwith (sprintf "Duplicate command short name %s" name)
#endif

    member x.GetOrCreateCommandList () = 
        if List.isEmpty _commandListStorage then
            x.CreateCommandListStorage()
        _commandListStorage

    member x.BadMessage = Resources.CommandMode_CannotRun _command

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    /// The flags from the previous substitute operation
    member x.PreviousSubstituteFlags = 
        match _vimData.LastSubstituteData with
        | None -> SubstituteFlags.None
        | Some data -> data.Flags

    /// Process the :close command
    member x.ProcessClose _ _ hasBang = _buffer.Vim.VimHost.Close _buffer.TextView (not hasBang)

    /// Process the :join command
    member x.ProcessJoin (rest:char list) (range:SnapshotLineRange option) hasBang =
        let kind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        let rest = CommandParseUtil.SkipWhitespace rest
        let count,rest = RangeUtil.ParseNumber rest

        let range = 
            match range with 
            | Some(range) -> RangeUtil.TryApplyCount count range
            | None -> 
                match count with
                | None -> _buffer.TextView |> RangeUtil.RangeForCurrentLine |> RangeUtil.ApplyCount 2
                | Some(1) -> _buffer.TextView |> RangeUtil.RangeForCurrentLine |> RangeUtil.ApplyCount 2
                | Some(count) -> _buffer.TextView |> RangeUtil.RangeForCurrentLine |> RangeUtil.ApplyCount count

        _operations.Join range kind

    /// Parse out the :edit commnad
    member x.ProcessEdit (rest:char list) _ hasBang = 
        let name = 
            rest 
                |> CommandParseUtil.SkipWhitespace
                |> StringUtil.ofCharSeq
        if System.String.IsNullOrEmpty name then 
            if not hasBang && _host.IsDirty _textBuffer then
                _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
            else
                let caret = 
                    let point = TextViewUtil.GetCaretPoint _textView
                    point.Snapshot.CreateTrackingPoint(point.Position, PointTrackingMode.Negative)
                if not (_host.Reload _textBuffer) then
                    _operations.Beep()
                else
                    match TrackingPointUtil.GetPoint _textView.TextSnapshot caret with
                    | None -> ()
                    | Some(point) -> 
                        TextViewUtil.MoveCaretToPoint _textView point
                        TextViewUtil.EnsureCaretOnScreen _textView

        elif not hasBang && _host.IsDirty _textBuffer then
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            match _host.LoadFileIntoExistingWindow name _textBuffer with
            | HostResult.Success -> ()
            | HostResult.Error(msg) -> _statusUtil.OnError(msg)

    /// Parse out the fold command and create the fold
    member x.ProcessFold _ (range : SnapshotLineRange option) _ =
        let range = RangeUtil.RangeOrCurrentLine _buffer.TextView range
        _foldManager.CreateFold range

    /// Parse out the Yank command
    member x.ProcessYank (rest:char list) (range: SnapshotLineRange option) _ =
        let reg,rest = rest |> CommandParseUtil.SkipRegister _buffer.RegisterMap
        let count,rest = RangeUtil.ParseNumber rest

        // Calculate the span to yank
        let range = RangeUtil.RangeOrCurrentLine _buffer.TextView range
        
        // Apply the count if present
        let range = 
            match count with             
            | Some(count) -> RangeUtil.ApplyCount count range
            | None -> range

        let stringData = StringData.OfSpan range.ExtentIncludingLineBreak
        let value = RegisterValue.String (stringData, OperationKind.LineWise)
        _registerMap.SetRegisterValue reg RegisterOperation.Yank value

    /// Process the :nohlsearch command
    member x.ProcessNoHighlightSearch _ _ _ =
        _vimData.RaiseHighlightSearchOneTimeDisable()

    /// Parse the Put command
    member x.ProcessPut (rest:char list) (range: SnapshotLineRange option) bang =
        let reg, rest = 
            rest
            |> CommandParseUtil.SkipWhitespace
            |> CommandParseUtil.SkipRegister _buffer.RegisterMap
        
        let range = RangeUtil.RangeOrCurrentLine _buffer.TextView range
        _commandOperations.PutLine reg range.EndLine bang

    /// Process the search pattern command
    member x.ProcessSearchPattern path (rest : char list) range _ =
        let pattern = StringUtil.ofCharList rest
        let pattern = 
            if StringUtil.isNullOrEmpty pattern then _vimData.LastPatternData.Pattern
            else pattern

        // The search should begin after the last line in the specified range
        let startPoint = 
            let range = RangeUtil.RangeOrCurrentLine _textView range
            range.EndLine.End

        let patternData = { Pattern = pattern; Path = path }
        let result = _searchService.FindNextPattern patternData startPoint _buffer.WordNavigator 1
        _operations.RaiseSearchResultMessage(result)

        match result with
        | SearchResult.Found (_, span, _) ->
            // Move it to the start of the line containing the match 
            let point = 
                span.Start 
                |> SnapshotPointUtil.GetContainingLine 
                |> SnapshotLineUtil.GetStart
            TextViewUtil.MoveCaretToPoint _textView point
            _operations.EnsureCaretOnScreenAndTextExpanded()
        | SearchResult.NotFound _ ->
            ()

    /// Parse the < command
    member x.ProcessShiftLeft (rest:char list) (range: SnapshotLineRange option) _ =
        let count,rest =  rest  |> RangeUtil.ParseNumber
        let range = 
            range
            |> RangeUtil.RangeOrCurrentLine _buffer.TextView 
            |> RangeUtil.TryApplyCount count
        _operations.ShiftLineRangeLeft range 1

    member x.ProcessShiftRight (rest:char list) (range: SnapshotLineRange option) _ =
        let count,rest = rest |> RangeUtil.ParseNumber
        let range = 
            range
            |> RangeUtil.RangeOrCurrentLine _buffer.TextView 
            |> RangeUtil.TryApplyCount count
        _operations.ShiftLineRangeRight range 1

    member x.ProcessWrite (rest:char list) _ _ = 
        let name = rest |> StringUtil.ofCharSeq 
        let name = name.Trim()
        if StringUtil.isNullOrEmpty name then 
            _host.Save _textBuffer |> ignore
        else 
            let text = _textBuffer.CurrentSnapshot.GetText()
            _host.SaveTextAs text name |> ignore

    /// Save all of the open IVimBuffer instances
    member x.ProcessWriteAll _ _ _ = 
        for buffer in _vim.Buffers do
            _host.Save buffer.TextBuffer |> ignore

    member x.ProcessWriteQuit (rest:char list) range hasBang = 
        let host = _buffer.Vim.VimHost
        let filePath = 
            let name = rest |> Seq.skipWhile CharUtil.IsWhiteSpace |> StringUtil.ofCharSeq
            if StringUtil.isNullOrEmpty name then None else Some name

        match range, filePath, hasBang with 
        | None, None, _ -> host.Save _textView.TextBuffer |> ignore  
        | None, Some(filePath), _ -> host.SaveAs _textView filePath |> ignore
        | Some(range), None, _ -> _statusUtil.OnError Resources.CommandMode_NoFileName
        | Some(range), Some(filePath), _ -> host.SaveTextAs (range.GetTextIncludingLineBreak()) filePath |> ignore

        host.Close _textView false

    member x.ProcessQuit _ _ hasBang = 
        _buffer.Vim.VimHost.Close _buffer.TextView (not hasBang)

    /// Process the ':quitall' family of commands.
    member x.ProcessQuitAll _ _ hasBang =

        // If the ! flag is not passed then we raise an error if any of the ITextBuffer instances 
        // are dirty
        if not hasBang then
            let anyDirty = _vim.Buffers |> Seq.exists (fun buffer -> _host.IsDirty buffer.TextBuffer)
            if anyDirty then 
                _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
            else
                _host.Quit()
        else
            _host.Quit()

    member x.ProcessTabNext rest _ _ =
        let count, _ = RangeUtil.ParseNumber rest
        match count with
        | None -> _operations.GoToNextTab Path.Forward 1
        | Some(index) -> _operations.GoToTab index

    member x.ProcessTabPrevious rest _ _ =
        let count, _ = RangeUtil.ParseNumber rest
        match count with
        | None -> _operations.GoToNextTab Path.Backward 1
        | Some(count) -> _operations.GoToNextTab Path.Backward count

    member x.ProcessTabFirst _ _ _ =
        _operations.GoToTab 0

    member x.ProcessTabLast _ _ _ =
        _operations.GoToTab -1 

    member x.ProcessMake _ _ bang =
        _buffer.Vim.VimHost.BuildSolution()

    /// Implements the :delete command
    member x.ProcessDelete (rest:char list) (range:SnapshotLineRange option) _ =
        let reg,rest = rest |> CommandParseUtil.SkipRegister _buffer.RegisterMap
        let count,rest = rest |> CommandParseUtil.SkipWhitespace |> RangeUtil.ParseNumber
        let range = 
            range
            |> RangeUtil.RangeOrCurrentLine _buffer.TextView 
            |> RangeUtil.TryApplyCount count

        let span = range.ExtentIncludingLineBreak
        _textBuffer.Delete(span.Span) |> ignore

        let value = RegisterValue.String (StringData.OfSpan span, OperationKind.LineWise)
        _registerMap.SetRegisterValue reg RegisterOperation.Delete value

    member x.ProcessUndo rest _ _ =
        match Seq.isEmpty rest with
        | true -> _operations.Undo 1
        | false -> _statusUtil.OnError x.BadMessage

    member x.ProcessRedo rest _ _ =
        match Seq.isEmpty rest with
        | true -> _operations.Redo 1
        | false -> _statusUtil.OnError x.BadMessage

    /// Process the :registers command.  This will display the contents of the specified registers
    /// or if none are specified the contents of non-empty registers
    member x.ProcessRegisters rest _ _ = 
        let rest = rest |> CommandParseUtil.SkipWhitespace

        let names = 
            match rest with
            | [] -> 
                // If no names are used then we display all named and numbered registers 
                RegisterName.All
                |> Seq.filter (fun name ->
                    match name with
                    | RegisterName.Numbered _ -> true
                    | RegisterName.Named named -> not named.IsAppend
                    | _ -> false)
            | _ ->
                // Convert the remaining items to registers.  Should work with any valid 
                // name not just named and numbers
                rest 
                |> Seq.map RegisterName.OfChar
                |> SeqUtil.filterToSome

        // Build up the status string messages
        let lines = 
            names 
            |> Seq.map (fun name -> 
                let register = _registerMap.GetRegister name
                match register.Name.Char, StringUtil.isNullOrEmpty register.StringValue with
                | None, _ -> None
                | Some c, true -> None
                | Some c, false -> Some (c, register.StringValue))
            |> SeqUtil.filterToSome
            |> Seq.map (fun (name, value) -> sprintf "\"%c   %s" name value)
        let lines = Seq.append (Seq.singleton Resources.CommandMode_RegisterBanner) lines
        _statusUtil.OnStatusLong lines

    /// Process the :retab command.  Changes all sequences of spaces and tabs which contain
    /// at least a single tab into the normalized value based on the provided 'tabstop' or 
    /// default 'tabstop' setting
    member x.ProcessRetab rest range hasBang = 

        // The default range for most commands is the current line.  This command instead 
        // defaults to the entire snapshot
        let range = 
            match range with
            | None -> SnapshotLineRangeUtil.CreateForSnapshot _textBuffer.CurrentSnapshot
            | Some range -> range

        // If the user explicitly specified a 'tabstop' it becomes the new value.  Do this before
        // we re-tab the line so the new value will be used
        let number, rest = rest |> CommandParseUtil.SkipWhitespace |> RangeUtil.ParseNumber
        match number with
        | None -> ()
        | Some number -> _localSettings.TabStop <- number

        _commandOperations.RetabLineRange range hasBang 

    member x.ProcessMarks rest _ _ =
        match Seq.isEmpty rest with
        | true -> _commandOperations.PrintMarks _buffer.MarkMap
        | false -> _statusUtil.OnError x.BadMessage

    /// Parse out the :set command
    member x.ProcessSet (rest:char list) _ _=
        let rest,data = rest |> CommandParseUtil.SkipNonWhitespace
        if System.String.IsNullOrEmpty(data) then _commandOperations.PrintModifiedSettings()
        else
            match data with
            | Match1("^all$") _ -> _commandOperations.PrintAllSettings()
            | Match2("^(\w+)\?$") (_,name) -> _commandOperations.PrintSetting name
            | Match2("^no(\w+)$") (_,name) -> _commandOperations.ResetSetting name
            | Match2("^(\w+)\!$") (_,name) -> _commandOperations.InvertSetting name
            | Match2("^inv(\w+)$") (_,name) -> _commandOperations.InvertSetting name
            | Match3("^(\w+):(\w+)$") (_,name,value) -> _commandOperations.SetSettingValue name value
            | Match3("^(\w+)=(\w+)$") (_,name,value) -> _commandOperations.SetSettingValue name value
            | Match2("^(\w+)$") (_,name) -> _commandOperations.OperateSetting(name)
            | _ -> ()

    /// Used to parse out the :source command.  List is pointing past the o in :source
    member x.ProcessSource (rest:char list) _ bang =
        let file = rest |> StringUtil.ofCharSeq
        if bang then _statusUtil.OnError Resources.CommandMode_NotSupported_SourceNormal
        else
            match _fileSystem.ReadAllLines file with
            | None -> _statusUtil.OnError (Resources.CommandMode_CouldNotOpenFile file)
            | Some(lines) ->
                lines 
                |> Seq.map (fun command -> command |> List.ofSeq)
                |> Seq.iter (fun input -> x.RunCommand input |> ignore)

    /// Split the given view into 2
    member x.ProcessSplit _ _ _ =
        match _buffer.Vim.VimHost.SplitViewHorizontally _buffer.TextView with
        | HostResult.Success -> ()
        | HostResult.Error msg -> _statusUtil.OnError msg

    /// Process the :substitute
    member x.ProcessSubstitute rest range _ = 
        x.ProcessSubstituteCommon rest range SubstituteFlags.None

    /// Process the :& command.  It has the form :[range]&[&][flags] [count].  There
    /// can be spaces between the name and the flags.  It's not implied by the documentation
    /// but is allowed in practice
    member x.ProcessSubstituteRepeat additionalFlags rest range _ =

        // Flags can occur after whitespace
        let flags, rest = 
            rest 
            |> CommandParseUtil.SkipWhitespace 
            |> CommandParseUtil.ParseSubstituteFlags x.PreviousSubstituteFlags
        let flags = additionalFlags ||| flags

        let count, rest = rest |> CommandParseUtil.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLineWithCount _textView range count
        let rest = rest |> CommandParseUtil.SkipWhitespace

        match _vimData.LastSubstituteData, Seq.isEmpty rest with
        | None, _ -> 
            _statusUtil.OnError Resources.CommandMode_NoPreviousSubstitute 
            RunResult.Completed
        | Some _, false ->  
            _statusUtil.OnError Resources.CommandMode_TrailingCharacters
            RunResult.Completed
        | Some data, true-> 
            x.ProcessSubstituteData data.SearchPattern data.Substitute flags range

    /// Process the :smagic command
    member x.ProcessSubstituteMagic rest range _ = 
        x.ProcessSubstituteCommon rest range SubstituteFlags.Magic

    /// Process the :snomagic command
    member x.ProcessSubstituteNomagic rest range _ = 
        x.ProcessSubstituteCommon rest range SubstituteFlags.Nomagic

    /// Process the :~ command
    member x.ProcessSubstituteWithLastPattern rest range _ = 

        // Flags can occur after whitespace
        let flags, rest = 
            rest 
            |> CommandParseUtil.SkipWhitespace 
            |> CommandParseUtil.ParseSubstituteFlags x.PreviousSubstituteFlags

        let count, rest = rest |> CommandParseUtil.SkipWhitespace |> RangeUtil.ParseNumber
        let range = RangeUtil.RangeOrCurrentLineWithCount _textView range count
        let rest = rest |> CommandParseUtil.SkipWhitespace

        let pattern = _vimData.LastPatternData.Pattern
        match _vimData.LastSubstituteData, Seq.isEmpty rest with
        | None, _ -> 
            _statusUtil.OnError Resources.CommandMode_NoPreviousSubstitute 
            RunResult.Completed
        | Some _, false ->  
            _statusUtil.OnError Resources.CommandMode_TrailingCharacters
            RunResult.Completed
        | Some data, true-> 
            x.ProcessSubstituteData pattern data.Substitute flags range

    /// Handles the processing of the common parts of the substitute command
    member x.ProcessSubstituteCommon (rest : char list) (range : SnapshotLineRange option) additionalFlags =

        let originalRange = RangeUtil.RangeOrCurrentLine _buffer.TextView range 

        // Determine if we are doing a full parse.  If the :s command is followed by 
        // a valid delimiter then we are doing a full parse.  See ':help E146' for 
        // description of valid delimiters
        let delimiter = 
            match rest |> Seq.skipWhile CharUtil.IsWhiteSpace |> SeqUtil.tryHeadOnly with
            | Some '\\' -> None
            | Some '|' -> None
            | Some '"' -> None
            | Some c -> if CharUtil.IsLetterOrDigit c then None else Some c 
            | _ -> None

        match delimiter with
        | Some delimiter ->

            // This is the full form of the substitute command.  That is the form having
            // :s/search/replace/flags where '/' is the value 'delimiter'.  We will refer
            // to the delimiter as '/' in comments for brevity.
            let parseFull rest badParse goodParse =

                // Parse one element out of the sequence.  We expect the rest to 
                // be pointed at a string prefixed with '/'.  "found" will be called
                // with both the text after the '/' and the remainder of the string
                let parseOne rest notFound found =

                    // Iterate recursively down the characters looking for the '/' which
                    // ends this value.  Have to check for escaped delimiter values (those
                    // which are prefixed with '\\'
                    let rec getValue value rest = 

                        match rest with
                        | [] ->
                            // Nothing left 
                            value |> List.rev |> StringUtil.ofCharList, []
                        | head :: tail ->
                            if head = delimiter then
                                // Head is the delimiter and there is no back slash ahead of it
                                // so we are done
                                value |> List.rev |> StringUtil.ofCharList, rest
                            elif head = '\\' then
                                if List.length tail > 0 && delimiter = List.head tail then
                                    // Back slash which precedes a delimiter character.  We should simply
                                    // append the delimiter here and not the back slash
                                    getValue (delimiter :: value) (List.tail tail)
                                elif List.length tail > 0 then
                                    // Append both the back slash and the following character.  
                                    let escaped = List.head tail
                                    getValue (escaped :: '\\' :: value) (List.tail tail)
                                else
                                    getValue ('\\' :: value) []
                            else
                                getValue (head :: value) tail

                    match rest with
                    | [] -> 
                        // Must begin with a '/'
                        notFound()
                    | head :: tail -> 
                        if head <> delimiter then
                            notFound()
                        else 
                            // Actually parse out the value
                            let value, rest = getValue [] tail
                            found value rest

                let defaultMsg = Resources.CommandMode_InvalidCommand
                parseOne rest (fun () -> badParse defaultMsg) (fun search rest -> 
                    parseOne rest (fun () -> badParse defaultMsg) (fun replace rest ->  
                        let rest = rest |> ListUtil.skipWhile (fun c -> c = delimiter)
                        let flags, rest = CommandParseUtil.ParseSubstituteFlags x.PreviousSubstituteFlags rest
                        let flags = flags ||| additionalFlags

                        // Parse out the count for the substitute.  Will apply the count if present to the
                        // range.  It's legal to have spaces before the count
                        let count, rest = rest |> CommandParseUtil.SkipWhitespace |> RangeUtil.ParseNumber
                        let range = RangeUtil.RangeOrCurrentLineWithCount _textView range count

                        if not (Seq.isEmpty rest) then
                            badParse Resources.CommandMode_TrailingCharacters
                        else
                            goodParse search replace flags range))

            let badParse msg = 
                _statusUtil.OnError msg
                RunResult.Completed

            let goodParse pattern replace flags range = 
                x.ProcessSubstituteData pattern replace flags range

            parseFull rest badParse goodParse    

        | None ->
            // This is the abbreviated form of substitute having the form 
            // :subsitute [flags] [count].  Use the repeat method
            x.ProcessSubstituteRepeat additionalFlags rest range false

    /// Handles the actual final substitute data produced by the various commands
    member x.ProcessSubstituteData pattern replace flags range : RunResult = 

        // Called to initialize the data and move to a confirm style substitution.  Have to find the first match
        // before passing off to confirm
        let setupConfirmSubstitute (range:SnapshotLineRange) (data:SubstituteData) =
            let regex = _regexFactory.CreateForSubstituteFlags data.SearchPattern data.Flags
            match regex with
            | None -> 
                _statusUtil.OnError (Resources.Common_PatternNotFound data.SearchPattern)
                RunResult.Completed
            | Some(regex) -> 

                let firstMatch = 
                    range.Lines
                    |> Seq.map (fun line -> line.ExtentIncludingLineBreak)
                    |> Seq.tryPick (fun span -> RegexUtil.MatchSpan span regex.Regex)
                match firstMatch with
                | None -> 
                    _statusUtil.OnError (Resources.Common_PatternNotFound data.SearchPattern)
                    RunResult.Completed
                | Some(span,_) ->
                    RunResult.SubstituteConfirm (span, range, data)

        // Check for the UsePrevious flag and update the flags as appropriate.  Make sure
        // to bitwise or them against the new flags
        let flags = 
            if Util.IsFlagSet flags SubstituteFlags.UsePreviousFlags then 
                match _buffer.VimData.LastSubstituteData with
                | None -> SubstituteFlags.None
                | Some(data) -> (Util.UnsetFlag flags SubstituteFlags.UsePreviousFlags) ||| data.Flags
            else 
                flags

        // Get the actual pattern to use
        let pattern, flags = 
            if Util.IsFlagSet flags SubstituteFlags.UsePreviousSearchPattern then
                _vimData.LastPatternData.Pattern, (Util.UnsetFlag flags SubstituteFlags.UsePreviousSearchPattern)
            elif StringUtil.isNullOrEmpty pattern then
                _vimData.LastPatternData.Pattern, flags
            else
                pattern, flags

        if StringUtil.isNullOrEmpty pattern then 
            _statusUtil.OnError Resources.CommandMode_InvalidCommand
            RunResult.Completed
        elif Util.IsFlagSet flags SubstituteFlags.Confirm then
            let data = { SearchPattern = pattern; Substitute = replace; Flags = flags}
            setupConfirmSubstitute range data
        else
            _operations.Substitute pattern replace range flags 
            RunResult.Completed

    member x.ProcessKeyMapClear modes hasBang =
        let modes = 
            if hasBang then [KeyRemapMode.Insert; KeyRemapMode.Command]
            else modes
        _commandOperations.ClearKeyMapModes modes

    member x.ProcessKeyUnmap (name:string) (modes: KeyRemapMode list) (hasBang:bool) (rest: char list) = 
        let modes = 
            if hasBang then [KeyRemapMode.Insert; KeyRemapMode.Command]
            else modes
        let rest,lhs = rest |> CommandParseUtil.SkipNonWhitespace
        _commandOperations.UnmapKeys lhs modes
        
    member x.ProcessKeyMap (name:string) (allowRemap:bool) (modes: KeyRemapMode list) (hasBang:bool) (rest: char list) = 
        let modes = 
            if hasBang then [KeyRemapMode.Insert; KeyRemapMode.Command]
            else modes

        // If there are no arguments then this is a print vs. remap call
        if rest |> Seq.skipWhile CharUtil.IsWhiteSpace |> Seq.isEmpty then 
            _commandOperations.PrintKeyMap modes
        else
            let withKeys lhs rhs _ = _commandOperations.RemapKeys lhs rhs modes allowRemap 
            CommandParseUtil.ParseKeys rest withKeys (fun() -> _statusUtil.OnError x.BadMessage)

    member x.RunCommandWithRange (rest : char list) (range : SnapshotLineRange option) =

        // Get the name of the command.  Need to special case a few items as they
        // don't quite play by normal rules
        let commandName = 
            match SeqUtil.tryHeadOnly rest with
            | None -> 
                StringUtil.empty
            | Some c ->
                if CharUtil.IsLetter c then
                    rest |> Seq.takeWhile CharUtil.IsLetter |> StringUtil.ofCharSeq
                else
                    StringUtil.ofChar c

        // Look for commands with that name
        let command =

            // First look for the exact match
            let found =
                x.GetOrCreateCommandList()
                |> Seq.tryFind (fun (name,shortName,_) -> name = commandName || shortName = commandName)
            match found,StringUtil.isNullOrEmpty commandName with
            | _,true -> None
            | Some(data),false -> Some(data)
            | None,false ->
                
                // No exact name matches look for a prefix match on the full command name
                let found =
                    x.GetOrCreateCommandList()
                    |> Seq.filter (fun (name,_,_) -> name.StartsWith(commandName, System.StringComparison.Ordinal))
                match found |> Seq.length with
                | 1 -> found |> Seq.head |> Some
                | _ -> None


        match command with
        | None -> 
            _statusUtil.OnError x.BadMessage
            RunResult.Completed
        | Some(name,shortName,action) ->
            let rest = rest |> ListUtil.skip commandName.Length 
            let hasBang,rest = rest |> CommandParseUtil.SkipBang
            let rest = rest |> CommandParseUtil.SkipWhitespace
            action rest range hasBang

    /// Parse out the range of the string and run the corresponding command
    member x.ParseAndRunInput (command : char list) =

        let moveToLine line = 
            let point = SnapshotLineUtil.GetFirstNonBlankOrStart line
            _operations.MoveCaretToPointAndEnsureVisible point

        if Seq.forall CharUtil.IsDigit command then
            // Dandle the ':[line number]' command.  It's a bit special in that it's really a command 
            // with no name and it allows the user to specify an invalid line number.
            let number, rest = RangeUtil.ParseNumber command
            match number, List.isEmpty rest with
            | Some number, true -> 
                // We have a valid number and no other input so move the caret to that line number
                // and ensure that it's visible on the screen.  This input is given in terms of 
                // Vim line numbers so convert appropriately
                let number = Util.VimLineToTssLine number
                let line = SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
                moveToLine line
            | _ ->
                // Anything else is an error
                _statusUtil.OnError (Resources.CommandMode_CannotRun (StringUtil.ofCharList command))
            RunResult.Completed
        elif command.Length = 1 && List.head command = '$' then
            let line = SnapshotUtil.GetLastLine x.CurrentSnapshot
            moveToLine line
            RunResult.Completed
        else
            match RangeUtil.ParseRange x.CaretLine _buffer.MarkMap command with
            | ParseRangeResult.Succeeded (range, inputs) -> 
                x.RunCommandWithRange inputs (Some range)
            | ParseRangeResult.NoRange -> 
                x.RunCommandWithRange command None
            | ParseRangeResult.Failed msg -> 
                _statusUtil.OnError msg
                RunResult.Completed

    /// Run the specified command.  This function can be called recursively
    member x.RunCommand (input: char list)=
        let prev = _command
        try
            // Strip off the preceding :
            let input = 
                match ListUtil.tryHead input with
                | None -> input
                | Some(head,tail) when head = ':' -> tail
                | _ -> input

            _command <- input |> StringUtil.ofCharSeq
            x.ParseAndRunInput input
        finally
            _command <- prev

    interface ICommandProcessor with
        member x.RunCommand input = x.RunCommand input

            


