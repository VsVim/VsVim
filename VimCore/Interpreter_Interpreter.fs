#light
namespace Vim.Interpreter
open Vim
open Microsoft.VisualStudio.Text

[<Sealed>]
[<Class>]
type Interpreter
    (
        _vimBufferData : VimBufferData,
        _commonOperations : ICommonOperations
    ) =

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _vimHost = _vimBufferData.Vim.VimHost
    let _vimData = _vimBufferData.Vim.VimData
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView
    let _markMap = _vimTextBuffer.Vim.MarkMap
    let _statusUtil = _vimBufferData.StatusUtil
    let _regexFactory = VimRegexFactory(_vimBufferData.LocalSettings.GlobalSettings)
    let _registerMap = _vimBufferData.Vim.RegisterMap

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

    /// Run the close command
    member x.RunClose hasBang = 
        _vimHost.Close _textView (not hasBang)
        RunResult.Completed

    /// Run the delete command.  Delete the specified range of text and set it to 
    /// the given Register
    member x.RunDelete (lineRange : SnapshotLineRange) register = 

        let span = lineRange.ExtentIncludingLineBreak
        _textBuffer.Delete(span.Span) |> ignore

        let value = RegisterValue.String (StringData.OfSpan span, OperationKind.LineWise)
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

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

        match lineCommand with
        | LineCommand.Close hasBang -> x.RunClose hasBang
        | LineCommand.Delete (lineRange, registerName, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunDelete lineRange (getRegister registerName))
        | LineCommand.JumpToLastLine -> x.RunJumpToLastLine()
        | LineCommand.JumpToLine number -> x.RunJumpToLine number
        | LineCommand.Substitute (lineRange, pattern, replace, flags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstitute lineRange pattern replace flags)
        | LineCommand.SubstituteRepeatLast (lineRange, substituteFlags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstituteRepeatLast lineRange substituteFlags)
        | LineCommand.SubstituteRepeatLastWithSearch (lineRange, substituteFlags, count) -> runWithLineRangeAndEndCount lineRange count (fun lineRange -> x.RunSubstituteRepeatLastWithSearch lineRange substituteFlags)



