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

        member x.GoToDefinitionWrapper () =
            match x.CommonImpl.GoToDefinition() with
            | Vim.Modes.Succeeded -> ()
            | Vim.Modes.Failed(msg) -> _statusUtil.OnError msg

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





