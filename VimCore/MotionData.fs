#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

/// Represents the different type of motions that are available in a Vim editor
type MotionKind =
    | Exclusive
    | Inclusive

/// Data about a complete motion operation. 
type MotionData = {
    /// Span of the motion.  
    Span : SnapshotSpan

    /// Was the motion forwards towards the end of the buffer
    IsForward : bool 

    /// Type of motion
    MotionKind : MotionKind

    /// OperationKind for the motion
    OperationKind : OperationKind 

    /// In addition to recording the Span certain line wise operations like j and k also
    /// record data about the desired column within the span.  This value may or may not
    /// be a valid point within the line
    Column : int option
} with
    
    /// Span is the true result of the motion.  However some commands only process a
    /// subset of the data.  This exception is covered in the help page :help exclusive.
    ///
    /// Note: The documentation on that page is incorrect.  It mentions that the exception
    /// only exists if the motion ends in column one.  In implementation though it 
    /// exists if the motion ends on the first non-blank
    member x.OperationSpan = 
        if x.MotionKind = MotionKind.Inclusive then x.Span
        else 
            let line = SnapshotPointUtil.GetContainingLine x.Span.End
            let point = TssUtil.FindFirstNonWhitespaceCharacter line
            if point = x.Span.End && line.LineNumber <> 0 then 
                let lineAbove = line.Snapshot.GetLineFromLineNumber(line.LineNumber-1)
                SnapshotSpan(x.Span.Start, lineAbove.End)
            else x.Span

        
