#light

namespace Vim
open Vim.Interpreter
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Formatting
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open System
open System.ComponentModel.Composition
open System.Text.RegularExpressions
open StringBuilderExtensions

module internal CommonUtil = 

    /// Raise the error / warning messages for a given SearchResult
    let RaiseSearchResultMessage (statusUtil: IStatusUtil) searchResult =

        match searchResult with 
        | SearchResult.Found (searchData, _, _, didWrap) ->
            if didWrap then
                let message = 
                    if searchData.Kind.IsAnyForward then Resources.Common_SearchForwardWrapped
                    else Resources.Common_SearchBackwardWrapped
                statusUtil.OnWarning message
        | SearchResult.NotFound (searchData, isOutsidePath) ->
            let format = 
                if isOutsidePath then
                    match searchData.Kind.Path with
                    | SearchPath.Forward -> Resources.Common_SearchHitBottomWithout
                    | SearchPath.Backward -> Resources.Common_SearchHitTopWithout 
                else
                    Resources.Common_PatternNotFound

            statusUtil.OnError (format searchData.Pattern)
        | SearchResult.Cancelled _ -> statusUtil.OnError Resources.Common_SearchCancelled
        | SearchResult.Error (_, msg) -> statusUtil.OnError msg

type internal CommonOperations
    (
        _vimBufferData: IVimBufferData,
        _editorOperations: IEditorOperations,
        _outliningManager: IOutliningManager option,
        _mouseDevice: IMouseDevice
    ) as this =

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
        |> Observable.subscribe (fun _ -> this.MaintainCaretColumn <- MaintainCaretColumn.None)
        |> _eventHandlers.Add

        _textView.Closed
        |> Observable.subscribe (fun _ -> _eventHandlers.DisposeAll())
        |> _eventHandlers.Add

    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretVirtualPoint = TextViewUtil.GetCaretVirtualPoint _textView

    member x.CaretColumn = SnapshotColumn(x.CaretPoint)

    member x.CaretVirtualColumn = VirtualSnapshotColumn(x.CaretVirtualPoint)

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The snapshot point in the buffer under the mouse cursor
    member x.MousePoint =
        match TextViewUtil.GetTextViewLines _textView, _mouseDevice.GetPosition _textView with
        | Some textViewLines, Some position ->
            let xCoordinate = position.X + _textView.ViewportLeft
            let yCoordinate = position.Y + _textView.ViewportTop
            let textViewLine = textViewLines.GetTextViewLineContainingYCoordinate(yCoordinate)

            // Avoid the phantom line.
            let textViewLine =
                if textViewLine <> null then
                    let isPhantomLine =
                        textViewLine.Start
                        |> SnapshotPointUtil.GetContainingLine
                        |> SnapshotLineUtil.IsPhantomLine
                    if isPhantomLine then
                        let index = textViewLines.GetIndexOfTextLine textViewLine
                        if index > 0 then
                            textViewLines.[index - 1]
                        else
                            textViewLine
                    else
                        textViewLine
                else
                    textViewLine

            // Get the point in the line under the mouse cursor or the
            // start/end of the line.
            if textViewLine <> null then
                match xCoordinate >= textViewLine.Left, xCoordinate <= textViewLine.Right with
                | true, true ->
                    textViewLine.GetBufferPositionFromXCoordinate(xCoordinate)
                    |> NullableUtil.ToOption
                    |> OptionUtil.map2 (VirtualSnapshotPointUtil.OfPoint >> Some)
                | false, true ->
                    textViewLine.Start
                    |> VirtualSnapshotPointUtil.OfPoint
                    |> Some
                | true, false ->
                    if _vimTextBuffer.UseVirtualSpace then
                        textViewLine.GetVirtualBufferPositionFromXCoordinate(xCoordinate)
                        |> Some
                    else
                        textViewLine.End
                        |> VirtualSnapshotPointUtil.OfPoint
                        |> Some
                | false, false ->
                    None
            else
                None

        | _ ->
            None

    member x.MaintainCaretColumn 
        with get() = _maintainCaretColumn
        and set value = _maintainCaretColumn <- value

    member x.CloseWindowUnlessDirty() = 
        if _vimHost.IsDirty _textView.TextBuffer then
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            _vimHost.Close _textView

    /// Create a possibly LineWise register value with the specified string value at the given 
    /// point.  This is factored out here because a LineWise value in vim should always
    /// end with a new line but we can't always guarantee the text we are working with 
    /// contains a new line.  This normalizes out the process needed to make this correct
    /// while respecting the settings of the ITextBuffer
    member x.CreateRegisterValue point stringData operationKind = 
        let stringData = 
            match operationKind, stringData with
            | OperationKind.LineWise, StringData.Simple str -> 
                if EditUtil.EndsWithNewLine str then
                    stringData
                else
                    let newLine = x.GetNewLineText point
                    StringData.Simple (str + newLine)
            | _ -> stringData

        RegisterValue(stringData, operationKind)

    /// Get the count of spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces
    member x.GetSpacesToColumnNumber line columnNumber = 
        SnapshotColumn.GetSpacesToColumnNumber(line, columnNumber, _localSettings.TabStop)

    /// Get the count of virtual spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces
    member x.GetVirtualSpacesToColumnNumber line columnNumber =
        VirtualSnapshotColumn.GetSpacesToColumnNumber(line, columnNumber, _localSettings.TabStop)

    /// Get the count of spaces to get to the specified point in it's line when tabs are expanded
    member x.GetSpacesToColumn (column: SnapshotColumn) =
        column.GetSpacesToColumn _localSettings.TabStop

    // Get the point in the given line which is count "spaces" into the line.  Returns End if 
    // it goes beyond the last point in the string
    member x.GetColumnForSpacesOrEnd line spaces = 
        SnapshotColumn.GetColumnForSpacesOrEnd(line, spaces, _localSettings.TabStop)

    /// Get the count of spaces to get to the specified virtual point in it's line when tabs are expanded
    member x.GetSpacesToVirtualColumn (column: VirtualSnapshotColumn) = 
        column.GetSpacesToColumn _localSettings.TabStop

    /// Get the count of spaces to get to the specified virtual point in it's line when tabs are expanded
    member x.GetSpacesToVirtualColumnNumber line columnNumber = 
        VirtualSnapshotColumn.GetSpacesToColumnNumber(line, columnNumber, _localSettings.TabStop)

    // Get the virtual point in the given line which is count "spaces" into the line.  Returns End if
    // it goes beyond the last point in the string
    member x.GetVirtualColumnForSpaces line spaces =
        VirtualSnapshotColumn.GetColumnForSpaces(line, spaces, _localSettings.TabStop)

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
                // If this line has no line break, use the line above
                let snapshot = point.Snapshot
                if not (SnapshotLineUtil.HasLineBreak line) && line.LineNumber > 0 then
                    SnapshotUtil.GetLine snapshot (line.LineNumber - 1)
                else
                    line
            if line.LineBreakLength > 0 then
                line.GetLineBreakText()
            else
                _editorOptions.GetNewLineCharacter()

    /// In order for Undo / Redo to function properly there needs to be an ITextView for the ITextBuffer
    /// accessible in the property bag of the ITextUndoHistory object. In 2017 and before this was added
    /// during AfterTextBufferChangeUndoPrimitive.Create. In 2019 this stopped happening and hence undo /
    /// redo is broken. Forcing it to be present here. 
    /// https://github.com/jaredpar/VsVim/issues/2463
    member x.EnsureUndoHasView() =
        match _undoRedoOperations.TextUndoHistory with
        | None -> ()
        | Some textUndoHistory -> 
            let key = typeof<ITextView>
            let properties = textUndoHistory.Properties
            if not (PropertyCollectionUtil.ContainsKey key properties) then
                properties.AddProperty(key, _textView)

    // Convert any virtual spaces to real normalized spaces
    member x.FillInVirtualSpace () =
        if x.CaretVirtualPoint.IsInVirtualSpace then
            let blanks: string = 
                let blanks = StringUtil.RepeatChar x.CaretVirtualColumn.VirtualSpaces ' '
                x.NormalizeBlanks blanks

            // Make sure to position the caret to the end of the newly inserted spaces
            let position = x.CaretColumn.StartPosition + blanks.Length
            _textBuffer.Insert(x.CaretColumn.StartPosition, blanks) |> ignore
            TextViewUtil.MoveCaretToPosition _textView position

    /// Filter the specified line range through the specified program
    member x.FilterLines (range: SnapshotLineRange) program =

        // Extract the lines to be filtered.
        let newLine = EditUtil.NewLine _editorOptions
        let input =
            range.Lines
            |> Seq.map SnapshotLineUtil.GetText
            |> Seq.map (fun line -> line + newLine)
            |> String.concat StringUtil.Empty

        // Filter the input to the output.
        let workingDirectory = _vimBufferData.WorkingDirectory
        let shell = _globalSettings.Shell
        let results = _vimHost.RunCommand workingDirectory shell program input

        // Display error output and error code, if any.
        let error = results.Error
        let error =
            if results.ExitCode <> 0 then
                let message = Resources.Filter_CommandReturned results.ExitCode
                message + newLine + error
            else
                error
        let error = EditUtil.RemoveEndingNewLine error
        _statusUtil.OnStatus error

        // Prepare the replacement.
        let replacement = results.Output

        if replacement.Length = 0 then

            // Forward to delete lines to handle special cases.
            let startLine = range.StartLine
            let count = range.Count
            let registerName = None
            x.DeleteLines startLine count registerName

        else

            // Remove final linebreak.
            let replacement = EditUtil.RemoveEndingNewLine replacement

            // Normalize linebreaks.
            let replacement = EditUtil.NormalizeNewLines replacement newLine

            // Replace the old lines with the filtered lines.
            _textBuffer.Replace(range.Extent.Span, replacement) |> ignore

            // Place the cursor on the first non-blank character of the first line filtered.
            let firstLine = SnapshotUtil.GetLine _textView.TextSnapshot range.StartLineNumber
            TextViewUtil.MoveCaretToPoint _textView firstLine.Start
            _editorOperations.MoveToStartOfLineAfterWhiteSpace(false)

    /// Format the code lines in the specified line range
    member x.FormatCodeLines range =
        _vimHost.FormatLines _textView range

        // Place the cursor on the first non-blank character of the first line formatted.
        let firstLine = SnapshotUtil.GetLine _textView.TextSnapshot range.StartLineNumber
        TextViewUtil.MoveCaretToPoint _textView firstLine.Start
        _editorOperations.MoveToStartOfLineAfterWhiteSpace(false)

    /// Format the text lines in the specified line range
    member x.FormatTextLines (range: SnapshotLineRange) preserveCaretPosition =

        // Get formatting configuration values.
        let autoIndent = _localSettings.AutoIndent
        let textWidth =
            if _localSettings.TextWidth = 0 then
                VimConstants.DefaultFormatTextWidth
            else
                _localSettings.TextWidth
        let comments = _localSettings.Comments
        let tabStop = _localSettings.TabStop

        // Extract the lines to be formatted and the first line.
        let lines = range.Lines |> Seq.map SnapshotLineUtil.GetText
        let firstLine = lines |> Seq.head

        // Extract the leader string from a comment specification, e.g. "//".
        let getLeaderFromSpec (spec: string) =
            let colonIndex = spec.IndexOf(':')
            if colonIndex = -1 then
                spec
            else
                spec.Substring(colonIndex + 1)

        // Get the leader pattern for a leader string.
        let getLeaderPattern (leader: string) =
            if leader = "" then
                @"^\s*"
            else
                @"^\s*" + Regex.Escape(leader) + @"+\s*"

        // Convert the comment specifications into leader patterns.
        let patterns =
            comments + ",:"
            |> StringUtil.Split ','
            |> Seq.map getLeaderFromSpec
            |> Seq.map getLeaderPattern

        // Check the first line against a potential comment pattern.
        let checkPattern pattern =
            let capture = Regex.Match(firstLine, pattern)
            if capture.Success then
                true, pattern, capture.Value
            else
                false, pattern, ""

        // Choose a pattern and a leader.
        let _, pattern, leader =
            patterns
            |> Seq.map checkPattern
            |> Seq.filter (fun (matches, _, _) -> matches)
            |> Seq.head

        // Decide whether to use the leader for all lines.
        let useLeaderForAllLines =
            not (StringUtil.IsWhiteSpace leader) || autoIndent

        // Strip the leader from a line.
        let stripLeader (line: string) =
            let capture = Regex.Match(line, pattern)
            if capture.Success then
                line.Substring(capture.Length)
            else
                line

        // Strip the leader from all the lines.
        let strippedLines =
            lines
            |> Seq.map stripLeader

        // Split a line into words on whitespace.
        let splitWords (line: string) =
            if StringUtil.IsWhiteSpace line then
                seq { yield "" }
            else
                Regex.Matches(line + " ", @"\S+\s+")
                |> Seq.cast<Capture>
                |> Seq.map (fun capture -> capture.Value)

        // Concatenate a reversed list of words into a line.
        let concatWords (words: string list) =
            words
            |> Seq.rev
            |> String.concat ""
            |> (fun line -> line.TrimEnd())

        // Concatenate words into a line and prepend it to a list of lines.
        let prependLine (words: string list) (lines: string list) =
            if words.IsEmpty then
                lines
            else
                concatWords words :: lines

        // Calculate the length of the leader with tabs expanded.
        let leaderLength =
            StringUtil.ExpandTabsForColumn leader 0 tabStop
            |> StringUtil.GetLength

        // Aggregrate individual words into lines of limited length.
        let takeWord ((column: int), (words: string list), (lines: string list)) (word: string) =

            // Calculate the working limit for line length.
            let limit =
                if lines.IsEmpty || useLeaderForAllLines then
                    textWidth - leaderLength
                else
                    textWidth

            if word = "" then
                0, List.Empty, "" :: prependLine words lines
            elif column = 0 || column + word.TrimEnd().Length <= limit then
                column + word.Length, word :: words, lines
            else
                word.Length, word :: List.Empty, concatWords words :: lines

        // Add a leader to the formatted line if appropriate.
        let addLeader (i: int) (line: string) =
            if i = 0 || useLeaderForAllLines then
                (leader + line).TrimEnd()
            else
                line

        // Split the lines into words and then format them into lines using the aggregator.
        let formattedLines =
            let _, words, lines =
                strippedLines
                |> Seq.collect splitWords
                |> Seq.fold takeWord (0, List.Empty, List.Empty)
            prependLine words lines
            |> Seq.rev
            |> Seq.mapi addLeader

        // Concatenate the formatted lines.
        let newLine = EditUtil.NewLine _editorOptions
        let replacement = formattedLines |> String.concat newLine

        // Replace the old lines with the formatted lines.
        _textBuffer.Replace(range.Extent.Span, replacement) |> ignore


        // Place the cursor on the first non-blank character of the first line formatted.
        let firstLine = SnapshotUtil.GetLine _textView.TextSnapshot range.StartLineNumber
        TextViewUtil.MoveCaretToPoint _textView firstLine.Start
        _editorOperations.MoveToStartOfLineAfterWhiteSpace(false)

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
    member x.AdjustCaretForVirtualEdit() =

        // Vim allows the caret past the end of the line if we are in a
        // one-time command and returning to insert mode momentarily.
        let allowPastEndOfLine = 
            _vimTextBuffer.ModeKind = ModeKind.Insert ||
            _globalSettings.IsVirtualEditOneMore ||
            VisualKind.IsAnySelect _vimTextBuffer.ModeKind ||
            _vimTextBuffer.InOneTimeCommand.IsSome

        if not allowPastEndOfLine && not (VisualKind.IsAnyVisual _vimTextBuffer.ModeKind) then
            let column = TextViewUtil.GetCaretColumn _textView
            let line = column.Line
            if column.StartPosition >= line.End.Position && line.Length > 0 then 
                let column = column.SubtractOrStart 1
                TextViewUtil.MoveCaretToColumn _textView column

    /// Adjust the ITextView scrolling to account for the 'scrolloff' setting after a move operation
    /// completes
    member x.AdjustTextViewForScrollOffset() =
        x.AdjustTextViewForScrollOffsetAtPoint x.CaretPoint

    member x.AdjustTextViewForScrollOffsetAtPoint point = 
        if _globalSettings.ScrollOffset > 0 then
            x.AdjustTextViewForScrollOffsetAtPointCore point _globalSettings.ScrollOffset
    
    /// The most efficient API for moving the scroll is DisplayTextLineContainingBufferPosition.  This is actually
    /// what the scrolling APIs use to implement scrolling.  Unfortunately this API deals is in buffer positions
    /// but we want to scroll visually.  
    ///
    /// This is easier to explain with folding.  If the line just above the caret is 300 lines folded into 1 then 
    /// we want to consider only the folded line for scroll offset, not the 299 invisible lines.  In order to do 
    /// this we need to map the caret position up to the visual buffer, do the line math there, find the correct 
    /// visual line, then map the start of that line back down to the edit buffer.  
    ///
    /// This function also suffers from another problem.  The font in Visual Studio is not guaranteed to be fixed
    /// width / height.  Adornments can also cause lines to be taller or shorter than they should be.  Hence we
    /// have to approximate the number of lines that can be on the screen in order to calculate the proper 
    /// offset to use.  
    member x.AdjustTextViewForScrollOffsetAtPointCore contextPoint offset = 
        Contract.Requires(offset > 0)

        // If the text view is still being initialized, the viewport will have zero height
        // which will force offset to zero. Likewise, if there are no text view lines,
        // trying to scroll is pointless.
        match _textView.ViewportHeight, TextViewUtil.GetTextViewLines _textView with
        | height, Some textViewLines when height <> 0.0 && textViewLines.Count <> 0 ->

            // First calculate the actual offset.  The actual offset can't be more than half of the lines which
            // are visible on the screen.  It's tempting to use the ITextViewLinesCollection.Count to see how
            // many possible lines are displayed.  This value will be wrong though when the view is scrolled 
            // to the bottom because it will be displaying the last few lines and several blanks which don't
            // count.  Instead we average out the height of the lines and divide that into the height of 
            // the view port 
            let offset =
                let lineHeight = textViewLines |> Seq.averageBy (fun l -> l.Height)
                let lineCount = int (_textView.ViewportHeight / lineHeight)
                let maxOffset = lineCount / 2
                min maxOffset offset

            // This function will do the actual positioning of the scroll based on the calculated lines 
            // in the buffer
            let doScroll topPoint bottomPoint =
                let firstVisibleLineNumber = SnapshotPointUtil.GetLineNumber textViewLines.FirstVisibleLine.Start 
                let lastVisibleLineNumber = SnapshotPointUtil.GetLineNumber textViewLines.LastVisibleLine.End 
                let topLineNumber = SnapshotPointUtil.GetLineNumber topPoint
                let bottomLineNumber = SnapshotPointUtil.GetLineNumber bottomPoint

                if topLineNumber < firstVisibleLineNumber then
                    _textView.DisplayTextLineContainingBufferPosition(topPoint, 0.0, ViewRelativePosition.Top)
                elif bottomLineNumber > lastVisibleLineNumber then
                    _textView.DisplayTextLineContainingBufferPosition(bottomPoint, 0.0, ViewRelativePosition.Bottom)

            // Time to map up and down the buffer graph to find the correct top and bottom point that the scroll
            // needs tobe adjusted to 
            let visualSnapshot = _textView.VisualSnapshot
            let editSnapshot = _textView.TextSnapshot
            match BufferGraphUtil.MapPointUpToSnapshotStandard _textView.BufferGraph contextPoint visualSnapshot with
            | None -> ()
            | Some visualPoint ->
                let visualLine = SnapshotPointUtil.GetContainingLine visualPoint
                let visualLineNumber = visualLine.LineNumber

                // Calculate the line information in the visual buffer
                let topLineNumber = max 0 (visualLineNumber - offset) 
                let bottomLineNumber = min (visualLineNumber + offset) (visualSnapshot.LineCount - 1)
                let visualTopLine = SnapshotUtil.GetLine visualSnapshot topLineNumber
                let visualBottomLine = SnapshotUtil.GetLine visualSnapshot bottomLineNumber

                // Map it back down to the edit buffer and then scroll
                let editTopPoint = BufferGraphUtil.MapPointDownToSnapshotStandard _textView.BufferGraph visualTopLine.Start editSnapshot
                let editBottomPoint = BufferGraphUtil.MapPointDownToSnapshotStandard _textView.BufferGraph visualBottomLine.Start editSnapshot
                match editTopPoint, editBottomPoint with
                | Some p1, Some p2 -> doScroll p1 p2
                | _ -> ()
        | _ -> ()

    /// This is the same function as AdjustTextViewForScrollOffsetAtPoint except that it moves the caret 
    /// not the view port.  Make the caret consistent with the setting not the display 
    ///
    /// Once again we are dealing with visual lines, not buffer lines
    member x.AdjustCaretForScrollOffset () =
        match TextViewUtil.GetTextViewLines _textView with
        | None -> ()
        | Some textViewLines ->

            // This function will do the actual caret positioning based on the top visual and bottom
            // visual line.  The return will be the position within the visual buffer, not the edit
            // buffer 
            let getLinePosition caretLineNumber topLineNumber bottomLineNumber =
                if _globalSettings.ScrollOffset <= 0 || _globalSettings.ScrollOffset * 2 >= (bottomLineNumber - topLineNumber) then
                    None
                elif caretLineNumber < (topLineNumber + _globalSettings.ScrollOffset) || caretLineNumber > (bottomLineNumber - _globalSettings.ScrollOffset) then
                    let lineNumber = caretLineNumber
                    let topOffset = min caretLineNumber _globalSettings.ScrollOffset
                    let lastVisualLineNumber = _textView.VisualSnapshot.LineCount - 1
                    let bottomOffset = min (lastVisualLineNumber - caretLineNumber) _globalSettings.ScrollOffset
                    let lineNumber = max lineNumber (topLineNumber + topOffset)
                    let lineNumber = min lineNumber (bottomLineNumber - bottomOffset)
                    if lineNumber <> caretLineNumber then
                        Some lineNumber
                    else
                        None
                else
                    None

            let visualSnapshot = _textView.VisualSnapshot
            let editSnapshot = _textView.TextSnapshot
            let bufferGraph = _textView.BufferGraph
            let topVisualPoint = BufferGraphUtil.MapPointUpToSnapshotStandard bufferGraph textViewLines.FirstVisibleLine.Start visualSnapshot 
            let bottomVisualPoint = BufferGraphUtil.MapPointUpToSnapshotStandard bufferGraph textViewLines.LastVisibleLine.Start visualSnapshot
            let caretVisualPoint = BufferGraphUtil.MapPointUpToSnapshotStandard bufferGraph x.CaretPoint visualSnapshot

            match topVisualPoint, bottomVisualPoint, caretVisualPoint with
            | Some topVisualPoint, Some bottomVisualPoint, Some caretVisualPoint ->
                let topLineNumber = SnapshotPointUtil.GetLineNumber topVisualPoint
                let bottomLineNumber = SnapshotPointUtil.GetLineNumber bottomVisualPoint
                let caretLineNumber = SnapshotPointUtil.GetLineNumber caretVisualPoint
                match getLinePosition caretLineNumber topLineNumber bottomLineNumber with
                | Some visualLineNumber -> 
                    let visualLine = visualSnapshot.GetLineFromLineNumber visualLineNumber
                    match BufferGraphUtil.MapPointDownToSnapshotStandard bufferGraph visualLine.Start editSnapshot with
                    | Some editPoint -> x.MoveCaretToLine (editPoint.GetContainingLine())
                    | None -> ()
                | None -> ()
            | _ -> ()

    /// This method is used to essentially find a line in the edit buffer which represents 
    /// the start of a visual line.  The context provided is a line in the edit buffer 
    /// which maps to some point on that visual line.  
    ///
    /// This is needed in outlining cases where a visual line has a different edit line 
    /// at the start and line break of the visual line.  The function will map the line at the
    /// line break back to the line of the start. 
    member x.AdjustEditLineForVisualSnapshotLine (line: ITextSnapshotLine) = 
        let bufferGraph = _textView.BufferGraph
        let visualSnapshot = _textView.TextViewModel.VisualBuffer.CurrentSnapshot
        match BufferGraphUtil.MapSpanUpToSnapshot bufferGraph line.ExtentIncludingLineBreak SpanTrackingMode.EdgeInclusive visualSnapshot with
        | None -> line
        | Some col ->
            if col.Count = 0 then
                line
            else
                let span = NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan col
                span.Start.GetContainingLine()

    /// Delete count lines from the cursor.  The caret should be positioned at the start
    /// of the first line for both undo / redo
    member x.DeleteLines (startLine: ITextSnapshotLine) count registerName =

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
                _undoRedoOperations.EditWithUndoTransaction "Delete Lines" _textView (fun() ->
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
                let value = x.CreateRegisterValue x.CaretPoint stringData OperationKind.LineWise
                x.SetRegisterValue registerName RegisterOperation.Delete value

            | _ ->
                // If we couldn't map back down raise an error
                _statusUtil.OnError Resources.Internal_ErrorMappingToVisual

        // First we need to remap the 'startLine' value.  To do the delete we need to know the 
        // correct start line in the edit buffer.  What we are provided is a value in the edit
        // buffer but it may be a line further down in the buffer do to folding.  
        let startLine = x.AdjustEditLineForVisualSnapshotLine startLine

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

            // The last line without a line break is an unfortunate special case.  Hence 
            // in order to delete the line we must delete the line break at the end of the preceding line.  
            //
            // This cannot be normalized by always deleting the line break from the previous line because
            // it would still break for the first line.  This is an unfortunate special case we must 
            // deal with
            let lastLineHasLineBreak = SnapshotLineUtil.HasLineBreak range.LastLine
            if not lastLineHasLineBreak && range.StartLineNumber > 0 then
                let aboveLine = SnapshotUtil.GetLine x.CurrentSnapshot (range.StartLineNumber - 1)
                let span = SnapshotSpan(aboveLine.End, range.End)
                doDelete span range.StartLine.Start true
            else 
                // Simpler case.  Get the line range and delete
                let stringData = range.ExtentIncludingLineBreak |> StringData.OfSpan
                doDelete range.ExtentIncludingLineBreak range.StartLine.Start false

    /// Move the caret in the given direction
    member x.MoveCaret caretMovement = 

        /// Move the caret to the same virtual position in line as the current caret line
        let moveToLineVirtual (line: ITextSnapshotLine) =
            if _vimTextBuffer.UseVirtualSpace then
                let caretPoint = x.CaretVirtualPoint
                let tabStop = _localSettings.TabStop
                let currentSpaces = VirtualSnapshotPointUtil.GetSpacesToPoint caretPoint tabStop
                let lineSpaces = SnapshotPointUtil.GetSpacesToPoint line.End tabStop
                if lineSpaces < currentSpaces then
                    let virtualSpaces = currentSpaces - lineSpaces
                    line.End
                    |> VirtualSnapshotPointUtil.OfPoint
                    |> VirtualSnapshotPointUtil.AddOnSameLine virtualSpaces
                    |> (fun point -> x.MoveCaretToVirtualPoint point ViewFlags.Standard)
                    true
                else
                    false
            else
                false

        /// Move the caret up
        let moveUp () =
            match SnapshotUtil.TryGetLine x.CurrentSnapshot (x.CaretLine.LineNumber - 1) with
            | None -> false
            | Some line ->
                if not (moveToLineVirtual line) then
                    _editorOperations.MoveLineUp(false)
                true

        /// Move the caret down
        let moveDown () =
            match SnapshotUtil.TryGetLine x.CurrentSnapshot (x.CaretLine.LineNumber + 1) with
            | None -> false
            | Some line ->
                if SnapshotLineUtil.IsPhantomLine line then
                    false
                else
                    if not (moveToLineVirtual line) then
                        _editorOperations.MoveLineDown(false)
                    true

        /// Move the caret left.  Don't go past the start of the line 
        let moveLeft () = 
            match x.CaretColumn.TrySubtractInLine 1 with
            | Some column ->
                x.MoveCaretToPoint column.StartPoint ViewFlags.Standard
                true
            | None ->
                false

        /// Move the caret right.  Don't go off the end of the line
        let moveRight () =
            match x.CaretColumn.TryAddInLine(1, includeLineBreak = true) with
            | Some column ->
                x.MoveCaretToPoint column.StartPoint ViewFlags.Standard
                true
            | None ->
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

        let moveControlUp () =
            moveUp()

        let moveControlDown () =
            moveDown()

        let moveControlLeft () =
            _editorOperations.MoveToPreviousWord(false)
            true

        let moveControlRight () =
            _editorOperations.MoveToNextWord(false)
            true

        let moveControlHome () =
            _editorOperations.MoveToStartOfDocument(false)
            true

        let moveControlEnd () =
            _editorOperations.MoveToEndOfDocument(false)
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
        | CaretMovement.ControlUp -> moveControlUp()
        | CaretMovement.ControlDown -> moveControlDown()
        | CaretMovement.ControlLeft -> moveControlLeft()
        | CaretMovement.ControlRight -> moveControlRight()
        | CaretMovement.ControlHome -> moveControlHome()
        | CaretMovement.ControlEnd -> moveControlEnd()

    /// Move the caret in the given direction with an arrow key
    member x.MoveCaretWithArrow caretMovement =

        /// Move left one character taking into account 'whichwrap'
        let moveLeft () =
            let caretPoint = x.CaretVirtualPoint
            if _vimTextBuffer.UseVirtualSpace && caretPoint.IsInVirtualSpace then
                caretPoint
                |> VirtualSnapshotPointUtil.SubtractOneOrCurrent
                |> (fun point -> x.MoveCaretToVirtualPoint point ViewFlags.Standard)
                true
            elif _globalSettings.IsWhichWrapArrowLeftInsert then
                if SnapshotPointUtil.IsStartPoint x.CaretPoint then
                    false
                else
                    let point = SnapshotPointUtil.GetPreviousCharacterSpanWithWrap x.CaretPoint
                    x.MoveCaretToPoint point ViewFlags.Standard
                    true
            else
                x.MoveCaret caretMovement

        /// Move right one character taking into account 'whichwrap'
        let moveRight () =
            let caretPoint = x.CaretVirtualPoint
            if _vimTextBuffer.UseVirtualSpace && caretPoint.Position = x.CaretLine.End then
                caretPoint
                |> VirtualSnapshotPointUtil.AddOneOnSameLine
                |> (fun point -> x.MoveCaretToVirtualPoint point ViewFlags.Standard)
                true
            elif _globalSettings.IsWhichWrapArrowRightInsert then
                if SnapshotPointUtil.IsEndPoint x.CaretPoint then
                    false
                else
                    let point = SnapshotPointUtil.GetNextCharacterSpanWithWrap x.CaretPoint
                    x.MoveCaretToPoint point ViewFlags.Standard
                    true
            else
                x.MoveCaret caretMovement

        match caretMovement with
        | CaretMovement.Left -> moveLeft()
        | CaretMovement.Right -> moveRight()
        | _ -> x.MoveCaret caretMovement

    member x.MoveCaretToColumn (point: SnapshotColumn) viewFlags = 
        x.MoveCaretToPoint point.StartPoint viewFlags

    member x.MoveCaretToVirtualColumn (point: VirtualSnapshotColumn) viewFlags = 
        x.MoveCaretToVirtualPoint point.VirtualStartPoint viewFlags

    /// Move the caret to the specified point with the specified view properties
    member x.MoveCaretToPoint (point: SnapshotPoint) viewFlags =
        let virtualPoint = VirtualSnapshotPointUtil.OfPoint point
        x.MoveCaretToVirtualPoint virtualPoint viewFlags

    /// Move the caret to the specified virtual point with the specified view properties
    member x.MoveCaretToVirtualPoint (point: VirtualSnapshotPoint) viewFlags =

        // In the case where we want to expand the text we are moving to we need to do the expansion
        // first before the move.  Before the text is expanded the point we are moving to will map to
        // the collapsed region.  When the text is subsequently expanded it has no memory and will
        // just stay in place.
        if Util.IsFlagSet viewFlags ViewFlags.TextExpanded then
            x.EnsurePointExpanded point.Position

        TextViewUtil.MoveCaretToVirtualPoint _textView point
        x.EnsureAtPoint point.Position viewFlags

    /// Move the caret to the specified line maintaining it's current column
    member x.MoveCaretToLine line = 
        let spaces = x.CaretColumn.GetSpacesToColumn _localSettings.TabStop
        let column = SnapshotColumn.GetColumnForSpacesOrEnd(line, spaces, _localSettings.TabStop)
        TextViewUtil.MoveCaretToColumn _textView column
        x.MaintainCaretColumn <- MaintainCaretColumn.Spaces spaces

    /// Move the caret to the position dictated by the given MotionResult value
    member x.MoveCaretToMotionResult (result: MotionResult) =

        let useVirtualSpace = _vimTextBuffer.UseVirtualSpace
        let shouldMaintainCaretColumn = Util.IsFlagSet result.MotionResultFlags MotionResultFlags.MaintainCaretColumn
        match shouldMaintainCaretColumn, result.CaretColumn with
        | true, CaretColumn.InLastLine columnNumber ->

            // Mappings should occur visually 
            let visualLastLine = x.GetDirectionLastLineInVisualSnapshot result

            // First calculate the column in terms of spaces for the maintained caret.
            let caretColumnSpaces = 
                let motionCaretColumnSpaces =
                    if useVirtualSpace then
                        x.GetSpacesToVirtualColumnNumber x.CaretLine columnNumber
                    else
                        x.GetSpacesToColumnNumber x.CaretLine columnNumber
                match x.MaintainCaretColumn with
                | MaintainCaretColumn.None -> motionCaretColumnSpaces
                | MaintainCaretColumn.Spaces maintainCaretColumnSpaces -> max maintainCaretColumnSpaces motionCaretColumnSpaces
                | MaintainCaretColumn.EndOfLine -> max 0 (visualLastLine.Length - 1)

            // Record the old setting.
            let oldMaintainCaretColumn = x.MaintainCaretColumn

            // The CaretColumn union is expressed in a position offset not a space offset 
            // which can differ with tabs.  Recalculate as appropriate.  
            let caretColumn = 
                if useVirtualSpace then
                    let column = x.GetVirtualColumnForSpaces visualLastLine caretColumnSpaces
                    column.VirtualColumnNumber
                else
                    let column = x.GetColumnForSpacesOrEnd visualLastLine caretColumnSpaces
                    column.ColumnNumber
                |> CaretColumn.InLastLine
            let result = 
                { result with CaretColumn = caretColumn }

            // Complete the motion with the updated value then reset the maintain caret.  Need
            // to do the save after the caret move since the move will clear out the saved value
            x.MoveCaretToMotionResultCore result 
            x.MaintainCaretColumn <-
                match oldMaintainCaretColumn with
                | MaintainCaretColumn.EndOfLine -> MaintainCaretColumn.EndOfLine
                | _ ->
                    if Util.IsFlagSet result.MotionResultFlags MotionResultFlags.EndOfLine then
                        MaintainCaretColumn.EndOfLine
                    else
                        MaintainCaretColumn.Spaces caretColumnSpaces

        | _ -> 

            // Not maintaining caret column so just do a normal movement
            x.MoveCaretToMotionResultCore result

            //If the motion wanted to maintain a specific column for the caret, we need to
            //save it.
            x.MaintainCaretColumn <-
                if Util.IsFlagSet result.MotionResultFlags MotionResultFlags.EndOfLine then
                    MaintainCaretColumn.EndOfLine
                else 
                    match result.CaretColumn with
                    | CaretColumn.ScreenColumn column -> MaintainCaretColumn.Spaces column
                    | _ -> MaintainCaretColumn.None

    /// Many operations for moving a motion result need to be calculated in the
    /// visual snapshot.  This method will return the DirectionLastLine value
    /// in that snapshot or the original value if no mapping is possible. 
    member x.GetDirectionLastLineInVisualSnapshot (result: MotionResult): ITextSnapshotLine =
        let line = result.DirectionLastLine
        x.AdjustEditLineForVisualSnapshotLine line

    /// Move the caret to the position dictated by the given MotionResult value
    ///
    /// Note: This method mixes points from the edit and visual snapshot.  Take care
    /// when changing this function to account for both. 
    member x.MoveCaretToMotionResultCore (result: MotionResult) =

        let point = 

            // All issues around caret column position should be calculated on the visual 
            // snapshot 
            let visualLine = x.GetDirectionLastLineInVisualSnapshot result
            if not result.IsForward then
                match result.MotionKind, result.CaretColumn with
                | MotionKind.LineWise, CaretColumn.InLastLine columnNumber -> 
                    // If we are moving linewise, but to a specific column, use
                    // that column as the target of the motion
                    let column = SnapshotColumn.GetForColumnNumberOrEnd(visualLine, columnNumber)
                    column.StartPoint
                | _, _ -> 
                    result.Span.Start
            else

                match result.MotionKind with 
                | MotionKind.CharacterWiseExclusive ->
                    // Exclusive motions are straight forward.  Move to the end of the SnapshotSpan
                    // which was recorded.  Exclusive doesn't include the last point in the span 
                    // but does move to it when going forward so End works here
                    result.Span.End
                | MotionKind.CharacterWiseInclusive -> 
                    // Normal inclusive motion should move the caret to the last real point on the 
                    // SnapshotSpan.  The exception is when we are in visual mode with an
                    // exclusive selection.  In that case we move as if it's an exclusive motion.  
                    // Couldn't find any documentation on this but it's indicated by several behavior 
                    // tests ('e', 'w' movements in particular).  However, dollar is a special case.
                    // In vim, it is allowed to move one further in visual mode regardless of
                    // the selection kind. See issue #2258 and vim ':help $'.
                    let adjustBackwards =
                        if VisualKind.IsAnyVisual _vimTextBuffer.ModeKind then
                            if _globalSettings.SelectionKind = SelectionKind.Exclusive then
                                false
                            elif result.MotionResultFlags.HasFlag MotionResultFlags.EndOfLine then
                                false
                            else
                                true
                        else
                            true

                    if adjustBackwards then
                        SnapshotPointUtil.TryGetPreviousPointOnLine result.Span.End 1 
                        |> OptionUtil.getOrDefault result.Span.End
                    else
                        result.Span.End
                | MotionKind.LineWise -> 
                    match result.CaretColumn with
                    | CaretColumn.None -> 
                        visualLine.End
                    | CaretColumn.InLastLine columnNumber ->
                        let column = SnapshotColumn.GetForColumnNumberOrEnd(visualLine, columnNumber)
                        column.StartPoint
                    | CaretColumn.ScreenColumn columnNumber ->
                        let column = SnapshotColumn.GetForColumnNumberOrEnd(visualLine, columnNumber)
                        column.StartPoint
                    | CaretColumn.AfterLastLine ->
                        match SnapshotUtil.TryGetLine visualLine.Snapshot (visualLine.LineNumber + 1) with
                        | None -> visualLine.End
                        | Some visualLine -> visualLine.Start

        // The value 'point' may be in either the visual or edit snapshot at this point.  Map to 
        // ensure it's in the edit.
        let point = 
            match BufferGraphUtil.MapPointDownToSnapshot _textView.BufferGraph point x.CurrentSnapshot PointTrackingMode.Negative PositionAffinity.Predecessor with
            | None -> point
            | Some point -> point

        let viewFlags = 
            if result.OperationKind = OperationKind.LineWise && not (Util.IsFlagSet result.MotionResultFlags MotionResultFlags.ExclusiveLineWise) then
                // Line wise motions should not cause any collapsed regions to be expanded.  Instead they
                // should leave the regions collapsed and just move the point into the region
                ViewFlags.All &&& (~~~ViewFlags.TextExpanded)
            else
                // Character wise motions should expand regions
                ViewFlags.All

        match _vimTextBuffer.UseVirtualSpace, result.CaretColumn with
        | true, CaretColumn.InLastLine column ->
            let columnNumber = SnapshotColumn(point).ColumnNumber
            let virtualSpaces = max 0 (column - columnNumber)
            point
            |> VirtualSnapshotPointUtil.OfPoint
            |> VirtualSnapshotPointUtil.AddOnSameLine virtualSpaces
            |> (fun point -> x.MoveCaretToVirtualPoint point viewFlags)
        | _ ->
            x.MoveCaretToPoint point viewFlags

        _editorOperations.ResetSelection()

    /// Move the caret to the proper indentation on a newly created line.  The context line 
    /// is provided to calculate an indentation off of
    member x.GetNewLineIndent  (contextLine: ITextSnapshotLine) (newLine: ITextSnapshotLine) =
        match _vimHost.GetNewLineIndent _textView contextLine newLine _localSettings with
        | Some indent -> Some indent
        | None ->
            if _localSettings.AutoIndent then
                EditUtil.GetAutoIndent contextLine _localSettings.TabStop |> Some
            else
                None

    /// Get the standard ReplaceData for the given SnapshotPoint in the ITextBuffer
    member x.GetReplaceData point = 
        let newLineText = x.GetNewLineText point
        {
            PreviousReplacement =
                _vimTextBuffer.Vim.VimData.LastSubstituteData
                |> Option.map (fun substituteData -> substituteData.Substitute)
                |> Option.defaultValue ""
            NewLine = newLineText
            Magic = _globalSettings.Magic
            Count = VimRegexReplaceCount.One }

    member x.GoToDefinition() =
        let before = TextViewUtil.GetCaretVirtualPoint _textView
        if _vimHost.GoToDefinition() then
            _jumpList.Add before
            Result.Succeeded
        else
            match _wordUtil.GetFullWordSpan WordKind.BigWord _textView.Caret.Position.BufferPosition with
            | Some(span) -> 
                let msg = Resources.Common_GotoDefFailed (span.GetText())
                Result.Failed(msg)
            | None ->  Result.Failed(Resources.Common_GotoDefNoWordUnderCursor) 

    member x.GoToLocalDeclaration() = 
        let caretPoint = x.CaretVirtualPoint
        if _vimHost.GoToLocalDeclaration _textView x.WordUnderCursorOrEmpty then
            _jumpList.Add caretPoint
        else
            _vimHost.Beep()

    member x.GoToGlobalDeclaration () = 
        let caretPoint = x.CaretVirtualPoint
        if _vimHost.GoToGlobalDeclaration _textView x.WordUnderCursorOrEmpty then 
            _jumpList.Add caretPoint
        else
            _vimHost.Beep()

    member x.GoToFile () = 
        x.GoToFile x.WordUnderCursorOrEmpty 

    member x.GoToFile name =
        x.CheckDirty (fun () ->
            if not (_vimHost.LoadFileIntoExistingWindow name _textView) then
                _statusUtil.OnError (Resources.NormalMode_CantFindFile name))

    /// Look for a word under the cursor and go to the specified file in a new window.
    member x.GoToFileInNewWindow () =
        x.GoToFileInNewWindow x.WordUnderCursorOrEmpty 

    /// No need to check for dirty since we are opening a new window
    member x.GoToFileInNewWindow name =
        if not (_vimHost.LoadFileIntoNewWindow name (Some 0) None) then
            _statusUtil.OnError (Resources.NormalMode_CantFindFile name)

    member x.GoToNextTab path count = 

        let tabCount = _vimHost.TabCount
        let mutable tabIndex = _vimHost.GetTabIndex _textView
        if tabCount >= 0 && tabIndex >= 0 && tabIndex < tabCount then
            let count = count % tabCount
            match path with
            | SearchPath.Forward -> 
                tabIndex <- tabIndex + count
                tabIndex <- tabIndex % tabCount
            | SearchPath.Backward -> 
                tabIndex <- tabIndex - count
                if tabIndex < 0 then
                    tabIndex <- tabIndex + tabCount

            _vimHost.GoToTab tabIndex

    member x.GoToTab index = 

        // Normalize out the specified tabIndex to a 0 based value.  Vim uses some odd
        // logic here but the host represents the tab as a 0 based list of tabs
        let mutable tabIndex = index
        let tabCount = _vimHost.TabCount
        if tabCount > 0 then
            if tabIndex < 0 then
                tabIndex <- tabCount - 1
            elif tabIndex > 0 then
                tabIndex <- tabIndex - 1

            if tabIndex >= 0 && tabIndex < tabCount then
                _vimHost.GoToTab tabIndex

    /// Return the full word under the cursor or an empty string
    member x.WordUnderCursorOrEmpty =
        let point =  TextViewUtil.GetCaretPoint _textView
        _wordUtil.GetFullWordSpan WordKind.BigWord point 
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateEmpty point)
        |> SnapshotSpanUtil.GetText

    member x.NavigateToPoint (point: VirtualSnapshotPoint) = 
        let textBuffer = point.Position.Snapshot.TextBuffer
        if textBuffer = _textView.TextBuffer then 
            x.MoveCaretToPoint point.Position (ViewFlags.Visible ||| ViewFlags.ScrollOffset)
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
            |> StringUtil.OfCharSeq
        x.NormalizeBlanksToSpaces text, text.Length

    /// Normalize any blanks to the appropriate number of space characters based on the 
    /// Vim settings
    member x.NormalizeBlanksToSpaces (text: string) =
        Contract.Assert(StringUtil.IsBlanks text)
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

    /// Normalize spaces into tabs / spaces based on the ExpandTab, TabStop settings
    member x.NormalizeSpaces (text: string) = 
        Contract.Assert(Seq.forall (fun c -> c = ' ') text)
        if _localSettings.ExpandTab then
            text
        else
            let tabSize = _localSettings.TabStop
            let spacesCount = text.Length % tabSize
            let tabCount = (text.Length - spacesCount) / tabSize 
            let prefix = StringUtil.RepeatChar tabCount '\t'
            let suffix = StringUtil.RepeatChar spacesCount ' '
            prefix + suffix

    /// Fully normalize white space into tabs / spaces based on the ExpandTab, TabSize 
    /// settings
    member x.NormalizeBlanks text = 
        Contract.Assert(StringUtil.IsBlanks text)
        text
        |> x.NormalizeBlanksToSpaces
        |> x.NormalizeSpaces

    /// Given the specified blank 'text' at the specified column normalize it out to the
    /// correct spaces / tab based on the 'expandtab' setting.  This has to consider the 
    /// difficulty of mixed spaces and tabs filling up the remaining tab boundary 
    member x.NormalizeBlanksAtColumn text (column: SnapshotColumn) = 
        let spacesToColumn = column.GetSpacesToColumn _localSettings.TabStop
        if spacesToColumn % _localSettings.TabStop = 0 then
            // If the column is on a 'tabstop' boundary then there is no difficulty here
            // with accounting for partial tabs.  Just normalize as we would for any other
            // function 
            x.NormalizeBlanks text
        else
            // First step is to trim away the start of the 'text' string which will fill up
            // the gap to the next tab boundary.  
            let gap = _localSettings.TabStop - spacesToColumn % _localSettings.TabStop
            let mutable index = 0
            let mutable count = 0
            while count < gap && index < text.Length do
                let c = text.[index]
                Contract.Assert (CharUtil.IsBlank c)
                if c = '\t' then
                    count <- gap
                else
                    count <- count + 1

                index <- index + 1

            if count < gap then
                // There isn't enough text here to even fill up the gap.  This can only happen when
                // it is comprised of spaces anyways and they can't be a tab since there isn't enough
                // of them so this just returns the input text
                text
            else
                let gapText = 
                    if _localSettings.ExpandTab then 
                        StringUtil.RepeatChar gap ' '
                    else 
                        "\t"

                let remainder = text.Substring(index)
                gapText + x.NormalizeBlanks remainder

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
                StringUtil.RepeatChar length ' ' |> x.NormalizeSpaces
            edit.Replace(span.Start.Position, originalLength, ws) |> ignore)

        edit.Apply() |> ignore

    /// Shift a block of lines to the right
    member x.ShiftLineBlockRight (col: SnapshotSpan seq) multiplier =
        let shiftText = 
            let count = _localSettings.ShiftWidth * multiplier
            StringUtil.RepeatChar count ' '

        use edit = _textBuffer.CreateEdit()

        col |> Seq.iter (fun span ->
            // Get the span we are formatting within the line
            let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
            let ws = x.NormalizeSpaces (ws + shiftText)
            edit.Replace(span.Start.Position, originalLength, ws) |> ignore)

        edit.Apply() |> ignore

    /// Shift lines in the specified range to the left by one shiftwidth
    /// item.  The shift will done against 'column' in the line
    member x.ShiftLineRangeLeft (range: SnapshotLineRange) multiplier =
        let count = _localSettings.ShiftWidth * multiplier

        use edit = _textBuffer.CreateEdit()
        range.Lines
        |> Seq.iter (fun line ->

            // Get the span we are formatting within the line
            let span = line.Extent
            let ws, originalLength = x.GetAndNormalizeLeadingBlanksToSpaces span
            let ws = 
                let length = max (ws.Length - count) 0
                StringUtil.RepeatChar length ' ' |> x.NormalizeSpaces
            edit.Replace(span.Start.Position, originalLength, ws) |> ignore)
        edit.Apply() |> ignore

    /// Shift lines in the specified range to the right by one shiftwidth 
    /// item.  The shift will occur against column 'column'
    member x.ShiftLineRangeRight (range: SnapshotLineRange) multiplier =
        let shiftText = 
            let count = _localSettings.ShiftWidth * multiplier
            StringUtil.RepeatChar count ' '

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

    /// Sort the given line range
    member x.SortLines (range: SnapshotLineRange) reverseOrder flags (pattern: string option) =

        // Extract the lines to be sorted.
        let lines = range.Lines |> Seq.map SnapshotLineUtil.GetText

        // Convert a line to lowercase.
        let toLower (line: string) = line.ToLower()

        // Compile sort pattern.
        let pattern =
            match pattern with
            | Some pattern ->

                // If the pattern is empty, use the last search pattern instead.
                let pattern = 
                    if pattern = "" then 
                        _vimData.LastSearchData.Pattern
                    else
                        pattern

                // Convert from vim regex syntax to native regex syntax.
                let options = VimRegexFactory.CreateRegexOptions _globalSettings
                match VimRegexFactory.Create pattern options with
                | Some vimRegex -> Some vimRegex.Regex
                | None -> None

            | None -> None

        // Sort the lines.
        let sortedLines =

            // Define a sort by function that handles reverse ordering.
            let sortByFunction (keyFunction: (string -> 'Key)) =
                (if reverseOrder then Seq.sortByDescending else Seq.sortBy) keyFunction

            // Project line using sort pattern.
            let projectLine (line: string) =
                match pattern with
                | Some pattern ->
                    let patternMatch = pattern.Match(line)
                    if patternMatch.Success then
                        let capture = patternMatch.Captures.[0]
                        if Util.IsFlagSet flags SortFlags.MatchPattern then
                            capture.ToString()
                        else
                            line.Substring(capture.Index + capture.Length)
                    else
                        ""
                | None -> line

            // Extract a key using a regular expression.
            let extractKey (keyPattern: Regex) (line: string) =

                // Project the line.
                let line = projectLine line

                // Extract key using key pattern.
                let keyMatch = keyPattern.Match(line)
                if keyMatch.Success then
                    keyMatch.Captures.[0].ToString()
                else
                    ""

            // Handle numeric or textual sorting.
            let anyInteger = (
                SortFlags.Decimal |||
                SortFlags.Hexidecimal |||
                SortFlags.Octal |||
                SortFlags.Binary
            )
            if Util.IsFlagSet flags anyInteger then

                // Define a function to convert a string to an integer.
                let parseInteger (keyPattern: Regex) (fromBase: int) (line: string) =
                    let defaultValue = System.Int64.MinValue
                    let key = extractKey keyPattern line
                    match key with
                    | "" -> defaultValue
                    | _ ->
                        try
                            if fromBase = 16 && key.StartsWith("-") then
                                -Convert.ToInt64(key.Substring(1), 16)
                            else
                                Convert.ToInt64(key, fromBase)
                        with
                        | _ -> defaultValue

                // Precompile the regular expression.
                let getKeyFunction (keyPattern: string) (fromBase: int) =
                    parseInteger (new Regex(keyPattern)) fromBase

                // Given a text line, extract an integer key.
                let keyFunction =
                    if Util.IsFlagSet flags SortFlags.Decimal then
                        getKeyFunction @"-?[0-9]+" 10
                    else if Util.IsFlagSet flags SortFlags.Hexidecimal then
                        getKeyFunction @"-?(0[xX])?[0-9a-fA-F]+" 16
                    else if Util.IsFlagSet flags SortFlags.Octal then
                        getKeyFunction @"[0-7]+" 8
                    else
                        getKeyFunction @"[0-1]+" 2

                sortByFunction keyFunction lines

            else if Util.IsFlagSet flags SortFlags.Float then

                // Define a function to convert a string to a float.
                let parseFloat (keyPattern: Regex) (line: string) =
                    let defaultValue = Double.MinValue
                    let key = extractKey keyPattern line
                    match key with
                    | "" -> defaultValue
                    | _ ->
                        try
                            Convert.ToDouble(key)
                        with
                        | _ -> defaultValue

                // Precompile the regular expression.
                let getKeyFunction (keyPattern: string) =
                    parseFloat (new Regex(keyPattern))

                // Given a text line, extract a float key.
                let floatPattern = @"[-+]?([0-9]*\.?[0-9]+|[0-9]+\.)([eE][-+]?[0-9]+)?"
                let keyFunction = getKeyFunction floatPattern

                sortByFunction keyFunction lines

            else

                // Given a text line, extract a text key.
                let keyFunction =
                    if Util.IsFlagSet flags SortFlags.IgnoreCase then
                        projectLine >> toLower
                    else
                        projectLine

                sortByFunction keyFunction lines

        // Optionally filter out duplicates.
        let sortedLines =
            if Util.IsFlagSet flags SortFlags.Unique then
                if Util.IsFlagSet flags SortFlags.IgnoreCase then
                    sortedLines |> Seq.distinctBy toLower
                else
                    sortedLines |> Seq.distinct
            else
                sortedLines

        // Concatenate the sorted lines.
        let newLine = EditUtil.NewLine _editorOptions
        let replacement = sortedLines |> String.concat newLine

        // Replace the old lines with the sorted lines.
        _textBuffer.Replace(range.Extent.Span, replacement) |> ignore

        // Place the cursor on the first non-blank character of the first line sorted.
        let firstLine = SnapshotUtil.GetLine _textView.TextSnapshot range.StartLineNumber
        TextViewUtil.MoveCaretToPoint _textView firstLine.Start
        _editorOperations.MoveToStartOfLineAfterWhiteSpace(false)

    /// Perform a regular expression substitution within the specified regions
    member x.SubstituteCore (regex: VimRegex) (replace: string) (flags: SubstituteFlags) (searchSpan: SnapshotSpan) (replacementSpan: SnapshotSpan) = 

        // This is complicated by the fact that vim allows patterns to
        // start within the line range but extend arbitarily far beyond
        // it into the buffer. So we have two concepts in play here:
        // - search region: The valid region for the search to start in
        // - replacement region: The valid region for the search to end in
        // Both regions should always start at same place.  For normal
        // (non-multiline) patterns, the two regions can be the same.
        // For multiline patterns, the second region should extend to
        // the end of the buffer.
        let snapshot = _textView.TextSnapshot;

        // Use an edit to apply all the changes at once after
        // we've found all the matches.
        use edit = _textView.TextBuffer.CreateEdit()

        // The start and end position delineate the bounds of the search region.
        let startPosition = searchSpan.Start.Position
        let endPosition = searchSpan.End.Position

        let replacementRegion = replacementSpan.GetText()
        let lastLineHasNoLineBreak = not (SnapshotUtil.AllLinesHaveLineBreaks snapshot)

        // The following code assumes we can use indexes arising from
        // the replacement region string as offsets into the snapshot.
        Contract.Assert(replacementRegion.Length = replacementSpan.Length)

        // Get a snapshot line corresponding to a replacement region index.
        let getLineForIndex (index: int) =
            new SnapshotPoint(snapshot, startPosition + index)
            |> SnapshotPointUtil.GetContainingLine

        // Does the match start within the specified limits?
        let isMatchValid (capture: Match) =
            if capture.Success then
                let index = capture.Index
                let length = endPosition - startPosition

                // We can only match at the very end of the search
                // region if the last line doesn't have a linebreak
                // and we're at the end of the buffer.
                if index < length || (index = length && lastLineHasNoLineBreak && index = snapshot.Length) then
                    if regex.MatchesVisualSelection then

                        // If the pattern includes '\%V', we need to check the match
                        // against the last visual selection.
                        let visualSelection = _vimTextBuffer.LastVisualSelection
                        match visualSelection with
                        | None -> false
                        | Some visualSelection ->

                            // Is the match entirely within the visual selection?
                            let selectionStartIndex = visualSelection.VisualSpan.Start.Position - startPosition
                            let selectionEndIndex = visualSelection.VisualSpan.End.Position - startPosition
                            index >= selectionStartIndex && index + capture.Length <= selectionEndIndex
                    else
                        true
                else
                    false
            else
                false

        // Get a snapshot span corresponding to a regex match.
        let getSpanforCapture (capture: Match) =
            let matchStartPosition = startPosition + capture.Index
            let matchLength = capture.Length
            new SnapshotSpan(snapshot, matchStartPosition, matchLength)

        // Replace one match.
        let replaceOne (capture: Match) =
            let matchSpan = getSpanforCapture capture

            // Get the new text to replace the match span with.
            let newText =
                if Util.IsFlagSet flags SubstituteFlags.LiteralReplacement then
                    replace
                else
                    let replaceData = x.GetReplaceData x.CaretPoint
                    let oldText = matchSpan.GetText()
                    regex.Replace oldText replace replaceData _registerMap

            // Perform the replacement.
            edit.Replace(matchSpan.Span, newText) |> ignore

        // Update the status for the substitute operation
        let printMessage (matches: ITextSnapshotLine list) =

            // Get the replace message for multiple lines.
            let replaceMessage =
                let replaceCount = matches.Length
                let lineCount =
                    matches
                    |> Seq.distinctBy (fun line -> line.LineNumber)
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
                    let line = matches |> SeqUtil.last
                    let span = line.ExtentIncludingLineBreak
                    let tracking = span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive)
                    match TrackingSpanUtil.GetSpan _textView.TextSnapshot tracking with
                    | None -> None
                    | Some(span) -> SnapshotSpanUtil.GetStartLine span |> Some

            // Now consider the options.
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

        // Get all the matches in the replacement region.
        let matches =
            regex.Regex.Matches(replacementRegion)
            |> Seq.cast<Match>
            |> Seq.filter isMatchValid
            |> Seq.map (fun capture -> (capture, getLineForIndex capture.Index))
            |> Seq.toList

        // Obey the 'replace all' flag by using only the first
        // match from each line group.
        let matches =
            if Util.IsFlagSet flags SubstituteFlags.ReplaceAll then
                matches
            else
                matches
                |> Seq.groupBy (fun (_, line) -> line.LineNumber)
                |> Seq.map (fun (lineNumber, group) -> Seq.head group)
                |> Seq.toList

        // Actually do the replace unless the 'report only' flag was specified.
        if not (Util.IsFlagSet flags SubstituteFlags.ReportOnly) then
            matches |> Seq.iter (fun (capture, _) -> replaceOne capture)

        // Discard the capture information.
        let matches =
            matches
            |> Seq.map (fun (_, line) -> line)
            |> Seq.toList

        if edit.HasEffectiveChanges then
            edit.Apply() |> ignore
            printMessage matches
        elif Util.IsFlagSet flags SubstituteFlags.ReportOnly then
            edit.Cancel()
            printMessage matches
        elif Util.IsFlagSet flags SubstituteFlags.SuppressError then
            edit.Cancel()
        else
            edit.Cancel()
            _statusUtil.OnError (Resources.Common_PatternNotFound regex.VimPattern)

    /// Perform a pattern substitution on the specifed line range
    member x.Substitute (pattern: string) (replace: string) (range: SnapshotLineRange) (flags: SubstituteFlags) =

        match VimRegexFactory.CreateForSubstituteFlags pattern _globalSettings flags with
        | None ->
            _statusUtil.OnError (Resources.Common_PatternNotFound pattern)
        | Some regex ->
            let snapshot = _textView.TextSnapshot;

            // The beginning of a match can occur within the search region.
            let searchSpan = range.ExtentIncludingLineBreak

            // The end of the match can occur within the replacement region.
            let replacementSpan =
                if regex.IncludesNewLine then

                    // Grow the replacement span to the end of the
                    // buffer if the regex could match a newline.
                    new SnapshotSpan(searchSpan.Start, SnapshotUtil.GetEndPoint snapshot)
                else
                    range.ExtentIncludingLineBreak

            // Check for expression replace (see vim ':help :s\=').
            if replace.StartsWith(@"\=") then
                let expressionText = replace.Substring(2)
                let vim = _vimBufferData.Vim
                match vim.TryGetVimBuffer _textView with
                | true, vimBuffer ->
                    let vimInterpreter = vim.GetVimInterpreter vimBuffer
                    match vimInterpreter.EvaluateExpression expressionText with
                    | EvaluateResult.Succeeded variableValue ->
                        let replace = variableValue.StringValue
                        let flags = flags ||| SubstituteFlags.LiteralReplacement
                        x.SubstituteCore regex replace flags searchSpan replacementSpan
                    | EvaluateResult.Failed message ->
                        _statusUtil.OnError message
                | _ ->
                    _statusUtil.OnError Resources.Parser_InvalidArgument
            else
                x.SubstituteCore regex replace flags searchSpan replacementSpan

            // Make sure to update the saved state.  Note that there are 2 patterns stored 
            // across buffers.
            //
            // 1. Last substituted pattern
            // 2. Last searched for pattern.
            //
            // A substitute command should update both of them 
            _vimData.LastSubstituteData <- Some { SearchPattern = pattern; Substitute = replace; Flags = flags}
            _vimData.LastSearchData <- SearchData(pattern, SearchPath.Forward, _globalSettings.WrapScan)

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
    member x.Join (lineRange: SnapshotLineRange) joinKind = 

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
                    if SnapshotPointUtil.IsEndPoint point && not (SnapshotPointUtil.IsStartOfLine point) then
                        // At the end of the file without a trailing line break so we need to
                        // manipulate the new line character a bit.
                        // It's typically at the end of the line but at the end of the 
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
                let column = SnapshotColumn(point)
                let lineNumber, columnNumber = column.LineNumber, column.ColumnNumber
    
                // First break the strings into the collection to edit against
                // existing lines and those which need to create new lines at
                // the end of the buffer
                let originalSnapshot = point.Snapshot
                let insertCol, appendCol = 
                    let lastLineNumber = SnapshotUtil.GetLastLineNumber originalSnapshot
                    let insertCount = min ((lastLineNumber - lineNumber) + 1) col.Count
                    (Seq.take insertCount col, Seq.skip insertCount col)
    
                // Insert the text into each of the existing lines.
                insertCol |> Seq.iteri (fun offset str -> 
                    let line =
                        lineNumber + offset
                        |> SnapshotUtil.GetLine originalSnapshot
                    let column = SnapshotColumn(line)
                    let columnCount = SnapshotLineUtil.GetColumnsCount SearchPath.Forward line
                    if columnCount < columnNumber then
                        let prefix = String.replicate (columnNumber - columnCount) " "
                        edit.Insert(line.End.Position, prefix + str) |> ignore
                    else
                        let offset = column.Add(columnNumber).Offset
                        edit.Insert(line.Start.Position + offset, str) |> ignore)
    
                // Add the text to the end of the buffer.
                if not (Seq.isEmpty appendCol) then
                    let prefix = (EditUtil.NewLine _editorOptions) + (String.replicate columnNumber " ")
                    let text = Seq.fold (fun text str -> text + prefix + str) "" appendCol
                    let endPoint = SnapshotUtil.GetEndPoint originalSnapshot
                    edit.Insert(endPoint.Position, text) |> ignore

            | OperationKind.LineWise ->

                // Strings are inserted line wise into the ITextBuffer.  Build up an
                // aggregate string and insert it here
                let text = col |> Seq.fold (fun state elem -> state + elem + (EditUtil.NewLine _editorOptions)) StringUtil.Empty

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

    /// Record last change start and end positions
    /// (spans must be from different snapshots)
    member x.RecordLastChange (oldSpan: SnapshotSpan) (newSpan: SnapshotSpan) =
        Contract.Requires(oldSpan.Snapshot <> newSpan.Snapshot)
        x.RecordLastChangeOrYank oldSpan newSpan

    /// Record last yank start and end positions
    member x.RecordLastYank span =
        x.RecordLastChangeOrYank span span

    /// Record last change or yankstart and end positions
    /// (it is a yank if the old span and the new span are the same)
    member x.RecordLastChangeOrYank oldSpan newSpan =
        let startPoint = SnapshotSpanUtil.GetStartPoint newSpan
        let endPoint = SnapshotSpanUtil.GetEndPoint newSpan
        let endPoint =
            match SnapshotSpanUtil.GetLastIncludedPoint newSpan with
            | Some point ->
                if SnapshotPointUtil.IsInsideLineBreak point then point else endPoint
            | None ->
                endPoint
        _vimTextBuffer.LastChangeOrYankStart <- Some startPoint
        _vimTextBuffer.LastChangeOrYankEnd <- Some endPoint
        let lineRange =
            if newSpan.Length = 0 then
                oldSpan
            else
                newSpan
            |> SnapshotLineRange.CreateForSpan
        let lineCount = lineRange.Count
        if lineCount >= 3 then
            if newSpan.Length = 0 then
                Resources.Common_LinesDeleted lineCount
            elif oldSpan = newSpan then
                Resources.Common_LinesYanked lineCount
            else
                Resources.Common_LinesChanged lineCount
            |> _statusUtil.OnStatus

    /// Undo 'count' operations in the ITextBuffer and ensure the caret is on the screen
    /// after the undo completes
    member x.Undo count = 
        x.EnsureUndoHasView()
        _undoRedoOperations.Undo count
        x.AdjustCaretForVirtualEdit()
        x.EnsureAtPoint x.CaretPoint ViewFlags.Standard

    /// Redo 'count' operations in the ITextBuffer and ensure the caret is on the screen
    /// after the redo completes
    member x.Redo count = 
        x.EnsureUndoHasView()
        _undoRedoOperations.Redo count
        x.AdjustCaretForVirtualEdit()
        x.EnsureAtPoint x.CaretPoint ViewFlags.Standard

    /// Ensure the given view properties are met at the given point
    member x.EnsureAtPoint point viewFlags = 
        if Util.IsFlagSet viewFlags ViewFlags.TextExpanded then
            x.EnsurePointExpanded point
        if Util.IsFlagSet viewFlags ViewFlags.Visible then
            x.EnsurePointVisible point
        if Util.IsFlagSet viewFlags ViewFlags.ScrollOffset then
            x.AdjustTextViewForScrollOffsetAtPoint point
        if Util.IsFlagSet viewFlags ViewFlags.VirtualEdit && point.Position = x.CaretPoint.Position then
            x.AdjustCaretForVirtualEdit()

    /// Ensure the given SnapshotPoint is not in a collapsed region on the screen
    member x.EnsurePointExpanded point = 
        match _outliningManager with
        | None ->
            ()
        | Some outliningManager -> 
            let span = SnapshotSpan(point, 0)
            outliningManager.ExpandAll(span, fun _ -> true) |> ignore

    /// Ensure the point is on screen / visible
    member x.EnsurePointVisible (point: SnapshotPoint) = 
        TextViewUtil.CheckScrollToPoint _textView point
        if point.Position = x.CaretPoint.Position then
            TextViewUtil.EnsureCaretOnScreen _textView
        else
            _vimHost.EnsureVisible _textView point  

    member x.GetRegisterName name =
        match name with
        | Some name -> name
        | None ->
            if Util.IsFlagSet _globalSettings.ClipboardOptions ClipboardOptions.Unnamed then
                RegisterName.SelectionAndDrop SelectionAndDropRegister.Star
            else
                RegisterName.Unnamed

    member x.GetRegister name = 
        let name = x.GetRegisterName name
        _registerMap.GetRegister name

    /// Updates the given register with the specified value.  This will also update 
    /// other registers based on the type of update that is being performed.  See 
    /// :help registers for the full details
    member x.SetRegisterValue (name: RegisterName option) operation (value: RegisterValue) = 
        let name, isUnnamedOrMissing, isMissing = 
            match name with 
            | None -> x.GetRegisterName None, true, true
            | Some name -> name, name = RegisterName.Unnamed, false

        if name <> RegisterName.Blackhole then

            _registerMap.SetRegisterValue name value

            // If this is not the unnamed register then the unnamed register needs to 
            // be updated 
            if name <> RegisterName.Unnamed then
                _registerMap.SetRegisterValue RegisterName.Unnamed value

            let hasNewLine = 
                match value.StringData with 
                | StringData.Block col -> Seq.exists EditUtil.HasNewLine col 
                | StringData.Simple str -> EditUtil.HasNewLine str

            // Performs a numbered delete.  Shifts all of the values down and puts 
            // the new value into register 1
            //
            // The documentation for registers 1-9 says this doesn't occur when a named
            // register is used.  Actual behavior shows this is not the case.
            let doNumberedDelete () = 
                for i in [9;8;7;6;5;4;3;2] do
                    let cur = RegisterNameUtil.NumberToRegister i |> Option.get 
                    let prev = RegisterNameUtil.NumberToRegister (i - 1) |> Option.get
                    let prevRegister = _registerMap.GetRegister prev
                    _registerMap.SetRegisterValue cur prevRegister.RegisterValue

                _registerMap.SetRegisterValue (RegisterName.Numbered NumberedRegister.Number1) value

            // Update the numbered register based on the type of the operation
            match operation with
            | RegisterOperation.Delete ->
                if hasNewLine then
                    doNumberedDelete()
                // Use small delete register unless a register was explicitly named
                else if isMissing then
                    _registerMap.SetRegisterValue RegisterName.SmallDelete value

            | RegisterOperation.BigDelete ->
                doNumberedDelete()
                if not hasNewLine && name = RegisterName.Unnamed then
                    _registerMap.SetRegisterValue RegisterName.SmallDelete value
            | RegisterOperation.Yank ->
                // If the register name was missing or explicitly the unnamed register then it needs 
                // to update register 0.
                if isUnnamedOrMissing then
                    _registerMap.SetRegisterValue (RegisterName.Numbered NumberedRegister.Number0) value

    /// Toggle the use of typing language characters for insert or search
    /// (see vim ':help i_CTRL-^' and ':help c_CTRL-^')
    member x.ToggleLanguage isForInsert =
        let keyMap = _vimBufferData.Vim.KeyMap
        let languageMappings = keyMap.GetKeyMappingsForMode KeyRemapMode.Language
        let languageMappingsAreDefined = not languageMappings.IsEmpty
        if isForInsert || _globalSettings.ImeSearch = -1 then
            if languageMappingsAreDefined then
                match _globalSettings.ImeInsert with
                | 1 -> _globalSettings.ImeInsert <- 0
                | _ -> _globalSettings.ImeInsert <- 1
            else
                match _globalSettings.ImeInsert with
                | 2 -> _globalSettings.ImeInsert <- 0
                | _ -> _globalSettings.ImeInsert <- 2
        else
            if languageMappingsAreDefined then
                match _globalSettings.ImeSearch with
                | 1 -> _globalSettings.ImeSearch <- 0
                | _ -> _globalSettings.ImeSearch <- 1
            else
                match _globalSettings.ImeSearch with
                | 2 -> _globalSettings.ImeSearch <- 0
                | _ -> _globalSettings.ImeSearch <- 2

    interface ICommonOperations with
        member x.VimBufferData = _vimBufferData
        member x.TextView = _textView 
        member x.MaintainCaretColumn 
            with get() = x.MaintainCaretColumn
            and set value = x.MaintainCaretColumn <- value
        member x.EditorOperations = _editorOperations
        member x.EditorOptions = _editorOptions
        member x.MousePoint = x.MousePoint

        member x.AdjustTextViewForScrollOffset() = x.AdjustTextViewForScrollOffset()
        member x.AdjustCaretForScrollOffset() = x.AdjustCaretForScrollOffset()
        member x.AdjustCaretForVirtualEdit() = x.AdjustCaretForVirtualEdit()
        member x.Beep() = x.Beep()
        member x.CloseWindowUnlessDirty() = x.CloseWindowUnlessDirty()
        member x.CreateRegisterValue point stringData operationKind = x.CreateRegisterValue point stringData operationKind
        member x.DeleteLines startLine count registerName = x.DeleteLines startLine count registerName
        member x.EnsureAtCaret viewFlags = x.EnsureAtPoint x.CaretPoint viewFlags
        member x.EnsureAtPoint point viewFlags = x.EnsureAtPoint point viewFlags
        member x.FillInVirtualSpace() = x.FillInVirtualSpace()
        member x.FilterLines range command = x.FilterLines range command
        member x.FormatCodeLines range = x.FormatCodeLines range
        member x.FormatTextLines range preserveCaretPosition = x.FormatTextLines range preserveCaretPosition
        member x.GetRegister registerName = x.GetRegister registerName
        member x.GetNewLineText point = x.GetNewLineText point
        member x.GetNewLineIndent contextLine newLine = x.GetNewLineIndent contextLine newLine
        member x.GetReplaceData point = x.GetReplaceData point
        member x.GetSpacesToColumn column = x.GetSpacesToColumn column
        member x.GetColumnForSpacesOrEnd contextLine spaces = x.GetColumnForSpacesOrEnd contextLine spaces
        member x.GetSpacesToVirtualColumn column = x.GetSpacesToVirtualColumn column
        member x.GetVirtualColumnForSpaces contextLine spaces = x.GetVirtualColumnForSpaces contextLine spaces
        member x.GoToLocalDeclaration() = x.GoToLocalDeclaration()
        member x.GoToGlobalDeclaration() = x.GoToGlobalDeclaration()
        member x.GoToFile() = x.GoToFile()
        member x.GoToFile name = x.GoToFile name
        member x.GoToFileInNewWindow() = x.GoToFileInNewWindow()
        member x.GoToFileInNewWindow name = x.GoToFileInNewWindow name
        member x.GoToDefinition() = x.GoToDefinition()
        member x.GoToNextTab direction count = x.GoToNextTab direction count
        member x.GoToTab index = x.GoToTab index
        member x.Join range kind = x.Join range kind
        member x.MoveCaret caretMovement = x.MoveCaret caretMovement
        member x.MoveCaretWithArrow caretMovement = x.MoveCaretWithArrow caretMovement
        member x.MoveCaretToColumn column viewFlags =  x.MoveCaretToColumn column viewFlags
        member x.MoveCaretToVirtualColumn column viewFlags =  x.MoveCaretToVirtualColumn column viewFlags
        member x.MoveCaretToPoint point viewFlags =  x.MoveCaretToPoint point viewFlags
        member x.MoveCaretToVirtualPoint point viewFlags =  x.MoveCaretToVirtualPoint point viewFlags
        member x.MoveCaretToMotionResult data = x.MoveCaretToMotionResult data
        member x.NavigateToPoint point = x.NavigateToPoint point
        member x.NormalizeBlanks text = x.NormalizeBlanks text
        member x.NormalizeBlanksAtColumn text column = x.NormalizeBlanksAtColumn text column
        member x.NormalizeBlanksToSpaces text = x.NormalizeBlanksToSpaces text
        member x.Put point stringData opKind = x.Put point stringData opKind
        member x.RaiseSearchResultMessage searchResult = x.RaiseSearchResultMessage searchResult
        member x.RecordLastChange oldSpan newSpan = x.RecordLastChange oldSpan newSpan
        member x.RecordLastYank span = x.RecordLastYank span
        member x.Redo count = x.Redo count
        member x.SetRegisterValue name operation value = x.SetRegisterValue name operation value
        member x.ScrollLines dir count = x.ScrollLines dir count
        member x.ShiftLineBlockLeft col multiplier = x.ShiftLineBlockLeft col multiplier
        member x.ShiftLineBlockRight col multiplier = x.ShiftLineBlockRight col multiplier
        member x.ShiftLineRangeLeft range multiplier = x.ShiftLineRangeLeft range multiplier
        member x.ShiftLineRangeRight range multiplier = x.ShiftLineRangeRight range multiplier
        member x.SortLines range reverseOrder flags pattern = x.SortLines range reverseOrder flags pattern
        member x.Substitute pattern replace range flags = x.Substitute pattern replace range flags
        member x.ToggleLanguage isForInsert = x.ToggleLanguage isForInsert
        member x.Undo count = x.Undo count

[<Export(typeof<ICommonOperationsFactory>)>]
type CommonOperationsFactory
    [<ImportingConstructor>]
    (
        _editorOperationsFactoryService: IEditorOperationsFactoryService,
        _outliningManagerService: IOutliningManagerService,
        _undoManagerProvider: ITextBufferUndoManagerProvider,
        _mouseDevice: IMouseDevice
    ) = 

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = System.Object()

    /// Create an ICommonOperations instance for the given VimBufferData
    member x.CreateCommonOperations (vimBufferData: IVimBufferData) =
        let textView = vimBufferData.TextView
        let editorOperations = _editorOperationsFactoryService.GetEditorOperations(textView)

        let outlining = 
            // This will return null in ITextBuffer instances where there is no IOutliningManager such
            // as TFS annotated buffers.
            let ret = _outliningManagerService.GetOutliningManager(textView)
            if ret = null then None else Some ret

        CommonOperations(vimBufferData, editorOperations, outlining, _mouseDevice) :> ICommonOperations

    /// Get or create the ICommonOperations for the given buffer
    member x.GetCommonOperations (bufferData: IVimBufferData) = 
        let properties = bufferData.TextView.Properties
        properties.GetOrCreateSingletonProperty(_key, (fun () -> x.CreateCommonOperations bufferData))

    interface ICommonOperationsFactory with
        member x.GetCommonOperations bufferData = x.GetCommonOperations bufferData
