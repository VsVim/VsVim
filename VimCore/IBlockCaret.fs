#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

/// Defines a block style caret for a given ITextView.  This allows normal mode to create 
/// a block style cursor when needed
type IBlockCaret =
    abstract TextView : ITextView
    abstract Show : unit -> unit
    abstract Hide : unit -> unit
    abstract Destroy : unit -> unit

