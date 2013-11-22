namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil

type IncrementalSearchData = {
    /// The point from which the search needs to occur 
    StartPoint : ITrackingPoint;

    /// Most recent result of the search
    SearchResult : SearchResult

} with 

    member x.SearchData = x.SearchResult.SearchData

    member x.Pattern = x.SearchData.Pattern 

    member x.Path = x.SearchData.Kind.Path

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
    let mutable _historySession : IHistorySession<IncrementalSearchData, SearchResult> option = None
    let _currentSearchUpdated = StandardEvent<SearchResultEventArgs>()
    let _currentSearchCompleted = StandardEvent<SearchResultEventArgs>()
    let _currentSearchCancelled = StandardEvent<SearchDataEventArgs>()

    member x.CurrentSearchData =
        match _historySession with 
        | Some historySession -> Some historySession.ClientData.SearchData
        | None -> None

    member x.CurrentSearchResult =
        match _historySession with 
        | Some historySession -> Some historySession.ClientData.SearchResult
        | None -> None

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
        let patternData = { Pattern = StringUtil.empty; Path = path }
        let searchData = SearchData.OfPatternData patternData _globalSettings.WrapScan
        let data = {
            StartPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
            SearchResult = SearchResult.NotFound (searchData, false)
        }

        let historyClient = { 
            new IHistoryClient<IncrementalSearchData, SearchResult> with
                member this.HistoryList = _vimData.SearchHistory
                member this.RemapMode = x.RemapMode
                member this.Beep() = _operations.Beep()
                member this.ProcessCommand data command = x.RunSearch data command
                member this.Completed (data : IncrementalSearchData) _ = x.Completed data
                member this.Cancelled (data : IncrementalSearchData) = x.Cancelled data.SearchData
            }

        let historySession = HistoryUtil.CreateHistorySession historyClient data StringUtil.empty
        _historySession <- Some historySession

        // Raise the event
        _currentSearchUpdated.Trigger x (SearchResultEventArgs(data.SearchResult))

        historySession.CreateBindDataStorage().CreateBindData()

    // Reset the view to it's original state.  We should only be doing this if the
    // 'incsearch' option is set.  Otherwise the view shouldn't change during an 
    // incremental search
    member x.ResetView () =
        if _globalSettings.IncrementalSearch then
             _operations.EnsureAtCaret ViewFlags.Standard

    /// Run the search for the specified text.  Returns the new IncrementalSearchData resulting
    /// from the search
    member x.RunSearch (data : IncrementalSearchData) pattern =

        // Get the SearchResult value for the new text
        let searchData = { data.SearchData with Pattern = pattern }
        let searchResult =
            if StringUtil.isNullOrEmpty pattern then
                SearchResult.NotFound (  searchData, false)
            else
                match TrackingPointUtil.GetPoint _textView.TextSnapshot data.StartPoint with
                | None -> SearchResult.NotFound (searchData, false)
                | Some point -> _searchService.FindNextPattern searchData.PatternData point _wordNavigator 1

        // Update our state based on the SearchResult.  Only update the view if the 'incsearch'
        // option is set
        if _globalSettings.IncrementalSearch then
            match searchResult with
            | SearchResult.Found (_, span, _) -> _operations.EnsureAtPoint span.Start ViewFlags.Standard
            | SearchResult.NotFound _ -> x.ResetView ()

        let args = SearchResultEventArgs(searchResult)
        _currentSearchUpdated.Trigger x args
        { data with SearchResult = searchResult }

    /// Called when the processing is completed.  Raise the completed event and return
    /// the final SearchResult
    member x.Completed (data : IncrementalSearchData) =
        let data =
            if StringUtil.isNullOrEmpty data.SearchData.Pattern then
                // When the user simply hits Enter on an empty incremental search then
                // we should be re-using the 'LastSearch' value.
                x.RunSearch data _vimData.LastPatternData.Pattern
            else 
                data

        _vimData.LastPatternData <- data.SearchData.PatternData
        _currentSearchCompleted.Trigger x (SearchResultEventArgs(data.SearchResult))
        _historySession <- None
        data.SearchResult

    /// Cancel the search.  Provide the last value searched for
    member x.Cancelled data =
        x.ResetView ()
        _currentSearchCancelled.Trigger x (SearchDataEventArgs(data))
        _historySession <- None

    member x.ResetSearch pattern = 
        match _historySession with
        | Some historySession -> historySession.ResetCommand pattern
        | None -> ()

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _historySession
        member x.WordNavigator = _wordNavigator
        member x.CurrentSearchData = x.CurrentSearchData
        member x.CurrentSearchResult = x.CurrentSearchResult
        member x.Begin kind = x.Begin kind
        member x.ResetSearch pattern = x.ResetSearch pattern
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish


