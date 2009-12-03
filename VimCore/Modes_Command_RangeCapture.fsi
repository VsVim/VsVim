#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text

type internal RangeResult =
    | Range of SnapshotSpan * KeyInput
    | NeedMore of (KeyInput -> RangeResult)
    | Empty of SnapshotPoint * KeyInput

module internal RangeCapture =
    val Capture : SnapshotPoint -> MarkMap -> KeyInput -> RangeResult
