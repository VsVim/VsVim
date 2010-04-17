#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

module internal MotionCapture = 
    val ProcessView : ITextView -> KeyInput -> int -> MotionResult
    val ProcessInput : SnapshotPoint -> KeyInput -> int -> MotionResult

      
    
    
