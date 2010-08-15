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
open Vim.Modes

/// Default implementation of IVim 
[<Export(typeof<IVimBufferFactory>)>]
type internal VimBufferFactory

    [<ImportingConstructor>]
    (
        _host : IVimHost,
        _editorOperationsFactoryService : IEditorOperationsFactoryService,
        _editorOptionsFactoryService : IEditorOptionsFactoryService,
        _outliningManagerService : IOutliningManagerService,
        _completionWindowBrokerFactoryService : IDisplayWindowBrokerFactoryService,
        _textSearchService : ITextSearchService,
        _textStructureNavigatorSelectorService : ITextStructureNavigatorSelectorService,
        _tlcService : ITrackingLineColumnService,
        _undoManagerProvider : ITextBufferUndoManagerProvider,
        _foldManagerFactory : IFoldManagerFactory ) =

    let _motionCaptureGlobalData = MotionCaptureGlobalData() :> IMotionCaptureGlobalData

    member x.CreateBuffer (vim:IVim) view = 
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let editOptions = _editorOptionsFactoryService.GetOptions(view)
        let motionUtil = MotionUtil(view, vim.Settings) :> IMotionUtil
        let capture = MotionCapture(vim.VimHost, view, motionUtil, _motionCaptureGlobalData) :> IMotionCapture
        let outlining = _outliningManagerService.GetOutliningManager(view)
        let jumpList = JumpList(_tlcService) :> IJumpList
        let foldManager = _foldManagerFactory.GetFoldManager(view.TextBuffer)
        let localSettings = LocalSettings(vim.Settings, view) :> IVimLocalSettings
        let bufferRaw = 
            VimBuffer( 
                vim,
                view,
                jumpList,
                localSettings)
        let buffer = bufferRaw :> IVimBuffer

        let statusUtil = x.CreateStatusUtil bufferRaw 
        let undoRedoOperations = 
            let history = 
                let manager = _undoManagerProvider.GetTextBufferUndoManager(view.TextBuffer)
                if manager = null then None
                else manager.TextBufferUndoHistory |> Some
            UndoRedoOperations(statusUtil, history) :> IUndoRedoOperations
        let wordNav = x.CreateTextStructureNavigator view.TextBuffer WordKind.NormalWord
        let operationsData = { 
            VimHost=_host;
            TextView=view;
            EditorOperations=editOperations;
            EditorOptions=editOptions;
            OutliningManager=outlining;
            JumpList=jumpList;
            LocalSettings=localSettings;
            UndoRedoOperations=undoRedoOperations;
            StatusUtil=statusUtil;
            KeyMap=vim.KeyMap;
            Navigator=wordNav;
            FoldManager=foldManager }

        let createCommandRunner() = CommandRunner (view, vim.RegisterMap, capture,statusUtil) :>ICommandRunner
        let broker = _completionWindowBrokerFactoryService.CreateDisplayWindowBroker view
        let bufferOptions = _editorOptionsFactoryService.GetOptions(view.TextBuffer)
        let normalIncrementalSearch = Vim.Modes.Normal.IncrementalSearch(view, outlining, localSettings, wordNav, vim.SearchService) :> IIncrementalSearch
        let normalOpts = Modes.Normal.DefaultOperations(operationsData, normalIncrementalSearch) :> Vim.Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(operationsData) :> Modes.Command.IOperations
        let commandProcessor = Modes.Command.CommandProcessor(buffer, commandOpts, statusUtil, FileSystem() :> IFileSystem) :> Modes.Command.ICommandProcessor
        let insertOpts = Modes.Insert.DefaultOperations(operationsData) :> Modes.ICommonOperations
        let visualOptsFactory kind = 
            let mode = 
                match kind with 
                | ModeKind.VisualBlock -> Modes.Visual.SelectionMode.Block
                | ModeKind.VisualCharacter -> Modes.Visual.SelectionMode.Character
                | ModeKind.VisualLine -> Modes.Visual.SelectionMode.Line
                | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"
            let tracker = Modes.Visual.SelectionTracker(view,mode) :> Modes.Visual.ISelectionTracker
            let opts = Modes.Visual.DefaultOperations(operationsData, kind ) :> Modes.Visual.IOperations
            (tracker, opts)

        let visualModeList =
            [ ModeKind.VisualBlock; ModeKind.VisualCharacter; ModeKind.VisualLine ]
            |> Seq.ofList
            |> Seq.map (fun kind -> 
                let tracker, opts = visualOptsFactory kind
                ((Modes.Visual.VisualMode(buffer, opts, kind, createCommandRunner(),capture, tracker)) :> IMode) )
            |> List.ofSeq
    
        // Normal mode values
        let modeList = 
            [
                ((Modes.Normal.NormalMode(buffer, normalOpts, normalIncrementalSearch,statusUtil,broker, createCommandRunner(),capture)) :> IMode);
                ((Modes.Command.CommandMode(buffer, commandProcessor)) :> IMode);
                ((Modes.Insert.InsertMode(buffer,insertOpts,broker, editOptions,false)) :> IMode);
                ((Modes.Insert.InsertMode(buffer,insertOpts,broker, editOptions,true)) :> IMode);
                (DisabledMode(buffer) :> IMode);
            ] @ visualModeList
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        buffer.SwitchMode ModeKind.Normal ModeArgument.None |> ignore
        bufferRaw

    member private x.CreateTextStructureNavigator textBuffer wordKind =
        let baseImpl = _textStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer)
        TssUtil.CreateTextStructureNavigator wordKind baseImpl

    member private x.CreateStatusUtil (buffer:VimBuffer) =
        { new IStatusUtil with 
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

    let _bufferMap = new System.Collections.Generic.Dictionary<ITextView, IVimBuffer>()

    [<ImportingConstructor>]
    new(
        host : IVimHost,
        bufferFactoryService : IVimBufferFactory,
        tlcService : ITrackingLineColumnService,
        [<ImportMany>] bufferCreationListeners : Lazy<IVimBufferCreationListener> seq,
        search : ITextSearchService,
        textChangeTrackerFactory : ITextChangeTrackerFactory ) =
        let markMap = MarkMap(tlcService)
        let tracker = ChangeTracker(textChangeTrackerFactory)
        let globalSettings = GlobalSettings() :> IVimGlobalSettings
        let listeners = 
            [tracker :> IVimBufferCreationListener; markMap :> IVimBufferCreationListener]
            |> Seq.map (fun t -> new Lazy<IVimBufferCreationListener>(fun () -> t))
            |> Seq.append bufferCreationListeners 
            |> List.ofSeq
        Vim(
            host,
            bufferFactoryService,
            listeners,
            globalSettings,
            RegisterMap() :> IRegisterMap,
            markMap :> IMarkMap,
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

    static member LoadVimRc (vim:IVim) (fileSystem:IFileSystem) (createViewFunc : (unit -> ITextView)) =
        let settings = vim.Settings
        settings.VimRc <- System.String.Empty

        settings.VimRcPaths <- fileSystem.GetVimRcDirectories() |> String.concat ";"
        match fileSystem.LoadVimRc() with
        | None -> false
        | Some(path,lines) ->
            settings.VimRc <- path
            let view = createViewFunc()
            let buffer = vim.GetOrCreateBuffer view
            let mode = buffer.CommandMode
            lines |> Seq.iter mode.RunCommand
            view.Close()
            true

    interface IVim with
        member x.VimHost = _host
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
        member x.LoadVimRc fileSystem createViewFunc = Vim.LoadVimRc x fileSystem createViewFunc
                

        
        
