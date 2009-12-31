#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification

/// Default implementation of IVim 
type internal Vim
    (
        _host : IVimHost,
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorFormatMapService : IEditorFormatMapService,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _blockCaretAdornmentLayerName : string ) =
    let _markMap = MarkMap()
    let _registerMap = RegisterMap()
    let _settings = VimSettingsUtil.CreateDefault
    let mutable _buffers : seq<IVimBuffer> = Seq.empty

    member x.CreateVimBufferCore view name = 
        let editorFormatMap = _editorFormatMapService.GetEditorFormatMap(view :> ITextView)
        let caret = BlockCaret(view, _blockCaretAdornmentLayerName, editorFormatMap) :> IBlockCaret
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
        let visualOpts = Modes.Visual.DefaultOperations(view,editOperations) :> Modes.ICommonOperations
        let modeList = 
            [
                ((Modes.Normal.NormalMode(buffer, normalOpts)) :> IMode);
                ((Modes.Command.CommandMode(buffer, commandOpts)) :> IMode);
                ((Modes.Insert.InsertMode(buffer,insertOpts,broker)) :> IMode);
                (DisabledMode(buffer) :> IMode);
                ((Modes.Visual.VisualMode(buffer, visualOpts, ModeKind.VisualBlock)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, visualOpts, ModeKind.VisualLineWise)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, visualOpts, ModeKind.VisualCharacter)) :> IMode);
            ]
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        buffer
    
    interface IVim with
        member x.Host = _host
        member x.MarkMap = _markMap
        member x.RegisterMap = _registerMap :> IRegisterMap
        member x.Settings = _settings
        member x.Buffers = _buffers
        member x.CreateBuffer view bufferName =
            x.CreateVimBufferCore view bufferName 
        
        
