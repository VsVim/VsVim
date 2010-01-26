#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim.Modes
open Vim

type internal DefaultOperations 
    ( 
        _textView : ITextView,
        _operations : IEditorOperations,
        _tracker : ISelectionTracker ) =
    inherit CommonOperations(_textView, _operations)

    member private x.CommonOperations = x :> ICommonOperations

    interface IOperations with
        member x.SelectionTracker = _tracker
        member x.DeleteSelection (reg:Register) = 
            let value = { Value=_tracker.SelectedText; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise }
            reg.UpdateValue(value)
            use edit = _textView.TextBuffer.CreateEdit()
            _textView.Selection.SelectedSpans |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
            edit.Apply() 
        member x.DeleteSelectedLines (reg:Register) = 
            let span = _tracker.SelectedLines
            let value = { Value=span.GetText(); MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise }
            reg.UpdateValue(value)
            use edit = _textView.TextBuffer.CreateEdit()
            edit.Delete(span.Span) |> ignore
            edit.Apply()
        member x.JoinSelection kind = 
            let selection = _textView.Selection
            let start = selection.Start.Position
            let startLine = start.GetContainingLine()
            let endLine = selection.End.Position.GetContainingLine()
            let count = (endLine.LineNumber - startLine.LineNumber) + 1
            x.CommonOperations.Join start kind count 
        
