#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open System.ComponentModel.Composition

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService() =

    [<ImportingConstructor>]
    new (
        editorOperationsService : IEditorOperationsFactoryService ) =
            VimFactoryService()

    interface IVimFactoryService with
        member x.CreateVim host = (Vim(host)) :> IVim
        member x.CreateVimBuffer host view editorOperations name caret broker = 
            let vim = (Vim(host)) :> IVim
            vim.CreateBuffer view editorOperations name caret broker
        member x.CreateKeyProcessor buffer = KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
      