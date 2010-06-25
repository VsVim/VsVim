#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open Vim.Modes
open Vim

type internal DefaultOperations ( _data:OperationsData, _mode : ModeKind ) = 
    inherit CommonOperations(_data)
    let _textView = _data.TextView
    let _operations = _data.EditorOperations
    let _outlining = _data.OutliningManager
    let _host = _data.VimHost
    let _jumpList = _data.JumpList
    let _settings = _data.LocalSettings
    let _undoRedoOperations = _data.UndoRedoOperations
    let _statusUtil = _data.StatusUtil

    member private x.CommonOperations = x :> ICommonOperations

    member private x.OperationKind = 
        match _mode with
        | ModeKind.VisualBlock -> OperationKind.CharacterWise
        | ModeKind.VisualCharacter -> OperationKind.CharacterWise
        | ModeKind.VisualLine -> OperationKind.LineWise
        | _ -> failwith "Invalid"

    member private x.SelectedText = 
        _textView.Selection.SelectedSpans
        |> Seq.map (fun x -> x.GetText())
        |> String.concat System.Environment.NewLine

    member private x.SelectedLinesSpan = 
        let col = _textView.Selection.SelectedSpans
        let startPoint = col.Item(0).Start
        let endPoint = col.Item(col.Count-1).End
        let span = SnapshotSpan(startPoint, endPoint)
        let startLine, endLine = SnapshotSpanUtil.GetStartAndEndLine span
        SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)

    member private x.DoWithOnlyOneSpan func = 
            if _textView.Selection.SelectedSpans.Count > 1 then _statusUtil.OnError Resources.VisualMode_MultiSelectNotSupported
            elif _textView.Selection.Mode = TextSelectionMode.Box then _statusUtil.OnError Resources.VisualMode_BoxSelectionNotSupported
            else 
                let single = _textView.Selection.SelectedSpans.Item(0)
                func single

    interface IOperations with
        member x.SelectedSpan = 
            let col = _textView.Selection.SelectedSpans
            if col.Count = 0 then 
                let caretPoint = TextViewUtil.GetCaretPoint _textView
                SnapshotSpan(caretPoint,0)
            else col.Item(0)
        member x.DeleteSelection (reg:Register) = 
            let value = { Value=x.SelectedText; MotionKind=MotionKind.Inclusive; OperationKind=x.OperationKind }
            reg.UpdateValue(value)
            use edit = _textView.TextBuffer.CreateEdit()
            _textView.Selection.SelectedSpans |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
            edit.Apply() |> ignore
        member x.DeleteSelectedLines (reg:Register) = 
            let span = x.SelectedLinesSpan
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
        member x.PasteOverSelection text (reg:Register) =
            x.DoWithOnlyOneSpan (fun span ->
                let value = { Value=x.SelectedText; MotionKind=MotionKind.Inclusive; OperationKind=x.OperationKind }
                reg.UpdateValue(value)

                // Paste over selection should not delete the last new line.  This is not specifically 
                // documented but is evident in the implementation
                let span = 
                    let lastLine = SnapshotSpanUtil.GetEndLine span
                    if span.End.Position > lastLine.End.Position then SnapshotSpan(span.Start, lastLine.End)
                    else span

                _textView.TextBuffer.Replace(span.Span, text) |> ignore )
        
