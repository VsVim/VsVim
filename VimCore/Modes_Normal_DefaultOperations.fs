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
    _operations : IEditorOperations,
    _host : IVimHost ) =
    inherit CommonOperations(_textView, _operations)

    member private x.CommonImpl = x :> ICommonOperations

    interface IOperations with 
        
        /// Paste the given text after the cursor
        member x.PasteAfterCursor text count opKind moveCursor = 
            let text = StringUtil.Repeat text count 
            let caret = ViewUtil.GetCaretPoint _textView
            let span = x.CommonImpl.PasteAfter caret text opKind
            if moveCursor then
                ViewUtil.MoveCaretToPoint _textView span.End |> ignore
            else if opKind = OperationKind.LineWise then
                // For a LineWise paste we want to place the cursor at the start
                // of the next line
                let caretLineNumber = caret.GetContainingLine().LineNumber
                let nextLine = _textView.TextSnapshot.GetLineFromLineNumber(caretLineNumber+1)
                ViewUtil.MoveCaretToPoint _textView nextLine.Start |> ignore
 
        /// Paste the text before the cursor
        member x.PasteBeforeCursor text count moveCursor = 
            let text = StringUtil.Repeat text count 
            let caret = ViewUtil.GetCaretPoint _textView
            let span = x.CommonImpl.PasteBefore caret text 
            if moveCursor then
                ViewUtil.MoveCaretToPoint _textView span.End |> ignore

        member x.InsertLineBelow () =
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            let tss = buffer.Replace(new Span(line.End.Position,0), System.Environment.NewLine)
            let newLine = tss.GetLineFromLineNumber(line.LineNumber+1)
        
            // Move the caret to the same indent position as the previous line
            let indent = TssUtil.FindIndentPosition(line)
            let point = new VirtualSnapshotPoint(newLine, indent)
            ViewUtil.MoveCaretToVirtualPoint _textView point |> ignore
            newLine
    
        member x.InsertLineAbove () = 
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            let tss = buffer.Replace(new Span(line.Start.Position,0), System.Environment.NewLine)
            let line = tss.GetLineFromLineNumber(line.LineNumber)
            ViewUtil.MoveCaretToPoint _textView line.Start |> ignore
            line
                
        /// Implement the r command in normal mode.  
        member x.ReplaceChar (ki:KeyInput) count = 
            let point = ViewUtil.GetCaretPoint _textView

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
    
        /// Yank lines from the buffer.  Implements the Y command
        member x.YankLines count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let point = point.GetContainingLine().Start
            let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
            x.CommonImpl.Yank span MotionKind.Inclusive OperationKind.LineWise reg |> ignore
    
        /// Implement the normal mode x command
        member x.DeleteCharacterAtCursor count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let count = min (count) (line.End.Position-point.Position)
            let span = new SnapshotSpan(point, count)
            x.CommonImpl.DeleteSpan span MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
        /// Implement the normal mode X command
        member x.DeleteCharacterBeforeCursor count reg = 
            let point = ViewUtil.GetCaretPoint _textView
            let range = TssUtil.GetReverseCharacterSpan point count
            x.CommonImpl.DeleteSpan range MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
        member x.JoinAtCaret count =     
            let start = ViewUtil.GetCaretPoint _textView
            let kind = Vim.Modes.JoinKind.RemoveEmptySpaces
            let res = x.CommonImpl.Join start kind count
            if not res then
                _host.Beep()

        member x.GoToDefinitionWrapper () =
            match x.CommonImpl.GoToDefinition _host with
            | Vim.Modes.Succeeded -> ()
            | Vim.Modes.Failed(msg) ->
                _host.UpdateStatus(msg)
    
    