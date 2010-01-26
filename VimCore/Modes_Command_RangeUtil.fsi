#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal Range = 
    | RawSpan of SnapshotSpan
    | Lines of ITextSnapshot * int * int
    | SingleLine of ITextSnapshotLine 

type internal ParseRangeResult =
    | Succeeded of Range * KeyInput list
    | NoRange 
    | Failed of string 

module internal RangeUtil =
    val GetSnapshotSpan : Range -> SnapshotSpan
    val RangeForCurrentLine : ITextView -> Range
    val RangeOrCurrentLine : ITextView -> Range option -> Range
    val ApplyCount : Range -> int -> Range
    val ParseNumber : KeyInput list -> (int option * KeyInput list)
    val ParseRange : SnapshotPoint -> IMarkMap -> KeyInput list -> ParseRangeResult
