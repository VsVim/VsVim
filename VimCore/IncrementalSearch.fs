namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil

type internal IncrementalSearchData = {
    /// The point from which the search needs to occur 
    StartPoint : ITrackingPoint;

    /// Most recent result of the search
    SearchResult : SearchResult
} with 

    member x.SearchData = x.SearchResult.SearchData

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
    let _currentSearchUpdated = Event<SearchResult>()
    let _currentSearchCompleted = Event<SearchResult>()
    let _currentSearchCancelled = Event<SearchData>()

    member x.Begin kind = 
        let caret = TextViewUtil.GetCaretPoint _textView
        let start = Util.GetSearchPoint kind caret
        let searchData = {Text = SearchText.Pattern(StringUtil.empty); Kind = kind; Options = _searchOptions}
        let data = {
            StartPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
            SearchResult = SearchResult.SearchNotFound searchData
        }
        _data <- Some data

        // Raise the event
        _currentSearchUpdated.Trigger data.SearchResult

        // There is a discrepancy between the documentation and implementation of key mapping
        // for searching.  If you look under "help language-mapping" it lists searching as one
        // of the items to which it should apply.  However in practice this isn't true.  Instead
        // command mode dictates the mappings for search
        { KeyRemapMode = Some KeyRemapMode.Command; BindFunction = x.Process }

    /// Process the next key stroke in the incremental search
    member x.Process (ki:KeyInput) = 

        let remapMode = Some KeyRemapMode.Command
        match _data with 
        | None -> 
            BindResult<_>.CreateNeedMoreInput remapMode x.Process
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
                    let result = SearchResult.SearchFound(searchData, span)
                    _operations.EnsurePointOnScreenAndTextExpanded span.Start
                    _currentSearchUpdated.Trigger result
                    _data <- Some { data with SearchResult = result }
                | None ->
                    resetView()
                    let result = SearchResult.SearchNotFound searchData
                    _currentSearchUpdated.Trigger result
                    _data <- Some { data with SearchResult = result }

            let oldSearchData = data.SearchData
            let doSearchWithNewPattern newPattern =  doSearch newPattern

            let cancelSearch () = 
                _data <- None
                _currentSearchCancelled.Trigger oldSearchData
                BindResult.Cancelled

            if ki = KeyInputUtil.EnterKey then

                // Need to update the status if the search wrapped around
                match data.SearchResult, TrackingPointUtil.GetPoint _textView.TextSnapshot data.StartPoint with
                | SearchResult.SearchFound(_, span), Some point ->
                    if data.SearchData.Kind = SearchKind.ForwardWithWrap && span.Start.Position < point.Position then
                        _statusUtil.OnStatus Resources.Common_SearchForwardWrapped
                    elif data.SearchData.Kind = SearchKind.BackwardWithWrap && span.Start.Position > point.Position then
                        _statusUtil.OnStatus Resources.Common_SearchBackwardWrapped 
                | _ -> 
                    ()

                _data <- None
                _vimData.LastSearchData <- oldSearchData
                _currentSearchCompleted.Trigger data.SearchResult
                BindResult.Complete data.SearchResult
            elif ki = KeyInputUtil.EscapeKey then
                resetView()
                cancelSearch()
            elif ki.Key = VimKey.Back then
                resetView()
                let pattern = data.SearchData.Text.RawText
                match pattern.Length with
                | 0 -> cancelSearch()
                | _ -> 
                    let pattern = pattern.Substring(0, pattern.Length - 1)
                    doSearchWithNewPattern pattern
                    BindResult<_>.CreateNeedMoreInput remapMode x.Process
            else
                let c = ki.Char
                let pattern = data.SearchData.Text.RawText + (c.ToString())
                doSearchWithNewPattern pattern
                BindResult<_>.CreateNeedMoreInput remapMode x.Process

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _data
        member x.SearchService = _search
        member x.WordNavigator = _navigator
        member x.CurrentSearch = 
            match _data with 
            | Some(data) -> Some data.SearchData
            | None -> None
        member x.Begin kind = x.Begin kind
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish



