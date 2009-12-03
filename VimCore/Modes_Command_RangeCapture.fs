#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text

type internal RangeResult =
    /// Represents a completed range and the KeyInput which ended the 
    /// range sequence
    | Range of SnapshotSpan * KeyInput

    /// An incomplete range is detected and more KeyInput is needed to 
    /// complete the range
    | NeedMore of (KeyInput -> RangeResult)

    /// No range was input.  Original SnapshotPoint passed into the Capture command is
    /// returned in this case
    | Empty of SnapshotPoint * KeyInput

module internal RangeCapture =
    let Capture (point:SnapshotPoint) (map:MarkMap) (ki:KeyInput) = Empty(point, ki)
    