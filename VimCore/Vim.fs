#light

namespace Vim
open Microsoft.VisualStudio.Text
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
        _completionWindowBrokerFactoryService : ICompletionWindowBrokerFactoryService,
        _blockCaretFactoryService : IBlockCaretFactoryService,
        _textSearchService : ITextSearchService,
        _textStructureNavigatorSelectorService : ITextStructureNavigatorSelectorService,
        _tlcService : ITrackingLineColumnService ) =
    let _markMap = MarkMap(_tlcService) :> IMarkMap
    let _registerMap = RegisterMap()
    let _settings = VimSettingsUtil.CreateDefault

    let _bufferMap = new System.Collections.Generic.Dictionary<IWpfTextView, IVimBuffer>()

    member x.CreateVimBufferCore view = 
        if _bufferMap.ContainsKey(view) then invalidArg "view" Resources.Vim_ViewAlreadyHasBuffer

        let editorFormatMap = _editorFormatMapService.GetEditorFormatMap(view :> ITextView)
        let caret = _blockCaretFactoryService.CreateBlockCaret view
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let jumpList = JumpList(_tlcService) :> IJumpList
        let bufferRaw = 
            VimBuffer( 
                x :> IVim,
                view,
                editOperations,
                caret,
                jumpList)
        let buffer = bufferRaw :> IVimBuffer

        let wordNav = x.CreateTextStructureNavigator view.TextBuffer WordKind.NormalWord
        let broker = _completionWindowBrokerFactoryService.CreateCompletionWindowBroker view
        let normalOpts = Modes.Normal.DefaultOperations(view,editOperations,_host,_settings,wordNav,_textSearchService,jumpList) :> Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(view,editOperations,_host, jumpList) :> Modes.Command.IOperations
        let insertOpts = Modes.Insert.DefaultOperations(view,editOperations,_host, jumpList) :> Modes.ICommonOperations
        let visualOptsFactory kind = 
            let mode = 
                match kind with 
                | ModeKind.VisualBlock -> Modes.Visual.SelectionMode.Block
                | ModeKind.VisualCharacter -> Modes.Visual.SelectionMode.Character
                | ModeKind.VisualLine -> Modes.Visual.SelectionMode.Line
                | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"
            let tracker = Modes.Visual.SelectionTracker(view,mode) :> Modes.Visual.ISelectionTracker
            Modes.Visual.DefaultOperations(view,editOperations,_host, jumpList, tracker) :> Modes.Visual.IOperations

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
        _bufferMap.Add(view, buffer)
        buffer

    member private x.GetBufferCore view =
        let tuple = _bufferMap.TryGetValue(view)
        match tuple with 
        | (true,buffer) -> Some buffer
        | (false,_) -> None

    member private x.CreateTextStructureNavigator textBuffer wordKind =
        let baseImpl = _textStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer)
        TssUtil.CreateTextStructureNavigator wordKind baseImpl

    interface IVim with
        member x.Host = _host
        member x.MarkMap = _markMap
        member x.RegisterMap = _registerMap :> IRegisterMap
        member x.Settings = _settings
        member x.CreateBuffer view = x.CreateVimBufferCore view 
        member x.RemoveBuffer view = _bufferMap.Remove(view)
        member x.GetBuffer view = x.GetBufferCore view
        member x.GetBufferForBuffer textBuffer =
            let keys = _bufferMap.Keys |> Seq.filter (fun view -> view.TextBuffer = textBuffer)
            match keys |> Seq.isEmpty with
            | true -> None
            | false -> keys |> Seq.head |> x.GetBufferCore
                

        
        
