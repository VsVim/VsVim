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
        _key: obj,
        _vimBuffer: IVimBuffer,
        _operations: ICommonOperations,
        _searchPath: SearchPath,
        _isWrap: bool
    ) =

    let mutable _searchData = SearchData("", _searchPath, _isWrap)
    let mutable _searchResult : SearchResult option = None
    let mutable _isActive = true
    let _globalSettings = _vimBuffer.GlobalSettings
    let _searchStart = StandardEvent<SearchDataEventArgs>()
    let _searchEnd = StandardEvent<SearchResultEventArgs>()
    let _sessionComplete = StandardEvent<EventArgs>()

    member x.Key = _key
    member x.IsWrap = _isWrap
    member x.IsActive = _isActive
    member x.SearchPath = _searchPath
    member x.SearchData = _searchData
    member x.SearchResult = _searchResult
    member x.IsSearchCompleted = Option.isSome _searchResult

    /// There is a big gap between the behavior and documentation of key mapping for an 
    /// incremental search operation.  The documentation properly documents the language
    /// mapping in "help language-mapping" and 'help imsearch'.  But it doesn't document
    /// that command mapping should used when 'imsearch' doesn't apply although it's
    /// the practice in implementation
    member x.RemapMode = KeyRemapMode.Command

    // Reset the view to it's original state.  We should only be doing this if the
    // 'incsearch' option is set.  Otherwise the view shouldn't change during an 
    // incremental search
    member x.ResetView () =
        if _globalSettings.IncrementalSearch then
             _operations.EnsureAtCaret ViewFlags.Standard

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
        0

    member private x.RunCancelled() =
        // TODO: in the async case this should set the SearchResult to Cancelled if it hasn't finished 
        // yet.
        _isActive <- false
        _sessionComplete.Trigger x (EventArgs.Empty)

    interface IHistoryClient<ITrackingPoint, int> with
        member x.HistoryList = _vimBuffer.VimData.SearchHistory
        member x.RegisterMap = _vimBuffer.Vim.RegisterMap
        member x.RemapMode = x.RemapMode
        member x.Beep() = _operations.Beep()
        member x.ProcessCommand searchPoint searchText = x.RunActive searchPoint (fun () -> 
            x.RunSearch searchPoint searchText
            searchPoint)
        member x.Completed _ _ = x.RunActive 0 (fun () -> x.RunCompleted())
        member x.Cancelled _ = x.RunActive () (fun () -> x.RunCancelled())

    interface IIncrementalSearchSession with
        member x.InSearch = Option.isNone _searchResult
        member x.SearchData = _searchData
        member x.SearchResult = _searchResult
        member x.Cancel() = x.RunCancelled()
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

    let _vimData = _vimBufferData.Vim.VimData
    let _statusUtil = _vimBufferData.StatusUtil
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _wordNavigator = _vimTextBuffer.WordNavigator
    let _localSettings = _vimTextBuffer.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    let _textView = _operations.TextView
    let _searchService = _vimBufferData.Vim.SearchService
    let mutable _incrementalSearchSession: IncrementalSearchSession option = None

    static member EmptySearchData = SearchData("", SearchPath.Forward, isWrap= false)
    static member EmptySearchText = ""

    member x.CurrentSearchData = 
        match _incrementalSearchSession with
        | Some session -> session.SearchData
        | None -> IncrementalSearch.EmptySearchData

    member x.CurrentSearchText =
        match _incrementalSearchSession with
        | Some session -> session.SearchText
        | None -> IncrementalSearch.EmptySearchText

    member x.InSearch = Option.isSome _incrementalSearchSession

    member x.InPasteWait = 
        match _incrementalSearchSession with
        | Some session -> session.HistorySession.InPasteWait
        | None -> false

    member x.IsCurrentSession key = 
        match _incrementalSearchSession with
        | None -> false
        | Some incrementalSearchSession -> incrementalSearchSession.Key = key

    /// Begin the incremental search along the specified path
    member x.Start path = 

        // If there is an existing session going on then we need to cancel it 
        x.Cancel()
        Debug.Assert(Option.isNone _incrementalSearchSession)

        let key = obj()

        // This local will only run the 'func' with the created session is still active.  If it is 
        // not active then nothing will happen and 'defaultValue' will be returned. 
        let runActive func defaultValue = 
            match _incrementalSearchSession with
            | None -> defaultValue
            | Some incrementalSearchSession ->
                if incrementalSearchSession.Key = key then
                    func incrementalSearchSession 
                else   
                    defaultValue

        let runCancelled (session: IncrementalSearchSession) =
            x.ResetView()
            if not session.IsSearchCompleted then 
                let searchResult = SearchResult.Cancelled session.SearchData
                _searchEnd.Trigger x (SearchResultEventArgs(searchResult))
            _incrementalSearchSession <- None
            
        let historyClient = { 
            new IHistoryClient<ITrackingPoint, SearchResult> with
                member this.HistoryList = _vimData.SearchHistory
                member this.RegisterMap = _vimBufferData.Vim.RegisterMap
                member this.RemapMode = x.RemapMode
                member this.Beep() = _operations.Beep()
                member this.ProcessCommand data command = runActive (fun session -> x.RunSearch session data command) data
                member this.Completed (data: ITrackingPoint) _ = runActive (fun session -> x.RunCompleted session data) IncrementalSearchData.Default.SearchResult
                member this.Cancelled _ = runActive runCancelled ()
            }

        let start = TextViewUtil.GetCaretPoint _textView
        let startPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
        let vimBuffer = _vimBufferData.Vim.GetVimBuffer _textView
        let historySession = HistoryUtil.CreateHistorySession historyClient startPoint StringUtil.Empty vimBuffer
        let incrementalSearchSession = IncrementalSearchSession(key, historySession, path, _globalSettings.WrapScan)
        _incrementalSearchSession <- Some incrementalSearchSession

        // Raise the event pair
        _searchStart.Trigger x (SearchDataEventArgs(incrementalSearchSession.SearchData))
        _searchEnd.Trigger x (SearchResultEventArgs(Option.get incrementalSearchSession.SearchResult))

        historySession.CreateBindDataStorage().CreateBindData()


    member x.Cancel() =
        match _incrementalSearchSession with 
        | None -> ()
        | Some incrementalSearchSession -> incrementalSearchSession.HistorySession.Cancel()
        Debug.Assert(Option.isNone _incrementalSearchSession)

    member x.ResetSearch pattern = 
        match _incrementalSearchSession with 
        | None -> ()
        | Some incrementalSearchSession -> incrementalSearchSession.HistorySession.ResetCommand pattern

    interface IIncrementalSearch with
        member x.InSearch = x.InSearch
        member x.InPasteWait = x.InPasteWait
        member x.WordNavigator = _wordNavigator
        member x.CurrentSearchData = x.CurrentSearchData
        member x.CurrentSearchText = x.CurrentSearchText
        member x.Begin kind = x.Begin kind
        member x.Cancel () = x.Cancel()
        member x.ResetSearch pattern = x.ResetSearch pattern
        [<CLIEvent>]
        member x.SearchStart = _searchStart.Publish
        [<CLIEvent>]
        member x.SearchEnd = _searchEnd.Publish 


