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
        _editorFormatMap : IEditorFormatMap,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _blockCaretAdornmentLayerName : string ) =
    let _data = VimData()
    let mutable _buffers : seq<IVimBuffer> = Seq.empty

    member x.CreateVimBufferCore view name = 
        let caret = BlockCaret(view, _blockCaretAdornmentLayerName, _editorFormatMap) :> IBlockCaret
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let broker = CompletionWindowBroker(view, _completionBroker, _signatureBroker) :> ICompletionWindowBroker
        let data = VimBufferData(name, view, _host, _data :> IVimData, caret, editOperations ) :> IVimBufferData
        let normalOpts = Modes.Normal.DefaultOperations(view,editOperations) :> Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(view,editOperations,_host) :> Modes.Command.IOperations
        let insertOpts = Modes.Insert.DefaultOperations(view,editOperations) :> Modes.ICommonOperations
        let visualOpts = Modes.Visual.DefaultOperations(view,editOperations) :> Modes.ICommonOperations
        let modeList = 
            [
                ((Modes.Normal.NormalMode(data, normalOpts)) :> IMode);
                ((Modes.Command.CommandMode(data, commandOpts)) :> IMode);
                ((Modes.Insert.InsertMode(data,insertOpts,broker)) :> IMode);
                (DisabledMode(data) :> IMode);
                ((Modes.Visual.VisualMode(data, visualOpts, ModeKind.VisualBlock)) :> IMode);
                ((Modes.Visual.VisualMode(data, visualOpts, ModeKind.VisualLineWise)) :> IMode);
                ((Modes.Visual.VisualMode(data, visualOpts, ModeKind.VisualCharacter)) :> IMode);
            ]
        let modeMap =
            modeList 
                |> List.map (fun x-> (x.ModeKind,x))
                |> Map.ofList
        let buf = VimBuffer(data, modeMap)
        buf :> IVimBuffer
    
    interface IVim with
        member x.Host = _host
        member x.Data = _data :> IVimData
        member x.Buffers = _buffers
        member x.CreateBuffer view bufferName =
            x.CreateVimBufferCore view bufferName 
        
        
