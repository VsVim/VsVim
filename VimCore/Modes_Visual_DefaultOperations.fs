#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim.Modes

type internal DefaultOperations 
    ( 
        _textView : ITextView,
        _operations : IEditorOperations ) =
    inherit CommonOperations(_textView, _operations)
        
