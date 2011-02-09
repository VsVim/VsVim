#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

/// Represents the different type of motions that are available in a Vim editor
type MotionKind =
    | Exclusive
    | Inclusive

/// Data about a complete motion operation. 
type MotionResult = {

    /// Span of the motion.
    Span : SnapshotSpan

    /// Was the motion forwards towards the end of the buffer
    IsForward : bool 

    /// Was this a w or W motion
    IsAnyWordMotion : bool

    /// Type of motion
    MotionKind : MotionKind

    /// OperationKind for the motion
    OperationKind : OperationKind 

    /// In addition to recording the Span certain line wise operations like j and k also
    /// record data about the desired column within the span.  This value may or may not
    /// be a valid point within the line
    Column : int option
} with

    /// The Span as an EditSpan value
    member x.EditSpan = EditSpan.Single x.Span

    /// The Span as a SnapshotLineRange value 
    member x.LineRange = SnapshotLineRangeUtil.CreateForSpan x.Span

    /// The OperationSpan as an EditSpan value
    member x.OperationEditSpan = EditSpan.Single x.OperationSpan

    /// The SnapshotLineRange for the OperationSpan 
    member x.OperationLineRange = SnapshotLineRangeUtil.CreateForSpan x.OperationSpan
    
    /// Span is the true result of the motion.  However some commands only process a
    /// subset of the data.  This exception is covered in the help page :help exclusive.
    ///
    /// Note: The documentation on that page is incorrect.  It mentions that the exception
    /// only exists if the motion ends in column one.  In implementation though it 
    /// exists if the motion ends on the first non-blank
    member x.OperationSpan = 
        if x.MotionKind = MotionKind.Inclusive then x.Span
        else 
            // Intentionally look at .End here because we need to find the line 
            // on which this span will end.  Need to see across line breaks
            let startLine = SnapshotSpanUtil.GetStartLine x.Span
            let endLine = x.Span.End.GetContainingLine()
            if endLine.LineNumber > startLine.LineNumber then
                let point = TssUtil.FindFirstNonWhiteSpaceCharacter endLine
                if point = x.Span.End then
                    let lineAbove = endLine.Snapshot.GetLineFromLineNumber(endLine.LineNumber-1)
                    SnapshotSpan(x.Span.Start, lineAbove.End)
                else x.Span
            else x.Span

    static member CreateEmptyFromPoint point motionKind operationKind = 
        let span = SnapshotSpanUtil.CreateWithLength point 0  
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = motionKind 
            OperationKind = operationKind 
            Column = None}

