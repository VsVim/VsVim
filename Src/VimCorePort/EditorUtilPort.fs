#light

namespace Vim
open Vim.ToDelete
open System
open System.Diagnostics;
open System.Linq;
open Microsoft.VisualStudio.Text

[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type LineRange
    (
        _startLine : int,
        _count : int
    ) = 

    member x.StartLineNumber = _startLine

    member x.LastLineNumber = _startLine + (_count - 1)

    member x.Count = _count

    member x.LineNumbers = Enumerable.Range(_startLine, _count)

    member x.ContainsLineNumber lineNumber = lineNumber >= _startLine && lineNumber <= x.LastLineNumber

    member x.Contains (lineRange : LineRange) = 
        x.StartLineNumber <= lineRange.StartLineNumber &&
        x.LastLineNumber >= lineRange.LastLineNumber

    member x.Intersects (lineRange : LineRange) = 
        x.ContainsLineNumber(lineRange.StartLineNumber) ||
        x.ContainsLineNumber(lineRange.LastLineNumber) ||
        x.LastLineNumber + 1 = lineRange.StartLineNumber ||
        x.StartLineNumber = lineRange.LastLineNumber + 1;

    override x.ToString() = sprintf "[%d - %d]" x.StartLineNumber x.LastLineNumber

    static member op_Equality(this : LineRange, other) = this = other;
    static member op_Inequality(this : LineRange, other) = this <> other;

    static member CreateFromBounds (startLineNumber : int) (lastLineNumber : int) = 
        if (lastLineNumber < startLineNumber) then
            raise (new ArgumentOutOfRangeException("lastLineNumber", "Must be greater than startLineNmuber"))

        let count = (lastLineNumber - startLineNumber) + 1;
        new LineRange(startLineNumber, count)

    static member CreateOverarching (left : LineRange) (right : LineRange) =
        let startLineNumber =  min left.StartLineNumber right.StartLineNumber
        let lastLineNumber = max left.LastLineNumber right.LastLineNumber
        LineRange.CreateFromBounds startLineNumber lastLineNumber

/// Represents a range of lines in an ITextSnapshot.  Different from a SnapshotSpan
/// because it declaratively supports lines instead of a position range
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type SnapshotLineRange  =

    val private _snapshot : ITextSnapshot
    val private _startLine : int
    val private _count : int

    member x.Snapshot = x._snapshot

    member x.StartLineNumber = x._startLine;

    member x.StartLine = x._snapshot.GetLineFromLineNumber(x.StartLineNumber)

    member x.Start = x.StartLine.Start

    member x.Count = x._count

    member x.LastLineNumber = x._startLine + (x._count - 1)

    member x.LastLine = x._snapshot.GetLineFromLineNumber(x.LastLineNumber)

    member x.LineRange = new LineRange(x._startLine, x._count)

    member x.End = x.LastLine.End

    member x.EndIncludingLineBreak = x.LastLine.EndIncludingLineBreak

    member x.Extent = new SnapshotSpan(x.Start, x.End)

    member x.ExtentIncludingLineBreak = new SnapshotSpan(x.Start, x.EndIncludingLineBreak)

    member x.Lines = 
        let snapshot = x._snapshot
        let start = x.StartLineNumber
        let last = x.LastLineNumber
        seq { for i = start to last do yield snapshot.GetLineFromLineNumber(i) }

    new (snapshot : ITextSnapshot, startLine : int, count : int) =
        if startLine >= snapshot.LineCount then
            raise (new ArgumentException("startLine", "Invalid Line Number"))

        if (startLine + (count - 1) >= snapshot.LineCount || count < 1) then
            raise (new ArgumentException("count", "Invalid Line Number"))

        { _snapshot = snapshot; _startLine = startLine; _count = count; }

    member x.GetText() = x.Extent.GetText()

    member x.GetTextIncludingLineBreak() = x.ExtentIncludingLineBreak.GetText()

    static member op_Equality(left : SnapshotLineRange, right) = left = right

    static member op_Inequality(left : SnapshotLineRange, right) = left <> right

    override x.ToString() = sprintf "[%d - %d] %O" x.StartLineNumber x.LastLineNumber x.Snapshot

    /// Create for the entire ITextSnapshot
    static member CreateForExtent (snapshot : ITextSnapshot) = new SnapshotLineRange(snapshot, 0, snapshot.LineCount)

    /// Create for a single ITextSnapshotLine
    static member CreateForLine (snapshotLine : ITextSnapshotLine) = new SnapshotLineRange(snapshotLine.Snapshot, snapshotLine.LineNumber, 1)

    static member CreateForSpan (span : SnapshotSpan) =
        let startLine = span.Start.GetContainingLine()
        // TODO use GetLastLine
        let lastLine = 
            if span.Length > 0 then span.End.Subtract(1).GetContainingLine()
            else span.Start.GetContainingLine()
        SnapshotLineRange.CreateForLineRange startLine lastLine

    /// Create a range for the provided ITextSnapshotLine and with at most count 
    /// length.  If count pushes the range past the end of the buffer then the 
    /// span will go to the end of the buffer
    static member CreateForLineAndMaxCount (snapshotLine : ITextSnapshotLine) (count : int) = 
        let maxCount = (snapshotLine.Snapshot.LineCount - snapshotLine.LineNumber)
        let count = Math.Min(count, maxCount)
        new SnapshotLineRange(snapshotLine.Snapshot, snapshotLine.LineNumber, count)

    /// Create a SnapshotLineRange which includes the 2 lines
    static member CreateForLineRange (startLine : ITextSnapshotLine) (lastLine : ITextSnapshotLine) =
        Contract.Requires(startLine.Snapshot = lastLine.Snapshot)
        let count = (lastLine.LineNumber - startLine.LineNumber) + 1
        new SnapshotLineRange(startLine.Snapshot, startLine.LineNumber, count)

    /// <summary>
    /// Create a SnapshotLineRange which includes the 2 lines
    /// </summary>
    static member CreateForLineNumberRange (snapshot : ITextSnapshot) (startLine : int) (lastLine : int) : Nullable<SnapshotLineRange> =
        Contract.Requires(startLine <= lastLine)
        if (startLine >= snapshot.LineCount || lastLine >= snapshot.LineCount) then
            Nullable<SnapshotLineRange>()
        else
            let range = SnapshotLineRange(snapshot, startLine, (lastLine - startLine) + 1)
            Nullable<SnapshotLineRange>(range)
