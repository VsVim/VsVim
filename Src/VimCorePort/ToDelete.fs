#light

// Duplication of code that we cant copy whole sale to this project. Delete once port is 
// complete 
namespace Vim.ToDelete
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Projection
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System.Diagnostics
open System.Text
open Vim

module EditorUtil =

    /// Create a span which is the overarching span of the two provided SnapshotSpan values
    let CreateOverarching (leftSpan : SnapshotSpan) (rightSpan : SnapshotSpan) = 
        Contract.Requires (leftSpan.Snapshot = rightSpan.Snapshot)
        let snapshot = leftSpan.Snapshot
        let startPoint = 
            let position = min leftSpan.Start.Position rightSpan.Start.Position
            SnapshotPoint(snapshot, position)
        let endPoint = 
            let position = max leftSpan.End.Position rightSpan.End.Position
            SnapshotPoint(snapshot, position)
        SnapshotSpan(startPoint, endPoint)

module SnapshotUtil = 

    /// Get the last line in the ITextSnapshot.  Avoid pulling the entire buffer into memory
    /// slowly by using the index
    let GetLastLine (tss:ITextSnapshot) =
        let lastIndex = tss.LineCount - 1
        tss.GetLineFromLineNumber(lastIndex)   

    /// Get the line for the specified number
    let GetLine (tss:ITextSnapshot) lineNumber = tss.GetLineFromLineNumber lineNumber

    /// Get the length of the ITextSnapshot
    let GetLength (tss:ITextSnapshot) = tss.Length

    /// Get the character at the specified index
    let GetChar index (tss: ITextSnapshot) = tss.[index]

    /// Get the first line in the snapshot
    let GetFirstLine tss = GetLine tss 0

    let GetLastLineNumber (tss:ITextSnapshot) = tss.LineCount - 1 

    /// Get the end point of the snapshot
    let GetEndPoint (tss:ITextSnapshot) = SnapshotPoint(tss, tss.Length)

    /// Get the start point of the snapshot
    let GetStartPoint (tss:ITextSnapshot) = SnapshotPoint(tss, 0)

    /// Get the full span of the buffer 
    let GetExtent snapshot = 
        let startPoint = GetStartPoint snapshot
        let endPoint = GetEndPoint snapshot
        SnapshotSpan(startPoint, endPoint)

    /// Get the text of the ITextSnapshot
    let GetText (snapshot:ITextSnapshot) = snapshot.GetText()

    /// Is the Line Number valid
    let IsLineNumberValid (tss:ITextSnapshot) lineNumber = lineNumber >= 0 && lineNumber < tss.LineCount

module NormalizedSnapshotSpanCollectionUtil =

    /// Get the first item 
    let GetFirst (col:NormalizedSnapshotSpanCollection) = col.[0]

    /// Get the first item 
    let GetLast (col:NormalizedSnapshotSpanCollection) = col.[col.Count-1]

    /// Get the inclusive span 
    let GetOverarchingSpan col =
        let first = GetFirst col
        let last = GetLast col
        SnapshotSpan(first.Start,last.End) 

    /// Get the first item 
    let TryGetFirst (col : NormalizedSnapshotSpanCollection) = if col.Count = 0 then None else Some (col.[0])

    let OfSeq (s:SnapshotSpan seq) = new NormalizedSnapshotSpanCollection(s)
