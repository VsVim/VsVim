#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type internal IncrementalSearchData = {
    Start : SnapshotPoint;
    SearchData : SearchData; }

type internal IncrementalSearch
    (
        _host : IVimHost,
        _textView : ITextView,
        _settings : VimSettings,
        _searchReplace : ISearchReplace ) =

    let mutable _data : IncrementalSearchData option = None
    let mutable _lastSearch = { Pattern = System.String.Empty; Kind = SearchKind.ForwardWithWrap; Flags = SearchReplaceFlags.None }
    let _currentSearchSpanChanged = Event<SnapshotSpan option>()

    /// Get the current search options based off of the stored data
    member private x.SearchReplaceFlags = 
        if _settings.IgnoreCase then SearchReplaceFlags.IgnoreCase
        else SearchReplaceFlags.None

    member private x.Begin kind = 
        let searchData = { Pattern = System.String.Empty; Kind = kind; Flags = x.SearchReplaceFlags }
        let data = {
            Start = ViewUtil.GetCaretPoint _textView;
            SearchData  = searchData }
        _data <- Some data
        _host.UpdateStatus "/"
        _currentSearchSpanChanged.Trigger None

    /// Process the next key stroke in the incremental search
    member private x.ProcessCore (ki:KeyInput) = 

        match _data with 
        | None -> SearchComplete
        | Some (data) -> 

            let resetView() = ViewUtil.MoveCaretToPoint _textView data.Start |> ignore

            let doSearch (searchData:SearchData) =
                _host.UpdateStatus ("/" + searchData.Pattern)
                let opt = _searchReplace.FindNextMatch searchData (ViewUtil.GetCaretPoint _textView) 
                match opt with
                | Some span -> ViewUtil.MoveCaretToPoint _textView span.Start |> ignore
                | None -> resetView()
                _currentSearchSpanChanged.Trigger opt
                _data <- Some { data with SearchData = searchData }

            let previousSearch = data.SearchData
            let pattern = previousSearch.Pattern
            let doSearchWithNewPattern newPattern = 
                let searchData = { previousSearch with Pattern=newPattern}
                doSearch searchData

            match ki.Key with 
            | Key.Enter -> 
                _lastSearch <- previousSearch
                _host.UpdateStatus System.String.Empty
                _currentSearchSpanChanged.Trigger None
                SearchComplete
            | Key.Escape -> 
                resetView()
                _host.UpdateStatus System.String.Empty
                _currentSearchSpanChanged.Trigger None
                SearchCanceled
            | Key.Back -> 
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
            match _searchReplace.FindNextMatch searchData next with
            | None -> false
            | Some(span) -> 
                ViewUtil.MoveCaretToPoint _textView span.Start |> ignore
                true

        let rec doSearchWithCount searchData count = 
            if not (doSearch searchData) then
                _host.UpdateStatus (Resources.NormalMode_PatternNotFound searchData.Pattern)
            elif count > 1 then
                doSearchWithCount searchData (count-1)
            else
                _host.UpdateStatus ("/" + searchData.Pattern)

        if System.String.IsNullOrEmpty(_lastSearch.Pattern) then 
            _host.UpdateStatus Resources.NormalMode_NoPreviousSearch
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
        member x.CurrentSearchSpanChanged = _currentSearchSpanChanged.Publish
    
    
