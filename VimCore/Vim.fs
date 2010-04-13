#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
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
        _outliningManagerService : IOutliningManagerService,
        _completionWindowBrokerFactoryService : IDisplayWindowBrokerFactoryService,
        _textSearchService : ITextSearchService,
        _textStructureNavigatorSelectorService : ITextStructureNavigatorSelectorService,
        _tlcService : ITrackingLineColumnService )=

    member x.CreateBuffer (vim:IVim) view = 
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let outlining = _outliningManagerService.GetOutliningManager(view)
        let jumpList = JumpList(_tlcService) :> IJumpList
        let localSettings = LocalSettings(vim.Settings, view) :> IVimLocalSettings
        let bufferRaw = 
            VimBuffer( 
                vim,
                view,
                jumpList,
                localSettings)
        let buffer = bufferRaw :> IVimBuffer

        let statusUtil = x.CreateStatusUtil bufferRaw 
        let wordNav = x.CreateTextStructureNavigator view.TextBuffer WordKind.NormalWord
        let broker = _completionWindowBrokerFactoryService.CreateDisplayWindowBroker view
        let normalIncrementalSearch = Vim.Modes.Normal.IncrementalSearch(view, outlining, localSettings, wordNav, vim.SearchService) :> IIncrementalSearch
        let normalOpts = Modes.Normal.DefaultOperations(view,editOperations, outlining,_host, statusUtil, localSettings,wordNav,jumpList, normalIncrementalSearch) :> Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(view,editOperations,outlining, _host, statusUtil, jumpList, localSettings, vim.KeyMap) :> Modes.Command.IOperations
        let commandProcessor = Modes.Command.CommandProcessor(buffer, commandOpts, statusUtil) :> Modes.Command.ICommandProcessor
        let insertOpts = Modes.Insert.DefaultOperations(view,editOperations,outlining,_host, jumpList, localSettings) :> Modes.ICommonOperations
        let visualOptsFactory kind = 
            let mode = 
                match kind with 
                | ModeKind.VisualBlock -> Modes.Visual.SelectionMode.Block
                | ModeKind.VisualCharacter -> Modes.Visual.SelectionMode.Character
                | ModeKind.VisualLine -> Modes.Visual.SelectionMode.Line
                | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"
            let tracker = Modes.Visual.SelectionTracker(view,mode) :> Modes.Visual.ISelectionTracker
            Modes.Visual.DefaultOperations(view,editOperations, outlining, _host, jumpList, tracker, localSettings) :> Modes.Visual.IOperations

        // Normal mode values
        let modeList = 
            [
                ((Modes.Normal.NormalMode(buffer, normalOpts, normalIncrementalSearch,statusUtil,broker)) :> IMode);
                ((Modes.Command.CommandMode(buffer, commandProcessor)) :> IMode);
                ((Modes.Insert.InsertMode(buffer,insertOpts,broker)) :> IMode);
                (DisabledMode(buffer) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualBlock), ModeKind.VisualBlock)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualLine), ModeKind.VisualLine)) :> IMode);
                ((Modes.Visual.VisualMode(buffer, (visualOptsFactory ModeKind.VisualCharacter), ModeKind.VisualCharacter)) :> IMode);
            ]
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        buffer.SwitchMode ModeKind.Normal |> ignore
        bufferRaw

    member private x.CreateTextStructureNavigator textBuffer wordKind =
        let baseImpl = _textStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer)
        TssUtil.CreateTextStructureNavigator wordKind baseImpl

    member private x.CreateStatusUtil (buffer:VimBuffer) =
        { new Vim.Modes.IStatusUtil with 
            member x.OnStatus msg = buffer.RaiseStatusMessage msg
            member x.OnError msg = buffer.RaiseErrorMessage msg
            member x.OnStatusLong msgSeq = buffer.RaiseStatusMessageLong msgSeq }
        

    interface IVimBufferFactory with
        member x.CreateBuffer vim view = x.CreateBuffer vim view :> IVimBuffer


/// Default implementation of IVim 
[<Export(typeof<IVim>)>]
type internal Vim
    (
        _host : IVimHost,
        _bufferFactoryService : IVimBufferFactory,
        _bufferCreationListeners : Lazy<IVimBufferCreationListener> list,
        _settings : IVimGlobalSettings,
        _registerMap : IRegisterMap,
        _markMap : IMarkMap,
        _keyMap : IKeyMap,
        _changeTracker : IChangeTracker,
        _search : ISearchService ) =


    static let _vimRcEnvironmentVariables = ["HOME";"VIM";"USERPROFILE"]

    let _bufferMap = new System.Collections.Generic.Dictionary<ITextView, IVimBuffer>()

    [<ImportingConstructor>]
    new(
        host : IVimHost,
        bufferFactoryService : IVimBufferFactory,
        tlcService : ITrackingLineColumnService,
        [<ImportMany>] bufferCreationListeners : Lazy<IVimBufferCreationListener> seq,
        [<Import>] search : ITextSearchService ) =
        let tracker = ChangeTracker() 
        let globalSettings = GlobalSettings() :> IVimGlobalSettings
        let listeners = 
            new Lazy<IVimBufferCreationListener>( fun () -> tracker :> IVimBufferCreationListener)
            |> Seq.singleton
            |> Seq.append bufferCreationListeners 
            |> List.ofSeq
        Vim(
            host,
            bufferFactoryService,
            listeners,
            globalSettings,
            RegisterMap() :> IRegisterMap,
            MarkMap(tlcService) :> IMarkMap,
            KeyMap() :> IKeyMap,
            tracker :> IChangeTracker,
            SearchService(search, globalSettings) :> ISearchService)

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

    static member LoadVimRc (vim:IVim) (createViewFunc : (unit -> ITextView)) =
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
            let view = createViewFunc()
            let buffer = vim.GetOrCreateBuffer view
            let mode = buffer.CommandMode
            lines |> Seq.iter mode.RunCommand
            vim.RemoveBuffer view |> ignore
            true

    interface IVim with
        member x.Host = _host
        member x.MarkMap = _markMap
        member x.KeyMap = _keyMap
        member x.ChangeTracker = _changeTracker
        member x.SearchService = _search
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
        member x.LoadVimRc createViewFunc = Vim.LoadVimRc x createViewFunc
                

        
        
