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
type internal BlockCaretFactoryService [<ImportingConstructor>] ( _formatMap : IEditorFormatMap ) =
    
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
        member x.CreateBlockCaret textView = BlockCaret(textView, _blockCaretAdornmentLayerName, _formatMap) :> IBlockCaret

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    [<ImportingConstructor>]
    ( _vim : IVim ) =

    interface IVimFactoryService with
        member x.Vim = _vim
        member x.CreateVimBuffer view name = _vim.CreateBuffer view name 
        member x.CreateKeyProcessor buffer = Vim.KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
        member x.CreateMouseProcessor buffer = Vim.MouseProcessor(buffer, MouseDeviceImpl() :> IMouseDevice) :> Microsoft.VisualStudio.Text.Editor.IMouseProcessor
        member x.CreateTagger (buffer:IVimBuffer) = 
            let normal = buffer.GetMode ModeKind.Normal :?> Modes.Normal.NormalMode
            let search = normal.IncrementalSearch
            let tagger = Modes.Normal.Tagger(search)
            tagger :> ITagger<TextMarkerTag>
      