namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil

type internal IncrementalSearchData = {
    /// The point from which the search needs to occur 
    StartPoint : ITrackingPoint;

    SearchData : SearchData;
    SearchResult : SearchResult;
}

type internal IncrementalSearch
    (
        _operations : Modes.ICommonOperations,
        _settings : IVimLocalSettings,
        _navigator : ITextStructureNavigator,
        _search : ISearchService,
        _statusUtil : IStatusUtil,
        _vimData : IVimData) =

    let _textView = _operations.TextView
    let mutable _data : IncrementalSearchData option = None
    let _searchOptions = SearchOptions.ConsiderIgnoreCase ||| SearchOptions.ConsiderSmartCase
    let _currentSearchUpdated = Event<SearchData * SearchResult>()
    let _currentSearchCompleted = Event<SearchData * SearchResult>()
    let _currentSearchCancelled = Event<SearchData>()

    member x.Begin kind = 
        let caret = TextViewUtil.GetCaretPoint _textView
        let start = Util.GetSearchPoint kind caret
        let data = {
            StartPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
            SearchData = {Text = SearchText.Pattern(StringUtil.empty); Kind = kind; Options = _searchOptions}
            SearchResult = SearchNotFound 
        }
        _data <- Some data

        // Raise the event
        _currentSearchUpdated.Trigger (data.SearchData,SearchNotFound)

    /// Process the next key stroke in the incremental search
    member x.Process (ki:KeyInput) = 

        match _data with 
        | None -> SearchNotStarted
        | Some (data) -> 

            let resetView () = _operations.EnsureCaretOnScreenAndTextExpanded()

            let doSearch pattern = 
                let searchData = {data.SearchData with Text=SearchText.Pattern(pattern)}
                let ret =
                    if StringUtil.isNullOrEmpty pattern then None
                    else
                        match TrackingPointUtil.GetPoint _textView.TextSnapshot data.StartPoint with
                        | None -> None
                        | Some(point) ->
                            let options = SearchOptions.ConsiderIgnoreCase ||| SearchOptions.ConsiderIgnoreCase
                            _search.FindNext searchData point _navigator 

                match ret with
                | Some(span) ->
                    _operations.EnsurePointOnScreenAndTextExpanded span.Start
                    _currentSearchUpdated.Trigger (searchData, SearchFound(span)) 
                    _data <- Some { data with SearchData = searchData; SearchResult = SearchFound(span) }
                | None ->
                    resetView()
                    _currentSearchUpdated.Trigger (searchData,SearchNotFound)
                    _data <- Some { data with SearchData = searchData; SearchResult = SearchNotFound }

            let oldSearchData = data.SearchData
            let doSearchWithNewPattern newPattern =  doSearch newPattern

            let cancelSearch = 
                _data <- None
                _currentSearchCancelled.Trigger oldSearchData
                SearchCancelled

            if ki = KeyInputUtil.EnterKey then

                // Need to update the status if the search wrapped around
                match data.SearchResult, TrackingPointUtil.GetPoint _textView.TextSnapshot data.StartPoint with
                | SearchResult.SearchFound(span), Some(point) ->
                    if data.SearchData.Kind = SearchKind.ForwardWithWrap && span.Start.Position < point.Position then
                        _statusUtil.OnStatus Resources.Common_SearchForwardWrapped
                    elif data.SearchData.Kind = SearchKind.BackwardWithWrap && span.Start.Position > point.Position then
                        _statusUtil.OnStatus Resources.Common_SearchBackwardWrapped 
                | _ -> 
                    ()

                _data <- None
                _vimData.LastSearchData <- oldSearchData
                _currentSearchCompleted.Trigger(oldSearchData, data.SearchResult)
                SearchComplete(data.SearchData, data.SearchResult)
            elif ki = KeyInputUtil.EscapeKey then
                resetView()
                cancelSearch
            elif ki.Key = VimKey.Back then
                resetView()
                let pattern = data.SearchData.Text.RawText
                match pattern.Length with
                | 0 -> cancelSearch
                | _ -> 
                    let pattern = pattern.Substring(0, pattern.Length - 1)
                    doSearchWithNewPattern pattern
                    SearchNeedMore
            else
                let c = ki.Char
                let pattern = data.SearchData.Text.RawText + (c.ToString())
                doSearchWithNewPattern pattern
                SearchNeedMore

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _data
        member x.SearchService = _search
        member x.WordNavigator = _navigator
        member x.CurrentSearch = 
            match _data with 
            | Some(data) -> Some data.SearchData
            | None -> None
        member x.Process ki = x.Process ki
        member x.Begin kind = x.Begin kind
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish



