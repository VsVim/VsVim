#light
namespace Vim.Interpreter
open Vim
open Microsoft.VisualStudio.Text
open Vim.VimHostExtensions

[<Sealed>]
[<Class>]
type Interpreter
    (
        _vimBufferData : VimBufferData,
        _commonOperations : ICommonOperations,
        _foldManager : IFoldManager
    ) =

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

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

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

    /// Clear out the key map for the given modes
    member x.RunClearKeyMap keyRemapModes = 
        keyRemapModes
        |> Seq.iter _keyMap.Clear
        RunResult.Completed

    /// Run the close command
    member x.RunClose hasBang = 
        _vimHost.Close _textView (not hasBang)
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
        // TODO: implement
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
                |> Seq.map (fun (letter, point) -> (letter.Char, point))
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
        _commonOperations.GoToNextTab Path.Forward count
        RunResult.Completed

    /// Join the lines in the specified range
    member x.RunJoin joinKind lineRange =
        _commonOperations.Join lineRange joinKind
        RunResult.Completed

    /// Jump to the last line
    member x.RunJumpToLastLine() =
        let line = SnapshotUtil.GetLastLine x.CurrentSnapshot
        _commonOperations.MoveCaretToPointAndEnsureVisible line.Start
        RunResult.Completed

    /// Jump to the specified line number
    member x.RunJumpToLine number = 
        let line = SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
        _commonOperations.MoveCaretToPointAndEnsureVisible line.Start
        RunResult.Completed

    /// Run the host make command 
    member x.RunMake hasBang arguments = 
        match _vimHost.Make (not hasBang) arguments with
        | HostResult.Error msg -> _statusUtil.OnError msg
        | HostResult.Success -> ()
        RunResult.Completed

    /// Run the map keys command
    member x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap =

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
        _vimHost.Close _textView (not hasBang)
        RunResult.Completed

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

            _vimHost.Close _textView false

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
    member x.RunSearch path pattern = 
        let pattern = 
            if StringUtil.isNullOrEmpty pattern then _vimData.LastPatternData.Pattern
            else pattern

        let startPoint = x.CaretPoint
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
        // TODO: implement
        Contract.Requires false
        RunResult.Completed

    /// Shift the given line range to the left
    member x.RunShiftLeft lineRange = 
        // TODO: implement
        Contract.Requires false
        RunResult.Completed

    /// Shift the given line range to the right
    member x.RunShiftRight lineRange = 
        // TODO: implement
        Contract.Requires false
        RunResult.Completed

    /// Run the :source command
    member x.RunSource hasBang filePath =
        // TODO: implement
        Contract.Requires false
        RunResult.Completed

    /// Split the window
    member x.RunSplit lineRange fileOptions commandOption = 
        // TODO: implement
        Contract.Requires false
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

    /// Run substitute using the last search pattern and replace from the last substitute
    member x.RunSubstituteRepeatLastWithSearch lineRange flags = 
        let pattern = _vimData.LastPatternData.Pattern

        let replace =
            _vimData.LastSubstituteData
            |> Option.map (fun substituteData -> substituteData.SearchPattern)
            |> OptionUtil.getOrDefault ""

        x.RunSubstitute lineRange pattern replace flags

    /// Run the undo command
    member x.RunUndo() =
        // TODO: implement
        Contract.Requires false
        RunResult.Completed

    member x.RunUnmapKeys keyNotation keyRemapModes =
        // TODO: implement
        Contract.Requires false
        RunResult.Completed

    member x.RunYank lineRange register =
        // TODO: implement
        Contract.Requires false
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
            let count = 
                match count with 
                | None -> 2
                | Some 1 -> 2
                | Some count -> count
            runWithLineRangeAndEndCount lineRange (Some count) (x.RunJoin joinKind)

        match lineCommand with
        | LineCommand.ClearKeyMap keyRemapModes -> x.RunClearKeyMap keyRemapModes
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
        | LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap) -> x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap
        | LineCommand.NoHighlightSearch -> x.RunNoHighlightSearch()
        | LineCommand.PutAfter (lineRange, registerName) -> runWithLineRange lineRange (fun lineRange -> x.RunPut lineRange (getRegister registerName) true)
        | LineCommand.PutBefore (lineRange, registerName) -> runWithLineRange lineRange (fun lineRange -> x.RunPut lineRange (getRegister registerName) false)
        | LineCommand.Quit hasBang -> x.RunQuit hasBang
        | LineCommand.QuitAll hasBang -> x.RunQuitAll hasBang
        | LineCommand.QuitWithWrite (lineRange, hasBang, fileOptions, filePath) -> x.RunQuitWithWrite lineRange hasBang fileOptions filePath
        | LineCommand.Redo -> x.RunRedo()
        | LineCommand.Retab (lineRange, hasBang, tabStop) -> x.RunRetab lineRange hasBang tabStop
        | LineCommand.Search (path, pattern) -> x.RunSearch path pattern
        | LineCommand.Set argumentList -> x.RunSet argumentList
        | LineCommand.ShiftLeft (lineRange, count) -> runWithLineRangeAndEndCount lineRange count x.RunShiftLeft
        | LineCommand.ShiftRight (lineRange, count) -> runWithLineRangeAndEndCount lineRange count x.RunShiftRight
        | LineCommand.Source (hasBang, filePath) -> x.RunSource hasBang filePath
        | LineCommand.Split (lineRange, fileOptions, commandOptions) -> runWithLineRange lineRange (fun lineRange -> x.RunSplit lineRange fileOptions commandOptions)
        | LineCommand.Substitute (lineRange, pattern, replace, flags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstitute lineRange pattern replace flags)
        | LineCommand.SubstituteRepeatLast (lineRange, substituteFlags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstituteRepeatLast lineRange substituteFlags)
        | LineCommand.SubstituteRepeatLastWithSearch (lineRange, substituteFlags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstituteRepeatLastWithSearch lineRange substituteFlags)
        | LineCommand.Undo -> x.RunUndo()
        | LineCommand.UnmapKeys (keyNotation, keyRemapModes) -> x.RunUnmapKeys keyNotation keyRemapModes
        | LineCommand.Yank (lineRange, registerName, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunYank lineRange (getRegister registerName))



