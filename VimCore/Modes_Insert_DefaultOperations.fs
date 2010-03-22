#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal DefaultOperations
    (
        _textView : ITextView, 
        _editorOpts : IEditorOperations, 
        _host : IVimHost, _jumpList : IJumpList, 
        _settings : IVimLocalSettings) =
    inherit Modes.CommonOperations(_textView, _editorOpts, _host, _jumpList, _settings)