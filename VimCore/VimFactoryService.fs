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
        _editorFormatMap : IEditorFormatMap,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _unused : int ) =

    let _blockCaretAdornmentLayerName = "BlockCaretAdornmentLayer"

    [<Export(typeof<AdornmentLayerDefinition>)>]
    [<Name("BlockCaretAdornmentLayer")>]
    [<Order(After = PredefinedAdornmentLayers.Selection)>]
    let _blockCaretAdornmentLayerDefinition : AdornmentLayerDefinition = null

    [<ImportingConstructor>]
    new (
        editorOperationsService : IEditorOperationsFactoryService,
        editorFormatMap : IEditorFormatMap,
        completionBroker : ICompletionBroker,
        signatureBroker : ISignatureHelpBroker ) =
            VimFactoryService(editorOperationsService, editorFormatMap, completionBroker, signatureBroker, 42)

    member private x.CreateVimCore host =
        Vim(
            host, 
            _editorOperationsFactoryService, 
            _editorFormatMap, 
            _completionBroker, 
            _signatureBroker,
            _blockCaretAdornmentLayerName)

    interface IVimFactoryService with
        member x.CreateVim host = (x.CreateVimCore host) :> IVim
        member x.CreateVimBuffer host view name = 
            let vim = (x.CreateVimCore host) :> IVim
            vim.CreateBuffer view name 
        member x.CreateKeyProcessor buffer = Vim.KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
      