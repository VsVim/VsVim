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
    let mutable _historySession : IHistorySession<ITrackingPoint, SearchResult> option = None
    let mutable _incrementalSearchData = IncrementalSearchData.Default
    let _currentSearchUpdated = StandardEvent<SearchResultEventArgs>()
    let _currentSearchCompleted = StandardEvent<SearchResultEventArgs>()
    let _currentSearchCancelled = StandardEvent<SearchDataEventArgs>()

    member x.CurrentSearchData = _incrementalSearchData.SearchResult.SearchData

    member x.CurrentSearchResult = _incrementalSearchData.SearchResult

    member x.CurrentSearchText =  _incrementalSearchData.SearchText

    /// There is a big gap between the behavior and documentation of key mapping for an 
    /// incremental search operation.  The documentation properly documents the language
    /// mapping in "help language-mapping" and 'help imsearch'.  But it doesn't document
    /// that command mapping should used when 'imsearch' doesn't apply although it's
    /// the practice in implementation
    ///
    /// TODO: actually implement the 'imsearch' option and fix this
    member x.RemapMode = KeyRemapMode.Command

    /// Begin the incremental search along the specified path
    member x.Begin path = 

        // If there is an existing session going on then we need to cancel it 
        match _historySession with 
        | Some historySession -> 
            historySession.Cancel()
            _historySession <- None
        | None -> ()

        let start = TextViewUtil.GetCaretPoint _textView
        let searchData = SearchData(StringUtil.empty, path, _globalSettings.WrapScan)
        let searchResult = SearchResult.NotFound (searchData, false)
        let incrementalSearchData = {
            SearchResult = searchResult
            SearchText = match path with | Path.Forward -> "/" | Path.Backward -> "?"
        }
        let startPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)

        let historyClient = { 
            new IHistoryClient<ITrackingPoint, SearchResult> with
                member this.HistoryList = _vimData.SearchHistory
                member this.RemapMode = x.RemapMode
                member this.Beep() = _operations.Beep()
                member this.ProcessCommand data command = x.ProcessCommand data command
                member this.Completed (data : ITrackingPoint) _ = x.Completed data
                member this.Cancelled (data : ITrackingPoint) = x.Cancelled()
            }

        let historySession = HistoryUtil.CreateHistorySession historyClient startPoint StringUtil.empty
        _historySession <- Some historySession
        _incrementalSearchData <- incrementalSearchData

        // Raise the event
        _currentSearchUpdated.Trigger x (SearchResultEventArgs(searchResult))

        historySession.CreateBindDataStorage().CreateBindData()

    // Reset the view to it's original state.  We should only be doing this if the
    // 'incsearch' option is set.  Otherwise the view shouldn't change during an 
    // incremental search
    member x.ResetView () =
        if _globalSettings.IncrementalSearch then
             _operations.EnsureAtCaret ViewFlags.Standard

    member x.ProcessCommand (startPoint : ITrackingPoint) rawPattern =
        x.RunSearch startPoint rawPattern
        startPoint

    /// Run the search for the specified text.  This will do the search, update the caret 
    /// position and raise events
    member x.RunSearch (startPoint : ITrackingPoint) rawPattern =

        // Get the SearchResult value for the new text
        let searchData = SearchData.Parse rawPattern _incrementalSearchData.SearchData.Kind _incrementalSearchData.SearchData.Options
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
        let searchText = 
            match _incrementalSearchData.SearchData.Path with
            | Path.Forward -> "/" + rawPattern
            | Path.Backward -> "?" + rawPattern
        _incrementalSearchData <- {
            SearchResult = searchResult
            SearchText = searchText
        }

        let args = SearchResultEventArgs(searchResult)
        _currentSearchUpdated.Trigger x args

    /// Called when the processing is completed.  Raise the completed event and return
    /// the final SearchResult
    member x.Completed startPoint =
        let searchResult =
            if StringUtil.isNullOrEmpty _incrementalSearchData.SearchData.Pattern then
                // When the user simply hits Enter on an empty incremental search then
                // we should be re-using the 'LastSearch' value.
                x.RunSearch startPoint _vimData.LastSearchData.Pattern

            _incrementalSearchData.SearchResult

        _vimData.LastSearchData <- searchResult.SearchData.LastSearchData
        _currentSearchCompleted.Trigger x (SearchResultEventArgs(searchResult))
        _historySession <- None
        _incrementalSearchData <- IncrementalSearchData.Default
        searchResult

    /// Cancel the search.  Provide the last value searched for
    member x.Cancelled data =
        x.ResetView ()
        _currentSearchCancelled.Trigger x (SearchDataEventArgs(_incrementalSearchData.SearchData))
        _historySession <- None
        _incrementalSearchData <- IncrementalSearchData.Default

    member x.ResetSearch pattern = 
        match _historySession with
        | Some historySession -> historySession.ResetCommand pattern
        | None -> ()

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _historySession
        member x.WordNavigator = _wordNavigator
        member x.CurrentSearchData = x.CurrentSearchData
        member x.CurrentSearchResult = x.CurrentSearchResult
        member x.CurrentSearchText = x.CurrentSearchText
        member x.Begin kind = x.Begin kind
        member x.ResetSearch pattern = x.ResetSearch pattern
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish


