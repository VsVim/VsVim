#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text
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

