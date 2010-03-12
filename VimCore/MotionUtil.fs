#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

/// This module is used to implement certain motion capture commands.  Operations
/// like calculated h with a count are implemented here 
module internal MotionUtil =

    /// Get the span by moving count characters to the left.  Do not move past the start
    /// of the current physical line
    let CharLeft (point:SnapshotPoint) count =
        let line = point.GetContainingLine()
        let start = line.Start
        if count > point.Position - start.Position then
            new SnapshotSpan(start, point)
        else 
            new SnapshotSpan(point.Subtract(count),point)

    /// Get the span by moving count characters to thi right.  Do not move past the end 
    /// of the current textual part of the physical line
    let CharRight (point:SnapshotPoint) count =
        let line = point.GetContainingLine()
        let endPoint = line.End
        if count < endPoint.Position - point.Position then
            new SnapshotSpan(point, point.Add(count))
        else
            new SnapshotSpan(point, endPoint)

    /// Get the span caused by moving the character up "count" lines.  Maintin the current
    /// offset into the line if possible
    let CharUp (point:SnapshotPoint) count =
        let tss = point.Snapshot
        let originalLine = point.GetContainingLine()
        let offset = point.Position - originalLine.Start.Position
        let newLine = 
            if originalLine.LineNumber - count >= 0 then tss.GetLineFromLineNumber(originalLine.LineNumber - count)
            else tss.GetLineFromLineNumber(0)
        let newPoint = (CharRight newLine.Start offset).End
        new SnapshotSpan(newPoint, point)

    /// Get the span caused by moving the character down "count" lines.  Maintain the current
    /// offest into the line if possible and don't go beyond the end of the buffer
    let CharDown (point:SnapshotPoint) count =
        let tss = point.Snapshot
        let originalLine = point.GetContainingLine()
        let offset = point.Position - originalLine.Start.Position
        let newLine = 
            if originalLine.LineNumber + count < tss.LineCount then tss.GetLineFromLineNumber(originalLine.LineNumber + count)
            else tss.GetLineFromLineNumber(tss.LineCount-1)
        let newPoint = (CharRight newLine.Start offset).End
        new SnapshotSpan(point, newPoint)     
        
    /// Get the span of "count" lines upward careful not to run off the begining of the
    /// buffer.  Implementation of the "k" motion
    let LineUp (point:SnapshotPoint) count =     
        let tss = point.Snapshot
        let endLine = point.GetContainingLine()
        let startLineNumber = endLine.LineNumber - count
        let startLine = 
            if startLineNumber >= 0 then tss.GetLineFromLineNumber(startLineNumber)
            else tss.GetLineFromLineNumber(0)
        new SnapshotSpan(startLine.Start, endLine.End)
                      
    /// Get the span of "count" lines downward careful not to run off the end of the
    /// buffer.  Implementation of the "j" motion
    let LineDown (point:SnapshotPoint) count =
        let tss = point.Snapshot
        let startLine = point.GetContainingLine()
        let endLineNumber = startLine.LineNumber + count
        let endLine = 
            if endLineNumber < tss.LineCount then tss.GetLineFromLineNumber(endLineNumber)
            else tss.GetLineFromLineNumber(tss.LineCount-1)
        new SnapshotSpan(startLine.Start, endLine.End)            
        