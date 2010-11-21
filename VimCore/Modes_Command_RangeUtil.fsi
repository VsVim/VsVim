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
    | Succeeded of Range * char list
    | NoRange 
    | Failed of string 

module internal RangeUtil =

    /// Get the SnapshotSpan for the given Range
    val GetSnapshotSpan : Range -> SnapshotSpan

    /// Get the SnapshotLineSpan for the given Range
    val GetSnapshotLineRange : Range -> SnapshotLineRange

    /// Get the range for the currently selected line
    val RangeForCurrentLine : ITextView -> Range

    /// Retrieve the passed in range if valid or the range for the current line
    /// if the Range Option is empty
    val RangeOrCurrentLine : ITextView -> Range option -> Range

    /// Apply the count to the given range
    val ApplyCount : Range -> int -> Range

    /// Parse out a number from the input string
    val ParseNumber : char list -> (int option * char list)

    /// Parse out a range from the input string
    val ParseRange : SnapshotPoint -> IMarkMap -> char list -> ParseRangeResult
