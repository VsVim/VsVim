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
    let _visualSpanCalculator = VisualSpanCalculator() :> IVisualSpanCalculator

    member x.CreateBuffer (vim:IVim) view = 
        let editOperations = _editorOperationsFactoryService.GetEditorOperations(view)
        let editOptions = _editorOptionsFactoryService.GetOptions(view)
        let localSettings = LocalSettings(vim.Settings, view) :> IVimLocalSettings
        let motionUtil = TextViewMotionUtil(view, localSettings) :> ITextViewMotionUtil
        let capture = MotionCapture(vim.VimHost, view, motionUtil, _motionCaptureGlobalData) :> IMotionCapture
        let outlining = _outliningManagerService.GetOutliningManager(view)
        let jumpList = JumpList(_tlcService) :> IJumpList
        let foldManager = _foldManagerFactory.GetFoldManager(view.TextBuffer)
        let bufferRaw = 
            VimBuffer( 
                vim,
                view,
                jumpList,
                localSettings)
        let buffer = bufferRaw :> IVimBuffer
        let selectionChangeTracker = SelectionChangeTracker(buffer)

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
            FoldManager=foldManager;
            RegisterMap=vim.RegisterMap }

        let createCommandRunner() = CommandRunner (view, vim.RegisterMap, capture,statusUtil) :>ICommandRunner
        let broker = _completionWindowBrokerFactoryService.CreateDisplayWindowBroker view
        let bufferOptions = _editorOptionsFactoryService.GetOptions(view.TextBuffer)
        let normalIncrementalSearch = Vim.Modes.Normal.IncrementalSearch(view, outlining, localSettings, wordNav, vim.SearchService) :> IIncrementalSearch
        let normalOpts = Modes.Normal.DefaultOperations(operationsData, normalIncrementalSearch) :> Vim.Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(operationsData) :> Modes.Command.IOperations
        let commandProcessor = Modes.Command.CommandProcessor(buffer, commandOpts, statusUtil, FileSystem() :> IFileSystem) :> Modes.Command.ICommandProcessor
        let insertOpts = Modes.Insert.DefaultOperations(operationsData) :> Modes.ICommonOperations
        let visualOptsFactory kind = 
            let kind = VisualKind.ofModeKind kind |> Option.get
            let tracker = Modes.Visual.SelectionTracker(view,vim.Settings,kind) :> Modes.Visual.ISelectionTracker
            let opts = Modes.Insert.DefaultOperations(operationsData ) :> Modes.ICommonOperations
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
                ((Modes.Normal.NormalMode(buffer, normalOpts, normalIncrementalSearch,statusUtil,broker, createCommandRunner(),capture, _visualSpanCalculator)) :> IMode);
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
        _markMap : IMarkMap,
        _keyMap : IKeyMap,
        _clipboardDevice : IClipboardDevice,
        _changeTracker : IChangeTracker,
        _search : ISearchService ) =

    /// Holds an IVimBuffer and the DisposableBag for event handlers on the IVimBuffer.  This
    /// needs to be removed when we're done with the IVimBuffer to avoid leaks
    let _bufferMap = new System.Collections.Generic.Dictionary<ITextView, IVimBuffer * DisposableBag>()

    /// Holds the active stack of IVimBuffer instances
    let mutable _activeBufferStack : IVimBuffer list = List.empty

    let _registerMap =
        let currentFileNameFunc() = 
            match _activeBufferStack with
            | [] -> None
            | h::_ -> 
                let name = _host.GetName h.TextBuffer 
                let name = Path.GetFileName(name)
                Some name
        RegisterMap(_clipboardDevice, currentFileNameFunc) :> IRegisterMap

    [<ImportingConstructor>]
    new(
        host : IVimHost,
        bufferFactoryService : IVimBufferFactory,
        tlcService : ITrackingLineColumnService,
        [<ImportMany>] bufferCreationListeners : Lazy<IVimBufferCreationListener> seq,
        search : ITextSearchService,
        textChangeTrackerFactory : ITextChangeTrackerFactory,
        clipboard : IClipboardDevice ) =
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
            markMap :> IMarkMap,
            KeyMap() :> IKeyMap,
            clipboard,
            tracker :> IChangeTracker,
            SearchService(search, globalSettings) :> ISearchService)

    member x.CreateVimBufferCore view = 
        if _bufferMap.ContainsKey(view) then invalidArg "view" Resources.Vim_ViewAlreadyHasBuffer
        let buffer = _bufferFactoryService.CreateBuffer (x:>IVim) view

        // Setup the handlers for KeyInputStart and KeyInputEnd to accurately track the active
        // IVimBuffer instance
        let eventBag = DisposableBag()
        buffer.KeyInputStart
            |> Observable.subscribe (fun _ -> _activeBufferStack <- buffer :: _activeBufferStack )
            |> eventBag.Add
        buffer.KeyInputEnd 
            |> Observable.subscribe (fun _ -> 
                _activeBufferStack <- 
                    match _activeBufferStack with
                    | h::t -> t
                    | [] -> [] )
            |> eventBag.Add

        _bufferMap.Add(view, (buffer,eventBag))
        _bufferCreationListeners |> Seq.iter (fun x -> x.Value.VimBufferCreated buffer)
        buffer

    member x.RemoveBufferCore view = 
        let found,tuple= _bufferMap.TryGetValue(view)
        if found then 
            let _,bag = tuple
            bag.DisposeAll()
        _bufferMap.Remove(view)

    member x.GetBufferCore view =
        let tuple = _bufferMap.TryGetValue(view)
        match tuple with 
        | (true,(buffer,_)) -> Some buffer
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
        member x.ActiveBuffer = ListUtil.tryHeadOnly _activeBufferStack
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
                

        
        
