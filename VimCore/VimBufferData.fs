#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input

/// Default implementation of the IVimBufferData interface
type internal VimBufferData 
    ( 
        _name : string,
        _view :IWpfTextView, 
        _host : IVimHost, 
        _vimData : IVimData,
        _blockCaret : IBlockCaret,
        _editorOperations : IEditorOperations ) =

    interface IVimBufferData with 
        member x.Name = _name
        member x.TextView = _view
        member x.TextBuffer = _view.TextBuffer
        member x.TextSnapshot = _view.TextSnapshot
        member x.EditorOperations = _editorOperations
        member x.VimHost = _host
        member x.VimData = _vimData
        member x.RegisterMap = _vimData.RegisterMap
        member x.Settings = _vimData.Settings
        member x.MarkMap = _vimData.MarkMap
        member x.BlockCaret = _blockCaret
    
    
    
