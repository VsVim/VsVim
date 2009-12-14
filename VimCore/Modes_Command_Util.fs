#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Text.RegularExpressions
open Vim.RegexUtil

module internal Util =
    
    /// Handle the :edit command
    let EditFile (host:IVimHost) fileName = host.OpenFile fileName

    /// Jump to the last line in the file
    let JumpToLastLine (d:IVimBufferData) =
        ViewUtil.MoveToLastLineStart d.TextView |> ignore

    /// Join a range of lines with a potential count
    let Join (view:ITextView) (range:SnapshotSpan option) kind (count:int option) = 
        let range = 
            match range with 
            | Some(s) -> s
            | None -> 
                let point = ViewUtil.GetCaretPoint view
                SnapshotSpan(point,0)

        match count with 
        | Some(c) -> Modes.ModeUtil.Join view range.End kind c
        | None -> 
            let startLine = range.Start.GetContainingLine().LineNumber
            let endLine = range.End.GetContainingLine().LineNumber
            let count = endLine - startLine
            Modes.ModeUtil.Join view range.Start kind count


    /// Implement the :pu[t] command
    let Put (host:IVimHost) (view:ITextView) (text:string) (line:ITextSnapshotLine) isAfter =

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
        view.Caret.MoveTo(point) |> ignore

