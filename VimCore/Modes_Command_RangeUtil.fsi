#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal Range = 
    | RawSpan of SnapshotSpan
    | Lines of ITextSnapshot * int * int

type internal ParseRangeResult =
    | Succeeded of Range * KeyInput list
    | NoRange 
    | Failed of string 

module internal RangeUtil =
    val GetSnapshotSpan : Range -> SnapshotSpan
    val RangeForCurrentLine : ITextView -> Range
    val ApplyCount : Range -> int -> Range
    val ParseNumber : KeyInput list -> (int option * KeyInput list)
    val ParseRange : SnapshotPoint -> MarkMap -> KeyInput list -> ParseRangeResult
