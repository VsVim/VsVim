﻿#light

namespace Vim
open EditorUtils
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open System.ComponentModel.Composition
open System.Text.RegularExpressions
open StringBuilderExtensions

module internal CommonUtil = 

    /// Get the point from which an incremental search should begin given
    /// a context point.  They don't begin at the point but rather before
    /// or after the point depending on the direction.  Return true if 
    /// a wrap was needed to get the start point
    let GetSearchPointAndWrap path point = 
        match path with
        | Path.Forward ->
            match SnapshotPointUtil.TryAddOne point with 
            | Some point -> point, false
            | None -> SnapshotPoint(point.Snapshot, 0), true
        | Path.Backward ->
            match SnapshotPointUtil.TrySubtractOne point with
            | Some point -> point, false
            | None -> SnapshotUtil.GetEndPoint point.Snapshot, true

    /// Get the point from which an incremental search should begin given
    /// a context point.  They don't begin at the point but rather before
    /// or after the point depending on the direction
    let GetSearchPoint path point = 
        let point, _ = GetSearchPointAndWrap path point
        point

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

/// When maintaining the caret column for motion moves this represents the desired 
/// column to jump to if there is enough space on the line
///
[<RequireQualifiedAccess>]
type MaintainCaretColumn = 

    /// There is no saved caret column. 
    | None

    /// This number is kept as a count of spaces.  Tabs need to be adjusted for when applying
    /// this setting to a motion
    | Spaces of int

    /// The caret was moved with the $ motion and the further moves should move to the end of 
    /// the line 
    | EndOfLine

type internal CommonOperations
    (
        _vimBufferData : IVimBufferData,
        _editorOperations : IEditorOperations,
        _outliningManager : IOutliningManager option,
        _smartIndentationService : ISmartIndentationService
    ) =

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView
    let _editorOptions = _textView.Options
    let _bufferGraph = _textView.BufferGraph
    let _jumpList = _vimBufferData.JumpList
    let _wordUtil = _vimBufferData.WordUtil
    let _statusUtil = _vimBufferData.StatusUtil
    let _vim = _vimBufferData.Vim
    let _vimData = _vim.VimData
    let _vimHost = _vim.VimHost
    let _registerMap = _vim.RegisterMap
    let _localSettings = _vimBufferData.LocalSettings
    let _undoRedoOperations = _vimBufferData.UndoRedoOperations
    let _globalSettings = _localSettings.GlobalSettings
    let _eventHandlers = new DisposableBag()
    let mutable _maintainCaretColumn = MaintainCaretColumn.None

    do
        _textView.Caret.PositionChanged
        |> Observable.subscribe (fun _ -> _maintainCaretColumn <- MaintainCaretColumn.None)
        |> _eventHandlers.Add

        _textView.Closed
        |> Observable.subscribe (fun _ -> _eventHandlers.DisposeAll())
        |> _eventHandlers.Add

    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    member x.MaintainCaretColumn 
        with get() = _maintainCaretColumn
        and set value = _maintainCaretColumn <- value

    /// Get the spaces for the given character
    member x.GetSpacesForCharAtPoint point = 
        let c = SnapshotPointUtil.GetChar point
        ColumnWiseUtils.GetCharacterWidth c _localSettings

    /// Get the count of spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces
    member x.GetSpacesToColumn line column = 
        SnapshotLineUtil.GetSpanInLine line 0 column
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.map x.GetSpacesForCharAtPoint
        |> Seq.sum

    /// Get the count of spaces to get to the specified point in it's line when tabs are expanded
    member x.GetSpacesToPoint point = 
        let line = SnapshotPointUtil.GetContainingLine point
        let column = point.Position - line.Start.Position 
        x.GetSpacesToColumn line column

    // Get the point in the given line which is count "spaces" into the line.  Returns End if 
    // it goes beyond the last point in the string
    member x.GetPointForSpaces line spacesCount = 
        ColumnWiseUtils.GetPointForSpaces line spacesCount _localSettings

    /// Get the new line text which should be used for inserts at the provided point.  This is done
    /// by looking at the current line and potentially the line above and simply re-using it's
    /// value
    member x.GetNewLineText point = 
        if not (_editorOptions.GetReplicateNewLineCharacter()) then

            // If we're not supposed to replicate the current new line character then just go ahead
            // and use the default new line character (so odd they call it character when it's 
            // by default 2 characters)
            _editorOptions.GetNewLineCharacter()
        else
            let line = SnapshotPointUtil.GetContainingLine point
            let line = 
                // If this is the last line there is no line break.  Use the line above
                let snapshot = point.Snapshot
                if line.LineNumber = SnapshotUtil.GetLastLineNumber snapshot && line.LineNumber > 0 then
                    SnapshotUtil.GetLine snapshot (line.LineNumber - 1)
                else
                    line
            if line.LineBreakLength > 0 then
                line.GetLineBreakText()
            else
                _editorOptions.GetNewLineCharacter()

    /// The caret sometimes needs to be adjusted after an Up or Down movement.  Caret position
    /// and virtual space is actually quite a predicament for VsVim because of how Vim standard 
    /// works.  Vim has no concept of Virtual Space and is designed to work in a fixed width
    /// font buffer.  Visual Studio has essentially the exact opposite.  Non-fixed width fonts are
    /// the most problematic because it makes the natural Vim motion of column based up and down
    /// make little sense visually.  Instead we rely on the core editor for up and down motions.
    ///
    /// The two exceptions to this are the Virtual Edit and exclusive setting in Visual Mode.  By
    /// default the 'l' motion will only move you to the last character on the line and no 
    /// further.  Visual Studio up and down though acts like virtualedit=onemore.  We correct 
    /// this here
    member x.AdjustCaretPostMove() =

        let allowPastEndOfLine = 
            _globalSettings.IsVirtualEditOneMore ||
            (_globalSettings.SelectionKind = SelectionKind.Exclusive && VisualKind.IsAnyVisual _vimTextBuffer.ModeKind)

        if not allowPastEndOfLine then
            let point = TextViewUtil.GetCaretPoint _textView
            let line = SnapshotPointUtil.GetContainingLine point
            if point.Position >= line.End.Position && line.Length > 0 then 
                TextViewUtil.MoveCaretToPoint _textView (line.End.Subtract(1))

    /// Delete count lines from the cursor.  The caret should be positioned at the start
    /// of the first line for both undo / redo
    member x.DeleteLines (startLine : ITextSnapshotLine) count register = 

        // Function to actually perform the delete
        let doDelete spanOnVisualSnapshot caretPointOnVisualSnapshot includesLastLine =  

            // Make sure to map the SnapshotSpan back into the text / edit buffer
            let span = BufferGraphUtil.MapSpanDownToSingle _bufferGraph spanOnVisualSnapshot x.CurrentSnapshot
            let point = BufferGraphUtil.MapPointDownToSnapshotStandard _bufferGraph caretPointOnVisualSnapshot x.CurrentSnapshot
            match span, point with
            | Some span, Some caretPoint ->
                // Use a transaction to properly position the caret for undo / redo.  We want it in the same
                // place for undo / redo so move it before the transaction
                TextViewUtil.MoveCaretToPoint _textView caretPoint
                _undoRedoOperations.EditWithUndoTransaction "Delete Lines" (fun() ->
                    let snapshot = _textBuffer.Delete(span.Span)

                    // After delete the span should move to the start of the line of the same number 
                    let caretPoint = 
                        let lineNumber = SnapshotPointUtil.GetLineNumber caretPoint
                        SnapshotUtil.GetLineOrLast x.CurrentSnapshot lineNumber
                        |> SnapshotLineUtil.GetStart

                    TextViewUtil.MoveCaretToPoint _textView caretPoint)

                // Need to manipulate the StringData so that it includes the expected trailing newline
                let stringData = 
                    if includesLastLine then
                        let newLineText = x.GetNewLineText x.CaretPoint
                        (span.GetText()) + newLineText |> EditUtil.RemoveBeginingNewLine |> StringData.Simple
                    else               
                        span |> StringData.OfSpan

                // Now update the register after the delete completes
                let value = RegisterValue.String (stringData, OperationKind.LineWise)
                _registerMap.SetRegisterValue register RegisterOperation.Delete value

            | _ ->
                // If we couldn't map back down raise an error
                _statusUtil.OnError Resources.Internal_ErrorMappingToVisual

        // The span should be calculated using the visual snapshot if available.  Binding 
        // it as 'x' here will help prevent us from accidentally mixing the visual and text
        // snapshot values
        let x = TextViewUtil.GetVisualSnapshotDataOrEdit _textView

        // Map the start line into the visual snapshot
        match BufferGraphUtil.MapPointUpToSnapshotStandard _bufferGraph startLine.Start x.CurrentSnapshot with
        | None -> 
            // If we couldn't map back down raise an error
            _statusUtil.OnError Resources.Internal_ErrorMappingToVisual
        | Some point ->

            // Calculate the range in the visual snapshot
            let range = 
                let line = SnapshotPointUtil.GetContainingLine point
                SnapshotLineRangeUtil.CreateForLineAndMaxCount line count

            // The last line is an unfortunate special case here as it does not have a line break.  Hence 
            // in order to delete the line we must delete the line break at the end of the preceding line.  
            //
            // This cannot be normalized by always deleting the line break from the previous line because
            // it would still break for the first line.  This is an unfortunate special case we must 
            // deal with
            let includesLastLine = range.LastLineNumber = SnapshotUtil.GetLastLineNumber x.CurrentSnapshot
            if includesLastLine && range.StartLineNumber > 0 then
                let aboveLine = SnapshotUtil.GetLine x.CurrentSnapshot (range.StartLineNumber - 1)
                let span = SnapshotSpan(aboveLine.End, range.EndIncludingLineBreak)
                doDelete span range.StartLine.Start true
            else 
                // Simpler case.  Get the line range and delete
                let stringData = range.ExtentIncludingLineBreak |> StringData.OfSpan
                doDelete range.ExtentIncludingLineBreak range.StartLine.Start false

    /// Move the caret in the given direction
    member x.MoveCaret caretMovement = 

        /// Move the caret up
        let moveUp () =
            match SnapshotUtil.TryGetLine x.CurrentSnapshot (x.CaretLine.LineNumber - 1) with
            | None -> false
            | Some line ->
                _editorOperations.MoveLineUp(false);
                true

        /// Move the caret down
        let moveDown () =
            match SnapshotUtil.TryGetLine x.CurrentSnapshot (x.CaretLine.LineNumber + 1) with
            | None -> false
            | Some line ->
                _editorOperations.MoveLineDown(false);
                true

        /// Move the caret left.  Don't go past the start of the line 
        let moveLeft () = 
            if x.CaretLine.Start.Position < x.CaretPoint.Position then
                let point = SnapshotPointUtil.SubtractOne x.CaretPoint
                x.MoveCaretToPointAndEnsureVisible point
                true
            else
                false

        /// Move the caret right.  Don't go off the end of the line
        let moveRight () =
            if x.CaretPoint.Position < x.CaretLine.End.Position then
                let point = SnapshotPointUtil.AddOne x.CaretPoint
                x.MoveCaretToPointAndEnsureVisible point
                true
            else
                false

        let moveHome () =
            _editorOperations.MoveToStartOfLine(false)
            true

        let moveEnd () =
            _editorOperations.MoveToEndOfLine(false)
            true

        let movePageUp () =
            _editorOperations.PageUp(false)
            true

        let movePageDown () =
            _editorOperations.PageDown(false)
            true

        match caretMovement with
        | CaretMovement.Up -> moveUp()
        | CaretMovement.Down -> moveDown()
        | CaretMovement.Left -> moveLeft()
        | CaretMovement.Right -> moveRight()
        | CaretMovement.Home -> moveHome()
        | CaretMovement.End -> moveEnd()
        | CaretMovement.PageUp -> movePageUp()
        | CaretMovement.PageDown -> movePageDown()

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

        let shouldMaintainCaretColumn = Util.IsFlagSet result.MotionResultFlags MotionResultFlags.MaintainCaretColumn
        match shouldMaintainCaretColumn, result.CaretColumn with
        | true, CaretColumn.InLastLine column ->

            // First calculate the column in terms of spaces for the maintained caret.
            let caretColumnSpaces = 
                let motionCaretColumnSpaces = x.GetSpacesToColumn x.CaretLine column
                match _maintainCaretColumn with
                | MaintainCaretColumn.None -> motionCaretColumnSpaces
                | MaintainCaretColumn.Spaces maintainCaretColumnSpaces -> max maintainCaretColumnSpaces motionCaretColumnSpaces
                | MaintainCaretColumn.EndOfLine -> max 0 (result.DirectionLastLine.Length - 1)

            // The CaretColumn union is expressed in a position offset not a space offset 
            // which can differ with tabs.  Recalculate as appropriate.  
            let caretColumn = 
                let lastLine = result.DirectionLastLine
                let column = x.GetPointForSpaces lastLine caretColumnSpaces |> SnapshotPointUtil.GetColumn
                CaretColumn.InLastLine column
            let result = 
                { result with DesiredColumn = caretColumn }

            // Complete the motion with the updated value then reset the maintain caret.  Need
            // to do the save after the caret move since the move will clear out the saved value
            x.MoveCaretToMotionResultCore result
            _maintainCaretColumn <-
                if Util.IsFlagSet result.MotionResultFlags MotionResultFlags.EndOfLine then
                    MaintainCaretColumn.EndOfLine
                else
                    MaintainCaretColumn.Spaces caretColumnSpaces

        | _ -> 

            // Not maintaining caret column so just do a normal movement
            x.MoveCaretToMotionResultCore result

            //If the motion wanted to maintain a specific column for the caret, we need to
            //save it.
            _maintainCaretColumn <-
                if Util.IsFlagSet result.MotionResultFlags MotionResultFlags.EndOfLine then
                    MaintainCaretColumn.EndOfLine
                else 
                    match result.CaretColumn with
                    | CaretColumn.ScreenColumn column -> MaintainCaretColumn.Spaces column
                    | _ -> MaintainCaretColumn.None

    /// Move the caret to the position dictated by the given MotionResult value
    member x.MoveCaretToMotionResultCore (result : MotionResult) =

        let point = 

            let line = result.DirectionLastLine
            if not result.IsForward then
                match result.MotionKind, result.CaretColumn with
                | MotionKind.LineWise, CaretColumn.InLastLine column -> 
                    // If we are moving linewise, but to a specific column, use
                    // that column as the target of the motion
                    SnapshotLineUtil.GetOffsetOrEnd column line
                | _, _ -> 
                    result.Span.Start
            else

                // Get the point when moving the caret after the last line in the SnapshotSpan
                let getAfterLastLine() = 
                    match SnapshotUtil.TryGetLine x.CurrentSnapshot (line.LineNumber + 1) with
                    | None -> line.End
                    | Some line -> line.Start

                match result.MotionKind with 
                | MotionKind.CharacterWiseExclusive ->
                    // Exclusive motions are straight forward.  Move to the end of the SnapshotSpan
                    // which was recorded.  Exclusive doesn't include the last point in the span 
                    // but does move to it when going forward so End works here
                    result.Span.End
                | MotionKind.CharacterWiseInclusive -> 
                    // Normal inclusive motion should move the caret to the last real point on the 
                    // SnapshotSpan.  The one exception is when we are in visual mode with an
                    // exclusive selection.  In that case we move as if it's an exclusive motion.  
                    // Couldn't find any documentation on this but it's indicated by several behavior 
                    // tests ('e', 'w' movements in particular)
                    if VisualKind.IsAnyVisual _vimTextBuffer.ModeKind && _globalSettings.SelectionKind = SelectionKind.Exclusive then
                        result.Span.End
                    else
                        SnapshotPointUtil.TryGetPreviousPointOnLine result.Span.End 1 
                        |> OptionUtil.getOrDefault result.Span.End
                | MotionKind.LineWise -> 
                    match result.CaretColumn with
                    | CaretColumn.None -> 
                        line.End
                    | CaretColumn.InLastLine column ->
                        SnapshotLineUtil.GetOffsetOrEnd column line
                    | CaretColumn.ScreenColumn column ->
                        SnapshotLineUtil.GetOffsetOrEnd (SnapshotPointUtil.GetColumn (x.GetPointForSpaces line column)) line
                    | CaretColumn.AfterLastLine ->
                        getAfterLastLine()

        if result.OperationKind = OperationKind.LineWise && not (Util.IsFlagSet result.MotionResultFlags MotionResultFlags.ExclusiveLineWise) then
            // Line wise motions should not cause any collapsed regions to be expanded.  Instead they
            // should leave the regions collapsed and just move the point into the region
            x.MoveCaretToPointAndEnsureOnScreen point
        else
            // Character wise motions should expand regions
            x.MoveCaretToPointAndEnsureVisible point

        x.AdjustCaretPostMove()
        _editorOperations.ResetSelection()

    /// Move the caret to the specified point but adjust the result if it's in virtual space
    /// and current settings disallow that
    member x.MoveCaretToPointAndCheckVirtualSpace point =
        TextViewUtil.MoveCaretToPoint _textView point
        x.AdjustCaretPostMove()

    /// Move the caret to the proper indentation on a newly created line.  The context line 
    /// is provided to calculate an indentation off of
    member x.GetNewLineIndent  (contextLine : ITextSnapshotLine) (newLine : ITextSnapshotLine) =
        let doAutoIndent() = contextLine |> SnapshotLineUtil.GetIndentPoint |> SnapshotPointUtil.GetColumn |> Some

        let doVimIndent() = 
            if _localSettings.AutoIndent then
                doAutoIndent()
            else
                None

        if _localSettings.GlobalSettings.UseEditorIndent then
            let indent = _smartIndentationService.GetDesiredIndentation(_textView, newLine)
            if indent.HasValue then 
                indent.Value |> Some
            else
                // If the user wanted editor indentation but the editor doesn't support indentation
                // even though it proffers an indentation service then fall back to what auto
                // indent would do if it were enabled (don't care if it actually is)
                //
                // Several editors like XAML offer the indentation service but don't actually 
                // provide information.  User clearly wants indent there since the editor indent
                // is enabled.  Do a best effort and us Vim style indenting
                doAutoIndent()
        else 
            doVimIndent()

    /// Get the standard ReplaceData for the given SnapshotPoint in the ITextBuffer
    member x.GetReplaceData point = 
        let newLineText = x.GetNewLineText point
        {
            NewLine = newLineText
            Magic = _globalSettings.Magic
            Count = 1 }

    member x.GoToDefinition() =
        let before = TextViewUtil.GetCaretPoint _textView
        if _vimHost.GoToDefinition() then
            _jumpList.Add before |> ignore
            Result.Succeeded
        else
            match _wordUtil.GetFullWordSpan WordKind.BigWord _textView.Caret.Position.BufferPosition with
            | Some(span) -> 
                let msg = Resources.Common_GotoDefFailed (span.GetText())
                Result.Failed(msg)
            | None ->  Result.Failed(Resources.Common_GotoDefNoWordUnderCursor) 

    member x.GoToLocalDeclaration() = 
        if not (_vimHost.GoToLocalDeclaration _textView x.WordUnderCursorOrEmpty) then _vimHost.Beep()

    member x.GoToGlobalDeclaration () = 
        if not (_vimHost.GoToGlobalDeclaration _textView x.WordUnderCursorOrEmpty) then _vimHost.Beep()

    member x.GoToFile () = 
        x.CheckDirty (fun () ->
            let text = x.WordUnderCursorOrEmpty 
            match _vimHost.LoadFileIntoExistingWindow text _textView with
            | HostResult.Success -> ()
            | HostResult.Error(_) -> _statusUtil.OnError (Resources.NormalMode_CantFindFile text))

    /// Look for a word under the cursor and go to the specified file in a new window.  No need to 
    /// check for dirty since we are opening a new window
    member x.GoToFileInNewWindow () =
        let text = x.WordUnderCursorOrEmpty 
        match _vimHost.LoadFileIntoNewWindow text with
        | HostResult.Success -> ()
        | HostResult.Error(_) -> _statusUtil.OnError (Resources.NormalMode_CantFindFile text)

    /// Return the full word under the cursor or an empty string
    member x.WordUnderCursorOrEmpty =
        let point =  TextViewUtil.GetCaretPoint _textView
        _wordUtil.GetFullWordSpan WordKind.BigWord point 
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateEmpty point)
        |> SnapshotSpanUtil.GetText

    member x.NavigateToPoint (point : VirtualSnapshotPoint) = 
        let textBuffer = point.Position.Snapshot.TextBuffer
        if textBuffer = _textView.TextBuffer then 
            x.MoveCaretToPointAndEnsureVisible point.Position
            true
        else
            _vimHost.NavigateTo point 

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
        let tabSize = _localSettings.TabStop
        for c in text do
            match c with 
            | ' ' -> 
                builder.AppendChar ' '
            | '\t' ->
                // Insert spaces up to the next tab size modulus.  
                let count = 
                    let remainder = builder.Length % tabSize
                    if remainder = 0 then tabSize else remainder
                for i = 1 to count do
                    builder.AppendChar ' '
            | _ -> 
                builder.AppendChar ' '
        builder.ToString()

    /// Normalize spaces into tabs / spaces based on the ExpandTab, TabSize settings
    member x.NormalizeSpaces (text : string) = 
        Contract.Assert(Seq.forall (fun c -> c = ' ') text)
        if _localSettings.ExpandTab then
            text
        else
            let tabSize = _localSettings.TabStop
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

    member x.ScrollLines dir count =
        for i = 1 to count do
            match dir with
            | ScrollDirection.Down -> _editorOperations.ScrollDownAndMoveCaretIfNecessary()
            | ScrollDirection.Up -> _editorOperations.ScrollUpAndMoveCaretIfNecessary()
            | _ -> failwith "Invalid enum value"

    /// Shifts a block of lines to the left
    member x.ShiftLineBlockLeft (col: SnapshotSpan seq) multiplier =
        let count = _localSettings.ShiftWidth * multiplier
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
            let count = _localSettings.ShiftWidth * multiplier
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
        let count = _localSettings.ShiftWidth * multiplier

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
            let count = _localSettings.ShiftWidth * multiplier
            StringUtil.repeatChar count ' '

        use edit = _textBuffer.CreateEdit()
        range.Lines
        |> Seq.iter (fun line ->

            // Only shift lines if they are non-empty
            if line.Length > 0 then

                // Get the span we are formatting within the line
                let span = line.Extent
                let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
                let ws = x.NormalizeSpaces (ws + shiftText)
                edit.Replace(line.Start.Position, originalLength, ws) |> ignore)

        edit.Apply() |> ignore

    member x.Substitute pattern replace (range : SnapshotLineRange) flags = 

        /// Actually do the replace with the given regex
        let doReplace (regex : VimRegex) = 
            use edit = _textView.TextBuffer.CreateEdit()

            let replaceOne line (c : Capture) = 
                let replaceData = x.GetReplaceData x.CaretPoint
                let newText =  regex.Replace c.Value replace replaceData
                let offset = 
                    line
                    |> SnapshotLineUtil.GetStart
                    |> SnapshotPointUtil.GetPosition
                edit.Replace(Span(c.Index + offset, c.Length), newText) |> ignore

            let getMatches line = 
                let text = 
                    if regex.IncludesNewLine then
                        SnapshotLineUtil.GetTextIncludingLineBreak line
                    else 
                        SnapshotLineUtil.GetText line
                if Util.IsFlagSet flags SubstituteFlags.ReplaceAll then
                    regex.Regex.Matches(text) |> Seq.cast<Match>
                else
                    regex.Regex.Match(text) |> Seq.singleton

            let matches = 
                range.Lines
                |> Seq.map (fun line -> getMatches line |> Seq.map (fun m -> (m, line)) )
                |> Seq.concat 
                |> Seq.filter (fun (m,_) -> m.Success)
                |> List.ofSeq

            if not (Util.IsFlagSet flags SubstituteFlags.ReportOnly) then
                // Actually do the edits
                matches |> Seq.iter (fun (m, line) -> replaceOne line m)

            // Update the status for the substitute operation
            let printMessage () = 

                // Get the replace message for multiple lines
                let replaceMessage = 
                    let replaceCount = matches |> Seq.length
                    let lineCount = 
                        matches 
                        |> Seq.map (fun (_, line) -> line.LineNumber)
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
                        let _, line = matches |> SeqUtil.last 
                        let span = line.ExtentIncludingLineBreak
                        let tracking = span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive)
                        match TrackingSpanUtil.GetSpan _textView.TextSnapshot tracking with
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

        match VimRegexFactory.CreateForSubstituteFlags pattern _globalSettings flags with
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
            _vimData.RaiseSearchRanEvent()

    /// Convert the provided whitespace into spaces.  The conversion of 
    /// tabs into spaces will be done based on the TabSize setting
    member x.GetAndNormalizeLeadingBlanksToSpaces line = 
        let text = 
            line
            |> SnapshotLineUtil.GetText
            |> Seq.takeWhile CharUtil.IsWhiteSpace
            |> List.ofSeq
        let builder = System.Text.StringBuilder()
        let tabSize = _localSettings.TabStop
        for c in text do
            match c with 
            | ' ' -> 
                builder.AppendChar ' '
            | '\t' ->
                // Insert spaces up to the next tab size modulus.  
                let count = 
                    let remainder = builder.Length % tabSize
                    if remainder = 0 then tabSize else remainder
                for i = 1 to count do
                    builder.AppendChar ' '
            | _ -> 
                builder.AppendChar ' '
        builder.ToString(), text.Length

    /// Join the lines in the specified line range together. 
    member x.Join (lineRange : SnapshotLineRange) joinKind = 

        if lineRange.Count > 1 then

            use edit = _textBuffer.CreateEdit()

            // Get the collection of ITextSnapshotLine instances that we need to perform 
            // the edit one.  The last line doesn't need anything done to it. 
            let lines = lineRange.Lines |> Seq.take (lineRange.Count - 1)

            match joinKind with
            | JoinKind.KeepEmptySpaces ->

                // Simplest implementation.  Just delete relevant line breaks from the 
                // ITextBuffer
                for line in lines do
                    let span = Span(line.End.Position, line.LineBreakLength)
                    edit.Delete span |> ignore

            | JoinKind.RemoveEmptySpaces -> 

                for line in lines do

                    // Getting the next line is safe here.  By excluding the last line above we've
                    // guaranteed there is at least one more line below us
                    let nextLine = SnapshotUtil.GetLine lineRange.Snapshot (line.LineNumber + 1)

                    let nextLineStartsWithCloseParen = SnapshotPointUtil.IsChar ')' nextLine.Start

                    let replaceText =
                        match SnapshotLineUtil.GetLastIncludedPoint line with
                        | None ->
                            if nextLineStartsWithCloseParen then 
                                "" 
                            else 
                                " "
                        | Some point ->
                            let c = SnapshotPointUtil.GetChar point
                            if CharUtil.IsBlank c || nextLineStartsWithCloseParen then
                                // When the line ends with a blank then we don't insert any new spaces
                                // for the EOL
                                ""
                            elif (c = '.' || c = '!' || c = '?') && _globalSettings.JoinSpaces then
                                "  "
                            else
                                " "

                    let span = Span(line.End.Position, line.LineBreakLength)
                    edit.Replace(span, replaceText) |> ignore

                    // Also need to delete any blanks at the start of the next line
                    let blanksEnd = SnapshotLineUtil.GetFirstNonBlankOrEnd nextLine
                    if blanksEnd <> nextLine.Start then
                        let span = Span.FromBounds(nextLine.Start.Position, blanksEnd.Position)
                        edit.Delete(span) |> ignore

            // Now position the caret on the new snapshot
            edit.Apply() |> ignore

    // Puts the provided StringData at the given point in the ITextBuffer.  Does not attempt
    // to move the caret as a result of this operation
    member x.Put point stringData opKind =

        match stringData with
        | StringData.Simple str -> 

            // Before inserting normalize the new lines in the string to the newline at the 
            // put position in the buffer.  This doesn't appear to be documented anywhere but
            // can be verified experimentally
            let str = 
                let newLine = x.GetNewLineText point
                EditUtil.NormalizeNewLines str newLine

            // Simple strings can go directly in at the position.  Need to adjust the text if 
            // we are inserting at the end of the buffer
            let text = 
                let newLine = EditUtil.NewLine _editorOptions
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
                    let prefix = (EditUtil.NewLine _editorOptions) + (String.replicate column " ")
                    let text = Seq.fold (fun text str -> text + prefix + str) "" appendCol
                    let endPoint = SnapshotUtil.GetEndPoint originalSnapshot
                    edit.Insert(endPoint.Position, text) |> ignore

            | OperationKind.LineWise ->

                // Strings are inserted line wise into the ITextBuffer.  Build up an
                // aggregate string and insert it here
                let text = col |> Seq.fold (fun state elem -> state + elem + (EditUtil.NewLine _editorOptions)) StringUtil.empty

                edit.Insert(point.Position, text) |> ignore

            edit.Apply() |> ignore

    member x.Beep() = if not _localSettings.GlobalSettings.VisualBell then _vimHost.Beep()

    member x.DoWithOutlining func = 
        match _outliningManager with
        | None -> x.Beep()
        | Some outliningManager -> func outliningManager

    member x.CheckDirty func = 
        if _vimHost.IsDirty _textView.TextBuffer then 
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
        _vimHost.EnsureVisible _textView point

    /// Ensure the text is on the screen and that if it's in the middle of a collapsed region 
    /// that the collapsed region is expanded to reveal the caret
    member x.EnsureCaretOnScreenAndTextExpanded () =
        x.EnsurePointExpanded x.CaretPoint
        x.EnsureCaretOnScreen()

    interface ICommonOperations with
        member x.VimBufferData = _vimBufferData
        member x.TextView = _textView 
        member x.EditorOperations = _editorOperations
        member x.EditorOptions = _editorOptions

        member x.Beep () = x.Beep()
        member x.DeleteLines startLine count register = x.DeleteLines startLine count register
        member x.EnsureCaretOnScreen () = x.EnsureCaretOnScreen()
        member x.EnsureCaretOnScreenAndTextExpanded () = x.EnsureCaretOnScreenAndTextExpanded()
        member x.EnsurePointOnScreenAndTextExpanded point = x.EnsurePointOnScreenAndTextExpanded point
        member x.FormatLines range = _vimHost.FormatLines _textView range
        member x.GetNewLineText point = x.GetNewLineText point
        member x.GetNewLineIndent contextLine newLine = x.GetNewLineIndent contextLine newLine
        member x.GetReplaceData point = x.GetReplaceData point
        member x.GetSpacesToPoint point = x.GetSpacesToPoint point
        member x.GetPointForSpaces contextLine column = x.GetPointForSpaces contextLine column
        member x.GoToLocalDeclaration() = x.GoToLocalDeclaration()
        member x.GoToGlobalDeclaration () = x.GoToGlobalDeclaration()
        member x.GoToFile () = x.GoToFile()
        member x.GoToFileInNewWindow () = x.GoToFileInNewWindow()
        member x.GoToDefinition () = x.GoToDefinition()
        member x.GoToNextTab direction count = _vimHost.GoToNextTab direction count
        member x.GoToTab index = _vimHost.GoToTab index
        member x.Join range kind = x.Join range kind
        member x.MoveCaret caretMovement = x.MoveCaret caretMovement
        member x.MoveCaretToPoint point =  TextViewUtil.MoveCaretToPoint _textView point 
        member x.MoveCaretToPointAndEnsureVisible point = x.MoveCaretToPointAndEnsureVisible point
        member x.MoveCaretToPointAndCheckVirtualSpace point = x.MoveCaretToPointAndCheckVirtualSpace point
        member x.MoveCaretToMotionResult data = x.MoveCaretToMotionResult data
        member x.NavigateToPoint point = x.NavigateToPoint point
        member x.NormalizeBlanks text = x.NormalizeBlanks text
        member x.NormalizeBlanksToSpaces text = x.NormalizeBlanksToSpaces text
        member x.Put point stringData opKind = x.Put point stringData opKind
        member x.RaiseSearchResultMessage searchResult = x.RaiseSearchResultMessage searchResult
        member x.Redo count = x.Redo count
        member x.ScrollLines dir count = x.ScrollLines dir count
        member x.ShiftLineBlockLeft col multiplier = x.ShiftLineBlockLeft col multiplier
        member x.ShiftLineBlockRight col multiplier = x.ShiftLineBlockRight col multiplier
        member x.ShiftLineRangeLeft range multiplier = x.ShiftLineRangeLeft range multiplier
        member x.ShiftLineRangeRight range multiplier = x.ShiftLineRangeRight range multiplier
        member x.Substitute pattern replace range flags = x.Substitute pattern replace range flags
        member x.Undo count = x.Undo count

[<Export(typeof<ICommonOperationsFactory>)>]
type CommonOperationsFactory
    [<ImportingConstructor>]
    (
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _outliningManagerService : IOutliningManagerService,
        _undoManagerProvider : ITextBufferUndoManagerProvider,
        _smartIndentationService : ISmartIndentationService
    ) = 

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = System.Object()

    /// Create an ICommonOperations instance for the given VimBufferData
    member x.CreateCommonOperations (vimBufferData : IVimBufferData) =
        let textView = vimBufferData.TextView
        let editorOperations = _editorOperationsFactoryService.GetEditorOperations(textView)

        let outlining = 
            // This will return null in ITextBuffer instances where there is no IOutliningManager such
            // as TFS annotated buffers.
            let ret = _outliningManagerService.GetOutliningManager(textView)
            if ret = null then None else Some ret

        CommonOperations(vimBufferData, editorOperations, outlining, _smartIndentationService) :> ICommonOperations

    /// Get or create the ICommonOperations for the given buffer
    member x.GetCommonOperations (bufferData : IVimBufferData) = 
        let properties = bufferData.TextView.Properties
        properties.GetOrCreateSingletonProperty(_key, (fun () -> x.CreateCommonOperations bufferData))

    interface ICommonOperationsFactory with
        member x.GetCommonOperations bufferData = x.GetCommonOperations bufferData
