#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input
open System.Text.RegularExpressions
open Vim.RegexUtil

type internal DefaultOperations
    (
        _textView : ITextView,
        _operations : IEditorOperations, 
        _host : IVimHost ) =

    interface IOperations with
        // From ICommonOperations
        member x.TextView = _textView 
        member x.MoveCaretLeft count = ModeUtil.MoveCaretLeft _textView _operations count
        member x.MoveCaretRight count = ModeUtil.MoveCaretRight _textView _operations count
        member x.MoveCaretUp count = ModeUtil.MoveCaretUp _textView _operations count
        member x.MoveCaretDown count = ModeUtil.MoveCaretDown _textView _operations count
        member x.JumpToMark ident map = ModeUtil.JumpToMark map _textView ident
        member x.SetMark ident map = 
            let point = ViewUtil.GetCaretPoint _textView
            ModeUtil.SetMark map point ident 

        member x.EditFile fileName = _host.OpenFile fileName

        /// Implement the :pu[t] command
        member x.Put (text:string) (line:ITextSnapshotLine) isAfter =

            // Force a newline at the end to be consistent with the implemented 
            // behavior in various VIM implementations.  This isn't called out in 
            // the documentation though
            let text = 
                if text.EndsWith(System.Environment.NewLine) then text
                else text + System.Environment.NewLine

            let point = line.Start
            let span =
                if isAfter then Modes.ModeUtil.PasteAfter point text OperationKind.LineWise
                else Modes.ModeUtil.PasteBefore point text

            // Move the cursor to the first non-blank character on the last line
            let line = span.End.Subtract(1).GetContainingLine()
            let rec inner (cur:SnapshotPoint) = 
                if cur = line.End || not (System.Char.IsWhiteSpace(cur.GetChar())) then
                    cur
                else
                    inner (cur.Add(1))
            let point = inner line.Start
            _textView.Caret.MoveTo(point) |> ignore
