#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

/// Default implementation of the IVimBufferData interface
type internal VimBufferData 
    ( 
        _name : string,
        _view :IWpfTextView, 
        _host : IVimHost, 
        _vimData : IVimData ) =

    interface IVimBufferData with 
        member x.Name = _name
        member x.TextView = _view
        member x.TextBuffer = _view.TextBuffer
        member x.TextSnapshot = _view.TextSnapshot
        member x.VimHost = _host
        member x.VimData = _vimData
        member x.RegisterMap = _vimData.RegisterMap
        member x.Settings = _vimData.Settings
        member x.MarkMap = _vimData.MarkMap
    
    
    
