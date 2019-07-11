#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Classification
open System
open System.ComponentModel.Composition
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Runtime.Serialization
open System.Runtime.Serialization.Json
open Vim.Modes
open Vim.Interpreter

[<DataContract>]
type SessionRegisterValue = {
    [<field: DataMember(Name = "name")>] 
    Name: char

    [<field: DataMember(Name = "isCharacterWise")>]
    IsCharacterWise: bool

    [<field: DataMember(Name = "value")>]
    Value: string

    [<field: DataMember(Name = "isMacro")>]
    IsMacro: bool
}

[<DataContract>]
type SessionData = {
    [<field: DataMember(Name = "registers")>]
    Registers: SessionRegisterValue[]
} 
    with

    static member Empty = { Registers = [| |] }


[<Export(typeof<IBulkOperations>)>]
type internal BulkOperations  
    [<ImportingConstructor>]
    (
        _vimHost: IVimHost
    ) =

    /// The active count of bulk operations
    let mutable _bulkOperationCount = 0

    /// Called when a bulk operation is initiated in VsVim.
    member x.BeginBulkOperation () = 

        if _bulkOperationCount = 0 then
            _vimHost.BeginBulkOperation()

        _bulkOperationCount <- _bulkOperationCount + 1

        {
            new System.IDisposable with 
                member x.Dispose() = 
                    _bulkOperationCount <- _bulkOperationCount - 1
                    if _bulkOperationCount = 0 then
                        _vimHost.EndBulkOperation() }

    interface IBulkOperations with
        member x.InBulkOperation = _bulkOperationCount > 0
        member x.BeginBulkOperation () = x.BeginBulkOperation()

type internal VimData(_globalSettings: IVimGlobalSettings) as this =

    let mutable _autoCommandGroups: AutoCommandGroup list = List.Empty
    let mutable _autoCommands: AutoCommand list = List.Empty
    let mutable _currentDirectory = System.Environment.CurrentDirectory
    let mutable _previousCurrentDirecotry = _currentDirectory
    let mutable _lastLineCommand: LineCommand option = None
    let mutable _commandHistory = HistoryList()
    let mutable _fileHistory = HistoryList()
    let mutable _searchHistory = HistoryList()
    let mutable _lastSubstituteData: SubstituteData option = None
    let mutable _lastSearchData = SearchData("", SearchPath.Forward)
    let mutable _lastShellCommand: string option = None
    let mutable _lastTextInsert: string option = None
    let mutable _lastCharSearch: (CharSearchKind * SearchPath * char) option = None
    let mutable _lastMacroRun: char option = None
    let mutable _lastCommand: StoredCommand option = None
    let mutable _lastVisualSelection: StoredVisualSelection option = None
    let mutable _lastCommandLine = ""
    let mutable _displayPattern = ""
    let mutable _displayPatternSuspended = false
    let _displayPatternChanged = StandardEvent()

    do 
        // When the setting is changed it also resets the one time disabled flag
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        //
        // The lifetime of VimData is the same as IGlobalSettings and hence there is no need
        // to unsubsribe from this event.  Nor is there any real mechanism.  
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.IsEqual args.Setting.Name GlobalSettingNames.HighlightSearchName)
        |> Observable.add (fun _ -> 
            _displayPatternSuspended <- false
            this.CheckDisplayPattern())

        this.CheckDisplayPattern()

    member x.LastSearchData 
        with get() = _lastSearchData
        and set value = 
            _lastSearchData <- value
            _displayPatternSuspended <- false
            x.CheckDisplayPattern()

    member x.SuspendDisplayPattern() = 
        _displayPatternSuspended <- true
        x.CheckDisplayPattern()

    member x.ResumeDisplayPattern() = 
        _displayPatternSuspended <- false
        x.CheckDisplayPattern()

    /// Recalculate the current value of display pattern against the cached value of display pattern.  If
    /// it has changed then update the value and raised the changed event
    member x.CheckDisplayPattern() =
        let currentDisplayPattern = 
            if _displayPatternSuspended then
                ""
            elif not _globalSettings.HighlightSearch then
                ""
            else
                _lastSearchData.Pattern
        if currentDisplayPattern <> _displayPattern then
            _displayPattern <- currentDisplayPattern
            _displayPatternChanged.Trigger x

    interface IVimData with 
        member x.AutoCommandGroups
            with get() = _autoCommandGroups
            and set value = _autoCommandGroups <- value
        member x.AutoCommands
            with get() = _autoCommands
            and set value = _autoCommands <- value 
        member x.CurrentDirectory
            with get() = _currentDirectory
            and set value = 
                _previousCurrentDirecotry <- _currentDirectory
                _currentDirectory <- value
        member x.CommandHistory
            with get() = _commandHistory
            and set value = _commandHistory <- value
        member x.FileHistory
            with get() = _fileHistory
            and set value = _fileHistory <- value
        member x.DisplayPattern = _displayPattern
        member x.SearchHistory 
            with get() = _searchHistory
            and set value = _searchHistory <- value
        member x.LastSubstituteData 
            with get() = _lastSubstituteData
            and set value = _lastSubstituteData <- value
        member x.LastLineCommand 
            with get() = _lastLineCommand
            and set value = _lastLineCommand <- value
        member x.LastCommand 
            with get() = _lastCommand
            and set value = _lastCommand <- value
        member x.LastCommandLine
            with get() = _lastCommandLine
            and set value = _lastCommandLine <- value
        member x.LastShellCommand
            with get() = _lastShellCommand
            and set value = _lastShellCommand <- value
        member x.LastSearchData 
            with get() = x.LastSearchData
            and set value = x.LastSearchData <- value
        member x.PreviousCurrentDirectory = _previousCurrentDirecotry
        member x.LastCharSearch 
            with get() = _lastCharSearch
            and set value = _lastCharSearch <- value
        member x.LastMacroRun 
            with get() = _lastMacroRun
            and set value = _lastMacroRun <- value
        member x.LastTextInsert
            with get() = _lastTextInsert
            and set value = _lastTextInsert <- value
        member x.LastVisualSelection 
            with get() = _lastVisualSelection
            and set value = _lastVisualSelection <- value
        member x.SuspendDisplayPattern() = x.SuspendDisplayPattern()
        member x.ResumeDisplayPattern() = x.ResumeDisplayPattern()
        [<CLIEvent>]
        member x.DisplayPatternChanged = _displayPatternChanged.Publish

[<Export(typeof<IVimBufferFactory>)>]
type internal VimBufferFactory

    [<ImportingConstructor>]
    (
        _host: IVimHost,
        _editorOperationsFactoryService: IEditorOperationsFactoryService,
        _editorOptionsFactoryService: IEditorOptionsFactoryService,
        _outliningManagerService: IOutliningManagerService,
        _completionWindowBrokerFactoryService: IDisplayWindowBrokerFactoryService,
        _commonOperationsFactory: ICommonOperationsFactory,
        _lineChangeTrackerFactory: ILineChangeTrackerFactory,
        _textSearchService: ITextSearchService,
        _bufferTrackingService: IBufferTrackingService,
        _undoManagerProvider: ITextBufferUndoManagerProvider,
        _statusUtilFactory: IStatusUtilFactory,
        _foldManagerFactory: IFoldManagerFactory,
        _keyboardDevice: IKeyboardDevice,
        _mouseDevice: IMouseDevice,
        _wordCompletionSessionFactoryService: IWordCompletionSessionFactoryService,
        _bulkOperations: IBulkOperations
    ) =

    /// Create an IVimTextBuffer instance for the provided ITextBuffer
    member x.CreateVimTextBuffer (textBuffer: ITextBuffer) (vim: IVim) = 
        let localSettings = LocalSettings(vim.GlobalSettings) :> IVimLocalSettings
        let wordUtil = WordUtil(textBuffer, localSettings)
        let statusUtil = _statusUtilFactory.GetStatusUtilForBuffer textBuffer
        let undoRedoOperations = 
            let history = 
                let manager = _undoManagerProvider.GetTextBufferUndoManager textBuffer
                if manager = null then None
                else manager.TextBufferUndoHistory |> Some
            UndoRedoOperations(_host, statusUtil, history, _editorOperationsFactoryService) :> IUndoRedoOperations

        VimTextBuffer(textBuffer, localSettings, _bufferTrackingService, undoRedoOperations, wordUtil, vim)

    /// Create a VimBufferData instance for the given ITextView and IVimTextBuffer.  This is mainly
    /// used for testing purposes
    member x.CreateVimBufferData (vimTextBuffer: IVimTextBuffer) (textView: ITextView) =
        Contract.Requires (vimTextBuffer.TextBuffer = textView.TextBuffer)

        let vim = vimTextBuffer.Vim
        let textBuffer = textView.TextBuffer
        let statusUtil = _statusUtilFactory.GetStatusUtilForView textView
        let localSettings = vimTextBuffer.LocalSettings
        let jumpList = JumpList(textView, _bufferTrackingService) :> IJumpList
        let windowSettings = WindowSettings(vim.GlobalSettings, textView)
        VimBufferData(vimTextBuffer, textView, windowSettings, jumpList, statusUtil) :> IVimBufferData

    /// Create an IVimBuffer instance for the provided VimBufferData
    member x.CreateVimBuffer (vimBufferData: IVimBufferData) = 
        let textView = vimBufferData.TextView
        let commonOperations = _commonOperationsFactory.GetCommonOperations vimBufferData
        let incrementalSearch = IncrementalSearch(vimBufferData, commonOperations) :> IIncrementalSearch
        let capture = MotionCapture(vimBufferData, incrementalSearch) :> IMotionCapture

        let textChangeTracker = Modes.Insert.TextChangeTracker.GetTextChangeTracker vimBufferData _commonOperationsFactory
        let lineChangeTracker = _lineChangeTrackerFactory.GetLineChangeTracker vimBufferData
        let motionUtil = MotionUtil(vimBufferData, commonOperations) :> IMotionUtil
        let foldManager = _foldManagerFactory.GetFoldManager textView
        let insertUtil = InsertUtil(vimBufferData, motionUtil, commonOperations) :> IInsertUtil
        let commandUtil = CommandUtil(vimBufferData, motionUtil, commonOperations, foldManager, insertUtil, _bulkOperations, lineChangeTracker) :> ICommandUtil

        let bufferRaw = VimBuffer(vimBufferData, incrementalSearch, motionUtil, vimBufferData.VimTextBuffer.WordNavigator, vimBufferData.WindowSettings, commandUtil)
        let buffer = bufferRaw :> IVimBuffer

        let vim = vimBufferData.Vim
        let createCommandRunner kind countKeyRemapMode = CommandRunner(vimBufferData, capture, commandUtil, kind, countKeyRemapMode) :>ICommandRunner
        let broker = _completionWindowBrokerFactoryService.GetDisplayWindowBroker textView
        let bufferOptions = _editorOptionsFactoryService.GetOptions(textView.TextBuffer)
        let visualOptsFactory visualKind = Modes.Visual.SelectionTracker(vimBufferData, incrementalSearch, visualKind) :> Modes.Visual.ISelectionTracker
        let undoRedoOperations = vimBufferData.UndoRedoOperations

        let visualModeSeq =
            VisualKind.All
            |> Seq.map (fun visualKind -> 
                let tracker = visualOptsFactory visualKind
                ((Modes.Visual.VisualMode(vimBufferData, commonOperations, motionUtil, visualKind, createCommandRunner visualKind KeyRemapMode.Visual, capture, tracker)) :> IMode) )

        let selectModeSeq = 
            VisualKind.All
            |> Seq.map (fun visualKind ->
                let tracker = visualOptsFactory visualKind
                let runner = createCommandRunner visualKind KeyRemapMode.Select
                Modes.Visual.SelectMode(vimBufferData, commonOperations, motionUtil, visualKind, runner, capture, undoRedoOperations, tracker) :> IMode)
            |> List.ofSeq

        let visualModeList =
            visualModeSeq
            |> Seq.append selectModeSeq
            |> List.ofSeq

        // Normal mode values
        let editOptions = _editorOptionsFactoryService.GetOptions(textView)
        let modeList = 
            [
                ((Modes.Normal.NormalMode(vimBufferData, commonOperations, motionUtil, createCommandRunner VisualKind.Character KeyRemapMode.Normal, capture, incrementalSearch)) :> IMode)
                ((Modes.Command.CommandMode(buffer, commonOperations)) :> IMode)
                ((Modes.Insert.InsertMode(buffer, commonOperations, broker, editOptions, undoRedoOperations, textChangeTracker :> Modes.Insert.ITextChangeTracker, insertUtil, motionUtil, commandUtil, capture, false, _keyboardDevice, _mouseDevice, _wordCompletionSessionFactoryService)) :> IMode)
                ((Modes.Insert.InsertMode(buffer, commonOperations, broker, editOptions, undoRedoOperations, textChangeTracker, insertUtil, motionUtil, commandUtil, capture, true, _keyboardDevice, _mouseDevice, _wordCompletionSessionFactoryService)) :> IMode)
                ((Modes.SubstituteConfirm.SubstituteConfirmMode(vimBufferData, commonOperations) :> IMode))
                (DisabledMode(vimBufferData) :> IMode)
                (ExternalEditMode(vimBufferData) :> IMode)
            ] @ visualModeList
        modeList |> List.iter (fun m -> bufferRaw.AddMode m)
        x.SetupInitialMode buffer
        _statusUtilFactory.InitializeVimBuffer (bufferRaw :> IVimBufferInternal)
        bufferRaw

    /// Setup the initial mode for an IVimBuffer
    member x.SetupInitialMode (vimBuffer: IVimBuffer) =
        let vimBufferData = vimBuffer.VimBufferData
        let commonOperations = _commonOperationsFactory.GetCommonOperations(vimBufferData)

        // The ITextView is initialized and no one has forced the IVimBuffer out of
        // the uninitialized state.  Do the switch now to the correct mode
        let setupInitialMode () =
            if vimBuffer.ModeKind = ModeKind.Uninitialized then
                vimBuffer.SwitchMode vimBufferData.VimTextBuffer.ModeKind ModeArgument.None
                |> ignore

        // The mode should be the current mode of the underlying
        // IVimTextBuffer.  This should be as easy as switching the mode on
        // startup  but this is complicated by the initialization of ITextView
        // instances.  They can, and  often are, passed to CreateVimBuffer in
        // an uninitialized state.  In that state certain operations like
        // Select can't be done.  Hence we have to delay the mode switch until
        // the ITextView is fully initialized.
        commonOperations.DoActionWhenReady setupInitialMode

    interface IVimBufferFactory with
        member x.CreateVimTextBuffer textBuffer vim = x.CreateVimTextBuffer textBuffer vim :> IVimTextBuffer
        member x.CreateVimBufferData vimTextBuffer textView = x.CreateVimBufferData vimTextBuffer textView 
        member x.CreateVimBuffer vimBufferData = x.CreateVimBuffer vimBufferData :> IVimBuffer

/// Default implementation of IVim 
[<Export(typeof<IVim>)>]
type internal Vim
    (
        _vimHost: IVimHost,
        _bufferFactoryService: IVimBufferFactory,
        _interpreterFactory: IVimInterpreterFactory,
        _bufferCreationListeners: Lazy<IVimBufferCreationListener> list,
        _globalSettings: IVimGlobalSettings,
        _markMap: IMarkMap,
        _keyMap: IKeyMap,
        _clipboardDevice: IClipboardDevice,
        _search: ISearchService,
        _fileSystem: IFileSystem,
        _vimData: IVimData,
        _bulkOperations: IBulkOperations,
        _variableMap: VariableMap,
        _editorToSettingSynchronizer: IEditorToSettingsSynchronizer,
        _statusUtilFactory: IStatusUtilFactory,
        _commonOperationsFactory: ICommonOperationsFactory,
        _mouseDevice: IMouseDevice
    ) as this =

    /// This key is placed in the ITextView property bag to note that vim buffer creation has
    /// been suppressed for that specific instance 
    static let ShouldCreateVimBufferKey = obj()

    /// Key for IVimTextBuffer instances inside of the ITextBuffer property bag
    let _vimTextBufferKey = obj()

    /// Holds an IVimBuffer and the DisposableBag for event handlers on the IVimBuffer.  This
    /// needs to be removed when we're done with the IVimBuffer to avoid leaks
    let _vimBufferMap = Dictionary<ITextView, IVimBuffer * IVimInterpreter * DisposableBag>()

    let _digraphMap = DigraphMap() :> IDigraphMap

    /// Holds the active stack of IVimBuffer instances
    let mutable _activeBufferStack: IVimBuffer list = List.empty

    /// Holds the recent stack of IVimBuffer instances
    let mutable _recentBufferStack: IVimBuffer list = List.empty

    /// Whether or not the vimrc file should be automatically loaded before creating the 
    /// first IVimBuffer instance
    let mutable _autoLoadDigraphs = true
    let mutable _autoLoadVimRc = true
    let mutable _autoLoadSessionData = true
    let mutable _digraphsAutoLoaded = false
    let mutable _sessionDataAutoLoaded = false
    let mutable _isLoadingVimRc = false
    let mutable _vimRcState = VimRcState.None

    /// Holds the setting information which was stored when loading the VimRc file.  This 
    /// is applied to IVimBuffer instances which are created when there is no active IVimBuffer
    let mutable _vimRcLocalSettings = LocalSettings(_globalSettings) :> IVimLocalSettings
    let mutable _vimRcWindowSettings = WindowSettings(_globalSettings) :> IVimWindowSettings

    /// Whether or not Vim is currently in disabled mode
    let mutable _isDisabled = false

    let mutable _fileSystem = _fileSystem

    let _registerMap =
        let currentFileNameFunc() = 
            match _activeBufferStack with
            | [] -> None
            | h::_ -> h.VimBufferData.CurrentRelativeFilePath
        RegisterMap(_vimData, _clipboardDevice, currentFileNameFunc) :> IRegisterMap

    let _recorder = MacroRecorder(_registerMap)

    /// Add the IMacroRecorder to the list of IVimBufferCreationListeners.  
    let _bufferCreationListeners =
        let item = Lazy<IVimBufferCreationListener>(fun () -> _recorder :> IVimBufferCreationListener)
        item :: _bufferCreationListeners

    do
        // When the 'history' setting is changed it impacts our history limits.  Keep track of 
        // them here
        //
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type

        (_globalSettings :> IVimSettings).SettingChanged 
        |> Event.filter (fun args -> StringUtil.IsEqual args.Setting.Name GlobalSettingNames.HistoryName)
        |> Event.add (fun _ -> 
            _vimData.SearchHistory.Limit <- _globalSettings.History
            _vimData.CommandHistory.Limit <- _globalSettings.History)

        // Notify the IVimHost that IVim is fully created.  
        _vimHost.VimCreated this

    [<ImportingConstructor>]
    new(
        host: IVimHost,
        bufferFactoryService: IVimBufferFactory,
        interpreterFactory: IVimInterpreterFactory,
        bufferTrackingService: IBufferTrackingService,
        [<ImportMany>] bufferCreationListeners: Lazy<IVimBufferCreationListener> seq,
        search: ITextSearchService,
        fileSystem: IFileSystem,
        clipboard: IClipboardDevice,
        bulkOperations: IBulkOperations,
        editorToSettingSynchronizer: IEditorToSettingsSynchronizer,
        statusUtilFactory: IStatusUtilFactory,
        commonOperationsFactory: ICommonOperationsFactory,
        mouseDevice: IMouseDevice) =
        let markMap = MarkMap(bufferTrackingService)
        let globalSettings = GlobalSettings() :> IVimGlobalSettings
        let vimData = VimData(globalSettings) :> IVimData
        let variableMap = VariableMap()
        let listeners = bufferCreationListeners |> List.ofSeq
        Vim(
            host,
            bufferFactoryService,
            interpreterFactory,
            listeners,
            globalSettings,
            markMap :> IMarkMap,
            KeyMap(globalSettings, variableMap) :> IKeyMap,
            clipboard,
            SearchService(search, globalSettings) :> ISearchService,
            fileSystem,
            vimData,
            bulkOperations,
            variableMap,
            editorToSettingSynchronizer,
            statusUtilFactory,
            commonOperationsFactory,
            mouseDevice)

    member x.ActiveBuffer = ListUtil.tryHeadOnly _activeBufferStack

    member x.ActiveStatusUtil =
        match x.ActiveBuffer with
        | Some vimBuffer -> vimBuffer.VimBufferData.StatusUtil
        | None -> _statusUtilFactory.EmptyStatusUtil

    member x.AutoLoadDigraphs
        with get() = _autoLoadDigraphs
        and set value = _autoLoadDigraphs <- value

    member x.AutoLoadVimRc 
        with get() = _autoLoadVimRc
        and set value = _autoLoadVimRc <- value

    member x.AutoLoadSessionData
        with get() = _autoLoadSessionData
        and set value = _autoLoadSessionData <- value

    member x.FileSystem
        with get() = _fileSystem
        and set value = _fileSystem <- value 

    member x.IsDisabled 
        with get() = _isDisabled
        and set value = 
            let changed = value <> _isDisabled
            _isDisabled <- value
            if changed then
                x.UpdatedDisabledMode()

    member x.VariableMap = _variableMap

    member x.VimBuffers = _vimBufferMap.Values |> Seq.map (fun (vimBuffer, _, _) -> vimBuffer) |> List.ofSeq

    member x.VimRcState 
        with get() = _vimRcState
        and set value = _vimRcState <- value

    member x.FocusedBuffer = 
        match _vimHost.GetFocusedTextView() with
        | None -> 
            None
        | Some textView -> 
            
            let found, tuple = _vimBufferMap.TryGetValue(textView)
            if found then 
                let (vimBuffer, _, _) = tuple
                Some vimBuffer
            else 
                None

    /// Get the IVimLocalSettings which should be the basis for a newly created IVimTextBuffer
    member x.GetLocalSettingsForNewTextBuffer () =
        x.MaybeLoadFiles()
        match x.ActiveBuffer with
        | Some buffer -> buffer.LocalSettings
        | None -> _vimRcLocalSettings

    /// Get the IVimWindowSettings which should be the basis for a newly created IVimBuffer
    member x.GetWindowSettingsForNewBuffer () =
        x.MaybeLoadFiles()
        match x.ActiveBuffer with
        | Some buffer -> buffer.WindowSettings
        | None -> _vimRcWindowSettings

    /// Close all IVimBuffer instances
    member x.CloseAllVimBuffers() =
        x.VimBuffers
        |> List.iter (fun vimBuffer -> if not vimBuffer.IsClosed then vimBuffer.Close())

    /// Create an IVimTextBuffer for the given ITextBuffer.  If an IVimLocalSettings instance is 
    /// provided then attempt to copy them into the created IVimTextBuffer copy of the 
    /// IVimLocalSettings
    member x.CreateVimTextBuffer (textBuffer: ITextBuffer) (localSettings: IVimLocalSettings option) = 
        if textBuffer.Properties.ContainsProperty _vimTextBufferKey then
            invalidArg "textBuffer" Resources.Vim_TextViewAlreadyHasVimBuffer

        let vimTextBuffer = _bufferFactoryService.CreateVimTextBuffer textBuffer x

        // Apply the specified local buffer settings
        match localSettings with
        | None -> ()
        | Some localSettings ->
            localSettings.Settings
            |> Seq.filter (fun s -> not s.IsValueCalculated)
            |> Seq.iter (fun s -> vimTextBuffer.LocalSettings.TrySetValue s.Name s.Value |> ignore)

        // Put the IVimTextBuffer into the ITextBuffer property bag so we can query for it in the future
        textBuffer.Properties.[_vimTextBufferKey] <- vimTextBuffer

        // If we are currently disabled then the new IVimTextBuffer instance should be disabled
        // as well
        if _isDisabled then
            vimTextBuffer.SwitchMode ModeKind.Disabled ModeArgument.None

        vimTextBuffer

    /// Create an IVimBuffer for the given ITextView and associated IVimTextBuffer.  This 
    /// will not notify the IVimBufferCreationListener collection about the new
    /// IVimBuffer
    member x.CreateVimBufferCore textView (windowSettings: IVimWindowSettings option) =
        if _vimBufferMap.ContainsKey(textView) then 
            invalidArg "textView" Resources.Vim_TextViewAlreadyHasVimBuffer

        let vimTextBuffer = x.GetOrCreateVimTextBuffer textView.TextBuffer
        let vimBufferData = _bufferFactoryService.CreateVimBufferData vimTextBuffer textView
        let vimBuffer = _bufferFactoryService.CreateVimBuffer vimBufferData

        // Apply the specified window settings
        match windowSettings with
        | None -> ()
        | Some windowSettings ->
            windowSettings.Settings
            |> Seq.filter (fun s -> not s.IsValueCalculated)
            |> Seq.iter (fun s -> vimBuffer.WindowSettings.TrySetValue s.Name s.Value |> ignore)

        // Setup the handlers for KeyInputStart and KeyInputEnd to accurately track the active
        // IVimBuffer instance
        let eventBag = DisposableBag()
        vimBuffer.KeyInputStart
        |> Observable.subscribe (fun _ -> _activeBufferStack <- vimBuffer :: _activeBufferStack )
        |> eventBag.Add

        vimBuffer.KeyInputEnd 
        |> Observable.subscribe (fun _ -> 
            _activeBufferStack <- 
                match _activeBufferStack with
                | h::t -> t
                | [] -> [] )
        |> eventBag.Add

        // Subscribe to text view focus events.
        vimBuffer.TextView.GotAggregateFocus
        |> Observable.subscribe (fun _ -> x.OnFocus vimBuffer)
        |> eventBag.Add

        let vimInterpreter = _interpreterFactory.CreateVimInterpreter vimBuffer _fileSystem
        _vimBufferMap.Add(textView, (vimBuffer, vimInterpreter, eventBag))

        if _vimHost.AutoSynchronizeSettings then
            _editorToSettingSynchronizer.StartSynchronizing vimBuffer SettingSyncSource.Vim

        vimBuffer

    member x.RemoveRecentBuffer vimBuffer =
        _recentBufferStack <-
            _recentBufferStack
            |> Seq.filter (fun item -> item <> vimBuffer)
            |> List.ofSeq

    member x.OnFocus vimBuffer =
        x.RemoveRecentBuffer vimBuffer
        _recentBufferStack <- vimBuffer :: _recentBufferStack
        let name = _vimHost.GetName vimBuffer.TextBuffer
        _vimData.FileHistory.Add name

    /// Create an IVimBuffer for the given ITextView and associated IVimTextBuffer and notify
    /// the IVimBufferCreationListener collection about it
    member x.CreateVimBuffer textView windowSettings = 
        let vimBuffer = x.CreateVimBufferCore textView windowSettings
        _bufferCreationListeners |> Seq.iter (fun x -> x.Value.VimBufferCreated vimBuffer)
        vimBuffer

    member x.GetVimInterpreter (vimBuffer: IVimBuffer) = 
        let tuple = _vimBufferMap.TryGetValue vimBuffer.TextView
        match tuple with 
        | (true, (_, vimInterpreter, _)) -> vimInterpreter
        | (false, _) -> _interpreterFactory.CreateVimInterpreter vimBuffer _fileSystem

    member x.GetOrCreateVimTextBuffer textBuffer =
        let success, vimTextBuffer = x.TryGetVimTextBuffer textBuffer
        if success then
            vimTextBuffer
        else
            let settings = x.GetLocalSettingsForNewTextBuffer()
            x.CreateVimTextBuffer textBuffer (Some settings)

    member x.GetOrCreateVimBuffer textView =
        let success, vimBuffer = x.TryGetVimBuffer textView
        if success then
            vimBuffer
        else
            let settings = x.GetWindowSettingsForNewBuffer()
            x.CreateVimBuffer textView (Some settings)

    member x.MaybeLoadFiles() =

        if x.AutoLoadDigraphs && not _digraphsAutoLoaded then
            DigraphUtil.AddToMap _digraphMap DigraphUtil.DefaultDigraphs
            _digraphsAutoLoaded <- true

        // Load registers before loading the vimrc so that
        // registers that are set in the vimrc "stick".
        if x.AutoLoadSessionData && not _sessionDataAutoLoaded then
            x.LoadSessionData()
            _sessionDataAutoLoaded <- true

        if x.AutoLoadVimRc then
            match _vimRcState with
            | VimRcState.None -> x.LoadVimRc() |> ignore
            | VimRcState.LoadSucceeded _ -> ()
            | VimRcState.LoadFailed -> ()

    member x.LoadVimRcCore() =
        Contract.Assert(_isLoadingVimRc)
        _globalSettings.VimRc <- System.String.Empty
        _globalSettings.VimRcPaths <- _fileSystem.GetVimRcDirectories() |> String.concat ";"

        match x.LoadVimRcFileContents() with
        | None -> 
            _vimRcLocalSettings <- LocalSettings(_globalSettings) 
            _vimRcWindowSettings <- WindowSettings(_globalSettings)
            _vimRcState <- VimRcState.LoadFailed
            x.LoadDefaultSettings()

        | Some (vimRcPath, lines) ->
            _globalSettings.VimRc <- vimRcPath.FilePath
            let bag = new DisposableBag()
            let errorList = List<string>()
            let textView = _vimHost.CreateHiddenTextView()
            let mutable createdVimBuffer: IVimBuffer option = None

            try
                // For the vimrc IVimBuffer we go straight to the factory methods.  We don't want
                // to notify any consumers that this IVimBuffer is ever created.  It should be 
                // transparent to them and showing it just causes confusion.  
                let vimTextBuffer = _bufferFactoryService.CreateVimTextBuffer textView.TextBuffer x
                let vimBufferData = _bufferFactoryService.CreateVimBufferData vimTextBuffer textView
                let vimBuffer = _bufferFactoryService.CreateVimBuffer vimBufferData
                createdVimBuffer <- Some vimBuffer

                vimBuffer.ErrorMessage
                |> Observable.subscribe (fun e -> errorList.Add(e.Message))
                |> bag.Add

                // Actually parse and run all of the commands
                let vimInterpreter = x.GetVimInterpreter vimBuffer
                vimInterpreter.RunScript(lines)

                _vimRcLocalSettings <- LocalSettings.Copy vimBuffer.LocalSettings
                _vimRcWindowSettings <- WindowSettings.Copy vimBuffer.WindowSettings
                _vimRcState <- VimRcState.LoadSucceeded (vimRcPath, errorList.ToArray())

                if errorList.Count <> 0 then
                    VimTrace.TraceError("Errors loading rc file: {0}", vimRcPath.FilePath)
                    VimTrace.TraceError(String.Join(Environment.NewLine, errorList))
            finally
                // Remove the event handlers
                bag.DisposeAll()

                // Be careful not to leak the ITextView in the case of an exception
                textView.Close()

                // In general it is the responsibility of the host to close IVimBuffer instances when
                // the corresponding ITextView is closed.  In this particular case though we don't actually 
                // inform the host it is created so make sure it gets closed here 
                match createdVimBuffer with
                | Some vimBuffer -> 
                    if not vimBuffer.IsClosed then
                        vimBuffer.Close()
                | None -> ()

    member x.LoadVimRc() =
        if _isLoadingVimRc then
            // Recursive load detected.  Bail out 
            VimRcState.LoadFailed
        else
            _isLoadingVimRc <- true

            try
                x.LoadVimRcCore()
            finally
                _isLoadingVimRc <- false

            _globalSettings.VimRcLocalSettings <- Some _vimRcLocalSettings
            _globalSettings.VimRcWindowSettings <- Some _vimRcWindowSettings

            _vimHost.VimRcLoaded _vimRcState _vimRcLocalSettings _vimRcWindowSettings

            _vimRcState

    /// This actually loads the lines of the vimrc that we should be using 
    member x.LoadVimRcFileContents() = 
        _fileSystem.GetVimRcFilePaths()
        |> Seq.tryPick (fun vimRcPath -> 
            if not (_vimHost.ShouldIncludeRcFile vimRcPath) then
                None
            else
                VimTrace.TraceInfo("Trying rc file: {0}", vimRcPath.FilePath)
                _fileSystem.ReadAllLines vimRcPath.FilePath
                |> Option.map (fun lines -> (vimRcPath, lines)))

    /// Called when there is no vimrc file.  Update IVimGlobalSettings to be the appropriate
    /// value for what the host requests
    member x.LoadDefaultSettings(): unit = 
        match _vimHost.DefaultSettings with
        | DefaultSettings.GVim73 ->
            // Strictly speaking this is not the default for 7.3.  However given this is running
            // on windows there is little sense in disallowing backspace in insert mode as the
            // out of the box experience.  If users truly want that they can put in a vimrc
            // file that adds it in 
            _globalSettings.Backspace <- "indent,eol,start"
        | DefaultSettings.GVim74 ->
            // User-friendly overrides for users without an rc file.
            // Compare with Vim 7.4 "C:\Program Files (x86)\Vim\_vimrc"
            _globalSettings.SelectMode <- "mouse,key"
            _globalSettings.MouseModel <- "popup"
            _globalSettings.KeyModel <- "startsel,stopsel"
            _globalSettings.Selection <- "exclusive"
            _globalSettings.Backspace <- "indent,eol,start"
            _globalSettings.WhichWrap <- "<,>,[,]"
        | _ -> 
            _globalSettings.Backspace <- "indent,eol,start"

    member x.GetSessionDataDirectory() =
        let filePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Path.Combine(filePath, "VsVim")

    member x.GetSessionDataFilePath() =
        Path.Combine(x.GetSessionDataDirectory(), "vimdata.json")

    member x.ReadSessionData filePath =

        // Note: Older session data will not have the 'isMacro' field.
        // If 'isMacro' is missing, then the value is a string,
        // which is backward compatible.
        let filePath=  x.GetSessionDataFilePath()
        match _fileSystem.Read filePath with
        | None -> SessionData.Empty
        | Some stream -> 
            try
                use stream = stream
                let serializer = new DataContractJsonSerializer(typeof<SessionData>)
                serializer.ReadObject(stream) :?> SessionData
            with
                _ -> SessionData.Empty

    member x.WriteSessionData (sessionData: SessionData) filePath = 
        let serializer = new DataContractJsonSerializer(typeof<SessionData>)
        use stream = new MemoryStream()
        try
            serializer.WriteObject(stream, sessionData)
            stream.Position <- 0L
            _fileSystem.Write filePath stream |> ignore
        with
            _ as ex -> VimTrace.TraceError ex

    member x.LoadSessionDataCore filePath =
        let sessionData = x.ReadSessionData filePath
        let registers = if sessionData.Registers = null then [| |] else sessionData.Registers
        for sessionReg in registers do
            match sessionReg.Name |> RegisterName.OfChar with
            | Some name -> 
                let kind =
                    if sessionReg.IsCharacterWise then
                        OperationKind.CharacterWise
                    else
                        OperationKind.LineWise
                let registerValue =
                    if sessionReg.IsMacro then
                        match KeyNotationUtil.TryStringToKeyInputSet(sessionReg.Value) with
                        | Some keyInputSet -> RegisterValue(keyInputSet.KeyInputs)
                        | None -> RegisterValue(sessionReg.Value, kind)
                    else
                        RegisterValue(sessionReg.Value, kind)
                _registerMap.SetRegisterValue name registerValue
            | None -> ()

    member x.LoadSessionData() =

        // Make sure the VsVim package is loaded so that session data
        // will be saved on exit (issues #2087 and #1726).
        _vimHost.EnsurePackageLoaded()

        x.LoadSessionDataCore (x.GetSessionDataFilePath())

    member x.SaveSessionDataCore filePath = 
        let sessionRegisterArray = 
            NamedRegister.All
            |> Seq.filter (fun n -> not n.IsAppend)
            |> Seq.map (fun n -> 
                let registerName = RegisterName.Named n
                let register = _registerMap.GetRegister registerName
                let registerValue = register.RegisterValue
                let isMacro = not registerValue.IsString
                let isCharacterWise = registerValue.OperationKind = OperationKind.CharacterWise
                let value =
                    if isMacro then
                        registerValue.KeyInputs
                        |> KeyInputSetUtil.OfList
                        |> KeyNotationUtil.KeyInputSetToString
                    else
                        registerValue.StringValue
                { Name = n.Char; IsCharacterWise = isCharacterWise; Value = value; IsMacro = isMacro; })
            |> Seq.toArray
        let sessionData = { Registers = sessionRegisterArray }
        x.WriteSessionData sessionData filePath

    member x.SaveSessionData() =
        _fileSystem.CreateDirectory (x.GetSessionDataDirectory()) |> ignore
        x.SaveSessionDataCore (x.GetSessionDataFilePath())

    member x.RemoveVimBuffer textView = 
        let found, tuple = _vimBufferMap.TryGetValue(textView)
        if found then 
            let vimBuffer,  _ , bag = tuple
            x.RemoveRecentBuffer vimBuffer
            bag.DisposeAll()
        _vimBufferMap.Remove textView

    member x.TryGetVimBuffer(textView: ITextView, [<Out>] vimBuffer: IVimBuffer byref) =
        let tuple = _vimBufferMap.TryGetValue textView
        match tuple with 
        | (true, (buffer, _, _)) -> 
            vimBuffer <- buffer
            true
        | (false, _) -> 
            false

    /// Determine if an IVimBuffer instance should be created for a given ITextView.  If the
    /// decision to not create an IVimBuffer then this decision is persisted for the lifetime
    /// of the ITextView.  
    ///
    /// Allowing it to change in the middle would mean only a portion of the services around
    /// and IVimBuffer were created and hence it would just appear buggy to the user.  
    ///
    /// Also we want to reduce the number of times we ask the host this question.  The determination
    /// could be expensive and there is no need to do it over and over again.  Scenarios like
    /// the command window end up forcing this code path a large number of times.  
    member x.ShouldCreateVimBuffer (textView: ITextView) = 
        match PropertyCollectionUtil.GetValue<bool> ShouldCreateVimBufferKey textView.Properties with 
        | Some value -> value
        | None -> 
            let value =  _vimHost.ShouldCreateVimBuffer textView 
            textView.Properties.AddProperty(ShouldCreateVimBufferKey, (box value))
            value

    member x.TryGetOrCreateVimBufferForHost(textView: ITextView, [<Out>] vimBuffer: IVimBuffer byref) =
        if x.TryGetVimBuffer(textView, &vimBuffer) then
            true
        elif x.ShouldCreateVimBuffer textView then
            let settings = x.GetWindowSettingsForNewBuffer()
            vimBuffer <- x.CreateVimBuffer textView (Some settings) 
            true
        else
            false

    member x.TryGetVimTextBuffer (textBuffer: ITextBuffer, [<Out>] vimTextBuffer: IVimTextBuffer byref) =
        match PropertyCollectionUtil.GetValue<IVimTextBuffer> _vimTextBufferKey textBuffer.Properties with
        | Some found ->
            vimTextBuffer <- found
            true
        | None ->
            false

    /// Get the nth most recent vim buffer
    member x.TryGetRecentBuffer (n: int) =
        if n >= _recentBufferStack.Length then
            None
        else
            _recentBufferStack |> Seq.skip n |> Seq.head |> Some

    member x.DisableVimBuffer (vimBuffer: IVimBuffer) =
        vimBuffer.SwitchMode ModeKind.Disabled ModeArgument.None |> ignore

    member x.EnableVimBuffer (vimBuffer: IVimBuffer) =
        let modeKind =
            if vimBuffer.TextView.Selection.IsEmpty then
                ModeKind.Normal
            elif Util.IsFlagSet _globalSettings.SelectModeOptions SelectModeOptions.Mouse then
                ModeKind.SelectCharacter
            else
                ModeKind.VisualCharacter
        vimBuffer.SwitchMode modeKind ModeArgument.None |> ignore

    /// Toggle disabled mode for all active IVimBuffer instances to sync up with the current
    /// state of _isDisabled
    member x.UpdatedDisabledMode() = 
        if _isDisabled then
            x.VimBuffers
            |> Seq.filter (fun vimBuffer -> vimBuffer.Mode.ModeKind <> ModeKind.Disabled)
            |> Seq.iter x.DisableVimBuffer

        else
            x.VimBuffers
            |> Seq.filter (fun vimBuffer -> vimBuffer.Mode.ModeKind = ModeKind.Disabled)
            |> Seq.iter x.EnableVimBuffer

    interface IVim with
        member x.ActiveBuffer = x.ActiveBuffer
        member x.ActiveStatusUtil = x.ActiveStatusUtil
        member x.AutoLoadDigraphs 
            with get() = x.AutoLoadDigraphs
            and set value = x.AutoLoadDigraphs <- value
        member x.AutoLoadVimRc 
            with get() = x.AutoLoadVimRc
            and set value = x.AutoLoadVimRc <- value
        member x.AutoLoadSessionData 
            with get() = x.AutoLoadSessionData 
            and set value = x.AutoLoadSessionData <- value
        member x.FocusedBuffer = x.FocusedBuffer
        member x.VariableMap = x.VariableMap
        member x.VimBuffers = x.VimBuffers
        member x.VimData = _vimData
        member x.VimHost = _vimHost
        member x.VimRcState = _vimRcState
        member x.MacroRecorder = _recorder :> IMacroRecorder
        member x.MarkMap = _markMap
        member x.KeyMap = _keyMap
        member x.DigraphMap = _digraphMap
        member x.SearchService = _search
        member x.IsDisabled
            with get() = x.IsDisabled
            and set value = x.IsDisabled <- value
        member x.InBulkOperation = _bulkOperations.InBulkOperation
        member x.RegisterMap = _registerMap 
        member x.GlobalSettings = _globalSettings
        member x.CloseAllVimBuffers() = x.CloseAllVimBuffers()
        member x.CreateVimBuffer textView = x.CreateVimBuffer textView (Some (x.GetWindowSettingsForNewBuffer()))
        member x.CreateVimTextBuffer textBuffer = x.CreateVimTextBuffer textBuffer (Some (x.GetLocalSettingsForNewTextBuffer()))
        member x.GetVimInterpreter vimBuffer = x.GetVimInterpreter vimBuffer
        member x.GetOrCreateVimBuffer textView = x.GetOrCreateVimBuffer textView
        member x.GetOrCreateVimTextBuffer textBuffer = x.GetOrCreateVimTextBuffer textBuffer
        member x.LoadVimRc() = x.LoadVimRc()
        member x.LoadSessionData() = x.LoadSessionData()
        member x.RemoveVimBuffer textView = x.RemoveVimBuffer textView
        member x.SaveSessionData() = x.SaveSessionData()
        member x.ShouldCreateVimBuffer textView = x.ShouldCreateVimBuffer textView
        member x.TryGetOrCreateVimBufferForHost(textView, vimBuffer) = x.TryGetOrCreateVimBufferForHost(textView, &vimBuffer)
        member x.TryGetVimBuffer(textView, vimBuffer) = x.TryGetVimBuffer(textView, &vimBuffer)
        member x.TryGetVimTextBuffer(textBuffer, vimBuffer) = x.TryGetVimTextBuffer(textBuffer, &vimBuffer)
        member x.TryGetRecentBuffer n = x.TryGetRecentBuffer n
