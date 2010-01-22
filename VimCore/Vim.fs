#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification
open System.ComponentModel.Composition

/// Default implementation of IVim 
[<Export(typeof<IVim>)>]
type internal Vim
    [<ImportingConstructor>]
    (
        _host : IVimHost,
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorFormatMapService : IEditorFormatMapService,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _blockCaretFactoryService : IBlockCaretFactoryService ) =
    let _markMap = MarkMap()
    let _registerMap = RegisterMap()
    let _settings = VimSettingsUtil.CreateDefault

    member x.CreateVimBufferCore view name = 
        let editorFormatMap = _editorFormatMapService.GetEditorFormatMap(view :> ITextView)
        let caret = _blockCaretFactoryService.CreateBlockCaret view
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let bufferRaw = 
            VimBuffer( 
                x :> IVim,
                view,
                name,
                editOperations,
                caret)
        let buffer = bufferRaw :> IVimBuffer

        let broker = CompletionWindowBroker(view, _completionBroker, _signatureBroker) :> ICompletionWindowBroker
        let normalOpts = Modes.Normal.DefaultOperations(view,editOperations) :> Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(view,editOperations,_host) :> Modes.Command.IOperations
        let insertOpts = Modes.Insert.DefaultOperations(view,editOperations) :> Modes.ICommonOperations
        let visualOptsFactory kind = 
            let mode = 
                match kind with 
                | ModeKind.VisualBlock -> Modes.Visual.SelectionMode.Block
                | ModeKind.VisualCharacter -> Modes.Visual.SelectionMode.Character
                | ModeKind.VisualLine -> Modes.Visual.SelectionMode.Line
                | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"
            let tracker = Modes.Visual.SelectionTracker(view,mode) :> Modes.Visual.ISelectionTracker
            Modes.Visual.DefaultOperations(view,editOperations,tracker) :> Modes.Visual.IOperations

        // Normal mode values
        let normalSearchReplace = RegexSearchReplace() :> ISearchReplace
        let normalIncrementalSearch = Vim.Modes.Normal.IncrementalSearch(_host, view, _settings, normalSearchReplace) :> Modes.Normal.IIncrementalSearch
        let modeList = 
            [
                ((Modes.Normal.NormalMode(buffer, normalOpts, normalSearchReplace, normalIncrementalSearch)) :> IMode);
                ((Modes.Command.CommandMode(buffer, commandOpts)) :> IMode);
                ((Modes.Insert.InsertMode(buffer,insertOpts,broker)) :> IMode);
                (DisabledMode(buffer) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualBlock), ModeKind.VisualBlock)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualLine), ModeKind.VisualLine)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualCharacter), ModeKind.VisualCharacter)) :> IMode);
            ]
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        buffer.SwitchMode ModeKind.Normal |> ignore
        buffer
    
    interface IVim with
        member x.Host = _host
        member x.MarkMap = _markMap
        member x.RegisterMap = _registerMap :> IRegisterMap
        member x.Settings = _settings
        member x.CreateBuffer view bufferName =
            x.CreateVimBufferCore view bufferName 
        
        
