#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal IncrementalSearchData = {
    Start : ITrackingPoint;
    SearchData : SearchData; 
    SearchResult : SearchResult;
    }

type internal IncrementalSearch
    (
        _textView : ITextView,
        _settings : IVimLocalSettings,
        _search : ITextSearchService,
        _navigator : ITextStructureNavigator ) =

    let mutable _data : IncrementalSearchData option = None
    let mutable _lastSearch = { Pattern = System.String.Empty; Kind = SearchKind.ForwardWithWrap; Options = FindOptions.None }
    let _currentSearchUpdated = Event<SearchData * SearchResult>()
    let _currentSearchCompleted = Event<SearchData * SearchResult>()
    let _currentSearchCancelled = Event<SearchData>()

    /// Get the current search options based off of the stored data
    member private x.CaculateFindOptions kind = 
        let options = if not _settings.GlobalSettings.IgnoreCase then FindOptions.MatchCase else FindOptions.None
        let options = if SearchKindUtil.IsBackward kind then options ||| FindOptions.SearchReverse else options
        options

    member private x.Begin kind = 
        let searchData = { Pattern = System.String.Empty; Kind = kind; Options = x.CaculateFindOptions kind}
        let pos = (ViewUtil.GetCaretPoint _textView).Position
        let start = _textView.TextSnapshot.CreateTrackingPoint(pos, PointTrackingMode.Negative)
        let data = {
            Start = start
            SearchData  = searchData
            SearchResult = SearchNotFound }
        _data <- Some data
        _currentSearchUpdated.Trigger (data.SearchData,SearchNotFound)

    /// Process the next key stroke in the incremental search
    member private x.ProcessCore (ki:KeyInput) = 

        match _data with 
        | None -> SearchComplete
        | Some (data) -> 

            let resetView() = 
                let point = data.Start.GetPoint _textView.TextSnapshot
                ViewUtil.MoveCaretToPoint _textView point |> ignore

            let doSearch (searchData:SearchData) =
                let findData = FindData(searchData.Pattern, _textView.TextSnapshot, searchData.Options, _navigator)
                let point = ViewUtil.GetCaretPoint _textView
                let ret= _search.FindNext(point.Position, SearchKindUtil.IsWrap searchData.Kind, findData)
                if ret.HasValue then 
                    let span = ret.Value
                    ViewUtil.MoveCaretToPoint _textView span.Start |> ignore
                    _currentSearchUpdated.Trigger (searchData, SearchFound(span)) 
                    _data <- Some { data with SearchData = searchData; SearchResult = SearchFound(span) }
                else
                    resetView()
                    _currentSearchUpdated.Trigger (searchData,SearchNotFound)
                    _data <- Some { data with SearchData = searchData; SearchResult = SearchNotFound }

            let previousSearch = data.SearchData
            let pattern = previousSearch.Pattern
            let doSearchWithNewPattern newPattern = 
                let searchData = { previousSearch with Pattern=newPattern}
                doSearch searchData

            match ki.Key with 
            | VimKey.EnterKey -> 
                _data <- None
                _lastSearch <- previousSearch
                _currentSearchCompleted.Trigger (data.SearchData,data.SearchResult)
                SearchComplete
            | VimKey.EscapeKey -> 
                resetView()
                _data <- None
                _currentSearchCancelled.Trigger data.SearchData
                SearchCancelled
            | VimKey.BackKey -> 
                resetView()
                let pattern = 
                    if pattern.Length = 1 then System.String.Empty
                    else pattern.Substring(0, pattern.Length - 1)
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
            | true -> TssUtil.GetNextPointWithWrap current
            | false -> TssUtil.GetPreviousPointWithWrap current

        let doSearch (searchData:SearchData) = 
            let caret = ViewUtil.GetCaretPoint _textView
            let next = getNextPoint caret searchData.Kind
            let findData = FindData(searchData.Pattern, _textView.TextSnapshot, searchData.Options, _navigator)
            let nullable = _search.FindNext(next.Position, SearchKindUtil.IsForward(searchData.Kind), findData)
            if nullable.HasValue then
                ViewUtil.MoveCaretToPoint _textView nullable.Value.Start |> ignore
            nullable.HasValue

        let rec doSearchWithCount searchData count = 
            if not (doSearch searchData) then false
            elif count > 1 then doSearchWithCount searchData (count-1)
            else true

        if System.String.IsNullOrEmpty(_lastSearch.Pattern) then false
        else doSearchWithCount _lastSearch count

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _data
        member x.CurrentSearch = 
            match _data with 
            | Some(data) -> Some data.SearchData
            | None -> None
        member x.LastSearch 
            with get() = _lastSearch 
            and set value = _lastSearch <- value
        member x.Process ki = x.ProcessCore ki
        member x.Begin kind = x.Begin kind
        member x.FindNextMatch count = x.FindNextMatch count
        [<CLIEvent>]
        member x.CurrentSearchUpdated = _currentSearchUpdated.Publish
        [<CLIEvent>]
        member x.CurrentSearchCompleted = _currentSearchCompleted.Publish 
        [<CLIEvent>]
        member x.CurrentSearchCancelled = _currentSearchCancelled.Publish


    
    
