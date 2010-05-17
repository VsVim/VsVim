#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal DefaultOperations
    (
        _textView : ITextView, 
        _editorOpts : IEditorOperations, 
        _outlining : IOutliningManager,
        _host : IVimHost, _jumpList : IJumpList, 
        _settings : IVimLocalSettings,
        _undoRedoOperations : IUndoRedoOperations) =
    inherit Modes.CommonOperations(_textView, _editorOpts, _outlining, _host, _jumpList, _settings, _undoRedoOperations)