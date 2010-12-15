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
    let _vimData = _data.VimData

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
                    if ki = KeyInputUtil.EnterKey then System.Environment.NewLine
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

        member x.JumpNext count = x.JumpCore count (fun () -> _jumpList.MoveNext())
        member x.JumpPrevious count = x.JumpCore count (fun() -> _jumpList.MovePrevious())

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




