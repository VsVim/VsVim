namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil
open System
open System.Collections.Generic
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Vim.VimCoreExtensions

[<RequireQualifiedAccess>]
type SearchState = 
    | NeverRun of SearchResult: SearchResult
    | InProgress of Key: obj * TaskCompletionSource: TaskCompletionSource<SearchResult>
    | Complete of SearchResult: SearchResult

[<RequireQualifiedAccess>]
type SessionState = 
    | NotStarted
    | Started of HistorySession: IHistorySession<ITrackingPoint, SearchResult>
    | Completed

/// Represents an incremental search session. The session is created when the initial 
/// `/` key is pressed and is updated until the session is completed, with enter, or 
/// cancelled.
type internal IncrementalSearchSession
    (
        _vimBufferData: IVimBufferData,
        _operations: ICommonOperations,
        _searchPath: SearchPath,
        _isWrap: bool
    ) =

    let mutable _searchData = SearchData("", _searchPath, _isWrap)
    let mutable _searchState = SearchState.NeverRun (SearchResult.NotFound (_searchData, CanFindWithWrap=false))
    let mutable _sessionState = SessionState.NotStarted
    let _key = obj()
    let _globalSettings = _vimBufferData.Vim.GlobalSettings
    let _textView = _operations.TextView
    let _searchStart = StandardEvent<SearchDataEventArgs>()
    let _searchEnd = StandardEvent<SearchResultEventArgs>()
    let _sessionComplete = StandardEvent<EventArgs>()

    member x.Key = _key
    member x.IsWrap = _isWrap
    member x.IsNotStarted = match _sessionState with | SessionState.NotStarted _ -> true | _ -> false
    member x.IsStarted = match _sessionState with | SessionState.Started _ -> true | _ -> false
    member x.IsCompleted = match _sessionState with | SessionState.Completed -> true | _ -> false
    member x.IsActive = x.IsStarted && not x.IsCompleted
    member x.SearchPath = _searchPath
    member x.SearchData = _searchData
    member x.SearchResult = 
        match _searchState with
        | SearchState.NeverRun searchResult -> Some searchResult
        | SearchState.InProgress _ -> None
        | SearchState.Complete searchResult -> Some searchResult
    member x.IsSearchInProgress = 
        match _searchState with
        | SearchState.NeverRun _-> false
        | SearchState.InProgress _ -> true
        | SearchState.Complete _ -> false
    member x.InPasteWait =
        match _sessionState with
        | SessionState.NotStarted -> false
        | SessionState.Started historySession -> historySession.InPasteWait
        | SessionState.Completed -> false

    [<CLIEvent>]
    member x.SessionComplete = _sessionComplete.Publish

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
        match _sessionState with
        | SessionState.NotStarted -> ()
        | SessionState.Started historySession -> historySession.ResetCommand pattern
        | SessionState.Completed -> ()

    /// Begin the incremental search along the specified path
    member x.Start() = 
        if not x.IsNotStarted then
            raise (InvalidOperationException())

        let start = TextViewUtil.GetCaretPoint _textView
        let vimBuffer = _vimBufferData.Vim.GetVimBuffer _textView
        let startPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
        let historySession = HistoryUtil.CreateHistorySession x startPoint StringUtil.Empty vimBuffer
        _sessionState <- SessionState.Started historySession
        historySession.CreateBindDataStorage().CreateMappedBindData().ConvertToBindData()

    member x.GetSearchResultAsync() =
        match _searchState with
        | SearchState.NeverRun searchResult -> Task.FromResult(searchResult)
        | SearchState.InProgress (_, tcs) -> tcs.Task
        | SearchState.Complete searchResult -> Task.FromResult(searchResult)

    member private x.RunActive defaultValue func =
        if x.IsActive then func ()
        else defaultValue

    /// Run the search for the specified text.  This will do the search, update the caret 
    /// position and raise events
    member private x.RunSearchSyncWithResult(searchPoint: ITrackingPoint, searchText) =
        let (point, _, searchData, searchService, wordNavigator) = x.RunSearchImplStart(searchPoint, searchText)
        let searchResult = 
            match point, StringUtil.IsNullOrEmpty searchText with
            | Some point, false -> searchService.FindNextPattern point searchData wordNavigator 1
            | _ -> x.RunSearchImplNotFound searchData

        // Search is completed now update the results
        x.RunSearchImplEnd(searchResult, updateView=true)
        searchResult

    member private x.RunSearchSync(searchPoint: ITrackingPoint) searchText: unit =
        x.RunSearchSyncWithResult(searchPoint, searchText) |> ignore

    /// Run the search for the specified text in an asynchronous fashion. This should mimic 
    /// RunSearchSync in every way but the synchronous nature
    member private x.RunSearchAsync (searchPoint: ITrackingPoint) searchText: unit =
        let (point, searchKey, searchData, searchService, wordNavigator) = x.RunSearchImplStart(searchPoint, searchText)

        match point, StringUtil.IsNullOrEmpty searchText with
        | Some point, false -> 
            let context = SynchronizationContext.Current

            // This function runs back on the UI thread. It can safely access all available state
            let searchDone searchResult = 
                // Have to account for the possibility this search has already been handled 
                // by the UI thread. In that case SearchEnd has already been raised with a 
                // Cancelled value and there is no more work to do.
                match _searchState with
                | SearchState.InProgress (currentKey, _) when currentKey = searchKey -> x.RunSearchImplEnd(searchResult, updateView=true)
                | _ -> ()

            // This is used in the background and must be careful to use types which are immutable
            // or threading aware.
            let searchAsync() = 
                let searchResult = searchService.FindNextPattern point searchData wordNavigator 1
                context.Post((fun _ -> searchDone searchResult), null)
                
            let task = new Task((fun _ -> searchAsync()))
            task.Start(TaskScheduler.Default)
        | _ -> 
            // In this case we complete synchronously
            let searchResult = x.RunSearchImplNotFound searchData
            x.RunSearchImplEnd(searchResult, updateView=true)

    member private x.RunSearchImplNotFound(searchData) = SearchResult.NotFound (_searchData, CanFindWithWrap=false)

    member private x.RunSearchImplStart(searchPoint, searchText): (SnapshotPoint option * obj * SearchData * ISearchService * ITextStructureNavigator) =

        // Don't update the view here. Let the next search do the view 
        // updating when it completes. Want to avoid chopiness here when
        // there is lots of typing.
        x.MaybeCancelSearchInProgress(updateView=false)

        // Update all the data for the start of the search
        let searchKey = obj()
        _searchData <- SearchData.Parse searchText _searchData.Kind _searchData.Options
        _searchState <- SearchState.InProgress (searchKey, (new TaskCompletionSource<SearchResult>()))
        _searchStart.Trigger x (SearchDataEventArgs(_searchData))

        // Get the data required to complete the search
        let point = TrackingPointUtil.GetPoint _textView.TextSnapshot searchPoint
        (point, searchKey, _searchData, _vimBufferData.Vim.SearchService, _vimBufferData.VimTextBuffer.WordNavigator)

    member private x.RunSearchImplEnd(searchResult, updateView) =
        match _searchState with
        | SearchState.InProgress (_, tcs) -> tcs.SetResult(searchResult)
        | _ -> ()

        // Update all of our internal state before we start raising events 
        _searchState <- SearchState.Complete searchResult

        // Update our state based on the SearchResult.  Only update the view if the 'incsearch'
        // option is set
        if _globalSettings.IncrementalSearch && updateView then
            match searchResult with
            | SearchResult.Found (_, span, _, _) -> _operations.EnsureAtPoint span.Start ViewFlags.Standard
            | SearchResult.NotFound _ -> x.ResetView()
            | SearchResult.Cancelled _ -> x.ResetView()
            | SearchResult.Error _ -> x.ResetView()

        _searchEnd.Trigger x (SearchResultEventArgs(searchResult))

    /// Called when the processing is completed.  Raise the completed event and
    /// return the final SearchResult
    member private x.RunCompleted(startPoint, searchText) =
        let vimData = _vimBufferData.Vim.VimData
        let searchResult = 

            // The search result if the search completed and wasn't cancelled.
            let completedSearchResult searchState =
                match searchState with
                | SearchState.Complete searchResult ->
                    match searchResult with
                    | SearchResult.Cancelled _ -> None
                    | _ -> Some searchResult
                | _ -> None

            if StringUtil.IsNullOrEmpty searchText then

                // When the user simply hits Enter on an empty incremental
                // search then we should be re-using the 'LastSearch' value.
                x.RunSearchSyncWithResult(startPoint, vimData.LastSearchData.Pattern)
            else
                
                // Use a completed search result if possible.
                match completedSearchResult _searchState with
                | Some searchResult -> searchResult
                | None ->

                    // If the search is still in progress then we need to force
                    // it to be complete here. Need to avoid the tempatation to
                    // use methods like Task.Wait as that can cause deadlocks.
                    // Instead just synchronously run the search here.
                    x.RunSearchSyncWithResult(startPoint, searchText)

        vimData.LastSearchData <- _searchData
        x.RunCompleteSession()
        searchResult

    member private x.RunCancel() =
        x.MaybeCancelSearchInProgress(updateView=true)
        x.RunCompleteSession()

    member private x.RunCompleteSession() =
        Debug.Assert(x.IsStarted)
        _sessionState <- SessionState.Completed
        _sessionComplete.Trigger x (EventArgs.Empty)

    /// If there is an unfinished search cancel it
    member private x.MaybeCancelSearchInProgress(updateView) =
        match _searchState with
        | SearchState.InProgress _ -> x.RunSearchImplEnd(SearchResult.Cancelled(_searchData), updateView)
        | _ -> ()

    member x.Cancel() =
        match _sessionState with
        | SessionState.Started historySession -> historySession.Cancel()
        | _ -> raise (InvalidOperationException())

    interface IHistoryClient<ITrackingPoint, SearchResult> with
        member x.HistoryList = _vimBufferData.Vim.VimData.SearchHistory
        member x.RegisterMap = _vimBufferData.Vim.RegisterMap
        member x.RemapMode = x.RemapMode
        member x.Beep() = _operations.Beep()
        member x.ProcessCommand searchPoint searchText = x.RunActive searchPoint (fun () -> 
            x.RunSearchAsync searchPoint searchText
            searchPoint)
        member x.Completed searchPoint searchText _ = x.RunActive (SearchResult.Error (_searchData, "Invalid Operation")) (fun () -> x.RunCompleted(searchPoint, searchText))
        member x.Cancelled _ = x.RunActive () (fun () -> x.RunCancel())

    interface IIncrementalSearchSession with
        member x.Key = _key
        member x.IsStarted = x.IsStarted
        member x.IsCompleted = x.IsCompleted
        member x.SearchData = _searchData
        member x.SearchResult = x.SearchResult
        member x.ResetSearch pattern = x.ResetSearch pattern
        member x.Start() = x.Start()
        member x.Cancel() = x.Cancel()
        member x.GetSearchResultAsync() = x.GetSearchResultAsync()
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

        let session = IncrementalSearchSession(_vimBufferData, _operations, searchPath, _globalSettings.WrapScan)

        // When the session completes need to clear out the active info if this is still 
        // the active session.
        session.SessionComplete
        |> Observable.add (fun _ -> 
            match _session with
            | Some activeSession when activeSession.Key = session.Key -> _session <- None
            | _ -> ())

        _session <- Some session
        _sessionCreated.Trigger x (IncrementalSearchSessionEventArgs(session))
        session

    interface IIncrementalSearch with
        member x.ActiveSession = Option.map (fun s -> s :> IIncrementalSearchSession) _session
        member x.HasActiveSession = Option.isSome _session
        member x.InPasteWait = x.InPasteWait
        member x.WordNavigator = _wordNavigator
        member x.CurrentSearchData = x.CurrentSearchData
        member x.CurrentSearchText = x.CurrentSearchText
        member x.CreateSession searchPath = x.CreateSession searchPath :> IIncrementalSearchSession
        member x.CancelSession() = match _session with | Some session -> session.Cancel() | None -> () 
        [<CLIEvent>]
        member x.SessionCreated = _sessionCreated.Publish


