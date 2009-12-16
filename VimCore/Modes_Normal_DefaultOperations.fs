#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input
open System.Windows.Media

type internal DefaultOperations
    (
    _textView : ITextView,
    _operations : IEditorOperations ) =

    interface IOperations with 
        member x.TextView = _textView 

        /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
        member x.Mark (d:NormalModeData) =
            let waitForKey (d2:NormalModeData) (ki:KeyInput) =
                let bufferData = d2.VimBufferData
                let cursor = ViewUtil.GetCaretPoint bufferData.TextView
                let res = Modes.ModeUtil.SetMark bufferData.MarkMap cursor ki.Char
                match res with
                | ModeUtil.Failed(_) -> bufferData.VimHost.Beep()
                | _ -> ()
                NormalModeResult.Complete
            NormalModeResult.NeedMore2 waitForKey
    
        /// Process the ' or ` jump to mark keys
        member x.JumpToMark (d:NormalModeData) =
            let waitForKey (d:NormalModeData) (ki:KeyInput) =
                let bufferData = d.VimBufferData
                let res = Modes.ModeUtil.JumpToMark bufferData.MarkMap bufferData.TextView ki.Char
                match res with 
                | ModeUtil.Failed(msg) -> bufferData.VimHost.UpdateStatus(msg)
                | _ -> ()
                NormalModeResult.Complete
            NormalModeResult.NeedMore2 waitForKey
    
        /// Paste the given text after the cursor
        member x.PasteAfter text count opKind moveCursor = 
            let text = StringUtil.Repeat text count 
            let caret = ViewUtil.GetCaretPoint _textView
            let span = ModeUtil.PasteAfter caret text opKind
            if moveCursor then
                ViewUtil.MoveCaretToPoint _textView span.End |> ignore
            else if opKind = OperationKind.LineWise then
                // For a LineWise paste we want to place the cursor at the start
                // of the next line
                let caretLineNumber = caret.GetContainingLine().LineNumber
                let nextLine = _textView.TextSnapshot.GetLineFromLineNumber(caretLineNumber+1)
                ViewUtil.MoveCaretToPoint _textView nextLine.Start |> ignore
 
        /// Paste the text before the cursor
        member x.PasteBefore text count moveCursor = 
            let text = StringUtil.Repeat text count 
            let caret = ViewUtil.GetCaretPoint _textView
            let span = ModeUtil.PasteBefore caret text 
            if moveCursor then
                ViewUtil.MoveCaretToPoint _textView span.End |> ignore

        /// Insert a line above the current cursor position
        member x.InsertLineAbove (d:NormalModeData) = 
            let point = ViewUtil.GetCaretPoint d.VimBufferData.TextView
            let line = BufferUtil.AddLineAbove (point.GetContainingLine()) 
            d.VimBufferData.TextView.Caret.MoveTo(line.Start) |> ignore
            NormalModeResult.Complete
            
        /// Implement the r command in normal mode.  
        member x.ReplaceChar (ki:KeyInput) count = 
            let point = ViewUtil.GetCaretPoint _textView

            // Make sure the replace string is valid
            if (point.Position + count) > point.GetContainingLine().End.Position then
                false
            else
                let isNewLine = (ki.Key = Key.LineFeed) || (ki.Key = Key.Return)
                let replaceText = 
                    if isNewLine then System.Environment.NewLine
                    else new System.String(ki.Char, count)
                let span = new Span(point.Position, count)
                let tss = _textView.TextBuffer.Replace(span, replaceText) 

                // Reset the caret to the point before the edit
                let point = new SnapshotPoint(tss,point.Position)
                _textView.Caret.MoveTo(point) |> ignore
                true
    
        /// Yank lines from the buffer.  Implements the Y command
        member x.YankLines count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let point = point.GetContainingLine().Start
            let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
            Modes.ModeUtil.Yank span MotionKind.Inclusive OperationKind.LineWise reg |> ignore
    
        /// Implement the normal mode x command
        member x.DeleteCharacterAtCursor count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let count = min (count) (line.End.Position-point.Position)
            let span = new SnapshotSpan(point, count)
            Modes.ModeUtil.DeleteSpan span MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
        /// Implement the normal mode X command
        member x.DeleteCharacterBeforeCursor count reg = 
            let point = ViewUtil.GetCaretPoint _textView
            let range = TssUtil.GetReverseCharacterSpan point count
            Modes.ModeUtil.DeleteSpan range MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
    
    
    