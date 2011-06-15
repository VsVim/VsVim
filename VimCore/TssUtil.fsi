#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

module TssUtil =

    /// Vim is fairly odd in that it considers the top line of the file to be both line numbers
    /// 1 and 0.  The next line is 2.  The editor is a zero based index though so we need
    /// to take that into account
    val VimLineToTssLine : int -> int

    /// Find the full span of the word at the given point
    val FindCurrentFullWordSpan : SnapshotPoint -> WordKind -> option<SnapshotSpan>

    /// Create an ITextStructureNavigator instance for the given WordKind with the provided 
    /// base implementation to fall back on
    val CreateTextStructureNavigator : WordKind -> ITextStructureNavigator -> ITextStructureNavigator

