#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotUtil = 
    
    /// Get the last line in the ITextSnapshot.  Avoid pulling the entire buffer into memory
    /// slowly by using the index
    let GetLastLine (tss:ITextSnapshot) =
        let lastIndex = tss.LineCount - 1
        tss.GetLineFromLineNumber(lastIndex)   
        
    /// Get the end point of the snapshot
    let GetEndPoint (tss:ITextSnapshot) =
        let line = GetLastLine tss
        line.End
        
    /// Get the start point of the snapshot
    let GetStartPoint (tss:ITextSnapshot) =
        let first = tss.GetLineFromLineNumber(0)
        first.Start

    /// Get the line number back if it's valid and if not the last line in the snapshot
    let GetValidLineNumberOrLast (tss:ITextSnapshot) lineNumber = 
        if lineNumber >= tss.LineCount then tss.LineCount-1 else lineNumber

    /// Get a valid line for the specified number if it's valid and the last line if it's
    /// not
    let GetValidLineOrLast (tss:ITextSnapshot) lineNumber =
        let lineNumber = GetValidLineNumberOrLast tss lineNumber
        tss.GetLineFromLineNumber(lineNumber)


/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotLineUtil =

    let Holder = 1

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotPointUtil =
   
    /// Get the ITextSnapshotLine containing the specified SnapshotPoint
    let GetContainingLine (point:SnapshotPoint) = point.GetContainingLine()

    /// Get the ITextSnapshot containing the SnapshotPoin
    let GetSnapshot (point:SnapshotPoint) = point.Snapshot

    /// Get the line range passed in.  If the count of lines exceeds the amount of lines remaining
    /// in the buffer, the span will be truncated to the final line
    let GetLineSpan point =
        let line = GetContainingLine point
        line.Extent

    /// Functions exactly line GetLineRangeSpan except it will include the final line up until
    /// the end of the line break
    let GetLineSpanIncludingLineBreak point =
        let line = GetContainingLine point
        line.ExtentIncludingLineBreak

    // Get the span of the character which is pointed to by the point.  Normally this is a 
    // trivial operation.  The only difficulty if the Point exists on an empty line.  In that
    // case it is the extent of the line
    let GetCharacterSpan point = 
        let line = GetContainingLine point
        let endSpan = new SnapshotSpan(line.End, line.EndIncludingLineBreak)
        match endSpan.Contains(point) with
            | true -> endSpan
            | false -> new SnapshotSpan(point,1)

    /// Get the next point in the buffer without wrap.  Will throw if you run off the end of 
    /// the ITextSnapshot
    let GetNextPoint point = 
        let tss = GetSnapshot point
        let line = GetContainingLine point
        if point.Position >= line.End.Position then
            let num = line.LineNumber+1
            if num = tss.LineCount then point
            else tss.GetLineFromLineNumber(num).Start
        else
            point.Add(1)    

    /// Get the next point in the buffer with wrap
    let GetNextPointWithWrap point = 
        let tss = GetSnapshot point
        let line = GetContainingLine point
        if point.Position >= line.End.Position then
            let num = line.LineNumber+1
            if num = tss.LineCount then SnapshotUtil.GetStartPoint tss
            else tss.GetLineFromLineNumber(num).Start
        else
            point.Add(1)                    

    /// Get the previous point in the buffer with wrap
    let GetPreviousPointWithWrap point = 
        let tss = GetSnapshot point
        let line = GetContainingLine point
        if point.Position = line.Start.Position then
            if line.LineNumber = 0 then SnapshotUtil.GetEndPoint tss
            else tss.GetLineFromLineNumber(line.LineNumber-1).End
        else
            point.Subtract(1)

    /// Get the line range passed in.  If the count of lines exceeds the amount of lines remaining
    /// in the buffer, the span will be truncated to the final line
    let GetLineRangeSpan start count = 
        let startLine = GetContainingLine start
        let tss = startLine.Snapshot
        let last = SnapshotUtil.GetValidLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.End)

    /// Functions exactly line GetLineRangeSpan except it will include the final line up until
    /// the end of the line break
    let GetLineRangeSpanIncludingLineBreak (start:SnapshotPoint) count =
        let tss = start.Snapshot
        let startLine = start.GetContainingLine()
        let last = SnapshotUtil.GetValidLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.EndIncludingLineBreak)
