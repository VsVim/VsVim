#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module internal ViewUtil =
    let paddingHorizontal = 10.0
    let paddingVertical = 10.0
    
    let ClearSelection (view:ITextView) =
        view.Selection.Clear()
    
    let VimLineToViewLine num = TssUtil.VimLineToTssLine num
    
    let GetLineNumber (view:ITextView) = 
        let pos = view.Caret.Position.BufferPosition
        let line = view.TextSnapshot.GetLineFromPosition pos.Position
        line.LineNumber
        
    let GetCaretPoint (view:ITextView) = 
        view.Caret.Position.BufferPosition

    let MoveCaretToPoint (view:ITextView) (point:SnapshotPoint) =
        let pos = view.Caret.MoveTo(point)
        view.Caret.EnsureVisible()
        pos        

    let MoveCaretToVirtualPoint (view:ITextView) (point:VirtualSnapshotPoint) =
        let pos = view.Caret.MoveTo(point)        
        view.Caret.EnsureVisible()
        pos
    
    let MoveCaretToPosition (view:ITextView) (pos : int) = 
        let point = SnapshotPoint(view.TextBuffer.CurrentSnapshot, pos)
        MoveCaretToPoint view point
        
    let MoveCaretLeft (view:ITextView) =
        let span = MotionUtil.CharLeft view.Caret.Position.BufferPosition 1
        let point = span.Start
        (MoveCaretToPoint view point).BufferPosition
    
    let MoveCaretRight (view:ITextView) = 
        let span = MotionUtil.CharRight view.Caret.Position.BufferPosition 1
        let point = span.End
        (MoveCaretToPoint view point).BufferPosition
    
    let MoveCaretUp (view:ITextView) =
        let span = MotionUtil.CharUp view.Caret.Position.BufferPosition 1
        let point = span.Start
        (MoveCaretToPoint view point).BufferPosition
                
    let MoveCaretDown (view:ITextView) =
        let span = MotionUtil.CharDown view.Caret.Position.BufferPosition 1
        let point = span.End
        (MoveCaretToPoint view point).BufferPosition

    let MoveCaretToEndOfLine (view:ITextView) = 
        let line = view.Caret.ContainingTextViewLine
        MoveCaretToPosition view line.Extent.End.Position
        
    let MoveCaretToBeginingOfLine (view:ITextView) = 
        let line = view.Caret.ContainingTextViewLine
        MoveCaretToPosition view line.Extent.Start.Position
       
            
    let MoveWordForward (view : ITextView ) kind = 
        let pos = TssUtil.FindNextWordPosition view.Caret.Position.BufferPosition kind
        MoveCaretToPoint view pos |> ignore
        pos
            
    let MoveWordBackward (view: ITextView) kind = 
        let pos = TssUtil.FindPreviousWordPosition view.Caret.Position.BufferPosition kind
        MoveCaretToPoint view pos |> ignore
        pos
    
    let InsertNewLineAfter (view:ITextView) pos =
        let line = view.TextSnapshot.GetLineFromPosition pos
        let next = line.EndIncludingLineBreak+1
        let edit = view.TextBuffer.CreateEdit()
        match edit.Insert(next.Position, System.Environment.NewLine) with
            | true -> 
                edit.Apply() |> ignore
                true
            | false -> false
    
    let MoveToLastLineStart (view:IWpfTextView) =
        let last = TssUtil.GetLastLine(view.TextSnapshot)
        (MoveCaretToPoint view (last.Start)).BufferPosition
    
    let MoveToLineStart (view:IWpfTextView) (line:ITextSnapshotLine) =
        (MoveCaretToPoint view (line.Start)).BufferPosition
    
    let CurrentPosition (view:ITextView) = view.Caret.Position.BufferPosition.Position
        
    let GetLineText (view:ITextView) pos =
        let line = view.TextSnapshot.GetLineFromPosition pos
        line.GetText()
    
    let FindCurrentFullWord (view:ITextView) kind =
        let pos = CurrentPosition view
        let line = view.TextSnapshot.GetLineFromPosition pos
        TextUtil.FindFullWord kind (line.GetText()) (pos-line.Start.Position)
        
    let FindNextWordStart (view:ITextView) kind =
        (TssUtil.FindNextWordPosition view.Caret.Position.BufferPosition kind).Position
        
    let FindPreviousWordStart (view:ITextView) kind =
        (TssUtil.FindPreviousWordPosition view.Caret.Position.BufferPosition kind).Position
        
    // Select the specified span inside the view
    let SelectSpan (view:ITextView) (span:Span) =
        let ss = SnapshotSpan(view.TextBuffer.CurrentSnapshot, span)
        view.Selection.Select(ss, false)

    
    
