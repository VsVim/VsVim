#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open System.ComponentModel.Composition

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    (
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _unused : int ) =

    [<ImportingConstructor>]
    new (
        editorOperationsService : IEditorOperationsFactoryService ) =
            VimFactoryService(editorOperationsService, 42)

    interface IVimFactoryService with
        member x.CreateVim host = (Vim(host)) :> IVim
        member x.CreateVimBuffer host view name caret broker = 
            let vim = (Vim(host)) :> IVim
            let opts = _editorOperationsFactoryService.GetEditorOperations(view)
            vim.CreateBuffer view opts name caret broker
        member x.CreateKeyProcessor buffer = KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
      