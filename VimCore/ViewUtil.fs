#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module internal ViewUtil =
    let paddingHorizontal = 10.0
    let paddingVertical = 10.0
    
    let ClearSelection (view:ITextView) =
        view.Selection.Clear()
    
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
        
    let MoveCaretToEndOfLine (view:ITextView) = 
        let line = view.Caret.ContainingTextViewLine
        MoveCaretToPosition view line.Extent.End.Position
        
    let MoveCaretToBeginingOfLine (view:ITextView) = 
        let line = view.Caret.ContainingTextViewLine
        MoveCaretToPosition view line.Extent.Start.Position
       
