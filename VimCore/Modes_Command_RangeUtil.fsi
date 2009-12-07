#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text

type internal Range =
    | ValidRange of SnapshotSpan * KeyInput list
    | NoRange of KeyInput list
    | Invalid of string * KeyInput list

module internal RangeUtil =
    val ParseNumber : KeyInput list -> (int option * KeyInput list)
    val ParseRange : SnapshotPoint -> MarkMap -> KeyInput list -> Range
