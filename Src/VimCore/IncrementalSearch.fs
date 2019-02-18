namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil
open System
open System.Diagnostics
open Vim.VimCoreExtensions

/// Represents an incremental search session. The session is created when the initial 
/// `/` key is pressed and is updated until the session is completed, with enter, or 
/// cancelled.
type internal IncrementalSearchSession
    (
        _vimBuffer: IVimBuffer,
        _operations: ICommonOperations,
        _searchPath: SearchPath,
        _isWrap: bool
    ) =

    let mutable _searchData = SearchData("", _searchPath, _isWrap)
    let mutable _searchResult : SearchResult option = None
    let mutable _historySession : IHistorySession<ITrackingPoint, SearchResult> option = None
    let mutable _isActive = true
    let _key = obj()
    let _globalSettings = _vimBuffer.GlobalSettings
    let _searchStart = StandardEvent<SearchDataEventArgs>()
    let _searchEnd = StandardEvent<SearchResultEventArgs>()
    let _sessionComplete = StandardEvent<EventArgs>()

    member x.Key = _key
    member x.IsWrap = _isWrap
    member x.IsStarted = Option.isSome _historySession
    member x.IsActive = _isActive
    member x.SearchPath = _searchPath
    member x.SearchData = _searchData
    member x.SearchResult = _searchResult
    member x.IsSearchCompleted = Option.isSome _searchResult
    member x.HistorySession = _historySession
    member x.InPasteWait =
        match _historySession with
        | Some session -> session.InPasteWait
        | None -> false

    /// There is a big gap between the behavior and documentation of key mapping for an 
    /// incremental search operation.  The documentation properly documents the language
    /// mapping in "help language-mapping" and 'help imsearch'.  But it doesn't document
    /// that command mapping should used when 'imsearch' doesn't apply although it's
    /// the practice in implementation
    member x.RemapMode = KeyRemapMode.Command

    // Reset the view to it's original state.  We should only be doing this if the
    // 'incsearch' option is set.  Otherwise the view shouldn't change during an 
    // incremental search
    member private x.ResetView () =
        if _globalSettings.IncrementalSearch then
             _operations.EnsureAtCaret ViewFlags.Standard

    member x.ResetSearch pattern = 
        match _historySession with 
        | Some historySession -> historySession.ResetCommand pattern
        | None -> ()

    /// Begin the incremental search along the specified path
    member x.Start() = 
        if x.IsStarted then
            raise (InvalidOperationException())

        let start = TextViewUtil.GetCaretPoint _vimBuffer.TextView
        let startPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
        let historySession = HistoryUtil.CreateHistorySession x startPoint StringUtil.Empty (Some _vimBuffer)
        _historySession <- Some historySession
        historySession.CreateBindDataStorage().CreateBindData()

    member private x.RunActive defaultValue func =
        if x.IsActive then func ()
        else defaultValue

    /// Run the search for the specified text.  This will do the search, update the caret 
    /// position and raise events
    member private x.RunSearch (startPoint: ITrackingPoint) searchText: unit =
        // TODO: have to check for a search in progress and cancel it

        // Update all the data for the start of the search
        _searchData <- SearchData.Parse searchText _searchData.Kind _searchData.Options
        _searchResult <- None
        _searchStart.Trigger x (SearchDataEventArgs(_searchData))

        // Actually execute the search. 
        // TODO: Goal is to make this next part async

        let searchResult = 
            if StringUtil.IsNullOrEmpty searchText then
                SearchResult.NotFound (_searchData, CanFindWithWrap=false)
            else
                match TrackingPointUtil.GetPoint _vimBuffer.TextView.TextSnapshot startPoint with
                | None -> SearchResult.NotFound (_searchData, false)
                | Some point -> _vimBuffer.Vim.SearchService.FindNextPattern point _searchData _vimBuffer.VimTextBuffer.WordNavigator 1

        // Search is completed now update the results

        // Update our state based on the SearchResult.  Only update the view if the 'incsearch'
        // option is set
        if _globalSettings.IncrementalSearch then
            match searchResult with
            | SearchResult.Found (_, span, _, _) -> _operations.EnsureAtPoint span.Start ViewFlags.Standard
            | SearchResult.NotFound _ -> x.ResetView()
            | SearchResult.Cancelled _ -> x.ResetView()
            | SearchResult.Error _ -> x.ResetView()

        // Update all of our internal state before we start raising events 
        _searchResult <- Some searchResult
        _searchEnd.Trigger x (SearchResultEventArgs(searchResult))

    /// Called when the processing is completed.  Raise the completed event and return
    /// the final SearchResult
    member private x.RunCompleted() =
        // TODO: in the async case this should wait for the completion 
        _isActive <- false
        _sessionComplete.Trigger x (EventArgs.Empty)
        Option.get _searchResult

    member x.Cancel() =
        // TODO: in the async case this should set the SearchResult to Cancelled if it hasn't finished 
        // yet.
        if x.IsActive then
            _isActive <- false
            _sessionComplete.Trigger x (EventArgs.Empty)

    interface IHistoryClient<ITrackingPoint, SearchResult> with
        member x.HistoryList = _vimBuffer.VimData.SearchHistory
        member x.RegisterMap = _vimBuffer.Vim.RegisterMap
        member x.RemapMode = x.RemapMode
        member x.Beep() = _operations.Beep()
        member x.ProcessCommand searchPoint searchText = x.RunActive searchPoint (fun () -> 
            x.RunSearch searchPoint searchText
            searchPoint)
        member x.Completed _ _ = x.RunActive (SearchResult.Error (_searchData, "Invalid Operation")) (fun () -> x.RunCompleted())
        member x.Cancelled _ = x.RunActive () (fun () -> x.Cancel())

    interface IIncrementalSearchSession with
        member x.InSearch = Option.isNone _searchResult
        member x.SearchData = _searchData
        member x.SearchResult = _searchResult
        member x.Cancel() = x.Cancel()
        [<CLIEvent>]
        member x.SearchStart = _searchStart.Publish
        [<CLIEvent>]
        member x.SearchEnd = _searchEnd.Publish
        [<CLIEvent>]
        member x.SessionComplete = _sessionComplete.Publish

type internal IncrementalSearch
    (
        _vimBufferData: IVimBufferData,
        _operations: ICommonOperations
    ) =

    // TODO: most of these aren't needed anymore.
    let _vimData = _vimBufferData.Vim.VimData
    let _statusUtil = _vimBufferData.StatusUtil
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _wordNavigator = _vimTextBuffer.WordNavigator
    let _localSettings = _vimTextBuffer.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    let _textView = _operations.TextView
    let _searchService = _vimBufferData.Vim.SearchService

    let mutable _session: IncrementalSearchSession option = None
    let _sessionCreated = StandardEvent<IncrementalSearchSessionEventArgs>()

    static member EmptySearchData = SearchData("", SearchPath.Forward, isWrap= false)

    member x.CurrentSearchData = 
        match _session with
        | Some session -> session.SearchData
        | None -> IncrementalSearch.EmptySearchData

    member x.CurrentSearchText = x.CurrentSearchData.Pattern

    member x.InSearch = Option.isSome _session

    member x.InPasteWait = 
        match _session with
        | Some session -> session.InPasteWait 
        | None -> false

    member x.IsCurrentSession key = 
        match _session with
        | None -> false
        | Some session -> session.Key = key

    member x.CreateSession searchPath = 
        match _session with
        | Some session -> session.Cancel()
        | None -> ()

        Debug.Assert(Option.isNone _session)

        let vimBuffer = _vimBufferData.Vim.GetVimBuffer _textView
        let session = IncrementalSearchSession(vimBuffer, _operations, searchPath, _globalSettings.WrapScan)
        _session <- Some session
        _sessionCreated.Trigger x (IIncrementalSearchSessionEventArgs(session))
        session

    interface IIncrementalSearch with
        member x.InSearch = x.InSearch
        member x.InPasteWait = x.InPasteWait
        member x.WordNavigator = _wordNavigator
        member x.CurrentSearchData = x.CurrentSearchData
        member x.CurrentSearchText = x.CurrentSearchText
        member x. kind = x.Begin kind
        member x.Cancel () = x.Cancel()
        member x.ResetSearch pattern = x.ResetSearch pattern
        [<CLIEvent>]
        member x.SearchStart = _searchStart.Publish
        [<CLIEvent>]
        member x.SearchEnd = _searchEnd.Publish 


