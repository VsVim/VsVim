#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

module ModeUtil =

    /// Implements the Join command.  Returns false in the case the join command cannot
    /// be complete (such as joining at the end of the buffer)
    let Join (view:ITextView) (start:SnapshotPoint) (kind:JoinKind) count = 

        // Always joining at least 2 lines so we subtract to get the number of join
        // operations.  1 is a valid input though
        let count = if count > 1 then count-1 else 1

        // Join the line returning place the caret should be positioned
        let joinLine (buffer:ITextBuffer) lineNumber =
            let tss = buffer.CurrentSnapshot
            use edit = buffer.CreateEdit()
            let line = buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber)
            let lineBreakSpan = Span(line.End.Position, line.LineBreakLength)

            // Strip out the whitespace at the start of the next line
            let maybeStripWhiteSpace () = 
                let nextLine = tss.GetLineFromLineNumber(lineNumber+1)
                let rec countSpace (index) =
                    if index < nextLine.Length && System.Char.IsWhiteSpace(nextLine.Start.Add(index).GetChar()) then
                        countSpace(index+1)
                    else
                        index
                match countSpace 0 with
                | 0 -> ()
                | value -> edit.Delete(nextLine.Start.Position, value) |> ignore

            match kind with 
            | RemoveEmptySpaces ->  
                edit.Replace(lineBreakSpan, " ") |> ignore
                maybeStripWhiteSpace()
            | KeepEmptySpaces -> 
                edit.Delete(lineBreakSpan) |> ignore

            edit.Apply() |> ignore
            line.End.Position + 1

        let joinLineAndMoveCaret lineNumber =
            let caret = joinLine view.TextBuffer lineNumber
            ViewUtil.MoveCaretToPosition view caret |> ignore

        let rec inner count = 
            let tss = view.TextBuffer.CurrentSnapshot
            let lineNumber = start.GetContainingLine().LineNumber
            if lineNumber = tss.LineCount + 1 then
                false
            else
                joinLineAndMoveCaret lineNumber
                match count with
                | 1 -> true
                | _ -> inner (count-1)
        inner count
            
    /// Attempt to GoToDefinition on the current state of the buffer.  If this operation fails, an error message will 
    /// be generated as appropriate
    let GoToDefinition (view:ITextView) (host:Vim.IVimHost) =
        if host.GoToDefinition() then
            Succeeded
        else
            match TssUtil.FindCurrentFullWordSpan view.Caret.Position.BufferPosition Vim.WordKind.BigWord with
            | Some(span) -> 
                let param1 = (span.GetText()) :> obj
                let msg = System.String.Format("Could not navigate to definition of '{0}'", param1)
                Failed(msg)
            | None ->  Failed("Could not navigate to definition of word under cursor")

            
    /// Sets a mark at the specified point.  If this operation fails an error message will be generated
    let SetMark (map:MarkMap) (point:SnapshotPoint) c =
        if System.Char.IsLetter(c) || c = '\'' || c = '`' then
            map.SetMark point c
            Succeeded
        else
            Failed("Argument must be a letter or forward / back quote")
            
            
    /// Jumps to a given mark in the buffer.  
    /// TODO: Support global marks.  
    let JumpToMark (map:MarkMap) (view:ITextView) ident =
        if not (MarkMap.IsLocalMark ident) then Failed "Only local marks are supported at this time"
        else
            match map.GetLocalMark view.TextBuffer ident with
            | Some(point) -> 
                ViewUtil.MoveCaretToPoint view point.Position |> ignore
                Succeeded
            | None -> Failed "Mark not set"

    /// Yank the span into the given register
    let Yank (span:SnapshotSpan) motion operation (reg:Register) =
        let regValue = {Value=span.GetText();MotionKind = motion; OperationKind = operation};
        reg.UpdateValue (regValue)
    
    /// Paste after the passed in position.  Don't forget that a linewise paste
    /// operation needs to occur under the cursor.  Returns the SnapshotSpan of
    /// the text on the new snapshot
    let PasteAfter (point:SnapshotPoint) text opKind = 
        let buffer = point.Snapshot.TextBuffer
        let replaceSpan = 
            match opKind with
            | OperationKind.LineWise ->
                let line = point.GetContainingLine()
                new SnapshotSpan(line.EndIncludingLineBreak, 0)
            | OperationKind.CharacterWise ->
                let line = point.GetContainingLine()
                let point =  if point.Position < line.End.Position then point.Add(1) else point
                new SnapshotSpan(point,0)
            | _ -> failwith "Invalid Enum Value"
        let tss = buffer.Replace(replaceSpan.Span, text)
        new SnapshotSpan(tss, replaceSpan.End.Position, text.Length)
    
    /// Paste the text before the passed in position.  Returns the SnapshotSpan for the text in
    /// the new snapshot of the buffer
    let PasteBefore (point:SnapshotPoint) text =
        let span = new SnapshotSpan(point,0)
        let buffer = point.Snapshot.TextBuffer
        let tss = buffer.Replace(span.Span, text) 
        new SnapshotSpan(tss,span.End.Position, text.Length)

    // Delete a range of text
    let DeleteSpan (span:SnapshotSpan) motionKind opKind (reg:Register) =
        let tss = span.Snapshot
        let regValue = {Value=span.GetText();MotionKind=motionKind;OperationKind=opKind}
        reg.UpdateValue(regValue) 
        tss.TextBuffer.Delete(span.Span)

    /// Move the cursor count spaces left
    let MoveCaretLeft view (editorOpts : IEditorOperations) count = 
        editorOpts.ResetSelection()
        let caret = ViewUtil.GetCaretPoint view
        let span = MotionUtil.CharLeft caret count
        ViewUtil.MoveCaretToPoint view span.Start |> ignore

    /// Move the cursor count spaces to the right
    let MoveCaretRight view (editorOpts : IEditorOperations) count =
        editorOpts.ResetSelection()
        let caret = ViewUtil.GetCaretPoint view
        let span = MotionUtil.CharRight caret count
        ViewUtil.MoveCaretToPoint view span.End  |> ignore

    /// Move the cursor count spaces up 
    let MoveCaretUp view (editorOpts:IEditorOperations) count =
        editorOpts.ResetSelection()
        let caret = ViewUtil.GetCaretPoint view
        let current = caret.GetContainingLine()
        let count = 
            if current.LineNumber - count > 0 then count
            else current.LineNumber 
        for i = 0 to count do   
            editorOpts.MoveLineUp(false)
        
    /// Move the cursor count spaces down
    let MoveCaretDown view (editorOpts : IEditorOperations) count =
        editorOpts.ResetSelection()
        let caret = ViewUtil.GetCaretPoint view
        let line = caret.GetContainingLine()
        let tss = line.Snapshot
        if line.LineNumber + count < tss.LineCount then 
            for i = 0 to count do
                editorOpts.MoveLineDown(false)
        else editorOpts.MoveToEndOfDocument(false)


