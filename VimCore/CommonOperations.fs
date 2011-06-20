#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open System.ComponentModel.Composition
open System.Text.RegularExpressions

// TODO: Delete this
module internal CommonUtil = 

    /// Raise the error / warning messages for a given SearchResult
    let RaiseSearchResultMessage (statusUtil : IStatusUtil) searchResult =

        match searchResult with 
        | SearchResult.Found (searchData, _, didWrap) ->
            if didWrap then
                let message = 
                    if searchData.Kind.IsAnyForward then Resources.Common_SearchForwardWrapped
                    else Resources.Common_SearchBackwardWrapped
                statusUtil.OnWarning message
        | SearchResult.NotFound (searchData, isOutsidePath) ->
            let format = 
                if isOutsidePath then
                    match searchData.Kind.Path with
                    | Path.Forward -> Resources.Common_SearchHitBottomWithout
                    | Path.Backward -> Resources.Common_SearchHitTopWithout 
                else
                    Resources.Common_PatternNotFound

            statusUtil.OnError (format searchData.Pattern)

type OperationsData = {
    EditorOperations : IEditorOperations
    EditorOptions : IEditorOptions
    FoldManager : IFoldManager
    JumpList : IJumpList
    KeyMap : IKeyMap
    LocalSettings : IVimLocalSettings
    OutliningManager : IOutliningManager option
    RegisterMap : IRegisterMap 
    SearchService : ISearchService
    StatusUtil : IStatusUtil
    TextView : ITextView
    UndoRedoOperations : IUndoRedoOperations
    VimData : IVimData
    VimHost : IVimHost
    WordUtil : IWordUtil
}

type internal CommonOperations ( _data : OperationsData ) =
    let _textBuffer = _data.TextView.TextBuffer
    let _textView = _data.TextView
    let _operations = _data.EditorOperations
    let _outliningManager = _data.OutliningManager
    let _vimData = _data.VimData
    let _host = _data.VimHost
    let _jumpList = _data.JumpList
    let _settings = _data.LocalSettings
    let _options = _data.EditorOptions
    let _undoRedoOperations = _data.UndoRedoOperations
    let _statusUtil = _data.StatusUtil
    let _registerMap = _data.RegisterMap
    let _search = _data.SearchService
    let _regexFactory = VimRegexFactory(_data.LocalSettings.GlobalSettings)
    let _globalSettings = _settings.GlobalSettings
    let _wordUtil = _data.WordUtil

    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// Apply the TextChange to the ITextBuffer 'count' times as a single operation.
    member x.ApplyTextChange textChange addNewLines count =
        Contract.Requires (count > 0)

        // Apply a single change to the ITextBuffer as a transaction 
        let rec applyChange textChange = 
            match textChange with
            | TextChange.Insert text -> 
                // Insert the same text 'count - 1' times at the cursor
                let text = 
                    if addNewLines then
                        System.Environment.NewLine + text
                    else 
                        text

                let caretPoint = TextViewUtil.GetCaretPoint _textView
                let span = SnapshotSpan(caretPoint, 0)
                let snapshot = _textView.TextBuffer.Replace(span.Span, text) |> ignore

                // Now make sure to position the caret at the end of the inserted
                // text
                TextViewUtil.MoveCaretToPosition _textView (caretPoint.Position + text.Length)
            | TextChange.Delete deleteCount -> 
                // Delete '(count - 1) * deleteCount' more characters
                let caretPoint = TextViewUtil.GetCaretPoint _textView
                let count = deleteCount
                let count = min (_textView.TextSnapshot.Length - caretPoint.Position) count
                _textView.TextBuffer.Delete((Span(caretPoint.Position, count))) |> ignore

                // Now make sure the caret is still at the same position
                TextViewUtil.MoveCaretToPosition _textView caretPoint.Position
            | TextChange.Combination (left, right) ->
                applyChange left 
                applyChange right

        // Create a transaction so the textChange is applied as a single edit and to 
        // maintain caret position 
        _undoRedoOperations.EditWithUndoTransaction "Repeat Edits" (fun () -> 

            for i = 1 to count do
                applyChange textChange)

    /// The caret sometimes needs to be adjusted after an Up or Down movement.  Caret position
    /// and virtual space is actually quite a predicamite for VsVim because of how Vim standard 
    /// works.  Vim has no concept of Virtual Space and is designed to work in a fixed width
    /// font buffer.  Visual Studio has essentially the exact opposite.  Non-fixed width fonts are
    /// the most problematic because it makes the natural Vim motion of column based up and down
    /// make little sense visually.  Instead we rely on the core editor for up and down motions.
    ///
    /// The one exception has to do with the VirtualEdit setting.  By default the 'l' motion will 
    /// only move you to the last character on the line and no further.  Visual Studio up and down
    /// though acts like virtualedit=onemore.  We correct this here
    member x.MoveCaretForVirtualEdit () =
        if not _settings.GlobalSettings.IsVirtualEditOneMore then 
            let point = TextViewUtil.GetCaretPoint _textView
            let line = SnapshotPointUtil.GetContainingLine point
            if point.Position >= line.End.Position && line.Length > 0 then 
                TextViewUtil.MoveCaretToPoint _textView (line.End.Subtract(1))

    /// Move the caret to the specified point and ensure it's visible and the surrounding 
    /// text is expanded
    ///
    /// Note: We actually do the operation in the opposite order.  The text needs to be expanded
    /// before we even move the point.  Before the text is expanded the point we are moving to 
    /// will map to the collapsed region.  When the text is subsequently expanded it has no 
    /// memory and will just stay in place.  
    ///
    /// To avoid this we will expand then move the caret and finally ensure it's visible on 
    /// the screen
    member x.MoveCaretToPointAndEnsureVisible point = 
        x.EnsurePointExpanded point
        TextViewUtil.MoveCaretToPoint _textView point
        x.EnsureCaretOnScreen()

    /// Move the caret to the specified point and ensure it's on screen.  Does not expand the 
    /// surrounding text if this is in the middle of a collapsed region
    member x.MoveCaretToPointAndEnsureOnScreen point = 
        TextViewUtil.MoveCaretToPoint _textView point
        x.EnsureCaretOnScreen()

    /// Move the caret to the position dictated by the given MotionResult value
    member x.MoveCaretToMotionResult (result : MotionResult) =

        let point = 

            if not result.IsForward then
                match result.CaretColumn with
                | CaretColumn.None -> 
                    // No column specified so just move to the start of the Span
                    result.Span.Start
                | CaretColumn.AfterLastLine ->
                    // This is only valid going forward so pretend there isn't a column
                    result.Span.Start
                | CaretColumn.InLastLine column -> 
                    let line = SnapshotSpanUtil.GetStartLine result.Span
                    if column > line.Length then
                        line.End
                    else
                        line.Start.Add column
            else

                // Reduce the Span to the line we care about 
                let line = SnapshotSpanUtil.GetEndLine result.Span

                // Get the point when moving the caret after the last line in the SnapshotSpan
                let getAfterLastLine() = 
                    match SnapshotUtil.TryGetLine x.CurrentSnapshot (line.LineNumber + 1) with
                    | None -> 
                        line.End
                    | Some line ->
                        line.Start

                match result.MotionKind with 
                | MotionKind.CharacterWiseExclusive ->
                    // Exclusive motions are straight forward.  Move to the end of the SnapshotSpan
                    // which was recorded.  Exclusive doesn't include the last point in the span 
                    // but does move to it when going forward so End works here
                    result.Span.End
                | MotionKind.CharacterWiseInclusive -> 
                    if Util.IsFlagSet result.MotionResultFlags MotionResultFlags.ExclusivePromotion then
                        // If we adjusted a span under rule #1 of ':help exclusive' then we should still
                        // move the caret to the original end of the span

                        if Util.IsFlagSet result.MotionResultFlags MotionResultFlags.ExclusivePromotionPlusOne then
                            match SnapshotUtil.TryGetLine x.CurrentSnapshot (line.LineNumber + 2) with
                            | None -> 
                                result.Span.End
                            | Some line ->
                                line.Start
                        else
                            getAfterLastLine()
                    else
                        // Normal exclusive motion should go to the last real point on the SnapshotSpan
                        SnapshotPointUtil.TryGetPreviousPointOnLine result.Span.End 1 
                        |> OptionUtil.getOrDefault result.Span.End
                | MotionKind.LineWise column -> 
                    match column with
                    | CaretColumn.None -> 
                        line.End
                    | CaretColumn.InLastLine col ->
                        let endCol = line |> SnapshotLineUtil.GetEnd |> SnapshotPointUtil.GetColumn
                        if col < endCol then 
                            line.Start.Add(col)
                        else 
                            line.End
                    | CaretColumn.AfterLastLine ->
                        getAfterLastLine()

        if result.OperationKind = OperationKind.LineWise && not (Util.IsFlagSet result.MotionResultFlags MotionResultFlags.ExclusiveLineWise) then
            // Line wise motions should not cause any collapsed regions to be expanded.  Instead they
            // should leave the regions collapsed and just move the point into the region
            x.MoveCaretToPointAndEnsureOnScreen point
        else
            // Character wise motions should expand regions
            x.MoveCaretToPointAndEnsureVisible point

        x.MoveCaretForVirtualEdit()
        _operations.ResetSelection()

    /// Return the full word under the cursor or an empty string
    member x.WordUnderCursorOrEmpty =
        let point =  TextViewUtil.GetCaretPoint _textView
        _wordUtil.GetFullWordSpan WordKind.BigWord point 
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateEmpty point)
        |> SnapshotSpanUtil.GetText

    member x.NavigateToPoint (point : VirtualSnapshotPoint) = 
        let buf = point.Position.Snapshot.TextBuffer
        if buf = _textView.TextBuffer then 
            x.MoveCaretToPointAndEnsureVisible point.Position
            true
        else  _host.NavigateTo point 

    /// Convert the provided whitespace into spaces.  The conversion of tabs into spaces will be 
    /// done based on the TabSize setting
    member x.GetAndNormalizeLeadingBlanksToSpaces span = 
        let text = 
            span
            |> SnapshotSpanUtil.GetText
            |> Seq.takeWhile CharUtil.IsBlank
            |> StringUtil.ofCharSeq
        x.NormalizeBlanksToSpaces text, text.Length

    /// Normalize any blanks to the appropriate number of space characters based on the 
    /// Vim settings
    member x.NormalizeBlanksToSpaces (text : string) =
        Contract.Assert(StringUtil.isBlanks text)
        let builder = System.Text.StringBuilder()
        let tabSize = _settings.TabStop
        for c in text do
            match c with 
            | ' ' -> 
                builder.Append(' ') |> ignore
            | '\t' ->
                // Insert spaces up to the next tab size modulus.  
                let count = 
                    let remainder = builder.Length % tabSize
                    if remainder = 0 then tabSize else remainder
                for i = 1 to count do
                    builder.Append(' ') |> ignore
            | _ -> 
                builder.Append(' ') |> ignore
        builder.ToString()

    /// Normalize spaces into tabs / spaces based on the ExpandTab, TabSize settings
    member x.NormalizeSpaces (text : string) = 
        Contract.Assert(Seq.forall (fun c -> c = ' ') text)
        if _settings.ExpandTab then
            text
        else
            let tabSize = _settings.TabStop
            let spacesCount = text.Length % tabSize
            let tabCount = (text.Length - spacesCount) / tabSize 
            let prefix = StringUtil.repeatChar tabCount '\t'
            let suffix = StringUtil.repeatChar spacesCount ' '
            prefix + suffix

    /// Fully normalize white space into tabs / spaces based on the ExpandTab, TabSize 
    /// settings
    member x.NormalizeBlanks text = 
        Contract.Assert(StringUtil.isBlanks text)
        text
        |> x.NormalizeBlanksToSpaces
        |> x.NormalizeSpaces

    /// Shifts a block of lines to the left
    member x.ShiftLineBlockLeft (col: SnapshotSpan seq) multiplier =
        let count = _globalSettings.ShiftWidth * multiplier
        use edit = _textBuffer.CreateEdit()

        col |> Seq.iter (fun span ->
            // Get the span we are formatting within the line.  The span we are given
            // here is the actual span of the selection.  What we want to shift though
            // involves all whitespace from the start of the span through the remainder
            // of the line
            let span = 
                let line = SnapshotPointUtil.GetContainingLine span.Start
                SnapshotSpan(span.Start, line.End)

            let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
            let ws = 
                let length = max (ws.Length - count) 0
                StringUtil.repeatChar length ' ' |> x.NormalizeSpaces
            edit.Replace(span.Start.Position, originalLength, ws) |> ignore)

        edit.Apply() |> ignore

    /// Shift a block of lines to the right
    member x.ShiftLineBlockRight (col: SnapshotSpan seq) multiplier =
        let shiftText = 
            let count = _globalSettings.ShiftWidth * multiplier
            StringUtil.repeatChar count ' '

        use edit = _textBuffer.CreateEdit()

        col |> Seq.iter (fun span ->
            // Get the span we are formatting within the line
            let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
            let ws = x.NormalizeSpaces (ws + shiftText)
            edit.Replace(span.Start.Position, originalLength, ws) |> ignore)

        edit.Apply() |> ignore

    /// Shift lines in the specified range to the left by one shiftwidth
    /// item.  The shift will done against 'column' in the line
    member x.ShiftLineRangeLeft (range : SnapshotLineRange) multiplier =
        let count = _globalSettings.ShiftWidth * multiplier

        use edit = _textBuffer.CreateEdit()
        range.Lines
        |> Seq.iter (fun line ->

            // Get the span we are formatting within the line
            let span = line.Extent
            let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
            let ws = 
                let length = max (ws.Length - count) 0
                StringUtil.repeatChar length ' ' |> x.NormalizeSpaces
            edit.Replace(span.Start.Position, originalLength, ws) |> ignore)
        edit.Apply() |> ignore

    /// Shift lines in the specified range to the right by one shiftwidth 
    /// item.  The shift will occur against column 'column'
    member x.ShiftLineRangeRight (range : SnapshotLineRange) multiplier =
        let shiftText = 
            let count = _globalSettings.ShiftWidth * multiplier
            StringUtil.repeatChar count ' '

        use edit = _textBuffer.CreateEdit()
        range.Lines
        |> Seq.iter (fun line ->

            // Get the span we are formatting within the line
            let span = line.Extent
            let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
            let ws = x.NormalizeSpaces (ws + shiftText)
            edit.Replace(line.Start.Position, originalLength, ws) |> ignore)
        edit.Apply() |> ignore

    /// Convert the provided whitespace into spaces.  The conversion of 
    /// tabs into spaces will be done based on the TabSize setting
    member x.GetAndNormalizeLeadingBlanksToSpaces line = 
        let text = 
            line
            |> SnapshotLineUtil.GetText
            |> Seq.takeWhile CharUtil.IsWhiteSpace
            |> List.ofSeq
        let builder = System.Text.StringBuilder()
        let tabSize = _settings.TabStop
        for c in text do
            match c with 
            | ' ' -> 
                builder.Append(' ') |> ignore
            | '\t' ->
                // Insert spaces up to the next tab size modulus.  
                let count = 
                    let remainder = builder.Length % tabSize
                    if remainder = 0 then tabSize else remainder
                for i = 1 to count do
                    builder.Append(' ') |> ignore
            | _ -> 
                builder.Append(' ') |> ignore
        builder.ToString(), text.Length

    member x.Join (range:SnapshotLineRange) kind = 

        if range.Count > 1 then

            use edit = _data.TextView.TextBuffer.CreateEdit()

            let replace = 
                match kind with
                | JoinKind.KeepEmptySpaces -> ""
                | JoinKind.RemoveEmptySpaces -> " "

            // First delete line breaks on all but the last line 
            range.Lines
            |> Seq.take (range.Count - 1)
            |> Seq.iter (fun line -> 

                // Delete the line break span
                let span = line |> SnapshotLineUtil.GetLineBreakSpan |> SnapshotSpanUtil.GetSpan
                edit.Replace(span, replace) |> ignore )

            // Remove the empty spaces from the start of all but the first line 
            // if the option was specified
            match kind with
            | JoinKind.KeepEmptySpaces -> ()
            | JoinKind.RemoveEmptySpaces ->
                range.Lines 
                |> Seq.skip 1
                |> Seq.iter (fun line ->
                        let count =
                            line.Extent 
                            |> SnapshotSpanUtil.GetText
                            |> Seq.takeWhile CharUtil.IsWhiteSpace
                            |> Seq.length
                        if count > 0 then
                            edit.Delete(line.Start.Position,count) |> ignore )

            // Now position the caret on the new snapshot
            edit.Apply() |> ignore

    // Puts the provided StringData at the given point in the ITextBuffer.  Does not attempt
    // to move the caret as a result of this operation
    member x.Put point stringData opKind =

        match stringData with
        | StringData.Simple str -> 

            // Simple strings can go directly in at the position.  Need to adjust the text if 
            // we are inserting at the end of the buffer
            let text = 
                let newLine = EditUtil.NewLine _options
                match opKind with
                | OperationKind.LineWise -> 
                    if SnapshotPointUtil.IsEndPoint point then
                        // At the end of the file so we need to manipulate the new line character
                        // a bit.  It's typically at the end of the line but at the end of the 
                        // ITextBuffer we need it to be at the beginning since there is no newline 
                        // to append after at the end of the buffer.  
                        let str = EditUtil.RemoveEndingNewLine str
                        newLine + str
                    elif not (SnapshotPointUtil.IsStartOfLine point) then
                        // Edit in the middle of the line.  Need to prefix the string with a newline
                        // in order to correctly insert here.  
                        //
                        // This type of put will occur when a linewise register value is inserted 
                        // from a visual mode character selection
                        newLine + str
                    elif not (EditUtil.EndsWithNewLine str) then
                        // All other linewise operation should have a trailing newline to ensure a
                        // new line is actually inserted
                        str + newLine
                    else
                        str
                | OperationKind.CharacterWise ->
                    // Simplest insert.  No changes needed
                    str

            _textBuffer.Insert(point.Position, text) |> ignore

        | StringData.Block col -> 

            use edit = _textBuffer.CreateEdit()

            match opKind with
            | OperationKind.CharacterWise ->

                // Collection strings are inserted at the original character
                // position down the set of lines creating whitespace as needed
                // to match the indent
                let lineNumber, column = SnapshotPointUtil.GetLineColumn point
    
                // First break the strings into the collection to edit against
                // existing lines and those which need to create new lines at
                // the end of the buffer
                let originalSnapshot = point.Snapshot
                let insertCol, appendCol = 
                    let lastLineNumber = SnapshotUtil.GetLastLineNumber originalSnapshot
                    let insertCount = min ((lastLineNumber - lineNumber) + 1) col.Count
                    (Seq.take insertCount col, Seq.skip insertCount col)
    
                // Insert the text at existing lines
                insertCol |> Seq.iteri (fun offset str -> 
                    let line = originalSnapshot.GetLineFromLineNumber (offset+lineNumber)
                    if line.Length < column then
                        let prefix = String.replicate (column - line.Length) " "
                        edit.Insert(line.Start.Position, prefix + str) |> ignore
                    else
                        edit.Insert(line.Start.Position + column, str) |> ignore)
    
                // Add the text to the end of the buffer.
                if not (Seq.isEmpty appendCol) then
                    let prefix = (EditUtil.NewLine _options) + (String.replicate column " ")
                    let text = Seq.fold (fun text str -> text + prefix + str) "" appendCol
                    let endPoint = SnapshotUtil.GetEndPoint originalSnapshot
                    edit.Insert(endPoint.Position, text) |> ignore

            | OperationKind.LineWise ->

                // Strings are inserted line wise into the ITextBuffer.  Build up an
                // aggregate string and insert it here
                let text = col |> Seq.fold (fun state elem -> state + elem + (EditUtil.NewLine _options)) StringUtil.empty

                edit.Insert(point.Position, text) |> ignore

            edit.Apply() |> ignore

    member x.Beep() = if not _settings.GlobalSettings.VisualBell then _host.Beep()

    member x.DoWithOutlining func = 
        match _outliningManager with
        | None -> x.Beep()
        | Some outliningManager -> func outliningManager

    member x.CheckDirty func = 
        if _host.IsDirty _textView.TextBuffer then 
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            func()

    member x.RaiseSearchResultMessage searchResult = 
        CommonUtil.RaiseSearchResultMessage _statusUtil searchResult

    /// Undo 'count' operations in the ITextBuffer and ensure the caret is on the screen
    /// after the undo completes
    member x.Undo count = 
        _undoRedoOperations.Undo count
        x.EnsureCaretOnScreenAndTextExpanded()

    /// Redo 'count' operations in the ITextBuffer and ensure the caret is on the screen
    /// after the redo completes
    member x.Redo count = 
        _undoRedoOperations.Redo count
        x.EnsureCaretOnScreenAndTextExpanded()

    /// Ensure the caret is visible on the screen.  Will not expand the text around the caret
    /// if it's in the middle of a collapsed region
    member x.EnsureCaretOnScreen () = 
        TextViewUtil.EnsureCaretOnScreen _textView 

    /// Ensure the given SnapshotPoint is not in a collapsed region on the screen
    member x.EnsurePointExpanded point = 
        match _outliningManager with
        | None ->
            ()
        | Some outliningManager -> 
            let span = SnapshotSpan(point, 0)
            outliningManager.ExpandAll(span, fun _ -> true) |> ignore

    /// Ensure the point is visible on the screen and any regions surrounding the point are
    /// expanded such that the actual caret is visible
    member x.EnsurePointOnScreenAndTextExpanded point = 
        x.EnsurePointExpanded point
        _host.EnsureVisible _textView point

    /// Ensure the text is on the screen and that if it's in the middle of a collapsed region 
    /// that the collapsed region is expanded to reveal the caret
    member x.EnsureCaretOnScreenAndTextExpanded () =
        x.EnsurePointExpanded x.CaretPoint
        x.EnsureCaretOnScreen()

    interface ICommonOperations with
        member x.TextView = _textView 
        member x.EditorOperations = _operations
        member x.EditorOptions = _options
        member x.SearchService = _data.SearchService
        member x.UndoRedoOperations = _data.UndoRedoOperations

        member x.ApplyTextChange textChange addNewLines count = x.ApplyTextChange textChange addNewLines count
        member x.Beep () = x.Beep()
        member x.Join range kind = x.Join range kind
        member x.GoToDefinition () = 
            let before = TextViewUtil.GetCaretPoint _textView
            if _host.GoToDefinition() then
                _jumpList.Add before |> ignore
                Result.Succeeded
            else
                match _wordUtil.GetFullWordSpan WordKind.BigWord _textView.Caret.Position.BufferPosition with
                | Some(span) -> 
                    let msg = Resources.Common_GotoDefFailed (span.GetText())
                    Result.Failed(msg)
                | None ->  Result.Failed(Resources.Common_GotoDefNoWordUnderCursor) 

        member x.RaiseSearchResultMessage searchResult = x.RaiseSearchResultMessage searchResult
        member x.SetMark point c (markMap : IMarkMap) = 
            if System.Char.IsLetter(c) || c = '\'' || c = '`' then
                markMap.SetMark point c
                Result.Succeeded
            else
                Result.Failed(Resources.Common_MarkInvalid)

        member x.NavigateToPoint point = x.NavigateToPoint point
                
        member x.JumpToMark ident (map:IMarkMap) = 
            let before = TextViewUtil.GetCaretPoint _textView
            let jumpLocal (point:VirtualSnapshotPoint) = 
                x.MoveCaretToPointAndEnsureVisible point.Position
                _jumpList.Add before |> ignore
                Result.Succeeded
            if not (map.IsLocalMark ident) then 
                match map.GetGlobalMark ident with
                | None -> Result.Failed Resources.Common_MarkNotSet
                | Some(point) -> 
                    match x.NavigateToPoint point with
                    | true -> 
                        _jumpList.Add before |> ignore
                        Result.Succeeded
                    | false -> Result.Failed Resources.Common_MarkInvalid
            else 
                match map.GetLocalMark _textView.TextBuffer ident with
                | Some(point) -> jumpLocal point
                | None -> Result.Failed Resources.Common_MarkNotSet

        member x.MoveCaretForVirtualEdit () = x.MoveCaretForVirtualEdit()

        member x.ScrollLines dir count =
            for i = 1 to count do
                match dir with
                | ScrollDirection.Down -> _operations.ScrollDownAndMoveCaretIfNecessary()
                | ScrollDirection.Up -> _operations.ScrollUpAndMoveCaretIfNecessary()
                | _ -> failwith "Invalid enum value"

        member x.ShiftLineBlockLeft col multiplier = x.ShiftLineBlockLeft col multiplier
        member x.ShiftLineBlockRight col multiplier = x.ShiftLineBlockRight col multiplier
        member x.ShiftLineRangeLeft range multiplier = x.ShiftLineRangeLeft range multiplier
        member x.ShiftLineRangeRight range multiplier = x.ShiftLineRangeRight range multiplier

        member x.Undo count = x.Undo count
        member x.Redo count = x.Redo count
        member x.GoToNextTab direction count = _host.GoToNextTab direction count
        member x.GoToTab index = _host.GoToTab index
        member x.EnsureCaretOnScreen () = x.EnsureCaretOnScreen()
        member x.EnsureCaretOnScreenAndTextExpanded () = x.EnsureCaretOnScreenAndTextExpanded()
        member x.EnsurePointOnScreenAndTextExpanded point = x.EnsurePointOnScreenAndTextExpanded point
        member x.MoveCaretToPoint point =  TextViewUtil.MoveCaretToPoint _textView point 
        member x.MoveCaretToPointAndEnsureVisible point = x.MoveCaretToPointAndEnsureVisible point
        member x.MoveCaretToMotionResult data = x.MoveCaretToMotionResult data
        member x.NormalizeBlanks text = x.NormalizeBlanks text
        member x.FormatLines range = _host.FormatLines _textView range

        member x.Substitute pattern replace (range:SnapshotLineRange) flags = 

            /// Actually do the replace with the given regex
            let doReplace (regex:VimRegex) = 
                use edit = _textView.TextBuffer.CreateEdit()

                let replaceOne (span:SnapshotSpan) (c:Capture) = 
                    let newText =  regex.Replace c.Value replace _globalSettings.Magic 1
                    let offset = span.Start.Position
                    edit.Replace(Span(c.Index+offset, c.Length), newText) |> ignore
                let getMatches (span:SnapshotSpan) = 
                    if Util.IsFlagSet flags SubstituteFlags.ReplaceAll then
                        regex.Regex.Matches(span.GetText()) |> Seq.cast<Match>
                    else
                        regex.Regex.Match(span.GetText()) |> Seq.singleton
                let matches = 
                    range.Lines
                    |> Seq.map (fun line -> line.ExtentIncludingLineBreak)
                    |> Seq.map (fun span -> getMatches span |> Seq.map (fun m -> (m,span)) )
                    |> Seq.concat 
                    |> Seq.filter (fun (m,_) -> m.Success)

                if not (Util.IsFlagSet flags SubstituteFlags.ReportOnly) then
                    // Actually do the edits
                    matches |> Seq.iter (fun (m,span) -> replaceOne span m)

                // Update the status for the substitute operation
                let printMessage () = 

                    // Get the replace message for multiple lines
                    let replaceMessage = 
                        let replaceCount = matches |> Seq.length
                        let lineCount = 
                            matches 
                            |> Seq.map (fun (_,s) -> s.Start.GetContainingLine().LineNumber)
                            |> Seq.distinct
                            |> Seq.length
                        if replaceCount > 1 then Resources.Common_SubstituteComplete replaceCount lineCount |> Some
                        else None

                    let printReplaceMessage () =
                        match replaceMessage with 
                        | None -> ()
                        | Some(msg) -> _statusUtil.OnStatus msg

                    // Find the last line in the replace sequence.  This is printed out to the 
                    // user and needs to represent the current state of the line, not the previous
                    let lastLine = 
                        if Seq.isEmpty matches then 
                            None
                        else 
                            let _, span = matches |> SeqUtil.last 
                            let tracking = span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive)
                            match TrackingSpanUtil.GetSpan _data.TextView.TextSnapshot tracking with
                            | None -> None
                            | Some(span) -> SnapshotSpanUtil.GetStartLine span |> Some

                    // Now consider the options 
                    match lastLine with 
                    | None -> printReplaceMessage()
                    | Some(line) ->

                        let printBoth msg = 
                            match replaceMessage with
                            | None -> _statusUtil.OnStatus msg
                            | Some(replaceMessage) -> _statusUtil.OnStatusLong [replaceMessage; msg]

                        if Util.IsFlagSet flags SubstituteFlags.PrintLast then
                            printBoth (line.GetText())
                        elif Util.IsFlagSet flags SubstituteFlags.PrintLastWithNumber then
                            sprintf "  %d %s" (line.LineNumber+1) (line.GetText()) |> printBoth 
                        elif Util.IsFlagSet flags SubstituteFlags.PrintLastWithList then
                            sprintf "%s$" (line.GetText()) |> printBoth 
                        else printReplaceMessage()

                if edit.HasEffectiveChanges then
                    edit.Apply() |> ignore                                
                    printMessage()
                elif Util.IsFlagSet flags SubstituteFlags.ReportOnly then
                    edit.Cancel()
                    printMessage ()
                elif Util.IsFlagSet flags SubstituteFlags.SuppressError then
                    edit.Cancel()
                else 
                    edit.Cancel()
                    _statusUtil.OnError (Resources.Common_PatternNotFound pattern)

            match _regexFactory.CreateForSubstituteFlags pattern flags with
            | None -> 
                _statusUtil.OnError (Resources.Common_PatternNotFound pattern)
            | Some (regex) -> 
                doReplace regex

                // Make sure to update the saved state.  Note that there are 2 patterns stored 
                // across buffers.
                //
                // 1. Last substituted pattern
                // 2. Last searched for pattern.
                //
                // A substitute command should update both of them 
                _vimData.LastSubstituteData <- Some { SearchPattern=pattern; Substitute=replace; Flags=flags}
                _vimData.LastPatternData <- { Pattern = pattern; Path = Path.Forward }

        member x.GoToLocalDeclaration() = 
            if not (_host.GoToLocalDeclaration _textView x.WordUnderCursorOrEmpty) then _host.Beep()

        member x.GoToGlobalDeclaration () = 
            if not (_host.GoToGlobalDeclaration _textView x.WordUnderCursorOrEmpty) then _host.Beep()

        member x.GoToFile () = 
            x.CheckDirty (fun () ->
                let text = x.WordUnderCursorOrEmpty 
                match _host.LoadFileIntoExistingWindow text _textBuffer with
                | HostResult.Success -> ()
                | HostResult.Error(_) -> _statusUtil.OnError (Resources.NormalMode_CantFindFile text))

        /// Look for a word under the cursor and go to the specified file in a new window.  No need to 
        /// check for dirty since we are opening a new window
        member x.GoToFileInNewWindow () =
            let text = x.WordUnderCursorOrEmpty 
            match _host.LoadFileIntoNewWindow text with
            | HostResult.Success -> ()
            | HostResult.Error(_) -> _statusUtil.OnError (Resources.NormalMode_CantFindFile text)

        member x.Put point stringData opKind = x.Put point stringData opKind

[<Export(typeof<ICommonOperationsFactory>)>]
type CommonOperationsFactory
    [<ImportingConstructor>]
    (
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorOptionsFactoryService : IEditorOptionsFactoryService,
        _outliningManagerService : IOutliningManagerService,
        _undoManagerProvider : ITextBufferUndoManagerProvider,
        _foldManagerFactory : IFoldManagerFactory,
        _wordUtilFactory : IWordUtilFactory
    ) = 

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = System.Object()

    /// Create an ICommonOperations instance for the given VimBufferData
    member x.CreateCommonOperations (bufferData : VimBufferData) =
        let textView = bufferData.TextView
        let editorOperations = _editorOperationsFactoryService.GetEditorOperations(textView)
        let editorOptions = _editorOptionsFactoryService.GetOptions(textView)

        let outlining = 
            // This will return null in ITextBuffer instances where there is no IOutliningManager such
            // as TFS annotated buffers.
            let ret = _outliningManagerService.GetOutliningManager(textView)
            if ret = null then None else Some ret
        let foldManager = _foldManagerFactory.GetFoldManager textView

        let vim = bufferData.Vim
        let operationsData = { 
            EditorOperations = editorOperations
            EditorOptions = editorOptions
            FoldManager = foldManager
            JumpList = bufferData.JumpList
            KeyMap = vim.KeyMap
            LocalSettings = bufferData.LocalSettings
            OutliningManager = outlining
            RegisterMap = vim.RegisterMap
            SearchService = vim.SearchService 
            StatusUtil = bufferData.StatusUtil
            TextView = textView
            UndoRedoOperations = bufferData.UndoRedoOperations
            VimData = vim.VimData
            VimHost = vim.VimHost
            WordUtil = _wordUtilFactory.GetWordUtil textView }
        CommonOperations(operationsData) :> ICommonOperations

    /// Get or create the ICommonOperations for the given buffer
    member x.GetCommonOperations (bufferData : VimBufferData) = 
        let properties = bufferData.TextView.Properties
        properties.GetOrCreateSingletonProperty(_key, (fun () -> x.CreateCommonOperations bufferData))

    interface ICommonOperationsFactory with
        member x.GetCommonOperations bufferData = x.GetCommonOperations bufferData

