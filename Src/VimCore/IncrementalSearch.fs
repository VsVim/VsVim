namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil

type IncrementalSearchData = { 

    /// Most recent result of the search
    SearchResult : SearchResult

    /// Most recent search text being usde
    SearchText : string

} with 

    member x.SearchData = x.SearchResult.SearchData

    static member Default = 
        let searchData = SearchData("", Path.Forward, false)
        let searchResult = SearchResult.NotFound (searchData, false)
        {
            SearchResult = searchResult
            SearchText = ""
        }

type internal IncrementalSearchSession
    (
        _key : obj,
        _historySession : IHistorySession<ITrackingPoint, SearchResult>,
        _incrementalSearchData : IncrementalSearchData
    ) =

    let mutable _incrementalSearchData = _incrementalSearchData

    member x.Key = _key

    member x.HistorySession = _historySession

    member x.IncrementalSearchData 
        with get() = _incrementalSearchData
        and set value = _incrementalSearchData <- value

type internal IncrementalSearch
    (
        _vimBufferData : IVimBufferData,
        _operations : ICommonOperations
    ) =

    let _vimData = _vimBufferData.Vim.VimData
    let _statusUtil = _vimBufferData.StatusUtil
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _wordNavigator = _vimTextBuffer.WordNavigator
    let _localSettings = _vimTextBuffer.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    let _textView = _operations.TextView
    let _searchService = _vimBufferData.Vim.SearchService
    let mutable _incrementalSearchSession : IncrementalSearchSession option = None
    let _currentSearchUpdated = StandardEvent<SearchResultEventArgs>()
    let _currentSearchCompleted = StandardEvent<SearchResultEventArgs>()
    let _currentSearchCancelled = StandardEvent<SearchDataEventArgs>()

    member x.CurrentIncrementalSearchData =
        match _incrementalSearchSession with
        | Some incrementalSearchSession -> incrementalSearchSession.IncrementalSearchData
        | None -> IncrementalSearchData.Default

    member x.CurrentSearchData = x.CurrentIncrementalSearchData.SearchResult.SearchData

    member x.CurrentSearchResult = x.CurrentIncrementalSearchData.SearchResult

    member x.CurrentSearchText =  x.CurrentIncrementalSearchData.SearchText

    member x.InSearch = Option.isSome _incrementalSearchSession

    member x.InPasteWait = 
        match _incrementalSearchSession with
        | Some session -> session.HistorySession.InPasteWait
        | None -> false

    /// There is a big gap between the behavior and documentation of key mapping for an 
    /// incremental search operation.  The documentation properly documents the language
    /// mapping in "help language-mapping" and 'help imsearch'.  But it doesn't document
    /// that command mapping should used when 'imsearch' doesn't apply although it's
    /// the practice in implementation
    ///
    /// TODO: actually implement the 'imsearch' option and fix this
    member x.RemapMode = KeyRemapMode.Command

    member x.IsCurrentSession key = 
        match _incrementalSearchSession with
        | None -> false
        | Some incrementalSearchSession -> incrementalSearchSession.Key = key

    /// Begin the incremental search along the specified path
    member x.Begin path = 

        // If there is an existing session going on then we need to cancel it 
        x.Cancel()

        let start = TextViewUtil.GetCaretPoint _textView
        let searchData = SearchData(StringUtil.empty, path, _globalSettings.WrapScan)
        let searchResult = SearchResult.NotFound (searchData, false)
        let incrementalSearchData = {
            SearchResult = searchResult
            SearchText = ""
        }
        let startPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)

        let key = obj()

        // This local will only run the 'func' with the created session is still active.  If it is 
        // not active then nothing will happen and 'defaultValue' will be returned
        let runActive func defaultValue = 
            match _incrementalSearchSession with
            | None -> defaultValue
            | Some incrementalSearchSession ->
                if incrementalSearchSession.Key = key then
                    func incrementalSearchSession 
                else   
                    defaultValue
            
        let historyClient = { 
            new IHistoryClient<ITrackingPoint, SearchResult> with
                member this.HistoryList = _vimData.SearchHistory
                member this.RegisterMap = _vimBufferData.Vim.RegisterMap
                member this.RemapMode = x.RemapMode
                member this.Beep() = _operations.Beep()
                member this.ProcessCommand data command = runActive (fun session -> x.RunSearch session data command) data
                member this.Completed (data : ITrackingPoint) _ = runActive (fun session -> x.RunCompleted session data) IncrementalSearchData.Default.SearchResult
                member this.Cancelled (data : ITrackingPoint) = runActive (fun session -> x.RunCancelled session) ()
            }

        let historySession = HistoryUtil.CreateHistorySession historyClient startPoint StringUtil.empty
        _incrementalSearchSession <- Some (IncrementalSearchSession(key, historySession, incrementalSearchData))

        // Raise the event
        _currentSearchUpdated.Trigger x (SearchResultEventArgs(searchResult))

        historySession.CreateBindDataStorage().CreateBindData()

    // Reset the view to it's original state.  We should only be doing this if the
    // 'incsearch' option is set.  Otherwise the view shouldn't change during an 
    // incremental search
    member x.ResetView () =
        if _globalSettings.IncrementalSearch then
             _operations.EnsureAtCaret ViewFlags.Standard

    member x.RunSearch incrementalSearchSession (startPoint : ITrackingPoint) rawPattern =
        let incrementalSearchData = x.RunSearchCore incrementalSearchSession startPoint rawPattern
        incrementalSearchSession.IncrementalSearchData <- incrementalSearchData
        let args = SearchResultEventArgs(incrementalSearchData.SearchResult)
        _currentSearchUpdated.Trigger x args
        startPoint

    /// Run the search for the specified text.  This will do the search, update the caret 
    /// position and raise events
    member x.RunSearchCore incrementalSearchSession (startPoint : ITrackingPoint) rawPattern =

        // Get the SearchResult value for the new text
        let incrementalSearchData = incrementalSearchSession.IncrementalSearchData
        let searchData = SearchData.Parse rawPattern incrementalSearchData.SearchData.Kind incrementalSearchData.SearchData.Options
        let searchResult =
            if StringUtil.isNullOrEmpty rawPattern then
                SearchResult.NotFound (searchData, false)
            else
                match TrackingPointUtil.GetPoint _textView.TextSnapshot startPoint with
                | None -> SearchResult.NotFound (searchData, false)
                | Some point -> _searchService.FindNextPattern point searchData _wordNavigator 1

        // Update our state based on the SearchResult.  Only update the view if the 'incsearch'
        // option is set
        if _globalSettings.IncrementalSearch then
            match searchResult with
            | SearchResult.Found (_, span, _, _) -> _operations.EnsureAtPoint span.Start ViewFlags.Standard
            | SearchResult.NotFound _ -> x.ResetView ()

        // Update all of our internal state before we start raising events 
        { SearchResult = searchResult; SearchText = rawPattern }

    /// Called when the processing is completed.  Raise the completed event and return
    /// the final SearchResult
    member x.RunCompleted incrementalSearchSession startPoint =
        if StringUtil.isNullOrEmpty incrementalSearchSession.IncrementalSearchData.SearchData.Pattern then
            // When the user simply hits Enter on an empty incremental search then
            // we should be re-using the 'LastSearch' value.
            let incrementalSearchData = x.RunSearchCore incrementalSearchSession startPoint _vimData.LastSearchData.Pattern
            incrementalSearchSession.IncrementalSearchData <- incrementalSearchData

        let searchResult = incrementalSearchSession.IncrementalSearchData.SearchResult
        _vimData.LastSearchData <- searchResult.SearchData.LastSearchData
        _currentSearchCompleted.Trigger x (SearchResultEventArgs(searchResult))
        _incrementalSearchSession <- None
        searchResult

    /// Cancel the search.  Provide the last value searched for
    member x.RunCancelled incrementalSearchSession =
        x.ResetView ()
        _currentSearchCancelled.Trigger x (SearchDataEventArgs(incrementalSearchSession.IncrementalSearchData.SearchData))
        _incrementalSearchSession <- None

    member x.Cancel() =
        match _incrementalSearchSession with 
        | None -> ()
        | Some incrementalSearchSession -> 
            incrementalSearchSession.HistorySession.Cancel()
            x.RunCancelled incrementalSearchSession

    member x.ResetSearch pattern = 
        match _incrementalSearchSession with 
        | None -> ()
        | Some incrementalSearchSession -> incrementalSearchSession.HistorySession.ResetCommand pattern

    interface IIncrementalSearch with
        member x.InSearch = x.InSearch
        member x.InPasteWait = x.InPasteWait
        member x.WordNavigator = _wordNavigator
        member x.CurrentSearchData = x.CurrentSearchData
        member x.CurrentSearchResult = x.CurrentSearchResult
        member x.CurrentSearchText = x.CurrentSearchText
        member x.Begin kind = x.Begin kind
        member x.Cancel () = x.Cancel()
        member x.ResetSearch pattern = x.ResetSearch pattern
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish


