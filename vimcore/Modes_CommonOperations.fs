#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

[<AbstractClass>]
type CommonOperations 
    (
        _textView : ITextView,
        _operations : IEditorOperations ) =

    interface ICommonOperations with
        member x.TextView = _textView 
        member x.MoveCaretLeft count = ModeUtil.MoveCaretLeft _textView _operations count
        member x.MoveCaretRight count = ModeUtil.MoveCaretRight _textView _operations count
        member x.MoveCaretUp count = ModeUtil.MoveCaretUp _textView _operations count
        member x.MoveCaretDown count = ModeUtil.MoveCaretDown _textView _operations count
        member x.JumpToMark ident map = ModeUtil.JumpToMark map _textView ident
        member x.SetMark ident map = 
            let point = ViewUtil.GetCaretPoint _textView
            ModeUtil.SetMark map point ident 
    

