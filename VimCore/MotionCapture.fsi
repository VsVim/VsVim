#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;
open System.Windows.Input

type MotionResult = 
    | Complete of (SnapshotSpan * MotionKind * OperationKind )
    | NeedMoreInput of (KeyInput -> MotionResult)
    | InvalidMotion of string * (KeyInput -> MotionResult) 
    | Error of string
    | Cancel
    
module MotionCapture = 
    val ProcessView : ITextView -> KeyInput -> int -> MotionResult
    val ProcessInput : SnapshotPoint -> KeyInput -> int -> MotionResult
      
    
    
