#light

namespace VimCore.Modes.Common
open VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module Operations =

    /// Implements the Join command.  Returns false in the case the join command cannot
    /// be complete (such as joining at the end of the buffer)
    let Join (view:ITextView) count = 

        // Always joining at least 2 lines so we subtract to get the number of join
        // operations.  1 is a valid input though
        let count = if count > 1 then count-1 else 1

        // Join the line returning place the caret should be positioned
        let joinLine (buffer:ITextBuffer) lineNumber =
            let tss = buffer.CurrentSnapshot
            use edit = buffer.CreateEdit()
            let line = buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber)
            let lineBreakSpan = Span(line.End.Position, line.LineBreakLength)
            edit.Replace(lineBreakSpan, " ") |> ignore

            // Strip out the whitespace at the start of the next line
            let maybeStripWhiteSpace ()= 
                let nextLine = tss.GetLineFromLineNumber(lineNumber+1)
                let rec countSpace (index) =
                    if index < nextLine.Length && System.Char.IsWhiteSpace(nextLine.Start.Add(index).GetChar()) then
                        countSpace(index+1)
                    else
                        index
                match countSpace 0 with
                | 0 -> ()
                | value -> edit.Delete(nextLine.Start.Position, value) |> ignore

            maybeStripWhiteSpace()
            edit.Apply() |> ignore
            line.End.Position + 1

        let joinLineAndMoveCaret lineNumber =
            let caret = joinLine view.TextBuffer lineNumber
            ViewUtil.MoveCaretToPosition view caret |> ignore

        let rec inner count = 
            let tss = view.TextBuffer.CurrentSnapshot
            let caret = ViewUtil.GetCaretPoint view
            let lineNumber = caret.GetContainingLine().LineNumber
            if lineNumber = tss.LineCount + 1 then
                false
            else
                joinLineAndMoveCaret lineNumber
                match count with
                | 1 -> true
                | _ -> inner (count-1)
        inner count
            

            
    
