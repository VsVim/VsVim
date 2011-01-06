#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open System.Text.RegularExpressions

type internal CommonOperations ( _data : OperationsData ) =
    let _textBuffer = _data.TextView.TextBuffer
    let _textView = _data.TextView
    let _operations = _data.EditorOperations
    let _outlining = _data.OutliningManager
    let _vimData = _data.VimData
    let _host = _data.VimHost
    let _jumpList = _data.JumpList
    let _settings = _data.LocalSettings
    let _options = _data.EditorOptions
    let _undoRedoOperations = _data.UndoRedoOperations
    let _statusUtil = _data.StatusUtil
    let _normalWordNav =  _data.Navigator
    let _registerMap = _data.RegisterMap
    let _search = _data.SearchService
    let _smartIndentationServtice = _data.SmartIndentationService
    let _regexFactory = VimRegexFactory(_data.LocalSettings.GlobalSettings)

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

    member x.WordUnderCursorOrEmpty =
        let point =  TextViewUtil.GetCaretPoint _textView
        TssUtil.FindCurrentFullWordSpan point WordKind.BigWord
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateEmpty point)
        |> SnapshotSpanUtil.GetText

    member x.NavigateToPoint (point:VirtualSnapshotPoint) = 
        let buf = point.Position.Snapshot.TextBuffer
        if buf = _textView.TextBuffer then 
            TextViewUtil.MoveCaretToPoint _textView point.Position
            TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining
            true
        else  _host.NavigateTo point 

    member x.DeleteBlock (col:NormalizedSnapshotSpanCollection) = 
        use edit = _textView.TextBuffer.CreateEdit()
        col |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
        edit.Apply() |> ignore

    member x.DeleteSpan (span:SnapshotSpan) = 
        let buffer = span.Snapshot.TextBuffer
        buffer.Delete(span.Span) |> ignore

    member x.ShiftLineRangeRight multiplier (lineSpan:SnapshotLineRange) =
        let text = new System.String(' ', _settings.GlobalSettings.ShiftWidth * multiplier)
        use edit = _data.TextView.TextBuffer.CreateEdit()
        lineSpan.Lines
        |> Seq.map (fun line -> line.Extent)
        |> Seq.iter (fun span -> edit.Replace(span.Start.Position, 0, text) |> ignore)
        edit.Apply() |> ignore

    member x.ShiftLineRangeLeft multiplier (lineSpan:SnapshotLineRange) = 
        let count = _settings.GlobalSettings.ShiftWidth * multiplier
        use edit = _data.TextView.TextBuffer.CreateEdit()
        lineSpan.Lines
        |> Seq.map (fun line -> line.Extent)
        |> Seq.iter (fun span -> 
            let text = SnapshotSpanUtil.GetText span
            let toReplace = 
                let whiteSpaceLen = text |> Seq.takeWhile CharUtil.IsWhiteSpace |> Seq.length
                min whiteSpaceLen count
            edit.Replace(span.Start.Position, toReplace, StringUtil.empty) |> ignore )
        edit.Apply() |> ignore

    /// Change the letters on the given span by applying the specified function
    /// to each of them
    member x.ChangeLettersCore col changeFunc = 
        use edit = _textView.TextBuffer.CreateEdit()
        col 
        |> Seq.map SnapshotSpanUtil.GetPoints 
        |> Seq.concat
        |> Seq.map (fun x -> x.Position,x.GetChar())
        |> Seq.filter (fun (_,c) -> CharUtil.IsLetter c)
        |> Seq.map (fun (pos,c) -> (pos, changeFunc c))
        |> Seq.iter (fun (pos,c) -> edit.Replace(new Span(pos,1), StringUtil.ofChar c) |> ignore)
        edit.Apply() |> ignore

    member x.ChangeLettersOnSpan span changeFunc = x.ChangeLettersCore (Seq.singleton span) changeFunc

    member x.Join (range:SnapshotLineRange) kind = 

        if range.Count > 1 then

            // Create a tracking point for the caret
            let snapshot = range.Snapshot
            let trackingPoint = 
                let point = range.EndLine.Start 
                snapshot.CreateTrackingPoint(point.Position, PointTrackingMode.Positive)

            use edit = _data.TextView.TextBuffer.CreateEdit()

            let replace = 
                match kind with
                | KeepEmptySpaces -> ""
                | RemoveEmptySpaces -> " "

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
            | KeepEmptySpaces -> ()
            | RemoveEmptySpaces ->
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
            let snapshot = edit.Apply()
            match TrackingPointUtil.GetPoint snapshot trackingPoint with
            | None -> ()
            | Some(point) ->  TextViewUtil.MoveCaretToPoint _textView point 

    /// Updates the given register with the specified value.  This will also update 
    /// other registers based on the type of update that is being performed.  See 
    /// :help registers for the full details
    member x.UpdateRegister (reg:Register) regOperation value = 
        if reg.Name <> RegisterName.Blackhole then
            reg.Value <- value
    
            // If this is not the unnamed register then the unnamed register needs to 
            // be updated 
            if reg.Name <> RegisterName.Unnamed then
                let unnamedReg = _registerMap.GetRegister RegisterName.Unnamed
                unnamedReg.Value <- value
    
            // Update the numbered registers with the new values.  First shift the existing
            // values up the stack
            let intToName num = 
                let c = char (num + (int '0'))
                let name = NumberedRegister.OfChar c |> Option.get
                RegisterName.Numbered name
    
            // Next is insert the new value into the numbered register list.  New value goes
            // into 0 and the rest shift up
            for i in [9;8;7;6;5;4;3;2;1] do
                let cur = intToName i |> _registerMap.GetRegister
                let prev = intToName (i-1) |> _registerMap.GetRegister
                cur.Value <- prev.Value
            let regZero = _registerMap.GetRegister (RegisterName.Numbered NumberedRegister.Register_0)
            regZero.Value <- value
    
            // Possibily update the small delete register
            if reg.Name <> RegisterName.Unnamed && regOperation = RegisterOperation.Delete then
                match value.Value with
                | StringData.Block(_) -> ()
                | StringData.Simple(str) -> 
                    if not (StringUtil.containsChar str '\n') then
                        let regSmallDelete = _registerMap.GetRegister RegisterName.SmallDelete
                        regSmallDelete.Value <- value

    member x.MoveToNextWordCore kind count isWholeWord = 
        let point = TextViewUtil.GetCaretPoint _textView
        match TssUtil.FindCurrentFullWordSpan point WordKind.NormalWord with
        | None -> _statusUtil.OnError Resources.NormalMode_NoWordUnderCursor
        | Some(span) ->

            // Build up the SearchData structure
            let word = span.GetText()
            let text = if isWholeWord then SearchText.WholeWord(word) else SearchText.StraightText(word)
            let data = {Text=text; Kind = kind; Options = SearchOptions.ConsiderIgnoreCase }

            // When forward the search will be starting on the current word so it will 
            // always match.  Without modification a count of 1 would simply find the word 
            // under the cursor.  Increment the count by 1 here so that it will find
            // the current word as the 0th match (so to speak)
            let count = if SearchKindUtil.IsForward kind then count + 1 else count 

            match _search.FindNextMultiple data point _normalWordNav count with
            | Some(span) -> 
                TextViewUtil.MoveCaretToPoint _textView span.Start
                TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining
            | None -> ()

            _vimData.LastSearchData <- data

    member x.MoveToNextOccuranceOfLastSearchCore count isReverse = 
        let last = _vimData.LastSearchData
        let last = 
            if isReverse then { last with Kind = SearchKindUtil.Reverse last.Kind }
            else last

        if StringUtil.isNullOrEmpty last.Text.RawText then
            _statusUtil.OnError Resources.NormalMode_NoPreviousSearch
        else

            let foundSpan (span:SnapshotSpan) = 
                TextViewUtil.MoveCaretToPoint _textView span.Start
                TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining

            let findMore (span:SnapshotSpan) count = 
                if count = 1 then foundSpan span
                else 
                    let count = count - 1 
                    match _search.FindNextMultiple last span.End _normalWordNav count with
                    | Some(span) -> foundSpan span
                    | None -> _statusUtil.OnError (Resources.Common_PatternNotFound last.Text.RawText)

            // Make sure we don't count the current word if the cursor is positioned
            // directly on top of the current word 
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            match _search.FindNext last caretPoint _normalWordNav with
            | None -> _statusUtil.OnError (Resources.Common_PatternNotFound last.Text.RawText)
            | Some(span) ->
                let count = if span.Start = caretPoint then count else count - 1 
                if count = 0 then foundSpan span
                else 
                    match _search.FindNextMultiple last span.End _normalWordNav count with
                    | Some(span) -> foundSpan span
                    | None -> _statusUtil.OnError (Resources.Common_PatternNotFound last.Text.RawText)

    /// Wrap the passed in "action" inside an undo transaction.  This is needed
    /// when making edits such as paste so that the cursor will move properly 
    /// during an undo operation
    member x.WrapEditInUndoTransaction name action =
        use undoTransaction = _undoRedoOperations.CreateUndoTransaction(name)
        _operations.AddBeforeTextBufferChangePrimitive()
        action()
        _operations.AddAfterTextBufferChangePrimitive()
        undoTransaction.Complete()

    /// Same as WrapInUndoTransaction except provides for a return value
    member x.WrapEditInUndoTransactionWithReturn name action =
        use undoTransaction = _undoRedoOperations.CreateUndoTransaction(name)
        _operations.AddBeforeTextBufferChangePrimitive()
        let ret = action()
        _operations.AddAfterTextBufferChangePrimitive()
        undoTransaction.Complete()
        ret

    member x.PutAt point stringData opKind =
        x.PutAtWithReturn point stringData opKind |> ignore

    member x.PutAtWithReturn point stringData opKind =
        let edit = _textBuffer.CreateEdit()

        // Delete any selections in the buffer
        _textView.Selection.SelectedSpans
        |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)

        match stringData with
        | StringData.Simple(str) -> 

            // Simple strings can go directly in at the position 
            let text = 
                let getLineWiseText() = 
                    let snapshot = SnapshotPointUtil.GetSnapshot point
                    let line = SnapshotUtil.GetLastLine snapshot
                    if point = line.End then
                        // At the end of the file we need to insert an additional
                        // newline prefix
                        System.Environment.NewLine + str
                    else
                        str

                match opKind with
                | OperationKind.LineWise -> getLineWiseText()
                | OperationKind.CharacterWise -> str

            let position = point.Position
            edit.Insert(position, text) |> ignore 
            let snapshot = edit.Apply()
            let startPoint = SnapshotPoint(snapshot, position)
            SnapshotSpanUtil.CreateWithLength startPoint text.Length

        | StringData.Block(col) -> 

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
                let insertCount = min ((lastLineNumber - lineNumber) + 1) col.Length
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
                let prefix = System.Environment.NewLine + (String.replicate column " ")
                let text = Seq.fold (fun text str -> text + prefix + str) "" appendCol
                let endPoint = SnapshotUtil.GetEndPoint originalSnapshot
                edit.Insert(endPoint.Position, text) |> ignore

            let newSnapshot = edit.Apply()
            let line = newSnapshot.GetLineFromLineNumber lineNumber
            let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount line col.Length 
            range.ExtentIncludingLineBreak

    member x.PutAtCaret stringData opKind putKind moveCaretAfterText = 

        // Get the point at which the insertion will occur 
        let caretPoint = TextViewUtil.GetCaretPoint _textView
        let editPoint = 
            match (putKind, opKind) with
            | PutKind.After, OperationKind.CharacterWise -> 
                SnapshotPointUtil.AddOneOrCurrent caretPoint
            | PutKind.After, OperationKind.LineWise -> 
                caretPoint |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetEndIncludingLineBreak
            | PutKind.Before, OperationKind.CharacterWise -> 
                caretPoint
            | PutKind.Before, OperationKind.LineWise -> 
                caretPoint |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetStart

        x.WrapEditInUndoTransaction "Paste" (fun () -> 
            x.PutAt editPoint stringData opKind 
            let position = 
                match opKind with 
                | OperationKind.CharacterWise -> 

                    // Characterwise will just move the cursor to the end of the text on the first line 
                    // of the put.  Unless we moving the caret after the text in which case it will go
                    // one further to the right. 
                    let length = 
                        match stringData with 
                        | StringData.Simple(str) -> str.Length - 1
                        | StringData.Block(col) -> 
                            match col with
                            | h::_ -> h.Length - 1
                            | [] -> 0
                    let length = max 0 length
                    let length = if moveCaretAfterText then length + 1 else length

                    // The PutAt operation can delete text to the left of the caret which changes it's 
                    // original position.  Use an ITrackingPoint to account for this.  
                    match TrackingPointUtil.GetPointInSnapshot editPoint PointTrackingMode.Negative _textBuffer.CurrentSnapshot with
                    | Some(point) -> point.Position + length
                    | None -> editPoint.Position + length   // guess if it can't be found 

                | OperationKind.LineWise ->

                    if moveCaretAfterText then 

                        // Move it past the last insert 
                        let lastChange = caretPoint.Snapshot.Version.Changes |> Seq.filter (fun c -> c.Delta > 0) |> SeqUtil.last
                        lastChange.NewPosition + 1

                    else

                        // Linewise puts it on the first character of the inserted line 
                        let line = 
                            let number = caretPoint |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetLineNumber 
                            match putKind with
                            | PutKind.After -> SnapshotUtil.GetLineOrLast _textBuffer.CurrentSnapshot (number + 1)
                            | PutKind.Before -> SnapshotUtil.GetLineOrFirst _textBuffer.CurrentSnapshot (number - 1)
                        line |> SnapshotLineUtil.GetIndent |> SnapshotPointUtil.GetPosition

            let position = min _textBuffer.CurrentSnapshot.Length position
            let point = SnapshotPoint(_textBuffer.CurrentSnapshot, position)
            TextViewUtil.MoveCaretToPoint _textView point)

    member x.Beep() = if not _settings.GlobalSettings.VisualBell then _host.Beep()

    member x.DoWithOutlining func = 
        match _outlining with
        | None -> x.Beep()
        | Some(outlining) -> func outlining

    member x.CheckDirty func = 
        if _host.IsDirty _textView.TextBuffer then 
            _statusUtil.OnError Resources.Common_NoWriteSinceLastChange
        else
            func()

    member x.IndentForNewLine oldLine (newLine : ITextSnapshotLine) =
        if _settings.UseEditorIndent then
            let indent = _smartIndentationServtice.GetDesiredIndentation(_textView, newLine)
            if indent.HasValue then 
                let point = new VirtualSnapshotPoint(newLine, indent.Value)
                TextViewUtil.MoveCaretToVirtualPoint _textView point |> ignore
        elif _settings.AutoIndent then
            let indent = oldLine |> SnapshotLineUtil.GetIndent |> SnapshotPointUtil.GetColumn
            let point = new VirtualSnapshotPoint(newLine, indent)
            TextViewUtil.MoveCaretToVirtualPoint _textView point |> ignore 
        else
            TextViewUtil.MoveCaretToPoint _textView newLine.Start |> ignore

    interface ICommonOperations with
        member x.TextView = _textView 
        member x.EditorOperations = _operations
        member x.FoldManager = _data.FoldManager
        member x.UndoRedoOperations = _data.UndoRedoOperations
        member x.Join range kind = x.Join range kind
        member x.GoToDefinition () = 
            let before = TextViewUtil.GetCaretPoint _textView
            if _host.GoToDefinition() then
                _jumpList.Add before |> ignore
                Succeeded
            else
                match TssUtil.FindCurrentFullWordSpan _textView.Caret.Position.BufferPosition Vim.WordKind.BigWord with
                | Some(span) -> 
                    let msg = Resources.Common_GotoDefFailed (span.GetText())
                    Failed(msg)
                | None ->  Failed(Resources.Common_GotoDefNoWordUnderCursor) 

        member x.GoToMatch () = _host.GoToMatch()
                
        member x.SetMark (vimBuffer:IVimBuffer) point c = 
            if System.Char.IsLetter(c) || c = '\'' || c = '`' then
                let map = vimBuffer.MarkMap
                map.SetMark point c
                Succeeded
            else
                Failed(Resources.Common_MarkInvalid)

        member x.NavigateToPoint point = x.NavigateToPoint point
                
        member x.JumpToMark ident (map:IMarkMap) = 
            let before = TextViewUtil.GetCaretPoint _textView
            let jumpLocal (point:VirtualSnapshotPoint) = 
                TextViewUtil.MoveCaretToPoint _textView point.Position
                TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining
                _jumpList.Add before |> ignore
                Succeeded
            if not (map.IsLocalMark ident) then 
                match map.GetGlobalMark ident with
                | None -> Failed Resources.Common_MarkNotSet
                | Some(point) -> 
                    match x.NavigateToPoint point with
                    | true -> 
                        _jumpList.Add before |> ignore
                        Succeeded
                    | false -> Failed Resources.Common_MarkInvalid
            else 
                match map.GetLocalMark _textView.TextBuffer ident with
                | Some(point) -> jumpLocal point
                | None -> Failed Resources.Common_MarkNotSet

        /// Move the cursor count spaces left
        member x.MoveCaretLeft count = 
            let caret = TextViewUtil.GetCaretPoint _textView
            let leftPoint = SnapshotPointUtil.GetPreviousPointOnLine caret count
            if caret <> leftPoint then
                _operations.ResetSelection()
                TextViewUtil.MoveCaretToPoint _textView leftPoint
    
        /// Move the cursor count spaces to the right
        member x.MoveCaretRight count =
            let caret = TextViewUtil.GetCaretPoint _textView
            let doMove point = 
                if point <> caret then
                    _operations.ResetSelection()
                    TextViewUtil.MoveCaretToPoint _textView point

            if SnapshotPointUtil.IsLastPointOnLine caret then

                // If we are an the last point of the line then only move if VirtualEdit=onemore
                let line = SnapshotPointUtil.GetContainingLine caret
                if _settings.GlobalSettings.IsVirtualEditOneMore && line.Length > 0 then 
                    doMove line.End
            else

                let rightPoint = SnapshotPointUtil.GetNextPointOnLine caret count
                doMove rightPoint
    
        /// Move the cursor count spaces up 
        member x.MoveCaretUp count =
            let caret = TextViewUtil.GetCaretPoint _textView
            let current = caret.GetContainingLine()
            let count = 
                if current.LineNumber - count > 0 then count
                else current.LineNumber 
            if count > 0 then _operations.ResetSelection()
            for i = 1 to count do   
                _operations.MoveLineUp(false)
            x.MoveCaretForVirtualEdit()

        /// Move the cursor count spaces down
        member x.MoveCaretDown count =
            let caret = TextViewUtil.GetCaretPoint _textView
            let line = caret.GetContainingLine()
            let tss = line.Snapshot
            let count = 
                if line.LineNumber + count < tss.LineCount then count
                else (tss.LineCount - line.LineNumber) - 1 
            if count > 0 then _operations.ResetSelection()
            for i = 1 to count do
                _operations.MoveLineDown(false)
            x.MoveCaretForVirtualEdit()

        member x.MoveWordForward kind count = 
            let caret = TextViewUtil.GetCaretPoint _textView
            let pos = TssUtil.FindNextWordStart caret count kind
            TextViewUtil.MoveCaretToPoint _textView pos 
            
        member x.MoveWordBackward kind count = 
            let caret = TextViewUtil.GetCaretPoint _textView
            let pos = TssUtil.FindPreviousWordStart caret count kind
            TextViewUtil.MoveCaretToPoint _textView pos 

        member x.MoveCaretForVirtualEdit () = x.MoveCaretForVirtualEdit()

        member x.ShiftLineRangeRight multiplier range = x.ShiftLineRangeRight multiplier range

        member x.ShiftBlockRight multiplier block = 
            let lineSpan = SnapshotLineRangeUtil.CreateForNormalizedSnapshotSpanCollection block
            x.ShiftLineRangeRight multiplier lineSpan
        member x.ShiftLineRangeLeft multiplier range = x.ShiftLineRangeLeft multiplier range

        member x.ShiftBlockLeft multiplier block = 
            let lineSpan = SnapshotLineRangeUtil.CreateForNormalizedSnapshotSpanCollection block
            x.ShiftLineRangeLeft multiplier lineSpan

        member x.ShiftLinesRight count = 
            let lineSpan = TextViewUtil.GetCaretLineRange _textView count
            x.ShiftLineRangeRight 1 lineSpan

        member x.ShiftLinesLeft count =
            let lineSpan= TextViewUtil.GetCaretLineRange _textView count
            x.ShiftLineRangeLeft 1 lineSpan

        member x.InsertText text count = 
            let text = StringUtil.repeat count text
            let point = TextViewUtil.GetCaretPoint _textView
            use edit = _textView.TextBuffer.CreateEdit()
            edit.Insert(point.Position, text) |> ignore
            edit.Apply() |> ignore
             
            // Need to adjust the caret to the end of the inserted text.  Very important
            // for operations like repeat
            if not (StringUtil.isNullOrEmpty text) then
                let snapshot = _textView.TextSnapshot
                let position = point.Position + text.Length - 1 
                let caret = SnapshotPoint(snapshot, position)
                _textView.Caret.MoveTo(caret) |> ignore
                _textView.Caret.EnsureVisible()
                

        member x.MoveCaretAndScrollLines dir count =
            let lines = _settings.Scroll
            let tss = _textView.TextSnapshot
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let curLine = caretPoint.GetContainingLine().LineNumber
            let newLine = 
                match dir with
                | ScrollDirection.Down -> min (tss.LineCount - 1) (curLine + lines)
                | ScrollDirection.Up -> max (0) (curLine - lines)
                | _ -> failwith "Invalid enum value"
            let newCaret = tss.GetLineFromLineNumber(newLine).Start
            _operations.ResetSelection()
            _textView.Caret.MoveTo(newCaret) |> ignore
            _textView.Caret.EnsureVisible()

        member x.ScrollLines dir count =
            for i = 1 to count do
                match dir with
                | ScrollDirection.Down -> _operations.ScrollDownAndMoveCaretIfNecessary()
                | ScrollDirection.Up -> _operations.ScrollUpAndMoveCaretIfNecessary()
                | _ -> failwith "Invalid enum value"
    
        member x.ScrollPages dir count = 
            let func,getLine =
                match dir with
                | ScrollDirection.Down -> (_operations.ScrollPageDown, fun () -> _textView.TextViewLines.LastVisibleLine)
                | ScrollDirection.Up -> (_operations.ScrollPageUp, fun () -> _textView.TextViewLines.FirstVisibleLine)
                | _ -> failwith "Invalid enum value"
            for i = 1 to count do
                func()

            // Scrolling itself does not move the caret.  Must be manually moved
            let line = getLine()
            _textView.Caret.MoveTo(line) |> ignore

        member x.DeleteSpan span = x.DeleteSpan span 
        member x.DeleteBlock col = x.DeleteBlock col 
        member x.DeleteLines count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let point = point.GetContainingLine().Start
            let span = SnapshotPointUtil.GetLineRangeSpan point count
            let span = SnapshotSpan(point, span.End)
            x.DeleteSpan span 
            span
        member x.DeleteLinesInSpan span =
            let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
            let span = SnapshotSpan(startLine.Start,endLine.End)
            x.DeleteSpan span 
            span
        member x.DeleteLinesFromCursor count = 
            let point,line = TextViewUtil.GetCaretPointAndLine _textView
            let span = SnapshotPointUtil.GetLineRangeSpan point count
            let span = SnapshotSpan(point, span.End)
            x.DeleteSpan span 
            span
        /// Delete count lines from the cursor.  The last line is an unfortunate special case here 
        /// as it does not have a line break.  Hence in order to delete the line we must delete the 
        /// line break at the end of the preceeding line.  
        ///
        /// This cannot be normalized by always deleting the line break from the previous line because
        /// it would still break for the first line.  This is an unfortunate special case we must 
        /// deal with
        member x.DeleteLinesIncludingLineBreak count = 
            let point,line = TextViewUtil.GetCaretPointAndLine _textView
            let snapshot = point.Snapshot
            let span = 
                if 1 = count && line.LineNumber = SnapshotUtil.GetLastLineNumber snapshot && snapshot.LineCount > 1 then
                    let above = snapshot.GetLineFromLineNumber (line.LineNumber-1)
                    SnapshotSpan(above.End, line.End)
                else
                    let point = line.Start
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    SnapshotSpan(point, span.End)
            x.DeleteSpan span 
            span
        member x.DeleteLinesIncludingLineBreakFromCursor count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
            let span = SnapshotSpan(point, span.End)
            x.DeleteSpan span
            span
        member x.Undo count = _undoRedoOperations.Undo count
        member x.Redo count = _undoRedoOperations.Redo count
        member x.Save() = _host.Save _textView.TextBuffer
        member x.SaveAs fileName = 
            let text = SnapshotUtil.GetText _textView.TextSnapshot
            _host.SaveTextAs text fileName
        member x.SaveAll() = _host.SaveAllFiles()
        member x.Close checkDirty = _host.Close _textView checkDirty
        member x.CloseAll checkDirty = _host.CloseAllFiles checkDirty
        member x.GoToNextTab count = _host.GoToNextTab count
        member x.GoToPreviousTab count = _host.GoToPreviousTab count
        member x.ChangeLetterCase span = x.ChangeLettersOnSpan span CharUtil.ChangeCase
        member x.ChangeLetterCaseBlock col = x.ChangeLettersCore col CharUtil.ChangeCase
        member x.MakeLettersLowercase span = x.ChangeLettersOnSpan span CharUtil.ToLower
        member x.MakeLettersUppercase span = x.ChangeLettersOnSpan span CharUtil.ToUpper
        member x.EnsureCaretOnScreen () = TextViewUtil.EnsureCaretOnScreen _textView 
        member x.EnsureCaretOnScreenAndTextExpanded () = TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining

        member x.EnsurePointOnScreenAndTextExpanded point = 
            _host.EnsureVisible _textView point
            match _outlining with
            | None -> ()
            | Some(outlining) -> outlining.ExpandAll(SnapshotSpan(point,0), fun _ -> true) |> ignore

        member x.MoveCaretToPoint point =  TextViewUtil.MoveCaretToPoint _textView point 
        member x.MoveCaretToMotionData (data:MotionData) =

            // Reduce the Span to the line we care about 
            let line = 
                if data.IsForward then SnapshotSpanUtil.GetEndLine data.Span
                else SnapshotSpanUtil.GetStartLine data.Span

            // Get the point which is the last or first point valid on the 
            // particular line / span 
            let getPointFromSpan () = 
                if data.OperationKind = OperationKind.LineWise then 
                    if data.IsForward then SnapshotLineUtil.GetEnd line
                    else SnapshotLineUtil.GetStart line
                else
                    if data.IsForward then
                        if data.MotionKind = MotionKind.Exclusive then data.Span.End
                        else SnapshotPointUtil.GetPreviousPointOnLine data.Span.End 1
                    else data.Span.Start

            let point = 
                match data.Column with
                | Some(col) -> 
                    let colLine = 

                        // For exclusive forward motions which have a span that ends at the
                        // end of a line and has an explicit column 0, we want to use the 
                        // start of the line following the span instead of the line containing
                        // the span
                        if col = 0 && 
                            data.IsForward && 
                            data.MotionKind = MotionKind.Exclusive &&
                            SnapshotPointUtil.IsStartOfLine data.Span.End then

                            SnapshotPointUtil.GetContainingLine data.Span.End
                        else line
                    let _,endCol = colLine |> SnapshotLineUtil.GetEnd |> SnapshotPointUtil.GetLineColumn
                    if col < endCol then colLine.Start.Add(col)
                    else getPointFromSpan()
                | None -> getPointFromSpan()

            TextViewUtil.MoveCaretToPoint _textView point
            _operations.ResetSelection()

        member x.Beep () = x.Beep()

        member x.OpenFold span count = 
            x.DoWithOutlining (fun outlining ->
                let regions = outlining.GetCollapsedRegions(span) |> Seq.truncate count
                if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
                else  regions |> Seq.iter (fun x -> outlining.Expand(x) |> ignore ))

        member x.OpenAllFolds span =
            x.DoWithOutlining (fun outlining ->
                let regions = outlining.GetCollapsedRegions(span) 
                if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
                else  regions |> Seq.iter (fun x -> outlining.Expand(x) |> ignore ))

        member x.CloseFold span count = 
            x.DoWithOutlining (fun outlining ->
                let pos = span |> SnapshotSpanUtil.GetStartPoint |> SnapshotPointUtil.GetPosition
                let temp = 
                    outlining.GetAllRegions(span) 
                    |> Seq.filter (fun x -> not (x.IsCollapsed))
                    |> Seq.map (fun x -> (TrackingSpanUtil.GetSpan _textView.TextSnapshot x.Extent) ,x)
                    |> SeqUtil.filterToSome2
                    |> Seq.sortBy (fun (span,_) -> pos - span.Start.Position )
                    |> List.ofSeq
                let regions = temp  |> Seq.truncate count
                if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
                else regions |> Seq.iter (fun (_,x) -> outlining.TryCollapse(x) |> ignore))

        member x.CloseAllFolds span =
            x.DoWithOutlining (fun outlining ->
                let regions = outlining.GetAllRegions(span) 
                if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
                else  regions |> Seq.iter (fun x -> outlining.TryCollapse(x) |> ignore ))

        member x.FoldLines count = 
            if count > 1 then 
                let caretLine = TextViewUtil.GetCaretLine _textView
                let span = SnapshotSpanUtil.ExtendDownIncludingLineBreak caretLine.Extent (count-1)
                _data.FoldManager.CreateFold span

        member x.DeleteOneFoldAtCursor () = 
            let point = TextViewUtil.GetCaretPoint _textView
            if not ( _data.FoldManager.DeleteFold point ) then
                _statusUtil.OnError Resources.Common_NoFoldFound

        member x.DeleteAllFoldsAtCursor () =
            let deleteAtCaret () = 
                let point = TextViewUtil.GetCaretPoint _textView
                _data.FoldManager.DeleteFold point
            if not (deleteAtCaret()) then
                _statusUtil.OnError Resources.Common_NoFoldFound
            else
                while deleteAtCaret() do
                    // Keep on deleteing 
                    ()

        member x.MoveToNextOccuranceOfWordAtCursor kind count =  x.MoveToNextWordCore kind count true
        member x.MoveToNextOccuranceOfPartialWordAtCursor kind count = x.MoveToNextWordCore kind count false
        member x.MoveToNextOccuranceOfLastSearch count isReverse = x.MoveToNextOccuranceOfLastSearchCore count isReverse

        member x.ChangeSpan (data:MotionData) =
            
            // For whatever reason the change commands will remove the trailing whitespace
            // for character wise motions that are forward
            let span = 
                if data.OperationKind = OperationKind.LineWise || not data.IsForward then 
                    data.OperationSpan
                else 
                    let point = 
                        data.OperationSpan
                        |> SnapshotSpanUtil.GetPointsBackward 
                        |> Seq.tryFind (fun x -> x.GetChar() |> CharUtil.IsWhiteSpace |> not)
                    match point with 
                    | Some(p) -> 
                        let endPoint = 
                            p
                            |> SnapshotPointUtil.TryAddOne 
                            |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint (p.Snapshot))
                        SnapshotSpan(data.OperationSpan.Start, endPoint)
                    | None -> data.OperationSpan
            x.DeleteSpan span
            span

        member x.Substitute pattern replace (range:SnapshotLineRange) flags = 

            /// Actually do the replace with the given regex
            let doReplace (regex:VimRegex) = 
                use edit = _textView.TextBuffer.CreateEdit()

                let replaceOne (span:SnapshotSpan) (c:Capture) = 
                    let newText =  regex.Replace c.Value replace 1
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
            | None -> _statusUtil.OnError (Resources.Common_PatternNotFound pattern)
            | Some (regex) -> 
                doReplace regex
                _vimData.LastSubstituteData <- Some {SearchPattern=pattern; Substitute=replace; Flags=flags}


        member x.UpdateRegisterForSpan reg regOp span opKind = 
            let value = { Value=StringData.OfSpan span; OperationKind=opKind }
            x.UpdateRegister reg regOp value
        member x.UpdateRegisterForCollection reg regOp col opKind = 
            let value = { Value=StringData.OfNormalizedSnasphotSpanCollection col; OperationKind=opKind }
            x.UpdateRegister reg regOp value

        member x.GoToLocalDeclaration() = 
            if not (_host.GoToLocalDeclaration _textView x.WordUnderCursorOrEmpty) then _host.Beep()

        member x.GoToGlobalDeclaration () = 
            if not (_host.GoToGlobalDeclaration _textView x.WordUnderCursorOrEmpty) then _host.Beep()

        member x.GoToFile () = 
            x.CheckDirty (fun () ->
                let text = x.WordUnderCursorOrEmpty 
                match _host.LoadFileIntoExisting text _textBuffer with
                | HostResult.Success -> ()
                | HostResult.Error(_) -> _statusUtil.OnError (Resources.NormalMode_CantFindFile text))

        member x.InsertLineBelow () =
            let point = TextViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            x.WrapEditInUndoTransactionWithReturn "Paste" (fun () -> 
                buffer.Replace(new Span(line.End.Position,0), System.Environment.NewLine) |> ignore
                let newLine = buffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber+1)
                x.IndentForNewLine line newLine
                newLine )

        member x.InsertLineAbove () = 
            let point = TextViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            x.WrapEditInUndoTransactionWithReturn "Paste" (fun() -> 
                buffer.Replace(new Span(line.Start.Position,0), System.Environment.NewLine) |> ignore
                let newLine = buffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber)
                x.IndentForNewLine line newLine
                newLine)

        member x.WrapEditInUndoTransaction name action = x.WrapEditInUndoTransaction name action

        member x.WrapEditInUndoTransactionWithReturn name action = x.WrapEditInUndoTransactionWithReturn name action

        member x.PutAt point stringData opKind = x.PutAt point stringData opKind

        member x.PutAtCaret stringData opKind putKind moveCaretAfterText = x.PutAtCaret stringData opKind putKind moveCaretAfterText

        member x.PutAtWithReturn point stringData opKind = x.PutAtWithReturn point stringData opKind



