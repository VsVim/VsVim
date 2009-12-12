#light

namespace VimCore
open Microsoft.VisualStudio.Text

module internal BufferUtil =
    val AddLineBelow : ITextSnapshotLine -> ITextSnapshotLine
    val AddLineAbove : ITextSnapshotLine -> ITextSnapshotLine
    val DeleteSpan : (SnapshotSpan * MotionKind * OperationKind) -> Register -> ITextSnapshot
    val ShiftRight : SnapshotSpan -> int -> ITextSnapshot
    val ShiftLeft : SnapshotSpan -> int -> ITextSnapshot
    
