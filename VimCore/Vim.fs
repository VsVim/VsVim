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

type internal StatusUtil() = 
    let mutable _buffer : VimBuffer option = None

    member x.VimBuffer 
        with get () = _buffer
        and set value = _buffer <- value

    member x.DoWithBuffer func = 
        match _buffer with
        | None -> ()
        | Some(buffer) -> func buffer

    interface IStatusUtil with
        member x.OnStatus msg = x.DoWithBuffer (fun buffer -> buffer.RaiseStatusMessage msg)
        member x.OnError msg = x.DoWithBuffer (fun buffer -> buffer.RaiseErrorMessage msg)
        member x.OnStatusLong msgSeq = x.DoWithBuffer (fun buffer -> buffer.RaiseStatusMessageLong msgSeq)

type internal VimData() =

    let mutable _lastSubstituteData : SubstituteData option = None
    let mutable _lastSearchData = { Text = SearchText.Pattern(StringUtil.empty); Kind = SearchKind.ForwardWithWrap; Options = SearchOptions.None }
    let _lastSearchChanged = Event<SearchData>()

    interface IVimData with 
        member x.LastSubstituteData 
            with get () = _lastSubstituteData
            and set value = _lastSubstituteData <- value
        member x.LastSearchData
            with get () = _lastSearchData
            and set value = 
                _lastSearchData <- value
                _lastSearchChanged.Trigger value
        [<CLIEvent>]
        member x.LastSearchDataChanged = _lastSearchChanged.Publish

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
        let outlining = 
            // This will return null in ITextBuffer instances where there is no IOutliningManager such
            // as TFS annotated buffers.
            let ret = _outliningManagerService.GetOutliningManager(view)
            if ret = null then None else Some ret
        let jumpList = JumpList(_tlcService) :> IJumpList
        let foldManager = _foldManagerFactory.GetFoldManager(view.TextBuffer)
        let wordNav = x.CreateTextStructureNavigator view.TextBuffer WordKind.NormalWord

        let statusUtil = StatusUtil()
        let undoRedoOperations = 
            let history = 
                let manager = _undoManagerProvider.GetTextBufferUndoManager(view.TextBuffer)
                if manager = null then None
                else manager.TextBufferUndoHistory |> Some
            UndoRedoOperations(statusUtil, history) :> IUndoRedoOperations
        let operationsData = { 
            EditorOperations = editOperations
            EditorOptions = editOptions
            FoldManager = foldManager
            JumpList = jumpList
            KeyMap = vim.KeyMap
            LocalSettings = localSettings
            Navigator = wordNav
            OutliningManager = outlining
            RegisterMap = vim.RegisterMap
            SearchService = vim.SearchService 
            StatusUtil = statusUtil :> IStatusUtil
            TextView = view
            UndoRedoOperations = undoRedoOperations
            VimData = vim.VimData
            VimHost = _host }
        let commonOperations = Modes.CommonOperations(operationsData) :> Modes.ICommonOperations

        let incrementalSearch = 
            IncrementalSearch(
                commonOperations,
                localSettings, 
                wordNav, 
                vim.SearchService, 
                vim.VimData) :> IIncrementalSearch
        let capture = MotionCapture(vim.VimHost, view, motionUtil, incrementalSearch, jumpList, _motionCaptureGlobalData) :> IMotionCapture
        let bufferRaw = 
            VimBuffer( 
                vim,
                view,
                jumpList,
                localSettings,
                incrementalSearch)
        let buffer = bufferRaw :> IVimBuffer

        /// Create the selection change tracker so that it will begin to monitor
        /// selection events.  
        ///
        /// TODO: This feels wrong.  Either the result should be stored somewhere
        /// or it should be exposed as a MEF service that listens to buffer 
        /// creation events.
        let selectionChangeTracker = SelectionChangeTracker(buffer)


        statusUtil.VimBuffer <- Some bufferRaw

        let createCommandRunner() = CommandRunner (view, vim.RegisterMap, capture,statusUtil) :>ICommandRunner
        let broker = _completionWindowBrokerFactoryService.CreateDisplayWindowBroker view
        let bufferOptions = _editorOptionsFactoryService.GetOptions(view.TextBuffer)
        let normalOpts = Modes.Normal.DefaultOperations(operationsData) :> Vim.Modes.Normal.IOperations
        let commandOpts = Modes.Command.DefaultOperations(operationsData) :> Modes.Command.IOperations
        let commandProcessor = Modes.Command.CommandProcessor(buffer, commandOpts, statusUtil, FileSystem() :> IFileSystem) :> Modes.Command.ICommandProcessor
        let visualOptsFactory kind = 
            let kind = VisualKind.ofModeKind kind |> Option.get
            let tracker = Modes.Visual.SelectionTracker(view, vim.Settings, incrementalSearch, kind) :> Modes.Visual.ISelectionTracker
            (tracker, commonOperations)

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
                ((Modes.Normal.NormalMode(buffer, normalOpts, statusUtil,broker, createCommandRunner(),capture, _visualSpanCalculator)) :> IMode)
                ((Modes.Command.CommandMode(buffer, commandProcessor)) :> IMode)
                ((Modes.Insert.InsertMode(buffer, commonOperations, broker, editOptions,false)) :> IMode)
                ((Modes.Insert.InsertMode(buffer, commonOperations, broker, editOptions,true)) :> IMode)
                ((Modes.SubstituteConfirm.SubstituteConfirmMode(buffer, commonOperations) :> IMode))
                (DisabledMode(buffer) :> IMode)
                (ExternalEditMode(buffer) :> IMode)
            ] @ visualModeList
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        buffer.SwitchMode ModeKind.Normal ModeArgument.None |> ignore
        bufferRaw

    member private x.CreateTextStructureNavigator textBuffer wordKind =
        let baseImpl = _textStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer)
        TssUtil.CreateTextStructureNavigator wordKind baseImpl

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

    let _vimData = VimData() :> IVimData

    /// Holds the active stack of IVimBuffer instances
    let mutable _activeBufferStack : IVimBuffer list = List.empty

    /// Holds the local setting information which was stored when loading the VimRc file.  This 
    /// is applied to IVimBuffer instances which are created when there is no active IVimBuffer
    let mutable _vimrcLocalSettings : Setting list = List.empty

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

    member x.ActiveBuffer = ListUtil.tryHeadOnly _activeBufferStack

    member x.CreateBuffer view (localSettingList : Setting seq)= 
        if _bufferMap.ContainsKey(view) then invalidArg "view" Resources.Vim_ViewAlreadyHasBuffer
        let buffer = _bufferFactoryService.CreateBuffer (x:>IVim) view

        // Apply the specified local buffer settings
        localSettingList |> Seq.iter (fun s -> buffer.Settings.TrySetValue s.Name s.Value |> ignore)

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

    member x.GetOrCreateBuffer view =
        match x.GetBufferCore view with
        | Some(buffer) -> buffer
        | None -> 
            // Determine the settings which need to be applied to the new IVimBuffer
            let settings = 
                match x.ActiveBuffer with
                | Some(buffer) -> buffer.Settings.AllSettings |> Seq.filter (fun s -> not s.IsGlobal)
                | None -> _vimrcLocalSettings |> Seq.ofList
                
            x.CreateBuffer view settings

    member x.LoadVimRc (fileSystem:IFileSystem) (createViewFunc : (unit -> ITextView)) =
        _settings.VimRc <- System.String.Empty
        _settings.VimRcPaths <- fileSystem.GetVimRcDirectories() |> String.concat ";"

        match fileSystem.LoadVimRc() with
        | None -> false
        | Some(path,lines) ->
            _settings.VimRc <- path
            let view = createViewFunc()
            let buffer = x.CreateBuffer view Seq.empty
            let mode = buffer.CommandMode
            lines |> Seq.iter (fun input -> mode.RunCommand input |> ignore)
            _vimrcLocalSettings <- 
                buffer.Settings.AllSettings 
                |> Seq.filter (fun s -> not s.IsGlobal && not s.IsValueDefault)
                |> List.ofSeq
            view.Close()
            true

    interface IVim with
        member x.ActiveBuffer = x.ActiveBuffer
        member x.VimData = _vimData
        member x.VimHost = _host
        member x.VimRcLocalSettings = _vimrcLocalSettings
        member x.MarkMap = _markMap
        member x.KeyMap = _keyMap
        member x.ChangeTracker = _changeTracker
        member x.SearchService = _search
        member x.IsVimRcLoaded = not (System.String.IsNullOrEmpty(_settings.VimRc))
        member x.RegisterMap = _registerMap 
        member x.Settings = _settings
        member x.CreateBuffer view = x.CreateBuffer view _vimrcLocalSettings
        member x.GetOrCreateBuffer view = x.GetOrCreateBuffer view
        member x.RemoveBuffer view = x.RemoveBufferCore view
        member x.GetBuffer view = x.GetBufferCore view
        member x.GetBufferForBuffer textBuffer =
            let keys = _bufferMap.Keys |> Seq.filter (fun view -> view.TextBuffer = textBuffer)
            match keys |> Seq.isEmpty with
            | true -> None
            | false -> keys |> Seq.head |> x.GetBufferCore
        member x.LoadVimRc fileSystem createViewFunc = x.LoadVimRc fileSystem createViewFunc

