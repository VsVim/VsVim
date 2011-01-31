#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal ParseRangeResult =
    | Succeeded of SnapshotLineRange * char list
    | NoRange 
    | Failed of string 

module internal RangeUtil =

    /// Get the range for the currently selected line
    val RangeForCurrentLine : ITextView -> SnapshotLineRange

    /// Retrieve the passed in range if valid or the range for the current line
    /// if the Range Option is empty
    val RangeOrCurrentLine : ITextView -> SnapshotLineRange option -> SnapshotLineRange

    /// Apply the count to the given range
    val ApplyCount : int -> SnapshotLineRange -> SnapshotLineRange

    /// Apply the count if present to the given range
    val TryApplyCount : int option -> SnapshotLineRange -> SnapshotLineRange

    /// Parse out a number from the input string
    val ParseNumber : char list -> (int option * char list)

    /// Parse out a range from the input string in the context of the provided
    /// ITextSnapshotLine
    val ParseRange : ITextSnapshotLine -> IMarkMap -> char list -> ParseRangeResult
