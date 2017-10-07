#light

namespace Vim
open System
open System.Diagnostics;
open System.Linq;

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
