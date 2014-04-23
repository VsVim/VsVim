#light
namespace Vim.Interpreter
open EditorUtils
open Microsoft.VisualStudio.Text
open Vim
open Vim.StringBuilderExtensions
open System.Collections.Generic
open System.ComponentModel.Composition

[<RequireQualifiedAccess>]
type DefaultLineRange =
    | None
    | EntireBuffer
    | CurrentLine

[<Sealed>]
[<Class>]
type ExpressionInterpreter
    (
        _statusUtil : IStatusUtil
    ) =

    /// Get the value as a number
    member x.GetValueAsNumber value = 

        // TODO: Need to actually support these cases
        let invalid msg = 
            _statusUtil.OnError msg
            None

        match value with 
        | VariableValue.Dictionary _ -> invalid ""
        | VariableValue.Float _ -> invalid ""
        | VariableValue.FunctionRef _ -> invalid ""
        | VariableValue.List _ -> invalid ""
        | VariableValue.Number number -> Some number
        | VariableValue.String _ -> invalid ""
        | VariableValue.Error -> None

    /// Get the specified expression as a number
    member x.GetExpressionAsNumber expr =
        x.RunExpression expr |> x.GetValueAsNumber

    /// Get the value of the specified expression 
    member x.RunExpression (expr : Expression) : VariableValue =
        match expr with
        | Expression.ConstantValue value -> value
        | Expression.Binary (binaryKind, leftExpr, rightExpr) -> x.RunBinaryExpression binaryKind leftExpr rightExpr

    /// Run the binary expression
    member x.RunBinaryExpression binaryKind (leftExpr : Expression) (rightExpr : Expression) = 

        let notSupported() =
            _statusUtil.OnError "Binary operation not supported at this time"
            VariableValue.Error

        let runAdd (leftValue : VariableValue) (rightValue : VariableValue) = 
            if leftValue.VariableType = VariableType.List && rightValue.VariableType = VariableType.List then
                // it's a list concatenation
                notSupported()
            else
                let leftNumber = x.GetValueAsNumber leftValue
                let rightNumber = x.GetValueAsNumber rightValue
                match leftNumber, rightNumber with
                | Some left, Some right -> left + right |> VariableValue.Number
                | _ -> VariableValue.Error

        let leftValue = x.RunExpression leftExpr
        let rightValue = x.RunExpression rightExpr
        match binaryKind with
        | BinaryKind.Add -> runAdd leftValue rightValue
        | BinaryKind.Concatenate -> notSupported()
        | BinaryKind.Divide -> notSupported()
        | BinaryKind.Modulo -> notSupported()
        | BinaryKind.Multiply -> notSupported()
        | BinaryKind.Subtract -> notSupported()

[<Sealed>]
[<Class>]
type VimInterpreter
    (
        _vimBuffer : IVimBuffer,
        _commonOperations : ICommonOperations,
        _foldManager : IFoldManager,
        _fileSystem : IFileSystem,
        _bufferTrackingService : IBufferTrackingService
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
    let _registerMap = _vimBufferData.Vim.RegisterMap
    let _undoRedoOperations = _vimBufferData.UndoRedoOperations
    let _localSettings = _vimBufferData.LocalSettings
    let _windowSettings = _vimBufferData.WindowSettings
    let _globalSettings = _localSettings.GlobalSettings
    let _searchService = _vim.SearchService
    let _variableMap = _vim.VariableMap

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

    /// Get the specified LineRange in the IVimBuffer.
    ///
    /// TODO: Should this calculation be done against the visual buffer?
    /// TODO: Note that :del is already configured against the visual buffer
    member x.GetLineRangeOrDefault lineRange defaultLineRange =
        match lineRange with
        | LineRangeSpecifier.None ->
            // None is specified so use the default
            match defaultLineRange with
            | DefaultLineRange.None -> None
            | DefaultLineRange.CurrentLine -> SnapshotLineRangeUtil.CreateForLine x.CaretLine |> Some
            | DefaultLineRange.EntireBuffer -> SnapshotLineRangeUtil.CreateForSnapshot x.CurrentSnapshot |> Some

        | LineRangeSpecifier.EntireBuffer -> 
            SnapshotLineRangeUtil.CreateForSnapshot x.CurrentSnapshot |> Some
        | LineRangeSpecifier.SingleLine lineSpecifier-> 
            x.GetLine lineSpecifier |> Option.map SnapshotLineRangeUtil.CreateForLine
        | LineRangeSpecifier.Range (leftLineSpecifier, rightLineSpecifier, adjustCaret) ->
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
        | LineRangeSpecifier.WithEndCount (lineRange, count) ->

            // WithEndCount should create for a single line which is 'count' lines below the
            // end of the specified range
            match count with
            | None -> x.GetLineRangeOrDefault lineRange defaultLineRange
            | Some count -> 
                match x.GetLineRangeOrDefault lineRange defaultLineRange with
                | None -> None
                | Some lineRange -> SnapshotLineRangeUtil.CreateForLineAndMaxCount lineRange.LastLine count |> Some

        | LineRangeSpecifier.Join (lineRange, count)->
            match lineRange with 
            | LineRangeSpecifier.None ->
                // Count is the only thing important when there is no explicit range is the
                // count.  It is special cased when there is no line range
                let count = 
                    match count with 
                    | None -> 2
                    | Some 1 -> 2
                    | Some count -> count
                SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count |> Some
            | _ ->
                x.GetLineRangeOrDefault (LineRangeSpecifier.WithEndCount (lineRange, count)) defaultLineRange

    member x.GetLineRange lineRange =
        x.GetLineRangeOrDefault lineRange DefaultLineRange.None

    /// Get the count value or the default of 1
    member x.GetCountOrDefault count = Util.CountOrDefault count

    /// Add the specified auto command to the list 
    member x.RunAddAutoCommand (autoCommandDefinition : AutoCommandDefinition) = 
        let builder = System.Collections.Generic.List<AutoCommand>()
        for eventKind in autoCommandDefinition.EventKinds do
            for pattern in autoCommandDefinition.Patterns do
                let autoCommand = { 
                    Group = autoCommandDefinition.Group
                    EventKind = eventKind
                    Pattern = pattern 
                    LineCommandText = autoCommandDefinition.LineCommandText
                }
                builder.Add(autoCommand)
                
        let newList = List.ofSeq builder
        let autoCommands = List.append _vimData.AutoCommands newList
        _vimData.AutoCommands <- autoCommands
        RunResult.Completed

    /// Run the behave command
    member x.RunBehave model = 
        match model with 
        | "mswin" ->
            _globalSettings.SelectModeOptions <- (SelectModeOptions.Mouse ||| SelectModeOptions.Keyboard)
            _globalSettings.MouseModel <- "popup"
            _globalSettings.KeyModelOptions <- (KeyModelOptions.StartSelection ||| KeyModelOptions.StopSelection)
            _globalSettings.Selection <- "exclusive"
        | "xterm" ->
            _globalSettings.SelectModeOptions <- SelectModeOptions.None
            _globalSettings.MouseModel <- "extend"
            _globalSettings.KeyModelOptions <- KeyModelOptions.None
            _globalSettings.Selection <- "inclusive"
        | _ -> _statusUtil.OnError (Resources.Interpreter_InvalidArgument model)

        RunResult.Completed

    member x.RunCall (callInfo : CallInfo) = 
        _statusUtil.OnError (Resources.Interpreter_CallNotSupported callInfo.Name)
        RunResult.Completed

    /// Change the directory to the given value
    member x.RunChangeDirectory directoryPath = 
        match directoryPath with
        | None -> 
            // On non-Unix systems the :cd commandshould print out the directory when
            // cd is given no options
            _statusUtil.OnStatus x.CurrentDirectory
        | Some directoryPath ->
            let directoryPath = 
                if not (System.IO.Path.IsPathRooted directoryPath) then
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(_vimData.CurrentDirectory, directoryPath))
                else directoryPath

            if not (System.IO.Directory.Exists directoryPath) then
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
            let directoryPath = 
                if not (System.IO.Path.IsPathRooted directoryPath) then
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(_vimData.CurrentDirectory, directoryPath))
                else directoryPath

            if not (System.IO.Directory.Exists directoryPath) then
                // Not a fan of this function but we need to emulate the Vim behavior here
                _statusUtil.OnError (Resources.Interpreter_CantFindDirectory directoryPath)
            else
                // Setting the global directory will clear out the local directory for the window
                _vimBuffer.CurrentDirectory <- Some directoryPath
        RunResult.Completed

    member x.RunCopyOrMoveTo sourceLineRange destLineRange count transactionName editOperation = 

        x.RunWithLineRangeOrDefault sourceLineRange DefaultLineRange.CurrentLine (fun sourceLineRange ->

            // The :copy command allows the specification of a full range but for the destination
            // it will only be valid for single line specifiers.  
            let destLine = 
                match destLineRange with 
                | LineRangeSpecifier.None -> None
                | LineRangeSpecifier.EntireBuffer -> None
                | LineRangeSpecifier.WithEndCount _ -> None
                | LineRangeSpecifier.Join _ -> None
                | LineRangeSpecifier.Range (left, _ , _) -> x.GetLine left
                | LineRangeSpecifier.SingleLine line -> 

                    // If a single line and a count is specified then we need to apply the count to
                    // the line
                    let line = x.GetLine line
                    match line, count with
                    | Some line, Some count -> SnapshotUtil.TryGetLine x.CurrentSnapshot (line.LineNumber + count)
                    | _ -> line

            match destLine with
            | None -> _statusUtil.OnError Resources.Common_InvalidAddress
            | Some destLine -> 

                let text = 
                    if destLine.LineBreakLength = 0 then
                        // Last line in the ITextBuffer.  Inserted text must begin with a line 
                        // break to force a new line and additionally don't use the final new
                        // line from the source as it would add an extra line to the buffer
                        let newLineText = _commonOperations.GetNewLineText destLine.EndIncludingLineBreak
                        newLineText + (sourceLineRange.GetText())
                    elif sourceLineRange.LastLine.LineBreakLength = 0 then
                        // Last line in the source doesn't have a new line (last line).  Need
                        // to add one to create a break for line after
                        let newLineText = _commonOperations.GetNewLineText destLine.EndIncludingLineBreak
                        (sourceLineRange.GetText()) + newLineText 
                    else
                        sourceLineRange.GetTextIncludingLineBreak()

                // Use an undo transaction so that the caret move and insert is a single
                // operation
                _undoRedoOperations.EditWithUndoTransaction transactionName _textView (fun() -> editOperation sourceLineRange destLine text)

            RunResult.Completed)

    /// Copy the text from the source address to the destination address
    member x.RunCopyTo sourceLineRange destLineRange count =
        x.RunCopyOrMoveTo sourceLineRange destLineRange count "CopyTo" (fun sourceLineRange destLine text ->
            let destPosition = destLine.EndIncludingLineBreak.Position

            _textBuffer.Insert(destPosition, text) |> ignore
            TextViewUtil.MoveCaretToPosition _textView destPosition)

    member x.RunMoveTo sourceLineRange destLineRange count =
        x.RunCopyOrMoveTo sourceLineRange destLineRange count "MoveTo" (fun sourceLineRange destLine text ->
            let destPosition = destLine.EndIncludingLineBreak.Position

            use edit = _textBuffer.CreateEdit()
            edit.Insert(destPosition, text) |> ignore
            edit.Delete(sourceLineRange.ExtentIncludingLineBreak.Span) |> ignore
            edit.Apply() |> ignore
            TextViewUtil.MoveCaretToPosition _textView destLine.End.Position)

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

    /// Run the delete command.  Delete the specified range of text and set it to 
    /// the given Register
    member x.RunDelete lineRange register = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.DeleteLines lineRange.StartLine lineRange.Count register
            RunResult.Completed)

    member x.RunDeleteMarkCore mark = 
        match mark with 
        | Mark.LocalMark localMark -> _vimTextBuffer.RemoveLocalMark localMark |> ignore
        | Mark.GlobalMark letter -> _markMap.RemoveGlobalMark letter |> ignore
        | Mark.LastJump -> ()

    member x.RunDeleteMarks marks = 
        marks |> Seq.iter x.RunDeleteMarkCore
        RunResult.Completed

    member x.RunDeleteAllMarks () = 
        LocalMark.All
        |> Seq.filter (fun mark -> 
            match mark with 
            | LocalMark.Number _ -> false
            | _ -> true)
        |> Seq.map (fun localMark -> Mark.LocalMark localMark)
        |> Seq.iter x.RunDeleteMarkCore
        RunResult.Completed

    member x.RunFunction (func : Function) = 
        _statusUtil.OnError Resources.Interpreter_FunctionNotSupported
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
                | KeyRemapMode.None -> ""
                | KeyRemapMode.Normal -> "n"
                | KeyRemapMode.Visual -> "x"
                | KeyRemapMode.Select -> "s"
                | KeyRemapMode.OperatorPending -> "o"
                | KeyRemapMode.Command -> "c"
                | KeyRemapMode.Language -> "l"
                | KeyRemapMode.Insert -> "i"

        // Get the printable format for the KeyInputSet 
        let getKeyInputSetLine (keyInputSet : KeyInputSet) = 

            keyInputSet.KeyInputs |> Seq.map KeyNotationUtil.GetDisplayName |> String.concat ""

        // Get the printable line for the provided mode, left and right side
        let getLine modes lhs rhs = 
            sprintf "%-5s%s %s" (getModeLine modes) (getKeyInputSetLine lhs) (getKeyInputSetLine rhs)

        let lines = 
            keyRemapModes
            |> Seq.map (fun mode -> 
                mode
                |> _keyMap.GetKeyMappingsForMode 
                |> Seq.map (fun keyMapping -> (mode, keyMapping.Left, keyMapping.Right)))
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

        // The value when display shouldn't contain any new lines.  They are expressed as instead
        // ^J which is the key-notation for <NL>
        let normalizeDisplayString (data : string) = data.Replace(System.Environment.NewLine, "^J")

        let names = 
            match registerName with
            | None -> 

                // The documentation for this command says that it should display only the
                // named and numbered registers.  Experimentation shows that it should also
                // display last search, the quote star and a few others
                RegisterName.All
                |> Seq.filter (fun name ->
                    match name with
                    | RegisterName.Numbered _ -> true
                    | RegisterName.Named named -> not named.IsAppend
                    | RegisterName.SelectionAndDrop drop -> drop <> SelectionAndDropRegister.Star
                    | RegisterName.LastSearchPattern -> true
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
                | Some c, false -> Some (c, normalizeDisplayString register.StringValue))
            |> SeqUtil.filterToSome
            |> Seq.map (fun (name, value) -> sprintf "\"%c   %s" name value)
        let lines = Seq.append (Seq.singleton Resources.CommandMode_RegisterBanner) lines
        _statusUtil.OnStatusLong lines
        RunResult.Completed

    member x.RunDisplayLets (names : VariableName list) = 
        let list = List<string>()
        for name in names do
            let found, value = _variableMap.TryGetValue name.Name
            let msg =  
                if found then
                    sprintf "%s %O" name.Name value
                else
                    Resources.Interpreter_UndefinedVariable name.Name
                
            list.Add(msg)
        _statusUtil.OnStatusLong list
        RunResult.Completed

    /// Display the specified marks
    member x.RunDisplayMarks (marks : Mark list) = 
        if not (List.isEmpty marks) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "Specific marks")
        else
            let printMark (ident : char) (point : VirtualSnapshotPoint) =
                let textLine = point.Position.GetContainingLine()
                let lineNum = textLine.LineNumber + 1
                let column = point.Position.Position - textLine.Start.Position
                let column = if point.IsInVirtualSpace then column + point.VirtualSpaces else column
                let name = _vimHost.GetName point.Position.Snapshot.TextBuffer
                sprintf " %c   %5d%5d %s" ident lineNum column name
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

    /// Edit the specified file
    member x.RunEdit hasBang fileOptions commandOption filePath =
        let filePath = SystemUtil.ResolvePath filePath
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
                    | Some point -> _commonOperations.MoveCaretToPoint point ViewFlags.Standard

        elif not hasBang && _vimHost.IsDirty _textBuffer then
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            match _vimHost.LoadFileIntoExistingWindow filePath _textView with
            | HostResult.Success -> ()
            | HostResult.Error(msg) -> _statusUtil.OnError(msg)

        RunResult.Completed

    /// Get the value of the specified expression 
    member x.RunExpression expr =
        let expressionInterpreter = ExpressionInterpreter(_statusUtil)
        expressionInterpreter.RunExpression expr

    /// Fold the specified line range
    member x.RunFold lineRange = 

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            if lineRange.Count > 1 then
                _foldManager.CreateFold lineRange
            RunResult.Completed)

    /// Run the global command.  
    member x.RunGlobal lineRange pattern matchPattern lineCommand =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->
            let options = VimRegexFactory.CreateRegexOptions _globalSettings
            match VimRegexFactory.Create pattern options with
            | None -> _statusUtil.OnError Resources.Interpreter_Error
            | Some regex ->
    
                // All of the edits should behave as a single vim undo.  Can't do this as a single
                // global undo as it executes as series of sub commands which create their own 
                // global undo units
                use transaction = _undoRedoOperations.CreateLinkedUndoTransaction "Global Command"
                try
    
                    // Each command we run can, and often will, change the underlying buffer whcih
                    // will change the current ITextSnapshot.  Run one pass to get the line numbers
                    // and then a second to edit the commands
                    let lineNumbers = 
                        lineRange.Lines
                        |> Seq.filter (fun snapshotLine ->
                            let text = SnapshotLineUtil.GetText snapshotLine
                            let didMatch = regex.IsMatch text
                            didMatch = matchPattern)
                        |> Seq.map (fun snapshotLine ->
                            let line, column = SnapshotPointUtil.GetLineColumn snapshotLine.Start
                            _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.Default)
                        |> List.ofSeq
    
                    // Now perform the edit for every line.  Make sure to map forward to the 
                    // current ITextSnapshot
                    lineNumbers |> List.iter (fun trackingLineColumn ->
                        match trackingLineColumn.Point with
                        | None -> ()
                        | Some point ->
                            let point = 
                                point
                                |> SnapshotPointUtil.GetContainingLine
                                |> SnapshotLineUtil.GetStart
    
                            // Caret needs to move to the start of the line for each :global command
                            // action.  The caret will persist on the final line in the range once
                            // the :global command completes
                            TextViewUtil.MoveCaretToPoint _textView point
                            x.RunLineCommand lineCommand |> ignore)
    
                    // Now close all of the ITrackingLineColumn values so that they stop taking up resources
                    lineNumbers |> List.iter (fun trackingLineColumn -> trackingLineColumn.Close())
    
                finally
                    transaction.Complete()

            RunResult.Completed)

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

    /// Print out the applicable history information
    member x.RunHistory () = 
        let output = List<string>()
        output.Add("      # cmd history")

        let historyList = _vimData.CommandHistory
        let mutable index = historyList.TotalCount - historyList.Count
        for item in historyList.Items |> List.rev do
            index <- index + 1
            let msg = sprintf "%7d %s" index item
            output.Add(msg)

        _statusUtil.OnStatusLong(output)
        RunResult.Completed

    /// Run the if command
    member x.RunIf (conditionalBlockList : ConditionalBlock list)  =
        let expressionInterpreter = ExpressionInterpreter(_statusUtil)

        let shouldRun (conditionalBlock : ConditionalBlock) =
            match conditionalBlock.Conditional with
            | None -> true
            | Some expr -> 
                match expressionInterpreter.GetExpressionAsNumber expr with
                | None -> false
                | Some value -> value <> 0

        match List.tryFind shouldRun conditionalBlockList with
        | None -> ()
        | Some conditionalBlock -> conditionalBlock.LineCommands |> Seq.iter (fun lineCommand -> x.RunLineCommand lineCommand |> ignore)
        
        RunResult.Completed

    /// Join the lines in the specified range
    member x.RunJoin lineRange joinKind =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.Join lineRange joinKind
            RunResult.Completed)

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

        _commonOperations.MoveCaretToPoint point (ViewFlags.Standard &&& (~~~ViewFlags.TextExpanded))
        RunResult.Completed

    /// Run the let command
    member x.RunLet (name : VariableName) value =
        // TODO: At this point we are treating all variables as if they were global.  Need to 
        // take into account the NameScope at this level too
        _variableMap.[name.Name] <- value
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
        _vimData.SuspendDisplayPattern()
        RunResult.Completed

    member x.RunParseError msg =
        _statusUtil.OnError msg
        RunResult.Completed 

    /// Print out the contents of the specified range
    member x.RunPrint lineRange lineCommandFlags = 
        
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            if lineCommandFlags <> LineCommandFlags.None then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[ex-flags]")
            else
                lineRange.Lines
                |> Seq.map SnapshotLineUtil.GetText
                |> _statusUtil.OnStatusLong
            RunResult.Completed)

    /// Print out the current directory
    member x.RunPrintCurrentDirectory() =
        _statusUtil.OnStatus x.CurrentDirectory
        RunResult.Completed

    /// Put the register after the last line in the given range
    member x.RunPut lineRange (register : Register) putAfter = 

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            // Need to get the cursor position correct for undo / redo so start an undo 
            // transaction 
            _undoRedoOperations.EditWithUndoTransaction "PutLine" _textView (fun () ->
    
                // Get the point to start the Put operation at 
                let line = 
                    if putAfter then lineRange.LastLine
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
                _commonOperations.MoveCaretToPoint point ViewFlags.VirtualEdit)
    
            RunResult.Completed)

    member x.RunQuickFixNext count hasBang =
        let count = OptionUtil.getOrDefault 1 count 
        _vimHost.GoToQuickFix QuickFix.Next count hasBang |> ignore
        RunResult.Completed

    member x.RunQuickFixPrevious count hasBang =
        let count = OptionUtil.getOrDefault 1 count 
        _vimHost.GoToQuickFix QuickFix.Previous count hasBang |> ignore
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

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->
            if not (List.isEmpty fileOptions) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
            else
    
                match filePath with
                | None -> _vimHost.Save _textView.TextBuffer |> ignore  
                | Some filePath ->
                    let filePath = SystemUtil.ResolvePath filePath
                    _vimHost.SaveTextAs (lineRange.GetTextIncludingLineBreak()) filePath |> ignore
    
                x.RunClose false |> ignore
    
            RunResult.Completed)

    /// Run the core parts of the read command
    member x.RunReadCore (lineRange : SnapshotLineRange) (lines : string[]) = 
        let point = lineRange.EndIncludingLineBreak
        let lineBreak = _commonOperations.GetNewLineText point
        let text = 
            let builder = System.Text.StringBuilder()
            for line in lines do
                builder.AppendString line
                builder.AppendString lineBreak
            builder.ToString()
        _textBuffer.Insert(point.Position, text) |> ignore

    /// Run the read command command
    member x.RunReadCommand lineRange command = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            match x.ExecuteCommand command with
            | None ->
                _statusUtil.OnError (Resources.Interpreter_CantRunCommand command)
            | Some lines ->
                x.RunReadCore lineRange lines
            RunResult.Completed)

    /// Run the read file command.
    member x.RunReadFile lineRange fileOptionList filePath =
        let filePath = SystemUtil.ResolvePath filePath
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            if not (List.isEmpty fileOptionList) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
            else
                match _fileSystem.ReadAllLines filePath with
                | None ->
                    _statusUtil.OnError (Resources.Interpreter_CantOpenFile filePath)
                | Some lines ->
                    x.RunReadCore lineRange lines
            RunResult.Completed)

    /// Run a single redo operation
    member x.RunRedo() = 
        _commonOperations.Redo 1
        RunResult.Completed

    /// Remove the auto commands which match the specified definition
    member x.RemoveAutoCommands (autoCommandDefinition : AutoCommandDefinition) = 
        let isMatch (autoCommand : AutoCommand) = 
            
            if autoCommand.Group = autoCommandDefinition.Group then
                let isPatternMatch = Seq.exists (fun p -> autoCommand.Pattern = p) autoCommandDefinition.Patterns
                let isEventMatch = Seq.exists (fun e -> autoCommand.EventKind = e) autoCommandDefinition.EventKinds

                match autoCommandDefinition.Patterns.Length > 0, autoCommandDefinition.EventKinds.Length > 0 with
                | true, true -> isPatternMatch && isEventMatch
                | true, false -> isPatternMatch
                | false, true -> isEventMatch
                | false, false -> true
            else
                false
                
        let rest = 
            _vimData.AutoCommands
            |> Seq.filter (fun x -> not (isMatch x))
            |> List.ofSeq
        _vimData.AutoCommands <- rest
        RunResult.Completed

    /// Process the :retab command.  Changes all sequences of spaces and tabs which contain
    /// at least a single tab into the normalized value based on the provided 'tabstop' or 
    /// default 'tabstop' setting
    member x.RunRetab lineRange includeSpaces tabStop =

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->
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
    
            RunResult.Completed)

    /// Run the search command in the given direction
    member x.RunSearch lineRange path pattern = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            let pattern = 
                if StringUtil.isNullOrEmpty pattern then _vimData.LastSearchData.Pattern
                else pattern
    
            // Searches start after the end of the specified line range
            let startPoint = lineRange.End
            let searchData = SearchData(pattern, path, _globalSettings.WrapScan)
            let result = _searchService.FindNextPattern startPoint searchData _vimBufferData.VimTextBuffer.WordNavigator 1
            _commonOperations.RaiseSearchResultMessage(result)
    
            match result with
            | SearchResult.Found (searchData, span, _, _) ->
                // Move it to the start of the line containing the match 
                let point = 
                    span.Start 
                    |> SnapshotPointUtil.GetContainingLine 
                    |> SnapshotLineUtil.GetFirstNonBlankOrStart
                _commonOperations.MoveCaretToPoint point ViewFlags.Standard
                _vimData.LastSearchData <- searchData
            | SearchResult.NotFound _ -> ()
    
            RunResult.Completed)

    /// Run the :set command.  Process each of the arguments 
    member x.RunSet setArguments =

        // Get the setting for the specified name
        let withSetting name msg (func : Setting -> IVimSettings -> unit) =
            match _localSettings.GetSetting name with
            | None -> 
                match _windowSettings.GetSetting name with
                | None -> _statusUtil.OnError (Resources.Interpreter_UnknownOption msg)
                | Some setting -> func setting _windowSettings
            | Some setting -> func setting _localSettings

        // Display the specified setting 
        let getSettingDisplay (setting: Setting ) =

            match setting.Value with
            | SettingValue.Toggle b -> 
                if b then setting.Name
                else sprintf "no%s" setting.Name
            | SettingValue.String s -> 
                sprintf "%s=\"%s\"" setting.Name s
            | SettingValue.Number n ->
                sprintf "%s=%d" setting.Name n

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
            withSetting name msg (fun setting container ->
                if not (container.TrySetValueFromString setting.Name value) then
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
            withSetting name name (fun setting _ ->
                let display = getSettingDisplay setting
                _statusUtil.OnStatus display)

        // Display the terminal options
        let displayAllTerminal() = 
            // TODO: Implement
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "term")

        // Use the specifiec setting
        let useSetting name =
            withSetting name name (fun setting container ->
                match setting.Kind with
                | SettingKind.Toggle -> container.TrySetValue setting.Name (SettingValue.Toggle true) |> ignore
                | SettingKind.Number -> displaySetting name
                | SettingKind.String -> displaySetting name)

        // Invert the setting of the specified name
        let invertSetting name = 
            let msg = "!" + name
            withSetting name msg (fun setting container -> 
                match setting.Value with
                | SettingValue.Toggle b -> container.TrySetValue setting.Name (SettingValue.Toggle(not b)) |> ignore
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
            withSetting name msg (fun setting container -> 
                match setting.Kind with
                | SettingKind.Number -> _statusUtil.OnError (Resources.Interpreter_InvalidArgument msg)
                | SettingKind.String -> _statusUtil.OnError (Resources.Interpreter_InvalidArgument msg)
                | SettingKind.Toggle -> container.TrySetValue setting.Name (SettingValue.Toggle false) |> ignore)

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

    /// Run the specified shell command
    member x.RunShellCommand (command : string) =

        // Actuall run the command
        let doRun command = 

            let file = _globalSettings.Shell
            let output = _vimHost.RunCommand _globalSettings.Shell command _vimData
            _statusUtil.OnStatus output

        // Build up the actual command replacing any non-escaped ! with the previous
        // shell command
        let builder = System.Text.StringBuilder()

        // Append the shell flag before the other arguments
        if _globalSettings.ShellFlag.Length > 0 then
            builder.AppendString _globalSettings.ShellFlag
            builder.AppendChar ' '

        let rec inner index afterBackslash = 
            if index >= command.Length then
                builder.ToString() |> doRun
            else
                let current = command.[index]
                if current = '\\' && (index + 1) < command.Length then
                    let next = command.[index + 1]
                    builder.AppendChar next

                    // It seems odd to escape ! after an escaped backslash but it's
                    // specifically called out in the documentation for :shell
                    let afterBackslash = next = '\\'
                    inner (index + 2) afterBackslash
                elif current = '!' then
                    match _vimData.LastShellCommand with
                    | None -> 
                        _statusUtil.OnError Resources.Common_NoPreviousShellCommand
                    | Some previousCommand ->
                        builder.AppendString previousCommand
                        inner (index + 1) false
                else
                    builder.AppendChar current
                    inner (index + 1) false

        inner 0 false

        RunResult.Completed

    /// Shift the given line range to the left
    member x.RunShiftLeft lineRange = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.ShiftLineRangeLeft lineRange 1
            RunResult.Completed)

    /// Shift the given line range to the right
    member x.RunShiftRight lineRange = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.ShiftLineRangeRight lineRange 1
            RunResult.Completed)

    /// Run the :source command
    member x.RunSource hasBang filePath =
        if hasBang then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "!")
        else
            let filePath = SystemUtil.ResolvePath filePath
            match _fileSystem.ReadAllLines filePath with
            | None -> _statusUtil.OnError (Resources.CommandMode_CouldNotOpenFile filePath)
            | Some lines -> x.RunScript lines

        RunResult.Completed

    /// Split the window
    member x.RunSplit behavior fileOptions commandOption = 
        let SplitArgumentsAreValid fileOptions commandOption =
            if not (List.isEmpty fileOptions) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
                false
            elif Option.isSome commandOption then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++cmd]")
                false
            else
                true

        if SplitArgumentsAreValid fileOptions commandOption then
            match behavior _textView with
            | HostResult.Success -> ()
            | HostResult.Error msg -> _statusUtil.OnError msg
        else
            ()

        RunResult.Completed

    /// Run the substitute command. 
    member x.RunSubstitute lineRange pattern replace flags =

        // Called to initialize the data and move to a confirm style substitution.  Have to find the first match
        // before passing off to confirm
        let setupConfirmSubstitute (range : SnapshotLineRange) (data : SubstituteData) =
            let regex = VimRegexFactory.CreateForSubstituteFlags data.SearchPattern _globalSettings data.Flags
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
                _vimData.LastSearchData.Pattern
            else
                // If a pattern is given then it is the one that we will use 
                pattern

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->

            if Util.IsFlagSet flags SubstituteFlags.Confirm then
                let data = { SearchPattern = pattern; Substitute = replace; Flags = flags}
                setupConfirmSubstitute lineRange data
            else
                _commonOperations.Substitute pattern replace lineRange flags 
                RunResult.Completed)

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

    /// Run the unlet command
    member x.RunUnlet ignoreMissing nameList = 
        let rec func nameList = 
            match nameList with
            | [] -> RunResult.Completed
            | name :: rest ->
                let removed = _variableMap.Remove(name)
                if not removed && not ignoreMissing then
                    let msg = Resources.Interpreter_NoSuchVariable name
                    _statusUtil.OnError msg
                    RunResult.Completed
                else
                    func rest
        func nameList

    /// Unmap the specified key notation in all of the listed modes
    member x.RunUnmapKeys keyNotation keyRemapModes mapArgumentList =
        if not (List.isEmpty mapArgumentList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "map special arguments")
        else

            let allSucceeded =
                keyRemapModes
                |> Seq.map (fun keyRemapMode -> _keyMap.Unmap keyNotation keyRemapMode || _keyMap.UnmapByMapping keyNotation keyRemapMode)
                |> Seq.filter (fun x -> not x)
                |> Seq.isEmpty

            if not allSucceeded then 
                _statusUtil.OnError Resources.CommandMode_NoSuchMapping

        RunResult.Completed

    member x.RunVersion() = 
        let msg = sprintf "VsVim Version %s" VimConstants.VersionNumber
        _statusUtil.OnStatus msg
        RunResult.Completed

    member x.RunVisualStudioCommand command argument =
        _vimHost.RunVisualStudioCommand command argument
        RunResult.Completed

    member x.RunWrite lineRange hasBang fileOptionList filePath =
        let filePath =
            match filePath with
            | Some filePath ->
                Some (SystemUtil.ResolvePath filePath)
            | None ->
                None
        if not (List.isEmpty fileOptionList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
        else
            match filePath with
            | Some filePath -> 
                let text = _textBuffer.CurrentSnapshot.GetText()
                _vimHost.SaveTextAs text filePath |> ignore
            | None ->
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
            elif _vimHost.IsDirty vimBuffer.TextBuffer then
                _vimHost.Save vimBuffer.TextBuffer |> ignore
        RunResult.Completed

    /// Yank the specified line range into the register.  This is done in a 
    /// linewise fashion
    member x.RunYank register lineRange count =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->

            // If the user specified a count then that count is applied to the end
            // of the specified line range
            let lineRange = 
                match count with
                | None -> lineRange
                | Some count -> SnapshotLineRangeUtil.CreateForLineAndMaxCount lineRange.LastLine count

            let stringData = StringData.OfSpan lineRange.ExtentIncludingLineBreak
            let value = RegisterValue(stringData, OperationKind.LineWise)
            _registerMap.SetRegisterValue register RegisterOperation.Yank value
    
            RunResult.Completed)

    /// Run the specified LineCommand
    member x.RunLineCommand lineCommand = 

        // Get the register with the specified name or Unnamed if no name is 
        // provided
        let getRegister name = 
            name 
            |> OptionUtil.getOrDefault RegisterName.Unnamed
            |> _registerMap.GetRegister

        let cantRun () =    
            _statusUtil.OnError Resources.Interpreter_Error
            RunResult.Completed

        match lineCommand with
        | LineCommand.AddAutoCommand autoCommandDefinition -> x.RunAddAutoCommand autoCommandDefinition
        | LineCommand.Behave model -> x.RunBehave model
        | LineCommand.Call callInfo -> x.RunCall callInfo
        | LineCommand.ChangeDirectory path -> x.RunChangeDirectory path
        | LineCommand.ChangeLocalDirectory path -> x.RunChangeLocalDirectory path
        | LineCommand.CopyTo (sourceLineRange, destLineRange, count) -> x.RunCopyTo sourceLineRange destLineRange count
        | LineCommand.ClearKeyMap (keyRemapModes, mapArgumentList) -> x.RunClearKeyMap keyRemapModes mapArgumentList
        | LineCommand.Close hasBang -> x.RunClose hasBang
        | LineCommand.Delete (lineRange, registerName) -> x.RunDelete lineRange (getRegister registerName)
        | LineCommand.DeleteMarks marks -> x.RunDeleteMarks marks
        | LineCommand.DeleteAllMarks -> x.RunDeleteAllMarks()
        | LineCommand.Edit (hasBang, fileOptions, commandOption, filePath) -> x.RunEdit hasBang fileOptions commandOption filePath
        | LineCommand.Else -> cantRun ()
        | LineCommand.ElseIf _ -> cantRun ()
        | LineCommand.Function func -> x.RunFunction func
        | LineCommand.FunctionStart _ -> cantRun ()
        | LineCommand.FunctionEnd _ -> cantRun ()
        | LineCommand.DisplayKeyMap (keyRemapModes, keyNotationOption) -> x.RunDisplayKeyMap keyRemapModes keyNotationOption
        | LineCommand.DisplayRegisters registerName -> x.RunDisplayRegisters registerName
        | LineCommand.DisplayLet variables -> x.RunDisplayLets variables
        | LineCommand.DisplayMarks marks -> x.RunDisplayMarks marks
        | LineCommand.Fold lineRange -> x.RunFold lineRange
        | LineCommand.Global (lineRange, pattern, matchPattern, lineCommand) -> x.RunGlobal lineRange pattern matchPattern lineCommand
        | LineCommand.History -> x.RunHistory()
        | LineCommand.IfStart _ -> cantRun ()
        | LineCommand.IfEnd -> cantRun ()
        | LineCommand.If conditionalBlock -> x.RunIf conditionalBlock
        | LineCommand.GoToFirstTab -> x.RunGoToFirstTab()
        | LineCommand.GoToLastTab -> x.RunGoToLastTab()
        | LineCommand.GoToNextTab count -> x.RunGoToNextTab count
        | LineCommand.GoToPreviousTab count -> x.RunGoToPreviousTab count
        | LineCommand.HorizontalSplit (lineRange, fileOptions, commandOptions) -> x.RunSplit _vimHost.SplitViewHorizontally fileOptions commandOptions
        | LineCommand.Join (lineRange, joinKind) -> x.RunJoin lineRange joinKind
        | LineCommand.JumpToLastLine -> x.RunJumpToLastLine()
        | LineCommand.JumpToLine number -> x.RunJumpToLine number
        | LineCommand.Let (name, value) -> x.RunLet name value
        | LineCommand.Make (hasBang, arguments) -> x.RunMake hasBang arguments
        | LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList) -> x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap mapArgumentList
        | LineCommand.MoveTo (sourceLineRange, destLineRange, count) -> x.RunMoveTo sourceLineRange destLineRange count
        | LineCommand.NoHighlightSearch -> x.RunNoHighlightSearch()
        | LineCommand.Nop -> RunResult.Completed
        | LineCommand.ParseError msg -> x.RunParseError msg
        | LineCommand.Print (lineRange, lineCommandFlags)-> x.RunPrint lineRange lineCommandFlags
        | LineCommand.PrintCurrentDirectory -> x.RunPrintCurrentDirectory()
        | LineCommand.PutAfter (lineRange, registerName) -> x.RunPut lineRange (getRegister registerName) true
        | LineCommand.PutBefore (lineRange, registerName) -> x.RunPut lineRange (getRegister registerName) false
        | LineCommand.QuickFixNext (count, hasBang) -> x.RunQuickFixNext count hasBang
        | LineCommand.QuickFixPrevious (count, hasBang) -> x.RunQuickFixPrevious count hasBang
        | LineCommand.Quit hasBang -> x.RunQuit hasBang
        | LineCommand.QuitAll hasBang -> x.RunQuitAll hasBang
        | LineCommand.QuitWithWrite (lineRange, hasBang, fileOptions, filePath) -> x.RunQuitWithWrite lineRange hasBang fileOptions filePath
        | LineCommand.ReadCommand (lineRange, command) -> x.RunReadCommand lineRange command
        | LineCommand.ReadFile (lineRange, fileOptionList, filePath) -> x.RunReadFile lineRange fileOptionList filePath
        | LineCommand.Redo -> x.RunRedo()
        | LineCommand.RemoveAutoCommands autoCommandDefinition -> x.RemoveAutoCommands autoCommandDefinition
        | LineCommand.Retab (lineRange, hasBang, tabStop) -> x.RunRetab lineRange hasBang tabStop
        | LineCommand.Search (lineRange, path, pattern) -> x.RunSearch lineRange path pattern
        | LineCommand.Set argumentList -> x.RunSet argumentList
        | LineCommand.ShellCommand command -> x.RunShellCommand command
        | LineCommand.ShiftLeft lineRange -> x.RunShiftLeft lineRange
        | LineCommand.ShiftRight lineRange -> x.RunShiftRight lineRange
        | LineCommand.Source (hasBang, filePath) -> x.RunSource hasBang filePath
        | LineCommand.Substitute (lineRange, pattern, replace, flags) -> x.RunSubstitute lineRange pattern replace flags
        | LineCommand.SubstituteRepeat (lineRange, substituteFlags) -> x.RunSubstituteRepeatLast lineRange substituteFlags
        | LineCommand.Undo -> x.RunUndo()
        | LineCommand.Unlet (ignoreMissing, nameList) -> x.RunUnlet ignoreMissing nameList
        | LineCommand.UnmapKeys (keyNotation, keyRemapModes, mapArgumentList) -> x.RunUnmapKeys keyNotation keyRemapModes mapArgumentList
        | LineCommand.Version -> x.RunVersion()
        | LineCommand.VerticalSplit (lineRange, fileOptions, commandOptions) -> x.RunSplit _vimHost.SplitViewVertically fileOptions commandOptions
        | LineCommand.VisualStudioCommand (command, argument) -> x.RunVisualStudioCommand command argument
        | LineCommand.Write (lineRange, hasBang, fileOptionList, filePath) -> x.RunWrite lineRange hasBang fileOptionList filePath
        | LineCommand.WriteAll hasBang -> x.RunWriteAll hasBang
        | LineCommand.Yank (lineRange, registerName, count) -> x.RunYank (getRegister registerName) lineRange count

    member x.RunWithLineRange lineRangeSpecifier (func : SnapshotLineRange -> RunResult) = 
        x.RunWithLineRangeOrDefault lineRangeSpecifier DefaultLineRange.None func

    member x.RunWithLineRangeOrDefault (lineRangeSpecifier : LineRangeSpecifier) defaultLineRange (func : SnapshotLineRange -> RunResult) = 
        match x.GetLineRangeOrDefault lineRangeSpecifier defaultLineRange with
        | None -> 
            _statusUtil.OnError Resources.Range_Invalid
            RunResult.Completed
        | Some lineRange ->
            func lineRange

    // Actually parse and run all of the commands which are included in the script
    member x.RunScript lines = 
        let parser = Parser(_globalSettings, _vimData, lines)
        while not parser.IsDone do
            let lineCommand = parser.ParseNextCommand()
            x.RunLineCommand lineCommand |> ignore

    interface IVimInterpreter with
        member x.GetLine lineSpecifier = x.GetLine lineSpecifier
        member x.GetLineRange lineRange = x.GetLineRange lineRange
        member x.RunLineCommand lineCommand = x.RunLineCommand lineCommand
        member x.RunExpression expression = x.RunExpression expression
        member x.RunScript lines = x.RunScript lines

[<Export(typeof<IVimInterpreterFactory>)>]
type VimInterpreterFactory
    [<ImportingConstructor>]
    (
        _commonOperationsFactory : ICommonOperationsFactory,
        _foldManagerFactory : IFoldManagerFactory,
        _bufferTrackingService : IBufferTrackingService
    ) = 

    member x.CreateVimInterpreter (vimBuffer : IVimBuffer) fileSystem =
        let commonOperations = _commonOperationsFactory.GetCommonOperations vimBuffer.VimBufferData
        let foldManager = _foldManagerFactory.GetFoldManager vimBuffer.TextView
        let interpreter = VimInterpreter(vimBuffer, commonOperations, foldManager, fileSystem, _bufferTrackingService)
        interpreter :> IVimInterpreter

    interface IVimInterpreterFactory with
        member x.CreateVimInterpreter vimBuffer fileSystem = x.CreateVimInterpreter vimBuffer fileSystem