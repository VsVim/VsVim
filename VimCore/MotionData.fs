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

    /// Returns the value of Column if it exists.  If not it will get the first point on
    /// the first line of a forward motion or the last point on the last line in a non
    /// forward motion
    member x.ColumnOrFirstPoint = 
        let column =
            match x.Column with
            | Some(value) -> value
            | None -> 0
        let line = 
            if x.IsForward then SnapshotPointUtil.GetContainingLine x.Span.Start
            else SnapshotPointUtil.GetContainingLine x.Span.End
        VirtualSnapshotPoint(line, column)

type MotionResult = 
    | Complete of MotionData 
    
    /// Motion needs more input to be completed
    | NeedMoreInput of (KeyInput -> MotionResult)
    
    /// Indicates the motion is currently in an invalid state and 
    /// won't ever complete.  But the utility will still provide a 
    /// function to capture input until the motion action is completed
    /// with a completing key
    | InvalidMotion of string * (KeyInput -> MotionResult) 
    | Error of string
    | Cancel
