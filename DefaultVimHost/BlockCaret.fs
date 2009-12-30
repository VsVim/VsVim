#light

namespace DefaultVimHost
open Vim
open Microsoft.VisualStudio.Text.Editor

type BlockCaret( _textView : ITextView ) =
    interface IBlockCaret with 
        member x.TextView = _textView
        member x.Show() = () 
        member x.Hide() = ()
        member x.Destroy() = ()
