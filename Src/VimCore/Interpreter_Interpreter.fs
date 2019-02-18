#light
namespace Vim.Interpreter
open Microsoft.VisualStudio.Text
open Vim
open Vim.StringBuilderExtensions
open Vim.VimCoreExtensions
open System.Collections.Generic
open System.ComponentModel.Composition
open System.IO

[<RequireQualifiedAccess>]
type DefaultLineRange =
    | None
    | EntireBuffer
    | CurrentLine

[<Sealed>]
[<Class>]
type VariableValueUtil
    (
        _statusUtil: IStatusUtil
    ) =

    member x.ConvertToNumber value = 
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

    member x.ConvertToString value = 
        let invalid typeName = 
            _statusUtil.OnError (Resources.Interpreter_InvalidConversionToString typeName)
            None
        match value with 
        | VariableValue.Dictionary _ -> invalid "Dictionary"
        | VariableValue.Float _ -> invalid "Float"
        | VariableValue.FunctionRef _ -> invalid "Funcref"
        | VariableValue.List _ -> invalid "List"
        | VariableValue.Number number -> Some (string number)
        | VariableValue.String str -> Some str
        | VariableValue.Error ->
            _statusUtil.OnError Resources.Interpreter_Error
            None

[<Sealed>]
[<Class>]
type BuiltinFunctionCaller
    (
        _variableMap: Dictionary<string, VariableValue>
    ) =
    member x.Call (func: BuiltinFunctionCall) =
        match func with
        | BuiltinFunctionCall.Escape(escapeIn, escapeWhat) ->
            let escapeChar (c: char) =
                let character = c.ToString()
                if escapeWhat.Contains character then sprintf @"\%s" character else character
            Seq.map escapeChar escapeIn
            |> String.concat ""
            |> VariableValue.String
        | BuiltinFunctionCall.Exists name ->
            _variableMap.ContainsKey name
            |> System.Convert.ToInt32
            |> VariableValue.Number
        | BuiltinFunctionCall.Localtime ->
            // TODO: .NET 4.6 will have builtin support for converting to Unix time http://stackoverflow.com/a/26225744/834176
            let epoch = System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
            let now = System.DateTime.Now.ToUniversalTime()
            (now - epoch).TotalSeconds
            |> System.Convert.ToInt32
            |> VariableValue.Number
        | BuiltinFunctionCall.Nr2char nr ->
            char nr
            |> string
            |> VariableValue.String

[<Sealed>]
[<Class>]
type VimScriptFunctionCaller
    (
        _builtinCaller: BuiltinFunctionCaller,
        _statusUtil: IStatusUtil
    ) =
    let _getValue = VariableValueUtil(_statusUtil)
    member x.Call (name: VariableName) (args: VariableValue list) =
        let tooManyArgs() =
            sprintf "Too many arguments for function: %s" name.Name |> _statusUtil.OnError
            VariableValue.Error
        let notEnoughArgs() =
            sprintf "Not enough arguments for function: %s" name.Name |> _statusUtil.OnError
            VariableValue.Error
            
        match name.Name with
        | "escape" ->
            match args.Length with
            | 0 | 1 -> notEnoughArgs()
            | 2 ->
                match _getValue.ConvertToString args.[0], _getValue.ConvertToString args.[1] with
                | Some str, Some chars -> BuiltinFunctionCall.Escape(str, chars) |> _builtinCaller.Call
                | _, _ -> VariableValue.Error
            | _ -> tooManyArgs()
        | "exists" ->
            match args.Length with
            | 0 -> notEnoughArgs()
            | 1 ->
                match _getValue.ConvertToString args.[0] with
                | Some arg -> BuiltinFunctionCall.Exists(arg) |> _builtinCaller.Call
                | None -> VariableValue.Error
            | _ -> tooManyArgs()
        | "localtime" ->
            if args.Length = 0 then _builtinCaller.Call BuiltinFunctionCall.Localtime
            else tooManyArgs()
        | "nr2char" ->
            match args.Length with
            | 0 -> notEnoughArgs()
            | 1 ->
                match _getValue.ConvertToNumber args.[0] with
                | Some arg -> BuiltinFunctionCall.Nr2char(arg) |> _builtinCaller.Call
                | None -> VariableValue.Error
            | _ -> tooManyArgs()
        | fname ->
            sprintf "Unknown function: %s" fname |> _statusUtil.OnError
            VariableValue.Error

[<Sealed>]
[<Class>]
type ExpressionInterpreter
    (
        _statusUtil: IStatusUtil,
        _localSettings: IVimSettings,
        _windowSettings: IVimSettings,
        _variableMap: Dictionary<string, VariableValue>,
        _registerMap: IRegisterMap
    ) =
    let _builtinCaller = BuiltinFunctionCaller(_variableMap)
    let _functionCaller = VimScriptFunctionCaller(_builtinCaller, _statusUtil)
    let _getValue = VariableValueUtil(_statusUtil)

    /// Get the specified expression as a number
    member x.GetExpressionAsNumber expr =
        x.RunExpression expr |> _getValue.ConvertToNumber

    /// Get the specified expression as a number
    member x.GetExpressionAsString expr =
        x.RunExpression expr |> _getValue.ConvertToString

    member x.GetSetting name = 
        match _localSettings.GetSetting name with
        | None -> _windowSettings.GetSetting name
        | Some setting -> Some setting

    member x.GetValueOfSetting setting =
        match setting.LiveSettingValue.Value with
        | SettingValue.Toggle x -> VariableValue.Number (System.Convert.ToInt32 x)
        | SettingValue.Number x -> VariableValue.Number x
        | SettingValue.String x -> VariableValue.String x

    member x.GetValueOfVariable name = 
        let found, value = _variableMap.TryGetValue name
        if found then
            value
        else
            VariableValue.Error

    member x.GetValueOfRegister name =
        (_registerMap.GetRegister name).StringValue |> VariableValue.String

    /// Get the value of the specified expression 
    member x.RunExpression (expr: Expression): VariableValue =
        let runExpression expressions = [for expr in expressions -> x.RunExpression expr]
        match expr with
        | Expression.ConstantValue value -> value
        | Expression.Binary (binaryKind, leftExpr, rightExpr) -> x.RunBinaryExpression binaryKind leftExpr rightExpr
        | Expression.OptionName name ->
            match x.GetSetting name with
            | None -> VariableValue.Error
            | Some setting -> x.GetValueOfSetting setting
        | Expression.VariableName name -> x.GetValueOfVariable name.Name
        | Expression.RegisterName name -> x.GetValueOfRegister name
        | Expression.FunctionCall(name, args) -> runExpression args |> _functionCaller.Call name
        | Expression.List expressions -> runExpression expressions |> VariableValue.List 

    /// Run the binary expression
    member x.RunBinaryExpression binaryKind (leftExpr: Expression) (rightExpr: Expression) = 
        
        // Actually it seems that the user doesn't see these messages
        // and see a generic expression interpreting error instead
        // as the error status is updated later...
        let notSupported() =
            _statusUtil.OnError "Binary operation not supported at this time"
            VariableValue.Error

        let divByZero() =
            _statusUtil.OnError Resources.Interpreter_DivByZero
            VariableValue.Error

        let modByZero() =
            _statusUtil.OnError Resources.Interpreter_ModByZero
            VariableValue.Error

        let runConcat (leftValue: VariableValue) (rightValue: VariableValue) =
            let leftString = _getValue.ConvertToString leftValue
            let rightString = _getValue.ConvertToString rightValue
            match leftString, rightString with
            | Some left, Some right -> left + right |> VariableValue.String 
            | _ -> VariableValue.Error
            
        let runNumericBinary (leftValue: VariableValue) (rightValue: VariableValue) (binaryKind: BinaryKind) = 
            if leftValue.VariableType = VariableType.List || rightValue.VariableType = VariableType.List then
                notSupported()
            else
                let leftNumber = _getValue.ConvertToNumber leftValue
                let rightNumber = _getValue.ConvertToNumber rightValue
                match leftNumber, rightNumber, binaryKind with
                | Some left, Some right, BinaryKind.Add -> left + right |> VariableValue.Number
                | Some left, Some right, BinaryKind.Subtract -> left - right |> VariableValue.Number
                | Some left, Some right, BinaryKind.Multiply -> left * right |> VariableValue.Number
                | Some left, Some right, BinaryKind.Divide when right = 0 -> divByZero() 
                | Some left, Some right, BinaryKind.Divide -> left / right |> VariableValue.Number
                | Some left, Some right, BinaryKind.Modulo when right = 0 -> modByZero() 
                | Some left, Some right, BinaryKind.Modulo -> left % right |> VariableValue.Number
                | Some left, Some right, BinaryKind.GreaterThan -> left > right |> System.Convert.ToInt32 |> VariableValue.Number
                | Some left, Some right, BinaryKind.LessThan -> left < right |> System.Convert.ToInt32 |> VariableValue.Number
                | Some left, Some right, BinaryKind.Equal -> left = right |> System.Convert.ToInt32 |> VariableValue.Number
                | Some left, Some right, BinaryKind.NotEqual -> left <> right |> System.Convert.ToInt32 |> VariableValue.Number
                | _ -> VariableValue.Error

        let leftValue = x.RunExpression leftExpr
        let rightValue = x.RunExpression rightExpr
        match binaryKind with
        | BinaryKind.Concatenate -> runConcat leftValue rightValue
        | BinaryKind.Add
        | BinaryKind.Divide
        | BinaryKind.Modulo
        | BinaryKind.Multiply
        | BinaryKind.Subtract
        | BinaryKind.GreaterThan
        | BinaryKind.LessThan
        | BinaryKind.Equal
        | BinaryKind.NotEqual -> runNumericBinary leftValue rightValue binaryKind

[<Sealed>]
[<Class>]
type VimInterpreter
    (
        _vimBuffer: IVimBuffer,
        _commonOperations: ICommonOperations,
        _foldManager: IFoldManager,
        _fileSystem: IFileSystem,
        _bufferTrackingService: IBufferTrackingService
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
    let _exprInterpreter = ExpressionInterpreter(_statusUtil, _localSettings, _windowSettings, _variableMap, _registerMap)

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

    /// Resolve the given path.  In the case the path contains illegal characters it 
    /// will be returned unaltered. 
    member x.ResolveVimPath (path: string) =
        try
            SystemUtil.ResolveVimPath _vimData.CurrentDirectory path
        with
            | _ -> path

    /// Get a tuple of the ITextSnapshotLine specified by the given LineSpecifier and the 
    /// corresponding vim line number
    member x.GetLineAndVimLineNumberCore lineSpecifier currentLine = 

        // To convert from a VS line number to a vim line number, simply add 1
        let getLineAndNumber (line: ITextSnapshotLine) = (line, line.LineNumber + 1)

        // Get the ITextSnapshotLine specified by lineSpecifier and then apply the
        // given adjustment to the number.  Can fail if the line number adjustment
        // is invalid
        let getAdjustment adjustment (line: ITextSnapshotLine) = 
            let vimLine = line.LineNumber + 1 + adjustment
            let number = Util.VimLineToTssLine vimLine

            SnapshotUtil.TryGetLine x.CurrentSnapshot number
            |> Option.map (fun line -> line, vimLine)

        match lineSpecifier with 
        | LineSpecifier.CurrentLine -> 
            currentLine |> getLineAndNumber |> Some

        | LineSpecifier.LastLine ->
            SnapshotUtil.GetLastNormalizedLine x.CurrentSnapshot |> getLineAndNumber |> Some

        | LineSpecifier.MarkLine mark ->
            match mark with
            | Mark.GlobalMark letter -> _markMap.GetGlobalMark letter
            | _ -> _markMap.GetMark mark _vimBufferData
            |> Option.map VirtualSnapshotPointUtil.GetPoint
            |> Option.map SnapshotPointUtil.GetContainingLine
            |> Option.map getLineAndNumber

        | LineSpecifier.Number number ->
            // Must be a valid number 
            let tssNumber = Util.VimLineToTssLine number
            SnapshotUtil.TryGetLine x.CurrentSnapshot tssNumber
            |> Option.map (fun line -> line, number)

        | LineSpecifier.LineSpecifierWithAdjustment (lineSpecifier, adjustment) ->
            x.GetLine lineSpecifier |> OptionUtil.map2 (getAdjustment adjustment)

        | LineSpecifier.NextLineWithPattern pattern ->
            let pattern =
                if pattern = "" then
                    _vim.VimData.LastSearchData.Pattern
                else
                    pattern
            x.GetMatchingLine pattern SearchPath.Forward currentLine
            |> Option.map getLineAndNumber

        | LineSpecifier.NextLineWithPreviousPattern ->
            let pattern = _vim.VimData.LastSearchData.Pattern
            x.GetMatchingLine pattern SearchPath.Forward currentLine
            |> Option.map getLineAndNumber

        | LineSpecifier.NextLineWithPreviousSubstitutePattern ->
            match _vim.VimData.LastSubstituteData with
            | Some substituteData ->
                let pattern = substituteData.SearchPattern
                x.GetMatchingLine pattern SearchPath.Forward currentLine
                |> Option.map getLineAndNumber
            | None ->
                None

        | LineSpecifier.PreviousLineWithPattern pattern ->
            let pattern =
                if pattern = "" then
                    _vim.VimData.LastSearchData.Pattern
                else
                    pattern
            x.GetMatchingLine pattern SearchPath.Backward currentLine
            |> Option.map getLineAndNumber

        | LineSpecifier.PreviousLineWithPreviousPattern ->
            let pattern = _vim.VimData.LastSearchData.Pattern
            x.GetMatchingLine pattern SearchPath.Backward currentLine
            |> Option.map getLineAndNumber

    /// Get the first line matching the specified pattern in the specified
    /// direction
    member x.GetMatchingLine pattern path currentLine =
        if StringUtil.IsNullOrEmpty pattern then
            None
        else
            let startPoint =
                match path with
                | SearchPath.Forward ->
                    currentLine |> SnapshotLineUtil.GetEnd
                | SearchPath.Backward ->
                    currentLine |> SnapshotLineUtil.GetStart
            let searchData = SearchData(pattern, path, _globalSettings.WrapScan)
            _vimData.LastSearchData <- searchData
            let wordNavigator = _vimBufferData.VimTextBuffer.WordNavigator
            let result = _searchService.FindNextPattern startPoint searchData wordNavigator 1
            match result with
            | SearchResult.Found (_, span, _, _) ->
                span.Start 
                |> SnapshotPointUtil.GetContainingLine
                |> Some
            | _ ->
                None

    // Get a tuple of the ITextSnapshotLine specified by the given LineSpecifier and the 
    // corresponding vim line number
    member x.GetLineAndVimLineNumber lineSpecifier =
        x.GetLineAndVimLineNumberCore lineSpecifier x.CaretLine

    /// Get the ITextSnapshotLine specified by the given LineSpecifier
    member x.GetLineCore lineSpecifier currentLine = 
        x.GetLineAndVimLineNumberCore lineSpecifier currentLine
        |> Option.map (fun (line, vimLine) -> line)

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
        | LineRangeSpecifier.SingleLine lineSpecifier -> 
            x.GetLine lineSpecifier |> Option.map SnapshotLineRangeUtil.CreateForLine
        | LineRangeSpecifier.Range (leftLineSpecifier, rightLineSpecifier, adjustCaret) ->
            match x.GetLine leftLineSpecifier, x.GetLine rightLineSpecifier with
            | Some leftLine, Some rightLine ->

                // Make sure both lines belong to the same text buffer.
                if leftLine.Snapshot = rightLine.Snapshot then
                    let result =
                        SnapshotLineRangeUtil.CreateForLineRange leftLine rightLine
                        |> Some

                    // If the adjustCaret option was specified then we need to
                    // move the caret before interpreting the next
                    // LineSpecifier. The caret should remain moved after this
                    // completes.
                    if adjustCaret then

                        // Be careful because the line might belong to a
                        // different text buffer.
                        if _textView.TextSnapshot = leftLine.Snapshot then
                            TextViewUtil.MoveCaretToPoint _textView leftLine.Start
                            result
                        else
                            None
                    else
                        result
                else
                    None

            | _ -> None

        | LineRangeSpecifier.WithEndCount (lineRange, count) ->

            // WithEndCount should create for a single line which is 'count' lines below the
            // end of the specified range
            match x.GetLineRangeOrDefault lineRange defaultLineRange with
            | None -> None
            | Some lineRange -> SnapshotLineRangeUtil.CreateForLineAndMaxCount lineRange.LastLine count |> Some

        | LineRangeSpecifier.Join (lineRange, count) ->
            match lineRange, count with 
            | LineRangeSpecifier.None, _ ->
                // Count is the only thing important when there is no explicit range is the
                // count.  It is special cased when there is no line range
                let count = 
                    match count with 
                    | None -> 2
                    | Some 1 -> 2
                    | Some count -> count
                SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count |> Some
            | _, Some count -> x.GetLineRangeOrDefault (LineRangeSpecifier.WithEndCount (lineRange, count)) defaultLineRange
            | _, None -> x.GetLineRangeOrDefault lineRange defaultLineRange

    member x.GetLineRange lineRange =
        x.GetLineRangeOrDefault lineRange DefaultLineRange.None

    /// Get the count value or the default of 1
    member x.GetCountOrDefault count = Util.CountOrDefault count

    /// Get the point after the specified range or the the default
    member x.GetPointAfterOrDefault lineRange defaultLineRange =
        match lineRange with
        | LineRangeSpecifier.SingleLine (LineSpecifier.Number line) when line = 0 ->
            SnapshotUtil.GetStartPoint _textView.TextSnapshot |> Some
        | _ ->
            match x.GetLineRangeOrDefault lineRange defaultLineRange with
            | Some lineRange ->
                lineRange.EndIncludingLineBreak |> Some
            | None -> None

    /// Get the point after the specified range or the the default
    member x.GetPointAfter lineRange =
        x.GetPointAfterOrDefault lineRange DefaultLineRange.None

    /// Add the specified auto command to the list 
    member x.RunAddAutoCommand (autoCommandDefinition: AutoCommandDefinition) = 
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

    member x.RunCall (callInfo: CallInfo) = 
        _statusUtil.OnError (Resources.Interpreter_CallNotSupported callInfo.Name)

    /// Change the directory to the given value
    member x.RunChangeDirectory symbolicPath = 
        match symbolicPath with
        | [] -> 
            // On non-Unix systems the :cd commandshould print out the directory when
            // cd is given no options
            _statusUtil.OnStatus x.CurrentDirectory
        | _ ->
            let directoryPath = x.InterpretSymbolicPath symbolicPath
            let directoryPath = 
                if not (Path.IsPathRooted directoryPath) then
                    Path.GetFullPath(Path.Combine(_vimData.CurrentDirectory, directoryPath))
                else directoryPath
            
            if not (Directory.Exists directoryPath) then
                // Not a fan of this function but we need to emulate the Vim behavior here
                _statusUtil.OnError (Resources.Interpreter_CantFindDirectory directoryPath)
            else
                // Setting the global directory will clear out the local directory for the window
                _vimBuffer.CurrentDirectory <- None
                _vimData.CurrentDirectory <- directoryPath

    /// Change the local directory to the given value
    member x.RunChangeLocalDirectory symbolicPath = 
        match symbolicPath with
        | [] -> 
            // On non-Unix systems the :cd commandshould print out the directory when
            // cd is given no options
            _statusUtil.OnStatus x.CurrentDirectory
        | _ ->
            let directoryPath = x.InterpretSymbolicPath symbolicPath
            let directoryPath = 
                if not (Path.IsPathRooted directoryPath) then
                    Path.GetFullPath(Path.Combine(_vimData.CurrentDirectory, directoryPath))
                else directoryPath

            if not (Directory.Exists directoryPath) then
                // Not a fan of this function but we need to emulate the Vim behavior here
                _statusUtil.OnError (Resources.Interpreter_CantFindDirectory directoryPath)
            else
                // Setting the global directory will clear out the local directory for the window
                _vimBuffer.CurrentDirectory <- Some directoryPath

    member x.RunCopyOrMoveTo sourceLineRange destLineRange count transactionName editOperation = 

        x.RunWithLineRangeOrDefault sourceLineRange DefaultLineRange.CurrentLine (fun sourceLineRange ->

            // The :copy command allows the specification of a full range but for the destination
            // it will only be valid for single line specifiers.  
            let destLineSpec = 
                match destLineRange with 
                | LineRangeSpecifier.None -> None
                | LineRangeSpecifier.EntireBuffer -> None
                | LineRangeSpecifier.WithEndCount _ -> None
                | LineRangeSpecifier.Join _ -> None
                | LineRangeSpecifier.Range (left, _ , _) -> left |> Some
                | LineRangeSpecifier.SingleLine line -> 
                    // If a single line and a count is specified then we need to apply the count to
                    // the line
                    match count with
                    | Some count -> LineSpecifier.LineSpecifierWithAdjustment(line, count) |> Some
                    | _ -> line |> Some


            let destLineInfo = destLineSpec |> OptionUtil.map2 x.GetLineAndVimLineNumber

            match destLineInfo with
            | None ->
                 _statusUtil.OnError Resources.Common_InvalidAddress
            
            | Some (destLine, destLineNum) -> 

                let destPoint = 
                    if destLineNum = 0 then
                        // If the target line is vim line 0, the intent is to insert the text
                        // above the first line
                        destLine.Start
                    else 
                        destLine.EndIncludingLineBreak

                let text = 
                    if _textView.TextSnapshot = destPoint.Snapshot then
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
                    else
                        sourceLineRange.GetTextIncludingLineBreak()

                // Use an undo transaction so that the caret move and insert is a single
                // operation
                if _textView.TextSnapshot = destPoint.Snapshot then
                    _undoRedoOperations.EditWithUndoTransaction transactionName _textView (fun() -> editOperation sourceLineRange destPoint text)
                else
                    editOperation sourceLineRange destPoint text)

    /// Copy the text from the source address to the destination address
    member x.RunCopyTo sourceLineRange destLineRange count =
        x.RunCopyOrMoveTo sourceLineRange destLineRange count "CopyTo" (fun sourceLineRange destPoint text ->

            let destTextBuffer = destPoint.Snapshot.TextBuffer
            destTextBuffer.Insert(destPoint.Position, text) |> ignore
            x.MoveCaretToPositionOrStartOfLine destPoint.Position)

    /// Move the text from the source address to the destination address
    member x.RunMoveTo sourceLineRange destLineRange count =
        x.RunCopyOrMoveTo sourceLineRange destLineRange count "MoveTo" (fun sourceLineRange destPoint text ->

            // Perform the move.
            let destTextBuffer = destPoint.Snapshot.TextBuffer
            let sourceTextBuffer = sourceLineRange.Start.Snapshot.TextBuffer
            use edit = destTextBuffer.CreateEdit()
            edit.Insert(destPoint.Position, text) |> ignore
            if sourceLineRange.Start.Snapshot = destPoint.Snapshot then
                edit.Delete(sourceLineRange.ExtentIncludingLineBreak.Span) |> ignore
            else
                sourceTextBuffer.Delete(sourceLineRange.ExtentIncludingLineBreak.Span) |> ignore
            edit.Apply() |> ignore

            // If the destination belongs to the current text buffer, move the
            // caret.
            let newSnapshot = destTextBuffer.CurrentSnapshot
            if newSnapshot = _textView.TextSnapshot then

                // Translate the insertion point to the current snapshot and
                // move to it.
                match TrackingPointUtil.GetPointInSnapshot destPoint PointTrackingMode.Negative newSnapshot with
                | Some destPoint -> destPoint.Position
                | None -> destPoint.Position
                |> x.MoveCaretToPositionOrStartOfLine)

    /// Compose two line commands
    member x.RunCompose lineCommand1 lineCommand2 =
        use transaction = _undoRedoOperations.CreateLinkedUndoTransactionWithFlags "Compose Command" LinkedUndoTransactionFlags.CanBeEmpty
        x.RunLineCommand lineCommand1
        x.RunLineCommand lineCommand2
        transaction.Complete()

    /// Move caret to position or the first non-blank on line if 'startofline' is set
    member x.MoveCaretToPositionOrStartOfLine position =
        if _globalSettings.StartOfLine then
            SnapshotPoint(_textBuffer.CurrentSnapshot, position)
            |> SnapshotPointUtil.GetContainingLine
            |> SnapshotLineUtil.GetFirstNonBlankOrStart
            |> SnapshotPointUtil.GetPosition
        else
            position
        |> TextViewUtil.MoveCaretToPosition _textView

    /// Clear out the key map for the given modes
    member x.RunClearKeyMap keyRemapModes mapArgumentList = 
        if not (List.isEmpty mapArgumentList) then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "map special arguments")
        else
            keyRemapModes
            |> Seq.iter _keyMap.Clear

    /// Run the close command
    member x.RunClose hasBang = 
        if hasBang then
            _vimHost.Close _textView
        else
            _commonOperations.CloseWindowUnlessDirty()

    /// Run the delete command.  Delete the specified range of text and set it to 
    /// the given Register
    member x.RunDelete lineRange registerName = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.DeleteLines lineRange.StartLine lineRange.Count registerName)

    member x.RunDeleteMarkCore mark = 
        let markMap = _vimBufferData.Vim.MarkMap
        markMap.DeleteMark mark _vimBufferData |> ignore

    member x.RunDeleteMarks marks = 
        marks |> Seq.iter x.RunDeleteMarkCore

    member x.RunDeleteAllMarks () = 
        LocalMark.All
        |> Seq.filter (fun mark -> 
            match mark with 
            | LocalMark.Number _ -> false
            | _ -> true)
        |> Seq.map (fun localMark -> Mark.LocalMark localMark)
        |> Seq.iter x.RunDeleteMarkCore

    member x.RunFunction (func: Function) = 
        _statusUtil.OnError Resources.Interpreter_FunctionNotSupported

    /// Run the ':digraphs' command
    member x.RunDigraphs digraphList =
        let digraphMap = _vimBuffer.Vim.DigraphMap

        // Get the two characters that make up the digraph.
        let getChars char1 char2 =
            string(char1) + string(char2)

        // Get the display string for the replacement text.
        let getDisplay code =
            code
            |> DigraphUtil.GetText
            |> StringUtil.GetDisplayString

        if List.isEmpty digraphList then
            let columns = 4
            digraphMap.Mappings
            |> Seq.map (fun (char1, char2, code) ->
                (getChars char1 char2, getDisplay code, code))
            |> Seq.sortBy (fun (_, _, code) -> code)
            |> Seq.mapi (fun index (digraph, text, code) ->
                (index + 1, sprintf "%2s %-2s %5d  " digraph text code))
            |> Seq.map (fun (index, output) ->
                output + (if index % columns = 0 then System.Environment.NewLine else ""))
            |> String.concat ""
            |> _statusUtil.OnStatus
        else
            DigraphUtil.AddToMap _vimBuffer.Vim.DigraphMap digraphList

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
        let getKeyInputSetLine (keyInputSet: KeyInputSet) = 
            KeyNotationUtil.KeyInputSetToString keyInputSet

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

    /// Display the registers.  If a particular name is specified only display that register
    member x.RunDisplayRegisters nameList =

        // Convert the register value to a human-readable form.
        let normalizeDisplayString (registerValue: RegisterValue) =
            if registerValue.IsString then
                StringUtil.GetDisplayString registerValue.StringValue
            else
                registerValue.KeyInputs
                |> KeyInputSetUtil.OfList
                |> KeyNotationUtil.KeyInputSetToString

        let displayNames = 
            match nameList with
            | [] -> 
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
                    | RegisterName.ReadOnly ReadOnlyRegister.Colon -> true
                    | _ -> false)
            | _ -> nameList |> Seq.ofList

        // Build up the status string messages
        let lines = 
            displayNames 
            |> Seq.map (fun name -> 
                let register = _registerMap.GetRegister name
                match register.Name.Char, StringUtil.IsNullOrEmpty register.StringValue with
                | None, _ -> None
                | Some c, true -> None
                | Some c, false -> Some (c, normalizeDisplayString register.RegisterValue))
            |> SeqUtil.filterToSome
            |> Seq.map (fun (name, value) -> sprintf "\"%c   %s" name value)
        let lines = Seq.append (Seq.singleton Resources.CommandMode_RegisterBanner) lines
        _statusUtil.OnStatusLong lines

    member x.RunDisplayLets (names: VariableName list) = 
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

    /// Display the specified marks
    member x.RunDisplayMarks (marks: Mark list) =
        let printMarkInfo info =
            let ident, name, line, column = info
            sprintf " %c  %5d%5d %s" ident (line + 1) column name
        let getMark (mark: Mark) =
            match _markMap.GetMarkInfo mark _vimBufferData with
            | Some markInfo ->
                (markInfo.Ident, markInfo.Name, markInfo.Line, markInfo.Column)
                |> Some
            | None ->
                None

        seq {
            yield Mark.LastJump
            for letter in Letter.All do
                yield Mark.LocalMark (LocalMark.Letter letter)
            for letter in Letter.All do
                yield Mark.GlobalMark letter
            for number in NumberMark.All do
                yield Mark.LocalMark (LocalMark.Number number)
            yield Mark.LastExitedPosition
            yield Mark.LocalMark LocalMark.LastChangeOrYankStart
            yield Mark.LocalMark LocalMark.LastChangeOrYankEnd
            yield Mark.LocalMark LocalMark.LastInsertExit
            yield Mark.LocalMark LocalMark.LastEdit
            yield Mark.LocalMark LocalMark.LastSelectionStart
            yield Mark.LocalMark LocalMark.LastSelectionEnd
        }
        |> Seq.filter (fun mark -> marks.Length = 0 || List.contains mark marks)
        |> Seq.map getMark
        |> Seq.filter (fun option -> option.IsSome)
        |> Seq.map (fun option -> option.Value)
        |> Seq.map (fun info -> printMarkInfo info)
        |> Seq.append ("mark line  col file/text" |> Seq.singleton)
        |> _statusUtil.OnStatusLong

    /// Run the echo command
    member x.RunEcho expression =
        let value = x.RunExpression expression 
        let rec valueAsString value =
            match value with
            | VariableValue.Number number -> string number
            | VariableValue.String str -> str
            | VariableValue.List values ->
                let listItemAsString value =
                    let stringified = valueAsString value
                    match value with
                    | VariableValue.String str -> sprintf "'%s'" stringified
                    | _ -> stringified
                List.map listItemAsString values
                |> String.concat ", "
                |> sprintf "[%s]"
            | VariableValue.Dictionary _ -> "{}"
            | _ -> "<error>"
        _statusUtil.OnStatus <| valueAsString value
    
    /// Run the execute command
    member x.RunExecute expression =
        let parser = Parser(_globalSettings, _vimData)
        let execute str =
            parser.ParseLineCommand str |> x.RunLineCommand
        match x.RunExpression expression  with
        | VariableValue.Number number -> execute (string number)
        | VariableValue.String str -> execute str
        | _ -> _statusUtil.OnStatus "Error executing expression"

    /// Edit the specified file
    member x.RunEdit hasBang fileOptions commandOption symbolicPath =
        let filePath = x.InterpretSymbolicPath symbolicPath
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
                if not (_vimHost.Reload _textView) then
                    _commonOperations.Beep()
                else
                    match TrackingPointUtil.GetPoint _textView.TextSnapshot caret with
                    | None -> ()
                    | Some point -> _commonOperations.MoveCaretToPoint point ViewFlags.Standard

        elif not hasBang && _vimHost.IsDirty _textBuffer then
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            let resolvedFilePath = x.ResolveVimPath filePath
            _vimHost.LoadFileIntoExistingWindow resolvedFilePath _textView |> ignore

    /// Get the value of the specified expression 
    member x.RunExpression expr =
        _exprInterpreter.RunExpression expr

    /// Evaluate the text as an expression and return its value
    member x.EvaluateExpression (text: string) =
        let parser = Parser(_globalSettings, _vimData)
        match parser.ParseExpression(text) with
        | ParseResult.Succeeded expression ->
            _exprInterpreter.RunExpression expression |> EvaluateResult.Succeeded
        | ParseResult.Failed message ->
            EvaluateResult.Failed message

    /// Print out the applicable file history information
    member x.RunFiles () = 
        let output = List<string>()
        output.Add("      # file history")

        let historyList = _vimData.FileHistory
        for i = 0 to historyList.Count - 1 do
            let item = historyList.Items.[i]
            let msg = sprintf "%7d %s" i item
            output.Add(msg)

        _statusUtil.OnStatusLong(output)

    /// Fold the specified line range
    member x.RunFold lineRange = 

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            if lineRange.Count > 1 then
                _foldManager.CreateFold lineRange)

    member x.RunNormal (lineRange: LineRangeSpecifier) input =
        let transactionMap = System.Collections.Generic.Dictionary<IVimBuffer, ILinkedUndoTransaction>();
        let modeSwitchMap = System.Collections.Generic.Dictionary<IVimBuffer, IVimBuffer>();
        try
            let rec inner list = 
                match list with 
                | [] -> 
                    // No more input so we are finished
                    true
                | keyInput :: tail -> 

                    // Prefer the focussed IVimBuffer over the current.  It's possible for the 
                    // macro playback switch the active buffer via gt, gT, etc ... and playback 
                    // should continue on the newly focussed IVimBuffer.  Should the host API
                    // fail to return an active IVimBuffer continue using the original one
                    let buffer = 
                        match _vim.FocusedBuffer with
                        | Some buffer -> buffer
                        | None -> _vimBuffer

                    // Make sure we have an IUndoTransaction open in the ITextBuffer
                    if not (transactionMap.ContainsKey(buffer)) then
                        let transaction = _undoRedoOperations.CreateLinkedUndoTransactionWithFlags "Normal Command" LinkedUndoTransactionFlags.CanBeEmpty
                        transactionMap.Add(buffer, transaction)

                    if not (modeSwitchMap.ContainsKey(buffer)) then
                        buffer.SwitchMode ModeKind.Normal ModeArgument.None |> ignore
                        modeSwitchMap.Add(buffer, buffer)

                    // Actually run the KeyInput.  If processing the KeyInput value results
                    // in an error then we should stop processing the macro
                    match buffer.Process keyInput with
                    | ProcessResult.Handled _ -> inner tail
                    | ProcessResult.HandledNeedMoreInput -> inner tail
                    | ProcessResult.NotHandled -> false
                    | ProcessResult.Error -> false

            let revertModes () =
                modeSwitchMap.Values |> Seq.iter (fun buffer ->
                    buffer.SwitchPreviousMode() |> ignore
                )
                modeSwitchMap.Clear()

            match x.GetLineRange lineRange  with
            | None ->
                try
                    inner input |> ignore
                finally
                    revertModes ()
            | _ ->
                x.RunWithLineRange lineRange (fun lineRange ->
                    // Each command we run can, and often will, change the underlying buffer whcih
                    // will change the current ITextSnapshot.  Run one pass to get the line numbers
                    // and then a second to edit the commands
                    let lineNumbers = 
                        lineRange.Lines
                        |> Seq.map (fun snapshotLine ->
                            let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset snapshotLine.Start
                            _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default)
                        |> List.ofSeq

                    // Now perform the command for every line.  Make sure to map forward to the 
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
                            try
                                inner input |> ignore
                            finally
                                revertModes ()
                        )
    
                    // Now close all of the ITrackingLineColumn values so that they stop taking up resources
                    lineNumbers |> List.iter (fun trackingLineColumn -> trackingLineColumn.Close())
                )

        finally
            transactionMap.Values |> Seq.iter (fun transaction ->
                transaction.Dispose()
            )


    /// Run the global command.  
    member x.RunGlobal lineRange pattern matchPattern lineCommand =

        let pattern = 
            if StringUtil.IsNullOrEmpty pattern then _vimData.LastSearchData.Pattern
            else
                _vimData.LastSearchData <- SearchData(pattern, SearchPath.Forward)
                pattern

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->
            let options = VimRegexFactory.CreateRegexOptions _globalSettings
            match VimRegexFactory.Create pattern options with
            | None -> _statusUtil.OnError Resources.Interpreter_Error
            | Some regex ->
    
                // All of the edits should behave as a single vim undo.  Can't do this as a single
                // global undo as it executes as series of sub commands which create their own 
                // global undo units
                use transaction = _undoRedoOperations.CreateLinkedUndoTransactionWithFlags "Global Command" LinkedUndoTransactionFlags.CanBeEmpty
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
                            let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset snapshotLine.Start
                            _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default)
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
                    transaction.Complete())

    /// Go to the first tab
    member x.RunGoToFirstTab() =
        _commonOperations.GoToTab 0

    /// Go to the last tab
    member x.RunGoToLastTab() =
        _commonOperations.GoToTab _vimHost.TabCount

    /// Go to the next "count" tab 
    member x.RunGoToNextTab count = 
        let count = x.GetCountOrDefault count
        _commonOperations.GoToNextTab SearchPath.Forward count

    /// Go to the previous "count" tab 
    member x.RunGoToPreviousTab count = 
        let count = x.GetCountOrDefault count
        _commonOperations.GoToNextTab SearchPath.Backward count

    member x.RunHelp () = 
        _statusUtil.OnStatus "For help on VsVim, please visit the Wiki page (https://github.com/jaredpar/VsVim/wiki)"

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

    /// Run the if command
    member x.RunIf (conditionalBlockList: ConditionalBlock list)  =
        let shouldRun (conditionalBlock: ConditionalBlock) =
            match conditionalBlock.Conditional with
            | None -> true
            | Some expr -> 
                match _exprInterpreter.GetExpressionAsNumber expr with
                | None -> false
                | Some value -> value <> 0

        match List.tryFind shouldRun conditionalBlockList with
        | None -> ()
        | Some conditionalBlock -> conditionalBlock.LineCommands |> Seq.iter (fun lineCommand -> x.RunLineCommand lineCommand |> ignore)

    /// Join the lines in the specified range
    member x.RunJoin lineRange joinKind =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.Join lineRange joinKind)

    /// Jump to the last line of the specified line range
    member x.RunJumpToLastLine lineRange = 
        x.RunWithLooseLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->

            // Make sure we jump to the first non-blank on this line
            let point = 
                lineRange.LastLine
                |> SnapshotLineUtil.GetFirstNonBlankOrEnd

            _commonOperations.MoveCaretToPoint point (ViewFlags.Standard &&& (~~~ViewFlags.TextExpanded)))

    /// Run the let command
    member x.RunLet (name: VariableName) expr =
        // TODO: At this point we are treating all variables as if they were global.  Need to 
        // take into account the NameScope at this level too
        match x.RunExpression expr with
        | VariableValue.Error -> _statusUtil.OnError Resources.Interpreter_Error
        | value -> _variableMap.[name.Name] <- value

    /// Run the let command for registers
    member x.RunLetRegister (name: RegisterName) expr =
        let setRegister (value: string) =
            let registerValue = RegisterValue(value, OperationKind.CharacterWise)
            _commonOperations.SetRegisterValue (Some name) RegisterOperation.Yank registerValue
        match _exprInterpreter.GetExpressionAsString expr with
        | Some value -> setRegister value
        | None -> ()

    /// Run the host make command 
    member x.RunMake hasBang arguments = 
        _vimHost.Make (not hasBang) arguments 

    /// Run the map keys command
    member x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap mapArgumentList =

        // At this point we can parse out all of the key mapping options, we just don't support
        // any of them.  Warn the developer but continue processing 
        for mapArgument in mapArgumentList do
            let name = sprintf "%A" mapArgument
            _statusUtil.OnWarning (Resources.Interpreter_KeyMappingOptionNotSupported name)

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

    /// Run the 'nohlsearch' command.  Temporarily disables highlighitng in the buffer
    member x.RunNoHighlightSearch() = 
        _vimData.SuspendDisplayPattern()

    member x.RunParseError msg =
        _statusUtil.OnError msg

    /// Print out the contents of the specified range
    member x.RunDisplayLines lineRange lineCommandFlags =

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->

            // Leave enough room to right justify any line number, with a
            // minimum of three digits to mimic vim.
            let snapshot = SnapshotLineUtil.GetSnapshot lineRange.StartLine
            let lastLineNumber = SnapshotUtil.GetLastNormalizedLineNumber snapshot
            let digits = max 3 (int(floor(log10(double(lastLineNumber + 1)))) + 1)

            let hasListFlag = Util.IsFlagSet lineCommandFlags LineCommandFlags.List
            let addLineNumber = Util.IsFlagSet lineCommandFlags LineCommandFlags.AddLineNumber

            // List line with control characters encoded.
            let listLine (line: ITextSnapshotLine) =
                line
                |> SnapshotLineUtil.GetText
                |> (fun text -> (StringUtil.GetDisplayString text) + "$")

            // Print line with tabs expanded.
            let printLine (line: ITextSnapshotLine) =
                line
                |> SnapshotLineUtil.GetText
                |> (fun text -> StringUtil.ExpandTabsForColumn text 0 _localSettings.TabStop)

            // Print or list line.
            let printOrListLine (line: ITextSnapshotLine) =
                line
                |> if hasListFlag then listLine else printLine

            // Display line with leading line number
            let formatLineNumberAndLine (line: ITextSnapshotLine) =
                line
                |> printOrListLine
                |> sprintf "%*d %s" digits (line.LineNumber + 1)

            let formatLine (line: ITextSnapshotLine) =
                if addLineNumber || _localSettings.Number then
                    formatLineNumberAndLine line
                else
                    printOrListLine line

            lineRange.Lines
            |> Seq.map formatLine
            |> _statusUtil.OnStatusLong)

    /// Print out the current directory
    member x.RunPrintCurrentDirectory() =
        _statusUtil.OnStatus x.CurrentDirectory

    /// Put the register after the last line in the given range
    member x.RunPut lineRange registerName putAfter = 

        let register = _commonOperations.GetRegister registerName
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
                _commonOperations.MoveCaretToPoint point ViewFlags.VirtualEdit))

    member x.RunOpenQuickFixWindow () =
        _vimHost.OpenQuickFixWindow()

    member x.RunQuickFixNext count hasBang =
        let count = OptionUtil.getOrDefault 1 count 
        _vimHost.GoToQuickFix QuickFix.Next count hasBang |> ignore

    member x.RunQuickFixPrevious count hasBang =
        let count = OptionUtil.getOrDefault 1 count 
        _vimHost.GoToQuickFix QuickFix.Previous count hasBang |> ignore

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

    member x.RunQuitWithWrite lineRange hasBang fileOptions filePath = 

        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->
            if not (List.isEmpty fileOptions) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
            else
    
                match filePath with
                | None -> _vimHost.Save _textView.TextBuffer |> ignore  
                | Some filePath ->
                    let filePath = x.ResolveVimPath filePath
                    _vimHost.SaveTextAs (lineRange.GetTextIncludingLineBreak()) filePath |> ignore
    
                _commonOperations.CloseWindowUnlessDirty())

    /// Run the core parts of the read command
    member x.RunReadCore (point: SnapshotPoint) (lines: string[]) = 
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
        x.ExecuteCommand lineRange command true

    /// Run the read file command.
    member x.RunReadFile lineRange fileOptionList filePath =
        let filePath = x.ResolveVimPath filePath
        x.RunWithPointAfterOrDefault lineRange DefaultLineRange.CurrentLine (fun point ->
            if not (List.isEmpty fileOptionList) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
            else
                match _fileSystem.ReadAllLines filePath with
                | None ->
                    _statusUtil.OnError (Resources.Interpreter_CantOpenFile filePath)
                | Some lines ->
                    x.RunReadCore point lines)

    /// Run a single redo operation
    member x.RunRedo() = 
        _commonOperations.Redo 1

    /// Remove the auto commands which match the specified definition
    member x.RemoveAutoCommands (autoCommandDefinition: AutoCommandDefinition) = 
        let isMatch (autoCommand: AutoCommand) = 
            
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
                let rec nextPoint (point: SnapshotPoint) = 
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
                            |> SnapshotSpanUtil.GetPoints SearchPath.Forward
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
                            |> SnapshotSpanUtil.GetPoints SearchPath.Forward
                            |> SeqUtil.any (SnapshotPointUtil.IsChar '\t')
                        hasTab)
    
            // Now that we have the set of spans perform the edit
            use edit = _textBuffer.CreateEdit()
            for span in spans do
                let oldText = span.GetText()
                let newText = _commonOperations.NormalizeBlanks oldText
                edit.Replace(span.Span, newText) |> ignore
    
            edit.Apply() |> ignore)

    /// Run the search command in the given direction
    member x.RunSearch lineRange path pattern = 
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            let pattern = 
                if StringUtil.IsNullOrEmpty pattern then _vimData.LastSearchData.Pattern
                else pattern
    
            // The search start after the end of the specified line range or
            // before its beginning.
            let startPoint =
                match path with
                | SearchPath.Forward ->
                    lineRange.End
                | SearchPath.Backward ->
                    lineRange.Start

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
            | SearchResult.Cancelled _ -> ()
            | SearchResult.Error _ -> ())

    /// Run the :set command.  Process each of the arguments 
    member x.RunSet setArguments =

        // Get the setting for the specified name
        let withSetting name msg (func: Setting -> IVimSettings -> unit) =
            match _localSettings.GetSetting name with
            | None -> 
                match _windowSettings.GetSetting name with
                | None -> _statusUtil.OnError (Resources.Interpreter_UnknownOption name)
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
            let allSettings = 
                _localSettings.Settings
                |> Seq.append _windowSettings.Settings
                |> Seq.append _globalSettings.Settings

            allSettings
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

    /// Run the ':shell' command
    member x.RunShell () =
        let shell = _globalSettings.Shell
        let workingDirectory = _vimBufferData.WorkingDirectory
        _vimHost.StartShell workingDirectory shell ""

    /// Run the ':!' command
    member x.RunShellCommand lineRange command =
        x.ExecuteCommand lineRange command false

    /// Execute the external command
    member x.ExecuteCommand (lineRange: LineRangeSpecifier) (command: string) (isReadCommand: bool) =

        // Actually run the command.
        let doRun (command: string) = 

            // Save the last shell command for repeats.
            _vimData.LastShellCommand <- Some command

            // Prepend the shell flag before the other arguments.
            let workingDirectory = _vimBufferData.WorkingDirectory
            let shell = _globalSettings.Shell
            let command =
                if _globalSettings.ShellFlag.Length > 0 then
                    sprintf "%s %s" _globalSettings.ShellFlag command
                else
                    command

            if isReadCommand then
                x.RunWithPointAfterOrDefault lineRange DefaultLineRange.CurrentLine (fun point ->
                    let results = _vimHost.RunCommand workingDirectory shell command StringUtil.Empty
                    let status = results.Error
                    let status = EditUtil.RemoveEndingNewLine status
                    _statusUtil.OnStatus status
                    let output = EditUtil.SplitLines results.Output
                    x.RunReadCore point output)
            else
                if lineRange = LineRangeSpecifier.None then
                    let results = _vimHost.RunCommand workingDirectory shell command StringUtil.Empty
                    let status = results.Output + results.Error
                    let status = EditUtil.RemoveEndingNewLine status
                    _statusUtil.OnStatus status
                else
                    x.RunWithLineRangeOrDefault lineRange DefaultLineRange.None (fun lineRange ->
                        _commonOperations.FilterLines lineRange command)

        // Build up the actual command replacing any non-escaped ! with the previous
        // shell command
        let builder = System.Text.StringBuilder()

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
                elif current = '!' && not afterBackslash then
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

    /// Shift the given line range to the left
    member x.RunShiftLeft lineRange count =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.ShiftLineRangeLeft lineRange count)

    /// Shift the given line range to the right
    member x.RunShiftRight lineRange count =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->
            _commonOperations.ShiftLineRangeRight lineRange count)

    /// Run the :sort command
    member x.RunSort lineRange reverseOrder flags pattern =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->
            _commonOperations.SortLines lineRange reverseOrder flags pattern)

    /// Run the :source command
    member x.RunSource hasBang filePath =
        if hasBang then
            _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "!")
        else
            let filePath = x.ResolveVimPath filePath
            match _fileSystem.ReadAllLines filePath with
            | None -> _statusUtil.OnError (Resources.CommandMode_CouldNotOpenFile filePath)
            | Some lines -> x.RunScript lines

    /// Run the :stopinsert command
    member x.RunStopInsert () =
        if _vimBuffer.ModeKind = ModeKind.Insert then
            _vimBuffer.SwitchMode ModeKind.Normal ModeArgument.None |> ignore

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
            behavior _textView 
        else
            ()

    /// Run the substitute command. 
    member x.RunSubstitute lineRange pattern replace flags =

        // Called to initialize the data and move to a confirm style substitution.  Have to find the first match
        // before passing off to confirm
        let setupConfirmSubstitute (range: SnapshotLineRange) (data: SubstituteData) =
            let regex = VimRegexFactory.CreateForSubstituteFlags data.SearchPattern _globalSettings data.Flags
            match regex with
            | None -> 
                _statusUtil.OnError (Resources.Common_PatternNotFound data.SearchPattern)
            | Some regex -> 
                let firstMatch = 
                    range.Lines
                    |> Seq.map (fun line -> line.ExtentIncludingLineBreak)
                    |> Seq.tryPick (fun span -> RegexUtil.MatchSpan span regex.Regex)
                match firstMatch with
                | None -> _statusUtil.OnError (Resources.Common_PatternNotFound data.SearchPattern)
                | Some(span,_) ->
                    let arg = ModeArgument.Substitute (span, range, data)
                    _vimBuffer.SwitchMode ModeKind.SubstituteConfirm arg |> ignore

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

                // If confirming, record the last patterns now.
                _vimData.LastSubstituteData <- Some data
                _vimData.LastSearchData <- SearchData(pattern, SearchPath.Forward)
            else
                _commonOperations.Substitute pattern replace lineRange flags)

    /// Run substitute using the pattern and replace values from the last substitute
    member x.RunSubstituteRepeatLast lineRange flags = 
        let pattern, replace = 
            match _vimData.LastSubstituteData with
            | None -> "", ""
            | Some substituteData -> substituteData.SearchPattern, substituteData.Substitute
        x.RunSubstitute lineRange pattern replace flags 

    member x.RunTabNew symbolicPath = 
        let filePath = x.InterpretSymbolicPath symbolicPath
        let resolvedFilePath = x.ResolveVimPath filePath
        _vimHost.LoadFileIntoNewWindow resolvedFilePath (Some 0) None |> ignore

    member x.RunOnly() =
        _vimHost.CloseAllOtherWindows _textView

    member x.RunTabOnly() =
        _vimHost.CloseAllOtherTabs _textView

    /// Run the undo command
    member x.RunUndo() =
        _commonOperations.Undo 1

    /// Run the unlet command
    member x.RunUnlet ignoreMissing nameList = 
        let rec func nameList = 
            match nameList with
            | [] -> ()
            | name :: rest ->
                let removed = _variableMap.Remove(name)
                if not removed && not ignoreMissing then
                    let msg = Resources.Interpreter_NoSuchVariable name
                    _statusUtil.OnError msg
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

    member x.RunVersion() = 
        let msg = sprintf "VsVim Version %s" VimConstants.VersionNumber
        _statusUtil.OnStatus msg

    member x.RunHostCommand command argument =
        _vimHost.RunHostCommand _textView command argument

    member x.RunWrite lineRange hasBang fileOptionList filePath =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.EntireBuffer (fun lineRange ->

            let filePath =
                match filePath with
                | Some filePath -> Some (x.ResolveVimPath filePath)
                | None -> None
            if not (List.isEmpty fileOptionList) then
                _statusUtil.OnError (Resources.Interpreter_OptionNotSupported "[++opt]")
            else
                match filePath with
                | Some filePath -> 
                    let text = lineRange.ExtentIncludingLineBreak.GetText()
                    _vimHost.SaveTextAs text filePath |> ignore
                | None ->
                    if not hasBang && _vimHost.IsReadOnly _textBuffer then
                        _statusUtil.OnError Resources.Interpreter_ReadOnlyOptionIsSet
                    else
                        _vimHost.Save _textBuffer |> ignore)

    /// Run the 'wall' or 'wqall' command
    member x.RunWriteAll hasBang andQuit =

        // Write a buffer and return success or failure.
        let writeDirtyBuffer (vimBuffer: IVimBuffer) =
            if not hasBang && _vimHost.IsReadOnly vimBuffer.TextBuffer then
                _statusUtil.OnError Resources.Interpreter_ReadOnlyOptionIsSet
                false
            else
                _vimHost.Save vimBuffer.TextBuffer

        // Try to write all dirty buffers.
        let succeeded =
            _vim.VimBuffers
            |> Seq.filter (fun vimBuffer -> _vimHost.IsDirty vimBuffer.TextBuffer)
            |> Seq.map writeDirtyBuffer
            |> Seq.filter (fun succeeded -> not succeeded)
            |> Seq.isEmpty

        if andQuit then
            if succeeded then 
                _vimHost.Quit()
            else
                _statusUtil.OnError Resources.Common_NoWriteSinceLastChange

    /// Yank the specified line range into the register.  This is done in a 
    /// linewise fashion
    member x.RunYank registerName lineRange count =
        x.RunWithLineRangeOrDefault lineRange DefaultLineRange.CurrentLine (fun lineRange ->

            // If the user specified a count then that count is applied to the end
            // of the specified line range
            let lineRange = 
                match count with
                | None -> lineRange
                | Some count -> SnapshotLineRangeUtil.CreateForLineAndMaxCount lineRange.LastLine count

            let stringData = StringData.OfSpan lineRange.ExtentIncludingLineBreak
            let value = _commonOperations.CreateRegisterValue x.CaretPoint stringData OperationKind.LineWise
            _commonOperations.SetRegisterValue registerName RegisterOperation.Yank value)

    /// Run the specified LineCommand
    member x.RunLineCommand lineCommand = 
        let cantRun () = _statusUtil.OnError Resources.Interpreter_Error

        match lineCommand with
        | LineCommand.AddAutoCommand autoCommandDefinition -> x.RunAddAutoCommand autoCommandDefinition
        | LineCommand.Behave model -> x.RunBehave model
        | LineCommand.Call callInfo -> x.RunCall callInfo
        | LineCommand.ChangeDirectory path -> x.RunChangeDirectory path
        | LineCommand.ChangeLocalDirectory path -> x.RunChangeLocalDirectory path
        | LineCommand.Compose (lineCommand1, lineCommand2) -> x.RunCompose lineCommand1 lineCommand2
        | LineCommand.CopyTo (sourceLineRange, destLineRange, count) -> x.RunCopyTo sourceLineRange destLineRange count
        | LineCommand.ClearKeyMap (keyRemapModes, mapArgumentList) -> x.RunClearKeyMap keyRemapModes mapArgumentList
        | LineCommand.Close hasBang -> x.RunClose hasBang
        | LineCommand.Delete (lineRange, registerName) -> x.RunDelete lineRange registerName
        | LineCommand.DeleteMarks marks -> x.RunDeleteMarks marks
        | LineCommand.DeleteAllMarks -> x.RunDeleteAllMarks()
        | LineCommand.Echo expression -> x.RunEcho expression
        | LineCommand.Edit (hasBang, fileOptions, commandOption, filePath) -> x.RunEdit hasBang fileOptions commandOption filePath
        | LineCommand.Else -> cantRun ()
        | LineCommand.ElseIf _ -> cantRun ()
        | LineCommand.Execute expression -> x.RunExecute expression
        | LineCommand.Function func -> x.RunFunction func
        | LineCommand.FunctionStart _ -> cantRun ()
        | LineCommand.FunctionEnd _ -> cantRun ()
        | LineCommand.Digraphs digraphList -> x.RunDigraphs digraphList
        | LineCommand.DisplayKeyMap (keyRemapModes, keyNotationOption) -> x.RunDisplayKeyMap keyRemapModes keyNotationOption
        | LineCommand.DisplayRegisters nameList -> x.RunDisplayRegisters nameList
        | LineCommand.DisplayLet variables -> x.RunDisplayLets variables
        | LineCommand.DisplayMarks marks -> x.RunDisplayMarks marks
        | LineCommand.Files -> x.RunFiles()
        | LineCommand.Fold lineRange -> x.RunFold lineRange
        | LineCommand.Global (lineRange, pattern, matchPattern, lineCommand) -> x.RunGlobal lineRange pattern matchPattern lineCommand
        | LineCommand.Help -> x.RunHelp()
        | LineCommand.History -> x.RunHistory()
        | LineCommand.IfStart _ -> cantRun ()
        | LineCommand.IfEnd -> cantRun ()
        | LineCommand.If conditionalBlock -> x.RunIf conditionalBlock
        | LineCommand.GoToFirstTab -> x.RunGoToFirstTab()
        | LineCommand.GoToLastTab -> x.RunGoToLastTab()
        | LineCommand.GoToNextTab count -> x.RunGoToNextTab count
        | LineCommand.GoToPreviousTab count -> x.RunGoToPreviousTab count
        | LineCommand.HorizontalSplit (lineRange, fileOptions, commandOptions) -> x.RunSplit _vimHost.SplitViewHorizontally fileOptions commandOptions
        | LineCommand.HostCommand (command, argument) -> x.RunHostCommand command argument
        | LineCommand.Join (lineRange, joinKind) -> x.RunJoin lineRange joinKind
        | LineCommand.JumpToLastLine lineRange -> x.RunJumpToLastLine lineRange
        | LineCommand.Let (name, value) -> x.RunLet name value
        | LineCommand.LetRegister (name, value) -> x.RunLetRegister name value
        | LineCommand.Make (hasBang, arguments) -> x.RunMake hasBang arguments
        | LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList) -> x.RunMapKeys leftKeyNotation rightKeyNotation keyRemapModes allowRemap mapArgumentList
        | LineCommand.MoveTo (sourceLineRange, destLineRange, count) -> x.RunMoveTo sourceLineRange destLineRange count
        | LineCommand.NoHighlightSearch -> x.RunNoHighlightSearch()
        | LineCommand.Nop -> ()
        | LineCommand.Normal (lineRange, command) -> x.RunNormal lineRange command
        | LineCommand.Only -> x.RunOnly()
        | LineCommand.ParseError msg -> x.RunParseError msg
        | LineCommand.DisplayLines (lineRange, lineCommandFlags) -> x.RunDisplayLines lineRange lineCommandFlags
        | LineCommand.PrintCurrentDirectory -> x.RunPrintCurrentDirectory()
        | LineCommand.PutAfter (lineRange, registerName) -> x.RunPut lineRange registerName true
        | LineCommand.PutBefore (lineRange, registerName) -> x.RunPut lineRange registerName false
        | LineCommand.QuickFixWindow -> x.RunOpenQuickFixWindow()
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
        | LineCommand.Shell -> x.RunShell()
        | LineCommand.ShellCommand (lineRange, command) -> x.RunShellCommand lineRange command
        | LineCommand.ShiftLeft (lineRange, count) -> x.RunShiftLeft lineRange count
        | LineCommand.ShiftRight (lineRange, count) -> x.RunShiftRight lineRange count
        | LineCommand.Sort (lineRange, hasBang, flags, pattern) -> x.RunSort lineRange hasBang flags pattern
        | LineCommand.Source (hasBang, filePath) -> x.RunSource hasBang filePath
        | LineCommand.StopInsert -> x.RunStopInsert()
        | LineCommand.Substitute (lineRange, pattern, replace, flags) -> x.RunSubstitute lineRange pattern replace flags
        | LineCommand.SubstituteRepeat (lineRange, substituteFlags) -> x.RunSubstituteRepeatLast lineRange substituteFlags
        | LineCommand.TabNew filePath -> x.RunTabNew filePath
        | LineCommand.TabOnly -> x.RunTabOnly()
        | LineCommand.Undo -> x.RunUndo()
        | LineCommand.Unlet (ignoreMissing, nameList) -> x.RunUnlet ignoreMissing nameList
        | LineCommand.UnmapKeys (keyNotation, keyRemapModes, mapArgumentList) -> x.RunUnmapKeys keyNotation keyRemapModes mapArgumentList
        | LineCommand.Version -> x.RunVersion()
        | LineCommand.VerticalSplit (lineRange, fileOptions, commandOptions) -> x.RunSplit _vimHost.SplitViewVertically fileOptions commandOptions
        | LineCommand.Write (lineRange, hasBang, fileOptionList, filePath) -> x.RunWrite lineRange hasBang fileOptionList filePath
        | LineCommand.WriteAll (hasBang, andQuit) -> x.RunWriteAll hasBang andQuit
        | LineCommand.Yank (lineRange, registerName, count) -> x.RunYank registerName lineRange count

    member x.RunWithLineRange lineRangeSpecifier (func: SnapshotLineRange -> unit) = 
        x.RunWithLineRangeOrDefault lineRangeSpecifier DefaultLineRange.None func

    member x.RunWithLineRangeOrDefault (lineRangeSpecifier: LineRangeSpecifier) defaultLineRange (func: SnapshotLineRange -> unit) = 
        match x.GetLineRangeOrDefault lineRangeSpecifier defaultLineRange with
        | None -> _statusUtil.OnError Resources.Range_Invalid
        | Some lineRange -> func lineRange

    /// Convert a loose line range specifier into a line range and run the
    /// specified function on it
    member x.RunWithLooseLineRangeOrDefault (lineRangeSpecifier: LineRangeSpecifier) defaultLineRange (func: SnapshotLineRange -> unit) = 
        let strictLineRangeSpecifier = x.GetStrictLineRangeSpecifier lineRangeSpecifier
        x.RunWithLineRangeOrDefault strictLineRangeSpecifier defaultLineRange func

    /// Get a strict line range specifier from a loose one
    member x.GetStrictLineRangeSpecifier looseLineRangeSpecifier =
        match looseLineRangeSpecifier with
        | LineRangeSpecifier.SingleLine looseLineSpecifier ->
            let strictLineSpecifier = x.GetStrictLineSpecifier looseLineSpecifier
            LineRangeSpecifier.SingleLine strictLineSpecifier
        | LineRangeSpecifier.Range (looseStartLineSpecifier, looseEndLineSpecifier, moveCaret) ->
            let strictStartLineSpecifier = x.GetStrictLineSpecifier looseStartLineSpecifier
            let strictEndLineSpecifier = x.GetStrictLineSpecifier looseEndLineSpecifier
            LineRangeSpecifier.Range (strictStartLineSpecifier, strictEndLineSpecifier, moveCaret)
        | otherLineRangeSpecifier ->
            otherLineRangeSpecifier

    /// Get a strict line specifier from a loose one (a loose line specifier
    /// tolerates line number that are too large)
    member x.GetStrictLineSpecifier looseLineSpecifier =
        match looseLineSpecifier with
        | LineSpecifier.Number number ->
            let lastLineNumber = SnapshotUtil.GetLastNormalizedLineNumber x.CurrentSnapshot
            LineSpecifier.Number (min number (lastLineNumber + 1))
        | otherLineSpecifier  ->
            otherLineSpecifier

    member x.RunWithPointAfterOrDefault (lineRangeSpecifier: LineRangeSpecifier) defaultLineRange (func: SnapshotPoint -> unit) = 
        match x.GetPointAfterOrDefault lineRangeSpecifier defaultLineRange with
        | None -> _statusUtil.OnError Resources.Range_Invalid
        | Some point -> func point

    // Actually parse and run all of the commands which are included in the script
    member x.RunScript lines = 
        let parser = Parser(_globalSettings, _vimData, lines)
        while not parser.IsDone do
            let lineCommand = parser.ParseNextCommand()
            x.RunLineCommand lineCommand |> ignore
   
    member x.InterpretSymbolicPath (symbolicPath: SymbolicPath) =
        let rec inner (sb:System.Text.StringBuilder) sp =
            match sp with
            | SymbolicPathComponent.Literal literal::tail -> 
                sb.AppendString literal
                inner sb tail
            | SymbolicPathComponent.CurrentFileName modifiers::tail -> 
                let path = 
                    match modifiers with
                    | FileNameModifier.PathFull::tail -> x.ApplyFileNameModifiers _vimBufferData.CurrentFilePath tail
                    | _ -> x.ApplyFileNameModifiers _vimBufferData.CurrentRelativeFilePath modifiers
                path |> sb.AppendString
                inner sb tail
            | SymbolicPathComponent.AlternateFileName (n, modifiers)::tail ->
                let fileHistory = _vimData.FileHistory
                let fileName = if n >= fileHistory.Count then None else Some fileHistory.Items.[n]
                let path =
                    match modifiers with
                    | FileNameModifier.PathFull::tail -> x.ApplyFileNameModifiers fileName tail
                    | _ -> 
                        let relativeFileName = 
                            match fileName with
                            | None -> None
                            | Some filePath ->
                                SystemUtil.StripPathPrefix x.CurrentDirectory filePath |> Some
                        x.ApplyFileNameModifiers relativeFileName modifiers
                path |> sb.AppendString
                inner sb tail
            | [] -> ()

        let builder = System.Text.StringBuilder()
        inner builder symbolicPath
        builder.ToString()

    member x.ApplyFileNameModifiers path modifiers : string =
        let rec inner path modifiers : string =
            match path with
            | "" -> ""
            | _ ->
                match modifiers with
                | FileNameModifier.Head::tail -> 
                    match Path.GetDirectoryName path with
                    | "" -> "."
                    | d -> inner d tail
                | FileNameModifier.Tail::tail -> inner (Path.GetFileName path) tail
                | FileNameModifier.Root::tail when path.StartsWith(".") -> inner path tail
                | FileNameModifier.Root::tail -> 
                    let s = path.Substring(0, path.Length - (Path.GetExtension path).Length)
                    inner s tail
                | FileNameModifier.Extension::_ when path.StartsWith(".") -> ""
                | FileNameModifier.Extension::tail ->
                    let tailNew = List.skipWhile (fun m -> m = FileNameModifier.Extension) tail
                    let count = 1 + (tail.Length - tailNew.Length)
                    let exts = Array.tail ((Path.GetFileName path).Split('.'))
                    let ext = Array.skip (exts.Length - count) exts |> String.concat "."
                    inner ext tailNew
                | _ -> path
        match path with
        | None -> ""
        | Some path -> inner path modifiers

    interface IVimInterpreter with
        member x.GetLine lineSpecifier = x.GetLine lineSpecifier
        member x.GetLineRange lineRange = x.GetLineRange lineRange
        member x.RunLineCommand lineCommand = x.RunLineCommand lineCommand
        member x.RunExpression expression = x.RunExpression expression
        member x.EvaluateExpression text = x.EvaluateExpression text
        member x.RunScript lines = x.RunScript lines

[<Export(typeof<IVimInterpreterFactory>)>]
type VimInterpreterFactory
    [<ImportingConstructor>]
    (
        _commonOperationsFactory: ICommonOperationsFactory,
        _foldManagerFactory: IFoldManagerFactory,
        _bufferTrackingService: IBufferTrackingService
    ) = 

    member x.CreateVimInterpreter (vimBuffer: IVimBuffer) fileSystem =
        let commonOperations = _commonOperationsFactory.GetCommonOperations vimBuffer.VimBufferData
        let foldManager = _foldManagerFactory.GetFoldManager vimBuffer.TextView
        let interpreter = VimInterpreter(vimBuffer, commonOperations, foldManager, fileSystem, _bufferTrackingService)
        interpreter :> IVimInterpreter

    interface IVimInterpreterFactory with
        member x.CreateVimInterpreter vimBuffer fileSystem = x.CreateVimInterpreter vimBuffer fileSystem
