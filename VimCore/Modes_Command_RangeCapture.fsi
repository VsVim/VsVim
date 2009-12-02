#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text

type RangeResult =
    | Range of SnapshotSpan
    | None 
    | Cancelled

module internal RangeCapture =
    val Capture : KeyInput -> RangeResult
