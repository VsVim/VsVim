#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text

type RangeResult =
    | Range of SnapshotSpan * KeyInput
    | NeedMore of (KeyInput -> RangeResult)
    | Empty of SnapshotPoint * KeyInput
    | Cancelled

module internal RangeCapture =
    val Capture : SnapshotPoint -> MarkMap -> KeyInput -> RangeResult
