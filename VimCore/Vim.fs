#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification
open System.ComponentModel.Composition
open System.IO

/// Default implementation of IVim 
[<Export(typeof<IVimBufferFactory>)>]
type internal VimBufferFactory
    [<ImportingConstructor>]
    (
        _host : IVimHost,
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorFormatMapService : IEditorFormatMapService,
        _completionWindowBrokerFactoryService : ICompletionWindowBrokerFactoryService,
        _textSearchService : ITextSearchService,
        _textStructureNavigatorSelectorService : ITextStructureNavigatorSelectorService,
        _tlcService : ITrackingLineColumnService,
        _editorFactoryService : ITextEditorFactoryService ) =

    let _bufferCreatedEvent = new Event<_>()
    
    member x.CreateBuffer (vim:IVim) view = 
        let editorFormatMap = _editorFormatMapService.GetEditorFormatMap(view :> ITextView)
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let jumpList = JumpList(_tlcService) :> IJumpList
        let localSettings = LocalSettings(vim.Settings, view) :> IVimLocalSettings
        let bufferRaw = 
            VimBuffer( 
                vim,
                view,
                editOperations,
                jumpList,
                localSettings)
        let buffer = bufferRaw :> IVimBuffer

        let wordNav = x.CreateTextStructureNavigator view.TextBuffer WordKind.NormalWord
        let broker = _completionWindowBrokerFactoryService.CreateCompletionWindowBroker view
        let normalOpts = Modes.Normal.DefaultOperations(view,editOperations,_host,localSettings,wordNav,_textSearchService,jumpList) :> Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(view,editOperations,_host, jumpList, localSettings, vim.KeyMap) :> Modes.Command.IOperations
        let commandProcessor = Modes.Command.CommandProcessor(buffer, commandOpts) :> Modes.Command.ICommandProcessor
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
        let normalIncrementalSearch = Vim.Modes.Normal.IncrementalSearch(_host, view, localSettings, normalSearchReplace) :> Modes.Normal.IIncrementalSearch
        let modeList = 
            [
                ((Modes.Normal.NormalMode(buffer, normalOpts, normalIncrementalSearch)) :> IMode);
                ((Modes.Command.CommandMode(buffer, commandProcessor)) :> IMode);
                ((Modes.Insert.InsertMode(buffer,insertOpts,broker)) :> IMode);
                (DisabledMode(buffer) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualBlock), ModeKind.VisualBlock)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualLine), ModeKind.VisualLine)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualCharacter), ModeKind.VisualCharacter)) :> IMode);
            ]
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        buffer.SwitchMode ModeKind.Normal |> ignore
        _bufferCreatedEvent.Trigger buffer
        bufferRaw

    member private x.CreateTextStructureNavigator textBuffer wordKind =
        let baseImpl = _textStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer)
        TssUtil.CreateTextStructureNavigator wordKind baseImpl

    interface IVimBufferFactory with
        member x.CreateBuffer vim view = x.CreateBuffer vim view :> IVimBuffer
        [<CLIEvent>]
        member x.BufferCreated = _bufferCreatedEvent.Publish


/// Default implementation of IVim 
[<Export(typeof<IVim>)>]
type internal Vim
    (
        _host : IVimHost,
        _bufferFactoryService : IVimBufferFactory,
        _editorFactoryService : ITextEditorFactoryService,
        _bufferCreationListeners : Lazy<IVimBufferCreationListener> seq,
        _settings : IVimGlobalSettings,
        _registerMap : IRegisterMap,
        _markMap : IMarkMap,
        _keyMap : IKeyMap ) =


    static let _vimRcEnvironmentVariables = ["HOME";"VIM";"USERPROFILE"]

    let _bufferMap = new System.Collections.Generic.Dictionary<IWpfTextView, IVimBuffer>()

    [<ImportingConstructor>]
    new(
        host : IVimHost,
        bufferFactoryService : IVimBufferFactory,
        tlcService : ITrackingLineColumnService,
        editorFactoryService : ITextEditorFactoryService,
        [<ImportMany>] bufferCreationListeners : Lazy<IVimBufferCreationListener> seq ) =
        Vim(
            host,
            bufferFactoryService,
            editorFactoryService,
            bufferCreationListeners,
            GlobalSettings() :> IVimGlobalSettings,
            RegisterMap() :> IRegisterMap,
            MarkMap(tlcService) :> IMarkMap,
            KeyMap() :> IKeyMap)

    member x.CreateVimBufferCore view = 
        if _bufferMap.ContainsKey(view) then invalidArg "view" Resources.Vim_ViewAlreadyHasBuffer
        let buffer = _bufferFactoryService.CreateBuffer (x:>IVim) view
        _bufferMap.Add(view, buffer)
        _bufferCreationListeners |> Seq.iter (fun x -> x.Value.VimBufferCreated buffer)
        buffer

    member private x.RemoveBufferCore view = _bufferMap.Remove(view)

    member private x.GetBufferCore view =
        let tuple = _bufferMap.TryGetValue(view)
        match tuple with 
        | (true,buffer) -> Some buffer
        | (false,_) -> None

    member private x.GetOrCreateBufferCore view =
        match x.GetBufferCore view with
        | Some(buffer) -> buffer
        | None -> x.CreateVimBufferCore view

    static member LoadVimRc (vim:IVim) (editorFactoryService:ITextEditorFactoryService)= 
        let settings = vim.Settings
        settings.VimRc <- System.String.Empty

        let getPaths var =
            match System.Environment.GetEnvironmentVariable(var) with
            | null -> Seq.empty
            | value ->
                let files = [".vimrc"; "_vimrc" ]
                files |> Seq.map (fun file -> Path.Combine(value,file)) 
        let paths = _vimRcEnvironmentVariables |> Seq.map getPaths |> Seq.concat
        settings.VimRcPaths <- paths |> String.concat ";"

        match paths |> Seq.tryPick Utils.ReadAllLines with
        | None -> false
        | Some(path,lines) ->
            settings.VimRc <- path
            let view = editorFactoryService.CreateTextView()
            let buffer = vim.GetOrCreateBuffer view
            let mode = buffer.GetMode ModeKind.Command :?> Modes.Command.ICommandMode
            lines |> Seq.iter mode.RunCommand
            vim.RemoveBuffer view |> ignore
            true

    interface IVim with
        member x.Host = _host
        member x.MarkMap = _markMap
        member x.KeyMap = _keyMap
        member x.IsVimRcLoaded = not (System.String.IsNullOrEmpty(_settings.VimRc))
        member x.RegisterMap = _registerMap 
        member x.Settings = _settings
        member x.CreateBuffer view = x.CreateVimBufferCore view 
        member x.GetOrCreateBuffer view = x.GetOrCreateBufferCore view
        member x.RemoveBuffer view = x.RemoveBufferCore view
        member x.GetBuffer view = x.GetBufferCore view
        member x.GetBufferForBuffer textBuffer =
            let keys = _bufferMap.Keys |> Seq.filter (fun view -> view.TextBuffer = textBuffer)
            match keys |> Seq.isEmpty with
            | true -> None
            | false -> keys |> Seq.head |> x.GetBufferCore
        member x.LoadVimRc () = Vim.LoadVimRc x _editorFactoryService
                

        
        
