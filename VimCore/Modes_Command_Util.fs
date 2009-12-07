#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Text.RegularExpressions
open VimCore.RegexUtil

module internal Util =
    
    /// Handle the :edit command
    let EditFile (host:IVimHost) fileName = host.OpenFile fileName

    /// Jump to a specified line number in the file
    let JumpToLineNumber (d:IVimBufferData) lineNumberStr = 
        let num = System.Int32.Parse(lineNumberStr)
        let num = TssUtil.VimLineToTssLine(num)
        let tss = d.TextSnapshot
        match num < tss.LineCount with
            | true -> 
                let line = tss.GetLineFromLineNumber(num)
                ViewUtil.MoveToLineStart d.TextView line |> ignore
            | false -> 
                d.VimHost.UpdateStatus("Invalid line number")

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
        | Some(c) -> Modes.Common.Operations.Join view range.End kind c
        | None -> 
            let startLine = range.Start.GetContainingLine().LineNumber
            let endLine = range.End.GetContainingLine().LineNumber
            let count = endLine - startLine
            Modes.Common.Operations.Join view range.Start kind count

