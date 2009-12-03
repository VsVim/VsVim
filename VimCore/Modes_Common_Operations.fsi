#light

namespace VimCore.Modes.Common
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

module internal Operations =
    type Result = 
        | Succeeded
        | Failed of string

    val Join : ITextView -> SnapshotPoint -> int -> bool
    val GoToDefinition : ITextView -> VimCore.IVimHost -> Result
    val SetMark : VimCore.MarkMap -> SnapshotPoint -> char -> Result
    val JumpToMark : VimCore.MarkMap -> ITextView -> char -> Result
