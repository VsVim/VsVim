#light

namespace Vim.Modes
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input
open System.Windows.Media

type internal DefaultCommonOperations
    (
    _textView : ITextView,
    _operations : IEditorOperations ) =

    interface ICommonOperations with 
        member x.TextView = _textView 
        member x.JumpToMark ident map = ModeUtil.JumpToMark map _textView ident 