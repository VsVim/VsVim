#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

[<Export(typeof<IBlockCaretFactoryService>)>]
type internal BlockCaretFactoryService [<ImportingConstructor>] ( _formatMapService : IEditorFormatMapService ) =
    
    let _blockCaretAdornmentLayerName = "BlockCaretAdornmentLayer"

    [<Export(typeof<AdornmentLayerDefinition>)>]
    [<Name("BlockCaretAdornmentLayer")>]
    [<Order(After = PredefinedAdornmentLayers.Selection)>]
    let mutable _blockCaretAdornmentLayerDefinition : AdornmentLayerDefinition = null

    /// This method is a hack.  Unless a let binding is explicitly used the F# compiler 
    /// will remove it from the final metadata definition.  This will prevent the MEF
    /// import from ever being resolved and hence cause us to not define the adornment
    /// layer.  Hacky member method that is never called to fake assign and prevent this
    /// problem
    member private x.Hack() =
        _blockCaretAdornmentLayerDefinition = AdornmentLayerDefinition()

    interface IBlockCaretFactoryService with
        member x.CreateBlockCaret textView = 
            let formatMap = _formatMapService.GetEditorFormatMap(textView)
            BlockCaret(textView, _blockCaretAdornmentLayerName, formatMap) :> IBlockCaret

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    [<ImportingConstructor>]
    ( _vim : IVim ) =

    interface IVimFactoryService with
        member x.Vim = _vim
        member x.CreateKeyProcessor buffer = Vim.KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
        member x.CreateMouseProcessor buffer = Vim.MouseProcessor(buffer, MouseDeviceImpl() :> IMouseDevice) :> Microsoft.VisualStudio.Text.Editor.IMouseProcessor

type internal CompletionWindowBroker 
    ( 
        _textView : ITextView,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker ) = 
    interface ICompletionWindowBroker with
        member x.TextView = _textView
        member x.IsCompletionWindowActive = 
            _completionBroker.IsCompletionActive(_textView) || _signatureBroker.IsSignatureHelpActive(_textView)
        member x.DismissCompletionWindow() = 
            if _completionBroker.IsCompletionActive(_textView) then
                _completionBroker.DismissAllSessions(_textView)
            if _signatureBroker.IsSignatureHelpActive(_textView) then
                _signatureBroker.DismissAllSessions(_textView)

[<Export(typeof<ICompletionWindowBrokerFactoryService>)>]
type internal CompletionWindowBrokerFactoryService
    [<ImportingConstructor>]
    (
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker ) = 

    interface ICompletionWindowBrokerFactoryService with
        member x.CreateCompletionWindowBroker textView = 
            let broker = CompletionWindowBroker(textView, _completionBroker, _signatureBroker)
            broker :> ICompletionWindowBroker
