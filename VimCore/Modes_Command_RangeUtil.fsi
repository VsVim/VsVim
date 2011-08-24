#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal ParseRangeResult =
    | Succeeded of SnapshotLineRange * char list
    | NoRange 
    | Failed of string 

type internal RangeUtil = 
    new : VimBufferData -> RangeUtil

    /// Get the range for the currently selected line
    member RangeForCurrentLine : SnapshotLineRange

    /// Retrieve the passed in range if valid or the range for the current line
    /// if the Range Option is empty
    member RangeOrCurrentLine : SnapshotLineRange option -> SnapshotLineRange

    /// Retrieve the passed in range if valid or the range for the current line
    /// if the Range Option is empty.  It will then try to apply the specified count
    member RangeOrCurrentLineWithCount : SnapshotLineRange option -> int option -> SnapshotLineRange

    /// Apply the count to the given range
    member ApplyCount : int -> SnapshotLineRange -> SnapshotLineRange

    /// Apply the count if present to the given range
    member TryApplyCount : int option -> SnapshotLineRange -> SnapshotLineRange

    /// Parse out a number from the input string
    member ParseNumber : char list -> (int option * char list)

    /// Parse out a range from the input string in the context of the provided
    /// ITextSnapshotLine
    member ParseRange : ITextSnapshotLine -> char list -> ParseRangeResult
