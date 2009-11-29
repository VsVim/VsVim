#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;
open System.Windows.Input

type internal MotionResult = 
    | Complete of (SnapshotSpan * MotionKind * OperationKind )
    | NeedMoreInput of (KeyInput -> MotionResult)
    | InvalidMotion of string * (KeyInput -> MotionResult) 
    | Error of string
    | Cancel
    
module internal MotionCapture = 
    val ProcessView : ITextView -> KeyInput -> int -> MotionResult
    val ProcessInput : SnapshotPoint -> KeyInput -> int -> MotionResult
      
    
    
