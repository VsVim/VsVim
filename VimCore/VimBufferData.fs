#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

/// Default implementation of the IVimBufferData interface
type VimBufferData 
    ( 
        _view :IWpfTextView, 
        _host : IVimHost, 
        _map : IRegisterMap ) =
    let _settings = VimSettingsUtil.CreateDefault    

    interface IVimBufferData with 
        member x.TextView = _view
        member x.TextBuffer = _view.TextBuffer
        member x.TextSnapshot = _view.TextSnapshot
        member x.VimHost = _host
        member x.RegisterMap = _map
        member x.Settings = _settings
        
    
    
    
