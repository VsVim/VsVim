#light

namespace Vim
open Microsoft.VisualStudio.Text

module internal BufferUtil =
    val AddLineBelow : ITextSnapshotLine -> ITextSnapshotLine
    val AddLineAbove : ITextSnapshotLine -> ITextSnapshotLine
    
