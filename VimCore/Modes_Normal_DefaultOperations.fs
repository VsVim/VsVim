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
    
        /// Handles commands which begin with g in normal mode.  This should be called when the g char is
        /// already processed
        member x.CharGCommand (d:NormalModeData) =
            let data = d.VimBufferData
            let inner (d:NormalModeData) (ki:KeyInput) =  
                match ki.Char with
                | 'J' -> 
                    let view = data.TextView
                    let caret = ViewUtil.GetCaretPoint view
                    ModeUtil.Join view caret JoinKind.KeepEmptySpaces d.Count |> ignore
                | 'p' -> 
                    let caret  = ViewUtil.GetCaretPoint data.TextView
                    let reg = d.Register.Value
                    let span = Modes.ModeUtil.PasteAfter caret reg.Value reg.OperationKind 
                    data.TextView.Caret.MoveTo(span.End) |> ignore
                | 'P' ->
                    let caret = ViewUtil.GetCaretPoint data.TextView
                    let text = d.Register.StringValue
                    let span = Modes.ModeUtil.PasteBefore caret text
                    data.TextView.Caret.MoveTo(span.End) |> ignore
                | _ ->
                    d.VimBufferData.VimHost.Beep()
                    ()
                NormalModeResult.Complete
            NeedMore2(inner)
                    
        /// Insert a line above the current cursor position
        member x.InsertLineAbove (d:NormalModeData) = 
            let point = ViewUtil.GetCaretPoint d.VimBufferData.TextView
            let line = BufferUtil.AddLineAbove (point.GetContainingLine()) 
            d.VimBufferData.TextView.Caret.MoveTo(line.Start) |> ignore
            NormalModeResult.Complete
            
        /// Implement the r command in normal mode.  
        member x.ReplaceChar (d:NormalModeData) = 
            let inner (d:NormalModeData) (ki:KeyInput) =
                let bufferData = d.VimBufferData
                let point = ViewUtil.GetCaretPoint bufferData.TextView
    
                // Make sure the replace string is valid
                if (point.Position + d.Count) > point.GetContainingLine().End.Position then
                    bufferData.VimHost.Beep()
                else
                    let isNewLine = (ki.Key = Key.LineFeed) || (ki.Key = Key.Return)
                    let replaceText = 
                        if isNewLine then System.Environment.NewLine
                        else new System.String(ki.Char, d.Count)
                    let span = new Span(point.Position,d.Count)
                    let tss = bufferData.TextBuffer.Replace(span, replaceText) 
    
                    // Reset the caret to the point before the edit
                    let point = new SnapshotPoint(tss,point.Position)
                    bufferData.TextView.Caret.MoveTo(point) |> ignore
                d.VimBufferData.BlockCaret.Show()
                NormalModeResult.Complete
            d.VimBufferData.BlockCaret.Hide()
            NeedMore2(inner)
    
        /// Yank lines from the buffer.  Implements the Y command
        member x.YankLines (d:NormalModeData) =
            let data = d.VimBufferData
            let point = ViewUtil.GetCaretPoint data.TextView
            let point = point.GetContainingLine().Start
            let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count 
            Modes.ModeUtil.Yank span MotionKind.Inclusive OperationKind.LineWise d.Register |> ignore
            NormalModeResult.Complete
    
        /// Implement the normal mode x command
        member x.DeleteCharacterAtCursor count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let count = min (count) (line.End.Position-point.Position)
            let span = new SnapshotSpan(point, count)
            Modes.ModeUtil.DeleteSpan span MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
        /// Implement the normal mode X command
        member x.DeleteCharacterBeforeCursor (d:NormalModeData) = 
            let data = d.VimBufferData
            let point = ViewUtil.GetCaretPoint data.TextView
            let range = TssUtil.GetReverseCharacterSpan point d.Count
            Modes.ModeUtil.DeleteSpan range MotionKind.Exclusive OperationKind.CharacterWise d.Register |> ignore
            NormalModeResult.Complete
    
    
    
    