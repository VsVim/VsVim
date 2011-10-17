#light
namespace Vim.Interpreter
open Vim
open Microsoft.VisualStudio.Text
open Vim.VimHostExtensions

[<Sealed>]
[<Class>]
type Interpreter
    (
        _vimBuffer : IVimBuffer,
        _commonOperations : ICommonOperations,
        _foldManager : IFoldManager,
        _fileSystem : IFileSystem
    ) =

    let _vimBufferData = _vimBuffer.VimBufferData
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _vim = _vimBufferData.Vim
    let _vimHost = _vim.VimHost
    let _vimData = _vim.VimData
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView
    let _markMap = _vim.MarkMap
    let _keyMap = _vim.KeyMap
    let _statusUtil = _vimBufferData.StatusUtil
    let _regexFactory = VimRegexFactory(_vimBufferData.LocalSettings.GlobalSettings)
    let _registerMap = _vimBufferData.Vim.RegisterMap
    let _undoRedoOperations = _vimBufferData.UndoRedoOperations
    let _localSettings = _vimBufferData.LocalSettings
    let _searchService = _vim.SearchService

    /// The column of the caret
    member x.CaretColumn = SnapshotPointUtil.GetColumn x.CaretPoint

    /// The SnapshotPoint for the caret
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// The ITextSnapshotLine for the caret
    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The line number for the caret
    member x.CaretLineNumber = x.CaretLine.LineNumber

    /// The SnapshotLineRange for the caret line
    member x.CaretLineRange = x.CaretLine |> SnapshotLineRangeUtil.CreateForLine

    /// The SnapshotPoint and ITextSnapshotLine for the caret
    member x.CaretPointAndLine = TextViewUtil.GetCaretPointAndLine _textView

    /// The current directory for the given IVimBuffer
    member x.CurrentDirectory = 
        match _vimBuffer.CurrentDirectory with
        | None -> _vimData.CurrentDirectory
        | Some currentDirectory -> currentDirectory

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    /// Execute the external command and return the lines of output
    member x.ExecuteCommand (command : string) : string[] option = 
        // TODO: Implement
        None

    /// Get the ITextSnapshotLine specified by the given LineSpecifier
    member x.GetLineCore lineSpecifier currentLine = 

        // Get the ITextSnapshotLine specified by lineSpecifier and then apply the
        // given adjustment to the number.  Can fail if the line number adjustment
        // is invalid
        let getAdjustment adjustment (line : ITextSnapshotLine) = 
            let number = 
                let start = line.LineNumber + 1
                Util.VimLineToTssLine (start + adjustment)

            SnapshotUtil.TryGetLine x.CurrentSnapshot number

        match lineSpecifier with 
        | LineSpecifier.CurrentLine -> 
            x.CaretLine |> Some
        | LineSpecifier.LastLine ->
            SnapshotUtil.GetLastLine x.CurrentSnapshot |> Some
        | LineSpecifier.LineSpecifierWithAdjustment (lineSpecifier, adjustment) ->

            x.GetLine lineSpecifier |> OptionUtil.map2 (getAdjustment adjustment)
        | LineSpecifier.MarkLine mark ->

            // Get the line containing the mark in the context of this IVimTextBuffer
            _markMap.GetMark mark _vimBufferData
            |> Option.map VirtualSnapshotPointUtil.GetPoint
            |> Option.map SnapshotPointUtil.GetContainingLine
        | LineSpecifier.NextLineWithPattern pattern ->
            // TODO: Implement
            None
        | LineSpecifier.NextLineWithPreviousPattern ->
            // TODO: Implement
            None
        | LineSpecifier.NextLineWithPreviousSubstitutePattern ->
            // TODO: Implement
            None
        | LineSpecifier.Number number ->
            // Must be a valid number 
            let number = Util.VimLineToTssLine number
            SnapshotUtil.TryGetLine x.CurrentSnapshot number
        | LineSpecifier.PreviousLineWithPattern pattern ->
            // TODO: Implement
            None
        | LineSpecifier.PreviousLineWithPreviousPattern ->
            // TODO: Implement
            None

        | LineSpecifier.AdjustmentOnCurrent adjustment -> 
            getAdjustment adjustment currentLine

    /// Get the ITextSnapshotLine specified by the given LineSpecifier
    member x.GetLine lineSpecifier = 
        x.GetLineCore lineSpecifier x.CaretLine

    /// Get the specified LineRange in the IVimBuffer
    member x.GetLineRange lineRange =
        match lineRange with
        | LineRange.EntireBuffer -> 
            SnapshotLineRangeUtil.CreateForSnapshot x.CurrentSnapshot |> Some
        | LineRange.SingleLine lineSpecifier-> 
            x.GetLine lineSpecifier |> Option.map SnapshotLineRangeUtil.CreateForLine
        | LineRange.Range (leftLineSpecifier, rightLineSpecifier, adjustCaret) ->
            match x.GetLine leftLineSpecifier with
            | None ->
                None
            | Some leftLine ->
                // If the adjustCaret option was specified then we need to move the caret before
                // interpreting the next LineSpecifier.  The caret should remain moved after this 
                // completes
                if adjustCaret then
                    TextViewUtil.MoveCaretToPoint _textView leftLine.Start

                // Get the right line and combine the results
                match x.GetLineCore rightLineSpecifier leftLine with
                | None -> None
                | Some rightLine -> SnapshotLineRangeUtil.CreateForLineRange leftLine rightLine |> Some

    /// Try and get the line range if one is specified.  If one is not specified then just 
    /// use the current line
    member x.GetLineRangeOrCurrent lineRange = 
        match lineRange with
        | None -> SnapshotLineRangeUtil.CreateForLine x.CaretLine |> Some
        | Some lineRange -> x.GetLineRange lineRange

    /// Get the count value or the default of 1
    member x.GetCountOrDefault count = 
        match count with
        | Some count -> count
        | None -> 1

    /// Change the directory to the given value
    member x.RunChangeDirectory directoryPath = 
        match directoryPath with
        | None -> 
            // On non-Unix systems the :cd commandshould print out the directory when
            // cd is given no options
            _statusUtil.OnStatus x.CurrentDirectory
        | Some directoryPath ->
            if not (System.IO.Path.IsPathRooted directoryPath) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "Relative paths")
            elif not (System.IO.Directory.Exists directoryPath) then
                // Not a fan of this function but we need to emulate the Vim behavior here
                _statusUtil.OnError (Resources.Interpreter_CantFindDirectory directoryPath)
            else
                // Setting the global directory will clear out the local directory for the window
                _vimBuffer.CurrentDirectory <- None
                _vimData.CurrentDirectory <- directoryPath
        RunResult.Completed

    /// Change the local directory to the given value
    member x.RunChangeLocalDirectory directoryPath = 
        match directoryPath with
        | None -> 
            // On non-Unix systems the :cd commandshould print out the directory when
            // cd is given no options
            _statusUtil.OnStatus x.CurrentDirectory
        | Some directoryPath ->
            if not (System.IO.Path.IsPathRooted directoryPath) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "Relative paths")
            elif not (System.IO.Directory.Exists directoryPath) then
                // Not a fan of this function but we need to emulate the Vim behavior here
                _statusUtil.OnError (Resources.Interpreter_CantFindDirectory directoryPath)
            else
                // Setting the global directory will clear out the local directory for the window
                _vimBuffer.CurrentDirectory <- Some directoryPath
        RunResult.Completed

    /// Clear out the key map for the given modes
    member x.RunClearKeyMap keyRemapModes mapArgumentList = 
        if not (List.isEmpty mapArgumentList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "map special arguments")
        else
            keyRemapModes
            |> Seq.iter _keyMap.Clear
        RunResult.Completed

    /// Run the close command
    member x.RunClose hasBang = 
        if not hasBang && _vimHost.IsDirty _textView.TextBuffer then
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            _vimHost.Close _textView
        RunResult.Completed

    /// Edit the specified file
    member x.RunEdit hasBang fileOptions commandOption filePath =
        if not (List.isEmpty fileOptions) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
        elif Option.isSome commandOption then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++cmd]")
        elif System.String.IsNullOrEmpty filePath then 
            if not hasBang && _vimHost.IsDirty _textBuffer then
                _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
            else
                let caret = 
                    let point = TextViewUtil.GetCaretPoint _textView
                    point.Snapshot.CreateTrackingPoint(point.Position, PointTrackingMode.Negative)
                if not (_vimHost.Reload _textBuffer) then
                    _commonOperations.Beep()
                else
                    match TrackingPointUtil.GetPoint _textView.TextSnapshot caret with
                    | None -> ()
                    | Some(point) -> 
                        TextViewUtil.MoveCaretToPoint _textView point
                        TextViewUtil.EnsureCaretOnScreen _textView

        elif not hasBang && _vimHost.IsDirty _textBuffer then
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            match _vimHost.LoadFileIntoExistingWindow filePath _textBuffer with
            | HostResult.Success -> ()
            | HostResult.Error(msg) -> _statusUtil.OnError(msg)

        RunResult.Completed

    /// Run the delete command.  Delete the specified range of text and set it to 
    /// the given Register
    member x.RunDelete (lineRange : SnapshotLineRange) register = 

        let span = lineRange.ExtentIncludingLineBreak
        _textBuffer.Delete(span.Span) |> ignore

        let value = RegisterValue.String (StringData.OfSpan span, OperationKind.LineWise)
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        RunResult.Completed

    /// Display the given map modes
    member x.RunDisplayKeyMap keyRemapModes keyNotationOption = 
        // Get the printable info for the set of modes
        let getModeLine modes =
            if ListUtil.contains KeyRemapMode.Normal modes 
                && ListUtil.contains KeyRemapMode.OperatorPending modes
                && ListUtil.contains KeyRemapMode.Visual modes then
                " "
            elif ListUtil.contains KeyRemapMode.Command modes 
                && ListUtil.contains KeyRemapMode.Insert modes then
                "!"
            elif ListUtil.contains KeyRemapMode.Visual modes 
                && ListUtil.contains KeyRemapMode.Select modes then
                "v"
            elif List.length modes <> 1 then 
                "?"
            else 
                match List.head modes with
                | KeyRemapMode.Normal -> "n"
                | KeyRemapMode.Visual -> "x"
                | KeyRemapMode.Select -> "s"
                | KeyRemapMode.OperatorPending -> "o"
                | KeyRemapMode.Command -> "c"
                | KeyRemapMode.Language -> "l"
                | KeyRemapMode.Insert -> "i"

        // Get the printable format for the KeyInputSet 
        let getKeyInputSetLine (keyInputSet:KeyInputSet) = 

            let inner (ki:KeyInput) = 

                let ki = ki |> KeyInputUtil.GetAlternateTarget |> OptionUtil.getOrDefault ki

                // Build up the prefix for the specified modifiers
                let rec getPrefix modifiers = 
                    if Util.IsFlagSet modifiers KeyModifiers.Alt then
                        "M-" + getPrefix (Util.UnsetFlag modifiers KeyModifiers.Alt)
                    elif Util.IsFlagSet modifiers KeyModifiers.Control then
                        "C-" + getPrefix (Util.UnsetFlag modifiers KeyModifiers.Control)
                    elif Util.IsFlagSet modifiers KeyModifiers.Shift then
                        "S-" + getPrefix (Util.UnsetFlag modifiers KeyModifiers.Shift)
                    else 
                        ""

                // Get the actual printable output for the raw KeyInput.  For a KeyInput with
                // a char this is straight forward.  Non-char KeyInput need to be special cased
                // though
                let prefix,output = 
                    match (KeyNotationUtil.TryGetSpecialKeyName ki),ki.Char with
                    | Some(name,extraModifiers), _ -> 
                        (getPrefix extraModifiers, name)
                    | None, c -> 
                        let c = 
                            if CharUtil.IsLetter c && ki.KeyModifiers <> KeyModifiers.None then CharUtil.ToUpper c 
                            else c
                        (getPrefix ki.KeyModifiers, StringUtil.ofChar c)

                if String.length prefix = 0 then 
                    if String.length output = 1 then output
                    else sprintf "<%s>" output
                else
                    sprintf "<%s%s>" prefix output 

            keyInputSet.KeyInputs |> Seq.map inner |> String.concat ""

        // Get the printable line for the provided mode, left and right side
        let getLine modes lhs rhs = 
            sprintf "%-5s%s %s" (getModeLine modes) (getKeyInputSetLine lhs) (getKeyInputSetLine rhs)

        let lines = 
            keyRemapModes
            |> Seq.map (fun mode -> 
                mode
                |> _keyMap.GetKeyMappingsForMode 
                |> Seq.map (fun (lhs,rhs) -> (mode,lhs,rhs)))
            |> Seq.concat
            |> Seq.groupBy (fun (mode,lhs,rhs) -> lhs)
            |> Seq.map (fun (lhs, all) ->
                let modes = all |> Seq.map (fun (mode, _, _) -> mode) |> List.ofSeq
                let rhs = all |> Seq.map (fun (_, _, rhs) -> rhs) |> Seq.head
                getLine modes lhs rhs)

        _statusUtil.OnStatusLong lines
        RunResult.Completed

    /// Display the registers.  If a particular name is specified only display that register
    member x.RunDisplayRegisters registerName =

        let names = 
            match registerName with
            | None -> 

                // If no names are used then we display all named and numbered registers 
                RegisterName.All
                |> Seq.filter (fun name ->
                    match name with
                    | RegisterName.Numbered _ -> true
                    | RegisterName.Named named -> not named.IsAppend
                    | _ -> false)
            | Some registerName ->
                // Convert the remaining items to registers.  Should work with any valid 
                // name not just named and numbers
                [registerName] |> Seq.ofList

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
        RunResult.Completed

    /// Display the specified marks
    member x.RunDisplayMarks marks = 
        if not (List.isEmpty marks) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "Specific marks")
        else
            let printMark (ident : char) (point : VirtualSnapshotPoint) =
                let textLine = point.Position.GetContainingLine()
                let lineNum = textLine.LineNumber
                let column = point.Position.Position - textLine.Start.Position
                let column = if point.IsInVirtualSpace then column + point.VirtualSpaces else column
                let name = _vimHost.GetName _textView.TextBuffer
                sprintf " %c   %5d%5d%s" ident lineNum column name

            let localSeq = 
                _vimTextBuffer.LocalMarks
                |> Seq.map (fun (localMark, point) -> (localMark.Char, point))
                |> Seq.sortBy fst
            let globalSeq = 
                _markMap.GlobalMarks 
                |> Seq.map (fun (letter, point) -> (CharUtil.ToUpper letter.Char, point))
                |> Seq.sortBy fst
            localSeq 
            |> Seq.append globalSeq
            |> Seq.map (fun (c,p) -> printMark c p )
            |> Seq.append ( "mark line  col file/text"  |> Seq.singleton)
            |> _statusUtil.OnStatusLong
        RunResult.Completed

    /// Fold the specified line range
    member x.RunFold lineRange = 
        match x.GetLineRangeOrCurrent lineRange with
        | None -> ()
        | Some lineRange -> _foldManager.CreateFold lineRange

        RunResult.Completed

    /// Go to the first tab
    member x.RunGoToFirstTab() =
        _commonOperations.GoToTab 0
        RunResult.Completed

    /// Go to the first tab
    member x.RunGoToLastTab() =
        _commonOperations.GoToTab 0
        RunResult.Completed

    /// Go to the next "count" tab 
    member x.RunGoToNextTab count = 
        let count = x.GetCountOrDefault count
        _commonOperations.GoToNextTab Path.Forward count
        RunResult.Completed

    /// Go to the previous "count" tab 
    member x.RunGoToPreviousTab count = 
        let count = x.GetCountOrDefault count
        _commonOperations.GoToNextTab Path.Backward count
        RunResult.Completed

    /// Join the lines in the specified range
    member x.RunJoin joinKind lineRange =
        _commonOperations.Join lineRange joinKind
        RunResult.Completed

    /// Jump to the last line
    member x.RunJumpToLastLine() =
        let lineNumber = SnapshotUtil.GetLastLineNumber x.CurrentSnapshot
        x.RunJumpToLine (lineNumber + 1)

    /// Jump to the specified line number
    member x.RunJumpToLine number = 
        let number = Util.VimLineToTssLine number

        // Make sure we jump to the first non-blank on this line
        let point = 
            SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
            |> SnapshotLineUtil.GetFirstNonBlankOrEnd

        _commonOperations.MoveCaretToPointAndEnsureVisible point
        RunResult.Completed

    /// Run the host make command 
    member x.RunMake hasBang arguments = 
        match _vimHost.Make (not hasBang) arguments with
        | HostResult.Error msg -> _statusUtil.OnError msg
        | HostResult.Success -> ()
        RunResult.Completed

    /// Run the map keys command
    member x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap mapArgumentList =

        if not (List.isEmpty mapArgumentList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "map special arguments")
        else
            // Get the appropriate mapping function based on whether or not remapping is 
            // allowed
            let mapFunc = if allowRemap then _keyMap.MapWithRemap else _keyMap.MapWithNoRemap
    
            // Perform the mapping for each mode and record if there is an error
            let anyErrors = 
                keyRemapModes
                |> Seq.map (fun keyRemapMode -> mapFunc leftKeyNotation rightKeyNotation keyRemapMode)
                |> Seq.exists (fun x -> not x)
    
            if anyErrors then
                _statusUtil.OnError (Resources.Interpreter_UnableToMapKeys leftKeyNotation rightKeyNotation)

        RunResult.Completed

    /// Run the 'nohlsearch' command.  Temporarily disables highlighitng in the buffer
    member x.RunNoHighlightSearch() = 
        _vimData.RaiseHighlightSearchOneTimeDisable()
        RunResult.Completed

    /// Print out the current directory
    member x.RunPrintCurrentDirectory() =
        _statusUtil.OnStatus x.CurrentDirectory
        RunResult.Completed

    /// Put the register after the last line in the given range
    member x.RunPut (lineRange : SnapshotLineRange) (register : Register) putAfter = 

        // Need to get the cursor position correct for undo / redo so start an undo 
        // transaction 
        _undoRedoOperations.EditWithUndoTransaction "PutLine" (fun () ->

            // Get the point to start the Put operation at 
            let line = 
                if putAfter then lineRange.EndLine
                else lineRange.StartLine

            let point = 
                if putAfter then line.EndIncludingLineBreak
                else line.Start

            _commonOperations.Put point register.StringData OperationKind.LineWise

            // Need to put the caret on the first non-blank of the last line of the 
            // inserted text
            let lineCount = x.CurrentSnapshot.LineCount - point.Snapshot.LineCount
            let line = 
                let number = if putAfter then line.LineNumber + 1 else line.LineNumber
                let number = number + (lineCount - 1)
                SnapshotUtil.GetLine x.CurrentSnapshot number
            let point = SnapshotLineUtil.GetFirstNonBlankOrEnd line
            _commonOperations.MoveCaretToPointAndCheckVirtualSpace point)

        RunResult.Completed

    /// Run the quit command
    member x.RunQuit hasBang =
        x.RunClose hasBang

    /// Run the quit all command
    member x.RunQuitAll hasBang =

        // If the ! flag is not passed then we raise an error if any of the ITextBuffer instances 
        // are dirty
        if not hasBang then
            let anyDirty = _vim.VimBuffers |> Seq.exists (fun buffer -> _vimHost.IsDirty buffer.TextBuffer)
            if anyDirty then 
                _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
            else
                _vimHost.Quit()
        else
            _vimHost.Quit()
        RunResult.Completed

    member x.RunQuitWithWrite lineRange hasBang fileOptions filePath = 
        
        if not (List.isEmpty fileOptions) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
        else

            // Do the actual work.  If a valid line range was provided then lineRange will
            // have that value, else it will be None
            let inner (lineRange : SnapshotLineRange option) = 
                match lineRange, filePath, hasBang with 
                | None, None, _ -> _vimHost.Save _textView.TextBuffer |> ignore  
                | None, Some filePath, _ -> _vimHost.SaveAs _textView filePath |> ignore
                | Some _, None, _ -> _statusUtil.OnError Resources.CommandMode_NoFileName
                | Some lineRange, Some filePath, _ -> _vimHost.SaveTextAs (lineRange.GetTextIncludingLineBreak()) filePath |> ignore

            match lineRange with
            | None -> 
                inner None
            | Some lineRange -> 
                match x.GetLineRange lineRange with
                | None ->
                    _statusUtil.OnError Resources.Range_Invalid
                | Some lineRange ->
                    inner (Some lineRange)

            x.RunClose false |> ignore

        RunResult.Completed

    /// Run the core parts of the read command
    member x.RunReadCore (lineRange : SnapshotLineRange) (lines : string[]) = 
        let point = lineRange.EndIncludingLineBreak
        let lineBreak = _commonOperations.GetNewLineText point
        let text = 
            let builder = System.Text.StringBuilder()
            for line in lines do
                builder.Append(line) |> ignore
                builder.Append(lineBreak) |> ignore
            builder.ToString()
        _textBuffer.Insert(point.Position, text) |> ignore

    /// Run the read command command
    member x.RunReadCommand lineRange command = 
        match x.ExecuteCommand command with
        | None ->
            _statusUtil.OnError (Resources.Interpreter_CantRunCommand command)
        | Some lines ->
            x.RunReadCore lineRange lines
        RunResult.Completed

    /// Run the read file command.
    member x.RunReadFile (lineRange : SnapshotLineRange) fileOptionList filePath =
        if not (List.isEmpty fileOptionList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
        else
            match _fileSystem.ReadAllLines filePath with
            | None ->
                _statusUtil.OnError (Resources.Interpreter_CantOpenFile filePath)
            | Some lines ->
                x.RunReadCore lineRange lines
        RunResult.Completed

    /// Run a single redo operation
    member x.RunRedo() = 
        _commonOperations.Redo 1
        RunResult.Completed

    /// Process the :retab command.  Changes all sequences of spaces and tabs which contain
    /// at least a single tab into the normalized value based on the provided 'tabstop' or 
    /// default 'tabstop' setting
    member x.RunRetab lineRange includeSpaces tabStop =

        let func (lineRange : SnapshotLineRange) = 

            // If the user explicitly specified a 'tabstop' it becomes the new value.  Do this before
            // we re-tab the line so the new value will be used
            match tabStop with
            | None -> ()
            | Some tabStop -> _localSettings.TabStop <- tabStop

            let snapshot = lineRange.Snapshot
    
            // First break into a sequence of SnapshotSpan values which contain only space and tab
            // values.  We'll filter out the space only ones later if needed
            let spans = 
    
                // Find the next position which has a space or tab value 
                let rec nextPoint (point : SnapshotPoint) = 
                    if point.Position >= lineRange.End.Position then
                        None
                    elif SnapshotPointUtil.IsBlank point then
                        Some point
                    else
                        point |> SnapshotPointUtil.AddOne |> nextPoint 
    
                Seq.unfold (fun point ->
                    match nextPoint point with
                    | None ->
                        None
                    | Some startPoint -> 
                        // Now find the first point which is not a space or tab. 
                        let endPoint = 
                            SnapshotSpan(startPoint, lineRange.End)
                            |> SnapshotSpanUtil.GetPoints Path.Forward
                            |> Seq.skipWhile SnapshotPointUtil.IsBlank
                            |> SeqUtil.headOrDefault lineRange.End
                        let span = SnapshotSpan(startPoint, endPoint)
                        Some (span, endPoint)) lineRange.Start
                |> Seq.filter (fun span -> 
    
                    // Filter down to the SnapshotSpan values which contain tabs or spaces
                    // depending on the switch
                    if includeSpaces then
                        true
                    else
                        let hasTab = 
                            span 
                            |> SnapshotSpanUtil.GetPoints Path.Forward
                            |> SeqUtil.any (SnapshotPointUtil.IsChar '\t')
                        hasTab)

            // Now that we have the set of spans perform the edit
            use edit = _textBuffer.CreateEdit()
            for span in spans do
                let oldText = span.GetText()
                let newText = _commonOperations.NormalizeBlanks oldText
                edit.Replace(span.Span, newText) |> ignore
    
            edit.Apply() |> ignore

        // The default range for most commands is the current line.  This command instead 
        // defaults to the entire snapshot
        match lineRange with
        | None -> 
            func (SnapshotLineRangeUtil.CreateForSnapshot x.CurrentSnapshot)
        | Some lineRange ->
            match x.GetLineRange lineRange with
            | None ->
                _statusUtil.OnError Resources.Range_Invalid
            | Some lineRange ->
                func lineRange

        RunResult.Completed

    /// Run the search command in the given direction
    member x.RunSearch (lineRange : SnapshotLineRange) path pattern = 
        let pattern = 
            if StringUtil.isNullOrEmpty pattern then _vimData.LastPatternData.Pattern
            else pattern

        // Searches start after the end of the specified line range
        let startPoint = lineRange.End
        let patternData = { Pattern = pattern; Path = path }
        let result = _searchService.FindNextPattern patternData startPoint _vimBufferData.VimTextBuffer.WordNavigator 1
        _commonOperations.RaiseSearchResultMessage(result)

        match result with
        | SearchResult.Found (_, span, _) ->
            // Move it to the start of the line containing the match 
            let point = 
                span.Start 
                |> SnapshotPointUtil.GetContainingLine 
                |> SnapshotLineUtil.GetStart
            TextViewUtil.MoveCaretToPoint _textView point
            _commonOperations.EnsureCaretOnScreenAndTextExpanded()
        | SearchResult.NotFound _ ->
            ()

        RunResult.Completed

    /// Run the :set command.  Process each of the arguments 
    member x.RunSet setArguments =

        // Get the setting for the specified name
        let withSetting name msg func =
            match _localSettings.GetSetting name with
            | None -> _statusUtil.OnError (Resources.Interpreter_UnknownOption msg)
            | Some setting -> func setting

        // Display the specified setting 
        let getSettingDisplay setting = 
    
            match setting.Kind, setting.AggregateValue with
            | SettingKind.ToggleKind, SettingValue.ToggleValue(b) -> 
                if b then setting.Name
                else sprintf "no%s" setting.Name
            | SettingKind.StringKind, SettingValue.StringValue(s) -> 
                sprintf "%s=\"%s\"" setting.Name s
            | SettingKind.NumberKind, SettingValue.NumberValue(n) ->
                sprintf "%s=%d" setting.Name n
            | _ -> "Invalid value"


        let addSetting name value = 
            // TODO: implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "+=")

        let multiplySetting name value =
            // TODO: implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "^=")

        let subtractSetting name value =
            // TODO: implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "-=")

        // Assign the given value to the setting with the specified name
        let assignSetting name value = 
            let msg = sprintf "%s=%s" name value
            withSetting name msg (fun setting ->
                if not (_localSettings.TrySetValueFromString setting.Name value) then
                    _statusUtil.OnError (Resources.Interpreter_InvalidArgument msg))

        // Display all of the setings which don't have the default value
        let displayAllNonDefault() = 

            // TODO: need to filter out terminal 
            _localSettings.AllSettings 
            |> Seq.filter (fun s -> not s.IsValueDefault) 
            |> Seq.map getSettingDisplay 
            |> _statusUtil.OnStatusLong

        // Display all of the setings but terminal
        let displayAllButTerminal() = 
            // TODO: Implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "all")

        // Display the inidividual setting
        let displaySetting name = 
            // TODO: Implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "display single setting")

        // Display the terminal options
        let displayAllTerminal() = 
            // TODO: Implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "term")

        // Use the specifiec setting
        let useSetting name =
            withSetting name name (fun setting ->
                match setting.Kind with
                | SettingKind.ToggleKind -> _localSettings.TrySetValue setting.Name (SettingValue.ToggleValue true) |> ignore
                | SettingKind.NumberKind -> displaySetting name
                | SettingKind.StringKind -> displaySetting name)

        // Invert the setting of the specified name
        let invertSetting name = 
            let msg = "!" + name
            withSetting name msg (fun setting -> 
                match setting.Kind, setting.AggregateValue with
                | ToggleKind,ToggleValue(b) -> _localSettings.TrySetValue setting.Name (ToggleValue(not b)) |> ignore
                | _ -> msg |> Resources.CommandMode_InvalidArgument |> _statusUtil.OnError)

        // Reset all settings to their default settings
        let resetAllToDefault () =
            // TODO: Implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "all&")

        // Reset setting to it's default value
        let resetSetting name = 
            // TODO: Implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "&")

        // Toggle the specified value off
        let toggleOffSetting name = 
            let msg = "no" + name
            withSetting name msg (fun setting -> 
                match setting.Kind with
                | SettingKind.NumberKind -> _statusUtil.OnError (Resources.Interpreter_InvalidArgument msg)
                | SettingKind.StringKind -> _statusUtil.OnError (Resources.Interpreter_InvalidArgument msg)
                | SettingKind.ToggleKind -> _localSettings.TrySetValue setting.Name (SettingValue.ToggleValue false) |> ignore)

        match setArguments with
        | [] -> 
            displayAllNonDefault()
        | _ ->
            // Process each of the SetArgument values in the order in which they
            // are declared
            setArguments
            |> List.iter (fun setArgument ->
                match setArgument with
                | SetArgument.AddSetting (name, value) -> addSetting name value
                | SetArgument.AssignSetting (name, value) -> assignSetting name value
                | SetArgument.DisplayAllButTerminal -> displayAllButTerminal()
                | SetArgument.DisplayAllTerminal -> displayAllTerminal()
                | SetArgument.DisplaySetting name -> displaySetting name
                | SetArgument.InvertSetting name -> invertSetting name
                | SetArgument.MultiplySetting (name, value) -> multiplySetting name value
                | SetArgument.ResetAllToDefault -> resetAllToDefault()
                | SetArgument.ResetSetting name -> resetSetting name
                | SetArgument.SubtractSetting (name, value) -> subtractSetting name value
                | SetArgument.ToggleOffSetting name -> toggleOffSetting name
                | SetArgument.UseSetting name -> useSetting name)

        RunResult.Completed

    /// Shift the given line range to the left
    member x.RunShiftLeft lineRange = 
        _commonOperations.ShiftLineRangeLeft lineRange 1
        RunResult.Completed

    /// Shift the given line range to the right
    member x.RunShiftRight lineRange = 
        _commonOperations.ShiftLineRangeRight lineRange 1
        RunResult.Completed

    /// Run the :source command
    member x.RunSource hasBang filePath =
        if hasBang then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "!")
        else
            match _fileSystem.ReadAllLines filePath with
            | None -> 
                _statusUtil.OnError (Resources.CommandMode_CouldNotOpenFile filePath)
            | Some lines ->
                lines 
                |> Seq.iter (fun line ->
                    match Parser.ParseLineCommand line with
                    | ParseResult.Failed msg -> _statusUtil.OnError msg
                    | ParseResult.Succeeded lineCommand -> x.RunLineCommand lineCommand |> ignore)

        RunResult.Completed

    /// Split the window
    member x.RunSplit lineRange fileOptions commandOption = 
        if not (List.isEmpty fileOptions) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
        elif Option.isSome commandOption then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++cmd]")
        else
            match _vimHost.SplitViewHorizontally _textView with
            | HostResult.Success -> ()
            | HostResult.Error msg -> _statusUtil.OnError msg

        RunResult.Completed

    /// Run the substitute command. 
    member x.RunSubstitute lineRange pattern replace flags =

        // Called to initialize the data and move to a confirm style substitution.  Have to find the first match
        // before passing off to confirm
        let setupConfirmSubstitute (range : SnapshotLineRange) (data : SubstituteData) =
            let regex = _regexFactory.CreateForSubstituteFlags data.SearchPattern data.Flags
            match regex with
            | None -> 
                _statusUtil.OnError (Resources.Common_PatternNotFound data.SearchPattern)
                RunResult.Completed
            | Some regex -> 

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
                match _vimData.LastSubstituteData with
                | None -> SubstituteFlags.None
                | Some data -> (Util.UnsetFlag flags SubstituteFlags.UsePreviousFlags) ||| data.Flags
            else 
                flags

        // Get the actual pattern to use
        let pattern = 
            if pattern = "" then 
                if Util.IsFlagSet flags SubstituteFlags.UsePreviousSearchPattern then
                    _vimData.LastPatternData.Pattern
                else
                    match _vimData.LastSubstituteData with
                    | None -> ""
                    | Some substituteData -> substituteData.SearchPattern
            else
                // If a pattern is given then it is the one that we will use 
                pattern

        if Util.IsFlagSet flags SubstituteFlags.Confirm then
            let data = { SearchPattern = pattern; Substitute = replace; Flags = flags}
            setupConfirmSubstitute lineRange data
        else
            _commonOperations.Substitute pattern replace lineRange flags 
            RunResult.Completed

    /// Run substitute using the pattern and replace values from the last substitute
    member x.RunSubstituteRepeatLast lineRange flags = 
        let pattern, replace = 
            match _vimData.LastSubstituteData with
            | None -> "", ""
            | Some substituteData -> substituteData.SearchPattern, substituteData.Substitute
        x.RunSubstitute lineRange pattern replace flags 

    /// Run the undo command
    member x.RunUndo() =
        _commonOperations.Undo 1
        RunResult.Completed

    /// Unmap the specified key notation in all of the listed modes
    member x.RunUnmapKeys keyNotation keyRemapModes mapArgumentList =
        if not (List.isEmpty mapArgumentList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "map special arguments")
        else
            let allSucceeded =
                keyRemapModes
                |> Seq.map (fun keyRemapMode -> _keyMap.Unmap keyNotation keyRemapMode)
                |> Seq.filter (fun x -> not x)
                |> Seq.isEmpty
    
            if not allSucceeded then 
                _statusUtil.OnError Resources.CommandMode_NoSuchMapping

        RunResult.Completed

    member x.RunWrite lineRange hasBang fileOptionList filePath =
        if not (List.isEmpty fileOptionList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
        elif Option.isSome filePath then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[filePath]")
        else
            if not hasBang && _vimHost.IsReadOnly _textBuffer then
                _statusUtil.OnError Resources.Interpreter_ReadOnlyOptionIsSet
            else
                _vimHost.Save _textBuffer |> ignore
        RunResult.Completed

    /// Run the 'wall' command
    member x.RunWriteAll hasBang = 
        for vimBuffer in _vim.VimBuffers do
            if not hasBang && _vimHost.IsReadOnly vimBuffer.TextBuffer then
                _statusUtil.OnError Resources.Interpreter_ReadOnlyOptionIsSet
            else
                _vimHost.Save vimBuffer.TextBuffer |> ignore
        RunResult.Completed

    /// Yank the specified line range into the register.  This is done in a 
    /// linewise fashion
    member x.RunYank (lineRange : SnapshotLineRange) register =
        let stringData = StringData.OfSpan lineRange.ExtentIncludingLineBreak
        let value = RegisterValue.String (stringData, OperationKind.LineWise)
        _registerMap.SetRegisterValue register RegisterOperation.Yank value

        RunResult.Completed

    /// Run the specified LineCommand
    member x.RunLineCommand lineCommand = 

        // Get the register with the specified name or Unnamed if no name is 
        // provided
        let getRegister name = 
            name 
            |> OptionUtil.getOrDefault RegisterName.Unnamed
            |> _registerMap.GetRegister

        // Run the func with the given line range and count
        let runWithLineRangeAndEndCount lineRange count func = 
            match x.GetLineRangeOrCurrent lineRange with
            | None ->
                _statusUtil.OnError Resources.Range_Invalid
                RunResult.Completed
            | Some lineRange ->
                let lineRange = 
                    match count with
                    | None -> lineRange
                    | Some count -> SnapshotLineRangeUtil.CreateForLineAndMaxCount lineRange.EndLine count
                func lineRange

        // Run the func with the given line range
        let runWithLineRange lineRange func =
            match x.GetLineRangeOrCurrent lineRange with
            | None -> 
                _statusUtil.OnError Resources.Range_Invalid
                RunResult.Completed
            | Some lineRange -> 
                func lineRange

        // Special case join here a bit.  The count must be at least 2 where as most
        // times count is 1 
        let runJoin lineRange count joinKind = 

            match lineRange with 
            | None ->
                // Count is the only thing important when there is no explicit range is the
                // count.  It is special cased when there is no line range
                let count = 
                    match count with 
                    | None -> 2
                    | Some 1 -> 2
                    | Some count -> count
                let lineRange = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
                x.RunJoin joinKind lineRange
            | Some _ ->
                runWithLineRangeAndEndCount lineRange count (x.RunJoin joinKind)

        match lineCommand with
        | LineCommand.ChangeDirectory path -> x.RunChangeDirectory path
        | LineCommand.ChangeLocalDirectory path -> x.RunChangeLocalDirectory path
        | LineCommand.ClearKeyMap (keyRemapModes, mapArgumentList) -> x.RunClearKeyMap keyRemapModes mapArgumentList
        | LineCommand.Close hasBang -> x.RunClose hasBang
        | LineCommand.Edit (hasBang, fileOptions, commandOption, filePath) -> x.RunEdit hasBang fileOptions commandOption filePath
        | LineCommand.Delete (lineRange, registerName, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunDelete lineRange (getRegister registerName))
        | LineCommand.DisplayKeyMap (keyRemapModes, keyNotationOption) -> x.RunDisplayKeyMap keyRemapModes keyNotationOption
        | LineCommand.DisplayRegisters registerName -> x.RunDisplayRegisters registerName
        | LineCommand.DisplayMarks marks -> x.RunDisplayMarks marks
        | LineCommand.Fold lineRange -> x.RunFold lineRange
        | LineCommand.GoToFirstTab -> x.RunGoToFirstTab()
        | LineCommand.GoToLastTab -> x.RunGoToLastTab()
        | LineCommand.GoToNextTab count -> x.RunGoToNextTab count
        | LineCommand.GoToPreviousTab count -> x.RunGoToPreviousTab count
        | LineCommand.Join (lineRange, joinKind, count) -> runJoin lineRange count joinKind
        | LineCommand.JumpToLastLine -> x.RunJumpToLastLine()
        | LineCommand.JumpToLine number -> x.RunJumpToLine number
        | LineCommand.Make (hasBang, arguments) -> x.RunMake hasBang arguments
        | LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList) -> x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap mapArgumentList
        | LineCommand.NoHighlightSearch -> x.RunNoHighlightSearch()
        | LineCommand.PrintCurrentDirectory -> x.RunPrintCurrentDirectory()
        | LineCommand.PutAfter (lineRange, registerName) -> runWithLineRange lineRange (fun lineRange -> x.RunPut lineRange (getRegister registerName) true)
        | LineCommand.PutBefore (lineRange, registerName) -> runWithLineRange lineRange (fun lineRange -> x.RunPut lineRange (getRegister registerName) false)
        | LineCommand.Quit hasBang -> x.RunQuit hasBang
        | LineCommand.QuitAll hasBang -> x.RunQuitAll hasBang
        | LineCommand.QuitWithWrite (lineRange, hasBang, fileOptions, filePath) -> x.RunQuitWithWrite lineRange hasBang fileOptions filePath
        | LineCommand.ReadCommand (lineRange, command) -> runWithLineRange lineRange (fun lineRange -> x.RunReadCommand lineRange command)
        | LineCommand.ReadFile (lineRange, fileOptionList, filePath) -> runWithLineRange lineRange (fun lineRange -> x.RunReadFile lineRange fileOptionList filePath)
        | LineCommand.Redo -> x.RunRedo()
        | LineCommand.Retab (lineRange, hasBang, tabStop) -> x.RunRetab lineRange hasBang tabStop
        | LineCommand.Search (lineRange, path, pattern) -> runWithLineRange lineRange (fun lineRange -> x.RunSearch lineRange path pattern)
        | LineCommand.Set argumentList -> x.RunSet argumentList
        | LineCommand.ShiftLeft (lineRange, count) -> runWithLineRangeAndEndCount lineRange count x.RunShiftLeft
        | LineCommand.ShiftRight (lineRange, count) -> runWithLineRangeAndEndCount lineRange count x.RunShiftRight
        | LineCommand.Source (hasBang, filePath) -> x.RunSource hasBang filePath
        | LineCommand.Split (lineRange, fileOptions, commandOptions) -> runWithLineRange lineRange (fun lineRange -> x.RunSplit lineRange fileOptions commandOptions)
        | LineCommand.Substitute (lineRange, pattern, replace, flags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstitute lineRange pattern replace flags)
        | LineCommand.SubstituteRepeat (lineRange, substituteFlags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstituteRepeatLast lineRange substituteFlags)
        | LineCommand.Undo -> x.RunUndo()
        | LineCommand.UnmapKeys (keyNotation, keyRemapModes, mapArgumentList) -> x.RunUnmapKeys keyNotation keyRemapModes mapArgumentList
        | LineCommand.Write (lineRange, hasBang, fileOptionList, filePath) -> runWithLineRange lineRange (fun lineRange -> x.RunWrite lineRange hasBang fileOptionList filePath)
        | LineCommand.WriteAll hasBang -> x.RunWriteAll hasBang
        | LineCommand.Yank (lineRange, registerName, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunYank lineRange (getRegister registerName))



