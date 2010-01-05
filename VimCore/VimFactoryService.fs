#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    (
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorFormatMapService : IEditorFormatMapService,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _unused : int ) =

    let _blockCaretAdornmentLayerName = "BlockCaretAdornmentLayer"

    [<Export(typeof<AdornmentLayerDefinition>)>]
    [<Name("BlockCaretAdornmentLayer")>]
    [<Order(After = PredefinedAdornmentLayers.Selection)>]
    let mutable _blockCaretAdornmentLayerDefinition : AdornmentLayerDefinition = null

    [<ImportingConstructor>]
    new (
        editorOperationsService : IEditorOperationsFactoryService,
        editorFormatMapService : IEditorFormatMapService,
        completionBroker : ICompletionBroker,
        signatureBroker : ISignatureHelpBroker ) =
            VimFactoryService(editorOperationsService, editorFormatMapService, completionBroker, signatureBroker, 42)

    /// This method is a hack.  Unless a let binding is explicitly used the F# compiler 
    /// will remove it from the final metadata definition.  This will prevent the MEF
    /// import from ever being resolved and hence cause us to not define the adornment
    /// layer.  Hacky member method that is never called to fake assign and prevent this
    /// problem
    member private x.Hack() =
        _blockCaretAdornmentLayerDefinition = AdornmentLayerDefinition()

    member private x.CreateVimCore host =
        Vim(
            host, 
            _editorOperationsFactoryService, 
            _editorFormatMapService, 
            _completionBroker, 
            _signatureBroker,
            _blockCaretAdornmentLayerName)

    interface IVimFactoryService with
        member x.CreateVim host = (x.CreateVimCore host) :> IVim
        member x.CreateVimBuffer host view name = 
            let vim = (x.CreateVimCore host) :> IVim
            vim.CreateBuffer view name 
        member x.CreateKeyProcessor buffer = Vim.KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
        member x.CreateMouseProcessor buffer = Vim.MouseProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.IMouseProcessor
      