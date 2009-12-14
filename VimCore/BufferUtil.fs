#light
namespace Vim
open Microsoft.VisualStudio.Text

module internal BufferUtil =

    /// Adds an empty line to the buffer below the specified line and returns
    /// the new ITextSnapshotLine instance representing the buffer
    let AddLineBelow (line:ITextSnapshotLine) =
        let buffer = line.Snapshot.TextBuffer
        let tss = buffer.Replace(new Span(line.End.Position,0), System.Environment.NewLine)
        tss.GetLineFromLineNumber(line.LineNumber+1)

    /// Adds an empty line to the buffer above the specified line and returns
    /// the new ITextSnapshotLine inserted
    let AddLineAbove (line:ITextSnapshotLine) =
        let buffer = line.Snapshot.TextBuffer
        let tss = buffer.Replace(new Span(line.Start.Position,0), System.Environment.NewLine)
        tss.GetLineFromLineNumber(line.LineNumber)

    /// Shift the lines enumerated by the specified span "count" characters to the right
    let ShiftRight (span:SnapshotSpan) count = 
        let text = new System.String(' ', count)
        let buf = span.Snapshot.TextBuffer
        let startLineNumber = span.Start.GetContainingLine().LineNumber
        let endLineNumber = span.End.GetContainingLine().LineNumber
        use edit = buf.CreateEdit()
        for i = startLineNumber to endLineNumber do
            let line = span.Snapshot.GetLineFromLineNumber(i)
            edit.Replace(line.Start.Position,0,text) |> ignore
        
        edit.Apply()
        
    /// Shift the lines unemerated by the specified span "count" characters left.  Essentially,
    /// eat the first count blank spaces on the line       
    let ShiftLeft (span:SnapshotSpan) count =
        let fixText (text:string) = 
            let count = min count (text.Length) // Deal with count being greater than line length
            let count = 
                match text |> Seq.tryFindIndex (fun x -> x <> ' ') with
                    | Some(i) ->
                        if i < count then i
                        else count
                    | None -> count
            text.Substring(count)                 
        let buf = span.Snapshot.TextBuffer
        let startLineNumber = span.Start.GetContainingLine().LineNumber
        let endLineNumber = span.End.GetContainingLine().LineNumber
        use edit = buf.CreateEdit()
        for i = startLineNumber to endLineNumber do
            let line = span.Snapshot.GetLineFromLineNumber(i)
            let text = fixText (line.GetText())
            edit.Replace(line.Extent.Span, text) |> ignore
        edit.Apply()
