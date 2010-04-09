#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open NullableUtil

type internal IncrementalSearchData = {
    Start : ITrackingPoint;
    Kind : SearchKind;
    Pattern : string
    SearchResult : SearchResult;
    }

type internal IncrementalSearch
    (
        _textView : ITextView,
        _settings : IVimLocalSettings,
        _navigator : ITextStructureNavigator,
        _search : ISearchService) =

    let mutable _data : IncrementalSearchData option = None
    let _currentSearchUpdated = Event<SearchData * SearchResult>()
    let _currentSearchCompleted = Event<SearchData * SearchResult>()
    let _currentSearchCancelled = Event<SearchData>()

    member private x.Begin kind = 
        let pos = (ViewUtil.GetCaretPoint _textView).Position
        let start = _textView.TextSnapshot.CreateTrackingPoint(pos, PointTrackingMode.Negative)
        let data = {
            Start = start
            Pattern = StringUtil.empty
            Kind = kind
            SearchResult = SearchNotFound }
        _data <- Some data

        // Raise the event
        let searchData = _search.CreateSearchData data.Pattern data.Kind 
        _currentSearchUpdated.Trigger (searchData,SearchNotFound)

    /// Process the next key stroke in the incremental search
    member private x.ProcessCore (ki:KeyInput) = 

        match _data with 
        | None -> SearchComplete
        | Some (data) -> 

            let resetView() = 
                let point = data.Start.GetPoint _textView.TextSnapshot
                ViewUtil.MoveCaretToPoint _textView point |> ignore

            let doSearch pattern = 
                let searchData = _search.CreateSearchData pattern data.Kind
                let ret =
                    if StringUtil.isNullOrEmpty pattern then None
                    else
                        let point = ViewUtil.GetCaretPoint _textView
                        _search.FindNextPattern pattern point data.Kind _navigator

                match ret with
                | Some(span) ->
                    ViewUtil.MoveCaretToPoint _textView span.Start |> ignore
                    _currentSearchUpdated.Trigger (searchData, SearchFound(span)) 
                    _data <- Some { data with Pattern = pattern; SearchResult = SearchFound(span) }
                | None ->
                    resetView()
                    _currentSearchUpdated.Trigger (searchData,SearchNotFound)
                    _data <- Some { data with Pattern = pattern; SearchResult = SearchNotFound }

            let pattern = data.Pattern
            let doSearchWithNewPattern newPattern =  doSearch newPattern

            let cancelSearch = 
                _data <- None
                _currentSearchCancelled.Trigger (_search.CreateSearchData data.Pattern data.Kind)
                SearchCancelled

            match ki.Key with 
            | VimKey.EnterKey -> 
                let searchData = _search.CreateSearchData data.Pattern data.Kind
                _data <- None
                _search.LastSearch <- searchData
                _currentSearchCompleted.Trigger (searchData,data.SearchResult)
                SearchComplete
            | VimKey.EscapeKey -> 
                resetView()
                cancelSearch
            | VimKey.BackKey -> 
                resetView()
                match pattern.Length with
                | 0 -> cancelSearch
                | _ -> 
                    let pattern = pattern.Substring(0, pattern.Length - 1)
                    doSearchWithNewPattern pattern
                    SearchNeedMore
            | _ -> 
                let c = ki.Char
                let pattern = pattern + (c.ToString())
                doSearchWithNewPattern pattern
                SearchNeedMore


    member private x.FindNextMatch (count:int) =
        let getNextPoint current kind = 
            match SearchKindUtil.IsForward kind with 
            | true -> SnapshotPointUtil.GetNextPointWithWrap current
            | false -> SnapshotPointUtil.GetPreviousPointWithWrap current

        let doSearch (searchData:SearchData) = 
            let caret = ViewUtil.GetCaretPoint _textView
            let next = getNextPoint caret searchData.Kind
            match _search.FindNextResult searchData next _navigator with
            | Some(span) -> 
                ViewUtil.MoveCaretToPoint _textView span.Start |> ignore
                true
            | None -> false

        let rec doSearchWithCount searchData count = 
            if not (doSearch searchData) then false
            elif count > 1 then doSearchWithCount searchData (count-1)
            else true

        if System.String.IsNullOrEmpty(_search.LastSearch.Pattern) then false
        else doSearchWithCount _search.LastSearch count

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _data
        member x.SearchService = _search
        member x.WordNavigator = _navigator
        member x.CurrentSearch = 
            match _data with 
            | Some(data) -> Some (_search.CreateSearchData data.Pattern data.Kind)
            | None -> None
        member x.Process ki = x.ProcessCore ki
        member x.Begin kind = x.Begin kind
        member x.FindNextMatch count = x.FindNextMatch count
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish


    
    
