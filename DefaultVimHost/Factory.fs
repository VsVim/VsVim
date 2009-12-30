#light

namespace DefaultVimHost
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.ComponentModel.Composition
open Vim

[<Export(typeof<IWpfTextViewCreationListener>)>]
[<Export(typeof<IKeyProcessorProvider>)>]
type Factory
    (
        _vimFactoryService :IVimFactoryService,
        _host : IVimHost,
        _vim : IVim ) =
    let _bufferKey = System.Guid.NewGuid()

    [<ImportingConstructor>]
    new ( vimFactoryService : IVimFactoryService) = 
        let host = VimHost() :> IVimHost
        let vim = vimFactoryService.CreateVim(host)
        Factory(vimFactoryService, host, vim)

    interface IWpfTextViewCreationListener with 
        member x.TextViewCreated(wpfTextView:IWpfTextView) =
            let textView = wpfTextView :> ITextView
            let buffer = _vim.CreateBuffer wpfTextView "Unknown" 
            wpfTextView.Properties.AddProperty(_bufferKey, buffer)
    
    interface IKeyProcessorProvider with
        member x.GetAssociatedProcessor(wpfTextView:IWpfTextView) =
            let buffer = wpfTextView.Properties.GetProperty<IVimBuffer>(_bufferKey)
            _vimFactoryService.CreateKeyProcessor(buffer)
