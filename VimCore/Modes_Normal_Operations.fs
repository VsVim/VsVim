#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes.Common
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

module internal Operations =

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    let Mark (d:NormalModeData) =
        let waitForKey (d2:NormalModeData) (ki:KeyInput) =
            let bufferData = d2.VimBufferData
            let cursor = ViewUtil.GetCaretPoint bufferData.TextView
            let res = Modes.Common.Operations.SetMark bufferData.MarkMap cursor ki.Char
            match res with
            | Operations.Failed(_) -> bufferData.VimHost.Beep()
            | _ -> ()
            NormalModeResult.Complete
        NormalModeResult.NeedMore2 waitForKey

    /// Process the ' or ` jump to mark keys
    let JumpToMark (d:NormalModeData) =
        let waitForKey (d:NormalModeData) (ki:KeyInput) =
            let bufferData = d.VimBufferData
            let res = Modes.Common.Operations.JumpToMark bufferData.MarkMap bufferData.TextView ki.Char
            match res with 
            | Operations.Failed(msg) -> bufferData.VimHost.UpdateStatus(msg)
            | _ -> ()
            NormalModeResult.Complete
        NormalModeResult.NeedMore2 waitForKey

    /// Handles commands which begin with g in normal mode.  This should be called when the g char is
    /// already processed
    let CharGCommand (d:NormalModeData) =
        let inner (d:NormalModeData) (ki:KeyInput) =  
            match ki.Char with
            | 'J' -> 
                let view = d.VimBufferData.TextView
                let caret = ViewUtil.GetCaretPoint view
                Operations.Join view caret JoinKind.KeepEmptySpaces d.Count |> ignore
            | _ ->
                d.VimBufferData.VimHost.Beep()
                ()
            NormalModeResult.Complete
        NeedMore2(inner)
                
    /// Insert a line above the current cursor position
    let InsertLineAbove (d:NormalModeData) = 
        let point = ViewUtil.GetCaretPoint d.VimBufferData.TextView
        let line = BufferUtil.AddLineAbove (point.GetContainingLine()) 
        d.VimBufferData.TextView.Caret.MoveTo(line.Start) |> ignore
        NormalModeResult.Complete
        
    /// Implement the r command in normal mode.  
    let ReplaceChar (d:NormalModeData) = 
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
    let YankLines (d:NormalModeData) =
        let data = d.VimBufferData
        let point = ViewUtil.GetCaretPoint data.TextView
        let point = point.GetContainingLine().Start
        let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count 
        Modes.Common.Operations.Yank span MotionKind.Inclusive OperationKind.LineWise d.Register |> ignore
        NormalModeResult.Complete



