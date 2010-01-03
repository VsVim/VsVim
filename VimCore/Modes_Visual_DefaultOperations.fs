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

    interface IOperations with
        member x.SelectionTracker = _tracker
        member x.DeleteSelection (reg:Register) = 
            let value = { Value=_tracker.SelectedText; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise }
            reg.UpdateValue(value)
            use edit = _textView.TextBuffer.CreateEdit()
            _textView.Selection.SelectedSpans |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
            edit.Apply() 

        
