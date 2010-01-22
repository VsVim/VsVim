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

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    (
        _host : IVimHost,
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorFormatMapService : IEditorFormatMapService,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _unused : int ) =

    let _blockCaretAdornmentLayerName = "BlockCaretAdornmentLayer"
    let mutable _vim : IVim option = None

    [<Export(typeof<AdornmentLayerDefinition>)>]
    [<Name("BlockCaretAdornmentLayer")>]
    [<Order(After = PredefinedAdornmentLayers.Selection)>]
    let mutable _blockCaretAdornmentLayerDefinition : AdornmentLayerDefinition = null

    [<ImportingConstructor>]
    new (
        host : IVimHost,
        editorOperationsService : IEditorOperationsFactoryService,
        editorFormatMapService : IEditorFormatMapService,
        completionBroker : ICompletionBroker,
        signatureBroker : ISignatureHelpBroker ) =
            VimFactoryService(host, editorOperationsService, editorFormatMapService, completionBroker, signatureBroker, 42)

    /// This method is a hack.  Unless a let binding is explicitly used the F# compiler 
    /// will remove it from the final metadata definition.  This will prevent the MEF
    /// import from ever being resolved and hence cause us to not define the adornment
    /// layer.  Hacky member method that is never called to fake assign and prevent this
    /// problem
    member private x.Hack() =
        _blockCaretAdornmentLayerDefinition = AdornmentLayerDefinition()

    member private x.GetOrCreateVimCore () =
        match _vim with 
        | Some(vim) -> vim
        | None ->
            let vim = Vim( _host, 
                           _editorOperationsFactoryService, 
                           _editorFormatMapService, 
                           _completionBroker, 
                           _signatureBroker,
                           _blockCaretAdornmentLayerName)
            let vim = vim :> IVim
            _vim <- Some vim
            vim
                

    interface IVimFactoryService with
        member x.Vim = x.GetOrCreateVimCore()
        member x.CreateVimBuffer view name = 
            let vim = x.GetOrCreateVimCore()
            vim.CreateBuffer view name 
        member x.CreateKeyProcessor buffer = Vim.KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
        member x.CreateMouseProcessor buffer = Vim.MouseProcessor(buffer, MouseDeviceImpl() :> IMouseDevice) :> Microsoft.VisualStudio.Text.Editor.IMouseProcessor
        member x.CreateTagger (buffer:IVimBuffer) = 
            let normal = buffer.GetMode ModeKind.Normal :?> Modes.Normal.NormalMode
            let search = normal.IncrementalSearch
            let tagger = Modes.Normal.Tagger(search)
            tagger :> ITagger<TextMarkerTag>
      