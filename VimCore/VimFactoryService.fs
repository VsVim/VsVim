#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open System.ComponentModel.Composition

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    (
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _unused : int ) =

    [<ImportingConstructor>]
    new (
        editorOperationsService : IEditorOperationsFactoryService,
        completionBroker : ICompletionBroker,
        signatureBroker : ISignatureHelpBroker ) =
            VimFactoryService(editorOperationsService, completionBroker, signatureBroker, 42)

    interface IVimFactoryService with
        member x.CreateVim host = (Vim(host, _editorOperationsFactoryService, _completionBroker, _signatureBroker)) :> IVim
        member x.CreateVimBuffer host view name caret = 
            let vim = (Vim(host, _editorOperationsFactoryService, _completionBroker, _signatureBroker)) :> IVim
            vim.CreateBuffer view name caret 
        member x.CreateKeyProcessor buffer = KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
      