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
        member x.CreateVim host = (Vim(host)) :> IVim
        member x.CreateVimBuffer host view name caret = 
            let vim = (Vim(host)) :> IVim
            let opts = _editorOperationsFactoryService.GetEditorOperations(view)
            let broker = CompletionWindowBroker(view, _completionBroker, _signatureBroker)
            vim.CreateBuffer view opts name caret broker
        member x.CreateKeyProcessor buffer = KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
      