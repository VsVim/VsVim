#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

type internal DefaultOperations ( _data : OperationsData) =
    inherit CommonOperations(_data)

    let _textView = _data.TextView
    let _operations = _data.EditorOperations
    let _outlining = _data.OutliningManager
    let _host = _data.VimHost
    let _jumpList = _data.JumpList
    let _settings = _data.LocalSettings
    let _undoRedoOperations = _data.UndoRedoOperations
    let _options = _data.EditorOptions
    let _normalWordNav =  _data.Navigator
    let _statusUtil = _data.StatusUtil
    let _search = _data.SearchService
    let _vimData = _data.VimData

    member private x.CommonImpl = x :> ICommonOperations

    member private x.JumpCore count moveJump =
        let rec inner count = 
            if count >= 1 && moveJump() then inner (count-1)
            elif count = 0 then true
            else false
        if not (inner count) then _host.Beep()
        else
            match _jumpList.Current with
            | None -> _host.Beep()
            | Some(point) -> 
                let ret = x.CommonImpl.NavigateToPoint (VirtualSnapshotPoint(point))
                if not ret then _host.Beep()

    member x.GoToLineCore line =
        let snapshot = _textView.TextSnapshot
        let lastLineNumber = snapshot.LineCount - 1
        let line = min line lastLineNumber
        let textLine = snapshot.GetLineFromLineNumber(line)
        if _settings.GlobalSettings.StartOfLine then 
            _textView.Caret.MoveTo( textLine.Start ) |> ignore
            _operations.MoveToStartOfLineAfterWhiteSpace(false)
        else 
            let point = TextViewUtil.GetCaretPoint _textView
            let _,column = SnapshotPointUtil.GetLineColumn point
            let column = min column textLine.Length
            let point = textLine.Start.Add(column)
            _textView.Caret.MoveTo (point) |> ignore

    interface IOperations with 

                    
        /// Implement the r command in normal mode.  
        member x.ReplaceChar (ki:KeyInput) count = 
            let point = TextViewUtil.GetCaretPoint _textView

            // Make sure the replace string is valid
            if (point.Position + count) > point.GetContainingLine().End.Position then
                false
            else
                let replaceText = 
                    if ki = KeyInputUtil.EnterKey then System.Environment.NewLine
                    else new System.String(ki.Char, count)
                let span = new Span(point.Position, count)
                let tss = _textView.TextBuffer.Replace(span, replaceText) 

                // Reset the caret to the point before the edit
                let point = new SnapshotPoint(tss,point.Position)
                _textView.Caret.MoveTo(point) |> ignore
                true
    
        /// Implement the normal mode x command
        member x.DeleteCharacterAtCursor count =
            let point = TextViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let count = min (count) (line.End.Position-point.Position)
            let span = new SnapshotSpan(point, count)
            x.CommonImpl.DeleteSpan span 

            span

        /// Implement the normal mode X command
        member x.DeleteCharacterBeforeCursor count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let span = TssUtil.GetReverseCharacterSpan point count
            x.CommonImpl.DeleteSpan span
            span

        member x.GoToDefinitionWrapper () =
            match x.CommonImpl.GoToDefinition() with
            | Vim.Modes.Succeeded -> ()
            | Vim.Modes.Failed(msg) -> _statusUtil.OnError msg


        member x.JumpNext count = x.JumpCore count (fun () -> _jumpList.MoveNext())
        member x.JumpPrevious count = x.JumpCore count (fun() -> _jumpList.MovePrevious())

        member x.GoToLineOrFirst count =
            let line =
                match count with
                | None -> 0
                | Some(c) -> c
            x.GoToLineCore line

        member x.GoToLineOrLast count =
            let snapshot = _textView.TextSnapshot
            let lastLineNumber = snapshot.LineCount - 1
            let line = 
                match count with
                | None -> lastLineNumber
                // Surprisingly 0 goes to the last line number in gVim
                | Some(c) when c = 0 -> lastLineNumber
                | Some(c) -> c
            x.GoToLineCore line

        member x.ChangeLetterCaseAtCursor count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let line = SnapshotPointUtil.GetContainingLine point
            let count = min count (line.End.Position - point.Position)
            let span = SnapshotSpan(point, count) |> EditSpan.Single
            x.CommonImpl.ChangeLetterCase span

            if line.Length > 0 then
                
                // Because we aren't changing the length of the buffer it's OK 
                // to calculate with respect to the points before the edit
                let pos = point.Position + count
                let pos = min pos (line.End.Position-1)
                TextViewUtil.MoveCaretToPosition _textView pos |> ignore
            
        member x.MoveCaretForAppend () = 
            let point = TextViewUtil.GetCaretPoint _textView
            _operations.ResetSelection()
            if SnapshotPointUtil.IsInsideLineBreak point then ()
            elif SnapshotPointUtil.IsEndPoint point then ()
            else 
                let point = point.Add(1)
                TextViewUtil.MoveCaretToPoint _textView point |> ignore




