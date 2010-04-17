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

type internal MotionResult = 
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
