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
    Span : SnapshotSpan
    IsForward : bool 
    MotionKind : MotionKind
    OperationKind : OperationKind 
}

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

/// Represents the types of MotionCommands which exist
type MotionCommand = 

    /// Simple motion which comprises of a char and a function which given a start point
    /// and count will produce the motion.  None is returned in the case the motion 
    /// is not valid
    | SimpleMotionCommand of char * (SnapshotPoint -> int -> MotionData option)

    /// Complex motion commands take more than one keystroke to complete.  For example 
    /// the f,t,F and T commands all require at least one additional input.  The bool
    /// in the middle of the tuple indicates whether or not the motion can be 
    /// used as a cursor movement operation  
    | ComplexMotionCommand of char * bool * (SnapshotPoint -> int -> MotionResult)
