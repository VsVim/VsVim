#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

type internal DefaultOperations ( _data : OperationsData, _incrementalSearch : IIncrementalSearch ) =
    inherit CommonOperations(_data)

    let _textView = _data.TextView
    let _operations = _data.EditorOperations
    let _outlining = _data.OutliningManager
    let _host = _data.VimHost
    let _jumpList = _data.JumpList
    let _settings = _data.LocalSettings
    let _undoRedoOperations = _data.UndoRedoOperations
    let _options = _data.EditorOptions
    let _normalWordNav =  _data.Navigator
    let _statusUtil = _data.StatusUtil
    let _search = _incrementalSearch.SearchService

    member private x.CommonImpl = x :> ICommonOperations

    member private x.JumpCore count moveJump =
        let rec inner count = 
            if count >= 1 && moveJump() then inner (count-1)
            elif count = 0 then true
            else false
        if not (inner count) then _host.Beep()
        else
            match _jumpList.Current with
            | None -> _host.Beep()
            | Some(point) -> 
                let ret = x.CommonImpl.NavigateToPoint (VirtualSnapshotPoint(point))
                if not ret then _host.Beep()

    member private x.WordUnderCursorOrEmpty =
        let point =  TextViewUtil.GetCaretPoint _textView
        TssUtil.FindCurrentFullWordSpan point WordKind.BigWord
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateEmpty point)
        |> SnapshotSpanUtil.GetText

    member private x.MoveToNextWordCore kind count isWholeWord = 
        let point = TextViewUtil.GetCaretPoint _textView
        match TssUtil.FindCurrentFullWordSpan point WordKind.NormalWord with
        | None -> _statusUtil.OnError Resources.NormalMode_NoWordUnderCursor
        | Some(span) ->

            // Build up the SearchData structure
            let word = span.GetText()
            let text = if isWholeWord then SearchText.WholeWord(word) else SearchText.StraightText(word)
            let data = {Text=text; Kind = kind; Options = SearchOptions.AllowIgnoreCase }

            // When forward the search will be starting on the current word so it will 
            // always match.  Without modification a count of 1 would simply find the word 
            // under the cursor.  Increment the count by 1 here so that it will find
            // the current word as the 0th match (so to speak)
            let count = if SearchKindUtil.IsForward kind then count + 1 else count 

            match _search.FindNextMultiple data point _normalWordNav count with
            | Some(span) -> 
                x.CommonImpl.MoveCaretToPoint span.Start 
                x.CommonImpl.EnsureCaretOnScreenAndTextExpanded()
            | None -> ()

            _search.LastSearch <- data

    member private x.MoveToNextOccuranceOfLastSearchCore count isReverse = 
        let search = _incrementalSearch.SearchService
        let last = search.LastSearch
        let last = 
            if isReverse then { last with Kind = SearchKindUtil.Reverse last.Kind }
            else last

        if StringUtil.isNullOrEmpty last.Text.RawText then
            _statusUtil.OnError Resources.NormalMode_NoPreviousSearch
        else

            let foundSpan (span:SnapshotSpan) = 
                x.CommonImpl.MoveCaretToPoint span.Start 
                x.CommonImpl.EnsureCaretOnScreenAndTextExpanded()

            let findMore (span:SnapshotSpan) count = 
                if count = 1 then foundSpan span
                else 
                    let count = count - 1 
                    match _search.FindNextMultiple last span.End _normalWordNav count with
                    | Some(span) -> foundSpan span
                    | None -> _statusUtil.OnError (Resources.NormalMode_PatternNotFound last.Text.RawText)

            // Make sure we don't count the current word if the cursor is positioned
            // directly on top of the current word 
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            match _search.FindNext last caretPoint _normalWordNav with
            | None -> _statusUtil.OnError (Resources.NormalMode_PatternNotFound last.Text.RawText)
            | Some(span) ->
                let count = if span.Start = caretPoint then count else count - 1 
                if count = 0 then foundSpan span
                else 
                    match _search.FindNextMultiple last span.End _normalWordNav count with
                    | Some(span) -> foundSpan span
                    | None -> _statusUtil.OnError (Resources.NormalMode_PatternNotFound last.Text.RawText)

    member x.GoToLineCore line =
        let snapshot = _textView.TextSnapshot
        let lastLineNumber = snapshot.LineCount - 1
        let line = min line lastLineNumber
        let textLine = snapshot.GetLineFromLineNumber(line)
        if _settings.GlobalSettings.StartOfLine then 
            _textView.Caret.MoveTo( textLine.Start ) |> ignore
            _operations.MoveToStartOfLineAfterWhiteSpace(false)
        else 
            let point = TextViewUtil.GetCaretPoint _textView
            let _,column = SnapshotPointUtil.GetLineColumn point
            let column = min column textLine.Length
            let point = textLine.Start.Add(column)
            _textView.Caret.MoveTo (point) |> ignore

    /// Wrap the passed in "action" inside an undo transaction.  This is needed
    /// when making edits such as paste so that the cursor will move properly 
    /// during an undo operation
    member private x.WrapInUndoTransaction name action =
        use undoTransaction = _undoRedoOperations.CreateUndoTransaction(name)
        _operations.AddBeforeTextBufferChangePrimitive()
        action()
        _operations.AddAfterTextBufferChangePrimitive()
        undoTransaction.Complete()

    /// Same as WrapInUndoTransaction except provides for a return value
    member private x.WrapInUndoTransactionWithRet name action =
        use undoTransaction = _undoRedoOperations.CreateUndoTransaction(name)
        _operations.AddBeforeTextBufferChangePrimitive()
        let ret = action()
        _operations.AddAfterTextBufferChangePrimitive()
        undoTransaction.Complete()
        ret

    interface IOperations with 
        
        /// Paste the given text after the cursor
        member x.PasteAfterCursor text count opKind moveCursor = 
            let text = StringUtil.repeat text count 
            let caret = TextViewUtil.GetCaretPoint _textView
            x.WrapInUndoTransaction "Paste" (fun () -> 
                let span = x.CommonImpl.PasteAfter caret text opKind
                if moveCursor then
                    x.CommonImpl.MoveCaretToPoint span.End 
                else if opKind = OperationKind.LineWise then
                    // For a LineWise paste we want to place the cursor at the start
                    // of the next line
                    let caretLineNumber = caret.GetContainingLine().LineNumber
                    let nextLine = _textView.TextSnapshot.GetLineFromLineNumber(caretLineNumber + 1)
                    let point = TssUtil.FindFirstNonWhitespaceCharacter nextLine
                    x.CommonImpl.MoveCaretToPoint point  )
 
        /// Paste the text before the cursor
        member x.PasteBeforeCursor text count opKind moveCursor = 
            let text = StringUtil.repeat text count 
            let caret = TextViewUtil.GetCaretPoint _textView
            x.WrapInUndoTransaction "Paste" (fun () -> 
                let span = x.CommonImpl.PasteBefore caret text opKind
                if moveCursor then
                    x.CommonImpl.MoveCaretToPoint span.End 
                else if opKind = OperationKind.LineWise then
                    // For a LineWise paste we want to place the cursor at the start of this line. caret is a a snapshot
                    // point from the old snapshot, so we need to find the same line in the new snapshot
                    let line = _textView.TextSnapshot.GetLineFromLineNumber(caret.GetContainingLine().LineNumber)
                    let point = TssUtil.FindFirstNonWhitespaceCharacter line
                    x.CommonImpl.MoveCaretToPoint point )

        member x.InsertLineBelow () =
            let point = TextViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            x.WrapInUndoTransactionWithRet "Paste" (fun () -> 
                buffer.Replace(new Span(line.End.Position,0), System.Environment.NewLine) |> ignore
                let newLine = buffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber+1)
            
                // Move the caret to the same indent position as the previous line
                let tabSize = EditorOptionsUtil.GetOptionValueOrDefault _options DefaultOptions.TabSizeOptionId 4
                let indent = TssUtil.FindIndentPosition line tabSize
                let point = new VirtualSnapshotPoint(newLine, indent)
                TextViewUtil.MoveCaretToVirtualPoint _textView point |> ignore 
                newLine )
    
        member x.InsertLineAbove () = 
            let point = TextViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            x.WrapInUndoTransactionWithRet "Paste" (fun() -> 
                buffer.Replace(new Span(line.Start.Position,0), System.Environment.NewLine) |> ignore
                let line = buffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber)
                x.CommonImpl.MoveCaretToPoint line.Start 
                line )
                    
        /// Implement the r command in normal mode.  
        member x.ReplaceChar (ki:KeyInput) count = 
            let point = TextViewUtil.GetCaretPoint _textView

            // Make sure the replace string is valid
            if (point.Position + count) > point.GetContainingLine().End.Position then
                false
            else
                let replaceText = 
                    if ki.IsNewLine then System.Environment.NewLine
                    else new System.String(ki.Char, count)
                let span = new Span(point.Position, count)
                let tss = _textView.TextBuffer.Replace(span, replaceText) 

                // Reset the caret to the point before the edit
                let point = new SnapshotPoint(tss,point.Position)
                _textView.Caret.MoveTo(point) |> ignore
                true
    
        /// Implement the normal mode x command
        member x.DeleteCharacterAtCursor count =
            let point = TextViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let count = min (count) (line.End.Position-point.Position)
            let span = new SnapshotSpan(point, count)
            x.CommonImpl.DeleteSpan span 

            // Need to respect the virtual edit setting here as we could have 
            // deleted the last character on the line
            x.CommonImpl.MoveCaretForVirtualEdit()

            span
    
        /// Implement the normal mode X command
        member x.DeleteCharacterBeforeCursor count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let span = TssUtil.GetReverseCharacterSpan point count
            x.CommonImpl.DeleteSpan span
            span

        member x.JoinAtCaret count =     
            let start = TextViewUtil.GetCaretPoint _textView
            let kind = Vim.Modes.JoinKind.RemoveEmptySpaces
            let res = x.CommonImpl.Join start kind count
            if not res then
                _host.Beep()

        member x.GoToDefinitionWrapper () =
            match x.CommonImpl.GoToDefinition() with
            | Vim.Modes.Succeeded -> ()
            | Vim.Modes.Failed(msg) -> _statusUtil.OnError msg


        member x.GoToLocalDeclaration() = 
            if not (_host.GoToLocalDeclaration _textView x.WordUnderCursorOrEmpty) then _host.Beep()

        member x.GoToGlobalDeclaration () = 
            if not (_host.GoToGlobalDeclaration _textView x.WordUnderCursorOrEmpty) then _host.Beep()

        member x.GoToFile () = 
            let text = x.WordUnderCursorOrEmpty 
            if not (_host.GoToFile text) then 
                _statusUtil.OnError (Resources.NormalMode_CantFindFile text)

        member x.MoveToNextOccuranceOfWordAtCursor kind count =  x.MoveToNextWordCore kind count true
        member x.MoveToNextOccuranceOfPartialWordAtCursor kind count = x.MoveToNextWordCore kind count false
        member x.JumpNext count = x.JumpCore count (fun () -> _jumpList.MoveNext())
        member x.JumpPrevious count = x.JumpCore count (fun() -> _jumpList.MovePrevious())
        member x.MoveToNextOccuranceOfLastSearch count isReverse = x.MoveToNextOccuranceOfLastSearchCore count isReverse

        member x.GoToLineOrFirst count =
            let line =
                match count with
                | None -> 0
                | Some(c) -> c
            x.GoToLineCore line

        member x.GoToLineOrLast count =
            let snapshot = _textView.TextSnapshot
            let lastLineNumber = snapshot.LineCount - 1
            let line = 
                match count with
                | None -> lastLineNumber
                // Surprisingly 0 goes to the last line number in gVim
                | Some(c) when c = 0 -> lastLineNumber
                | Some(c) -> c
            x.GoToLineCore line

        member x.ChangeLetterCaseAtCursor count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let line = SnapshotPointUtil.GetContainingLine point
            let count = min count (line.End.Position - point.Position)
            let span = SnapshotSpan(point, count)
            x.CommonImpl.ChangeLetterCase span

            if line.Length > 0 then
                
                // Because we aren't changing the length of the buffer it's OK 
                // to calculate with respect to the points before the edit
                let pos = point.Position + count
                let pos = min pos (line.End.Position-1)
                TextViewUtil.MoveCaretToPosition _textView pos |> ignore
            
        member x.MoveCaretForAppend () = 
            let point = TextViewUtil.GetCaretPoint _textView
            _operations.ResetSelection()
            if SnapshotPointUtil.IsInsideLineBreak point then ()
            elif SnapshotPointUtil.IsEndPoint point then ()
            else 
                let point = point.Add(1)
                TextViewUtil.MoveCaretToPoint _textView point |> ignore




