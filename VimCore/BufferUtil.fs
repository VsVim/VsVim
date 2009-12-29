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

