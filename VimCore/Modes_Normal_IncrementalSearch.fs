#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type internal IncrementalSearchData = {
    Start : SnapshotPoint;
    Kind : SearchKind; 
    Pattern : string }

type internal IncrementalSearch
    (
        _host : IVimHost,
        _textView : ITextView,
        _settings : VimSettings,
        _searchReplace : ISearchReplace ) =

    let mutable _data : IncrementalSearchData option = None
    let mutable _lastSearch : SearchData option = None 

    /// Get the current search options based off of the stored data
    member private x.SearchReplaceFlags = 
        if _settings.IgnoreCase then SearchReplaceFlags.IgnoreCase
        else SearchReplaceFlags.None

    member private x.Begin kind = 
        let data = {
            Start = ViewUtil.GetCaretPoint _textView;
            Kind = kind;
            Pattern = System.String.Empty }
        _data <- Some data
        _host.UpdateStatus "/"

    /// Process the next key stroke in the incremental search
    member private x.ProcessCore (ki:KeyInput) = 

        let resetView() = 
            match ( _data ) with 
            | Some(data) ->  ViewUtil.MoveCaretToPoint _textView data.Start |> ignore
            | None -> ()

        let doSearch (searchData:SearchData) =
            _host.UpdateStatus ("/" + searchData.Pattern)
            match _searchReplace.FindNextMatch searchData (ViewUtil.GetCaretPoint _textView) with
            | Some span -> ViewUtil.MoveCaretToPoint _textView span.Start |> ignore
            | None -> resetView()

        let handleKeyStroke (previousSearch:SearchData) =
            let pattern = previousSearch.Pattern
            let doSearchWithNewPattern newPattern = 
                let searchData = { previousSearch with Pattern=pattern }
                doSearch searchData
                Some searchData

            match ki.Key with 
            | Key.Enter -> 
                _lastSearch <- Some previousSearch 
                _host.UpdateStatus System.String.Empty
                None
            | Key.Escape -> 
                resetView()
                _host.UpdateStatus System.String.Empty
                None
            | Key.Back -> 
                resetView()
                let pattern = 
                    if pattern.Length = 1 then System.String.Empty
                    else pattern.Substring(0, pattern.Length - 1)
                doSearchWithNewPattern pattern 
            | _ -> 
                let c = ki.Char
                let pattern = pattern + (c.ToString())
                doSearchWithNewPattern pattern

        match _data with 
        | None -> true
        | Some (data) -> 
            let previousSearch = { Pattern = data.Pattern; Kind = data.Kind; Flags = x.SearchReplaceFlags }
            match handleKeyStroke previousSearch with
            | Some(searchData) ->
                _data <- Some { data with Pattern=searchData.Pattern }
                true
            | None ->  false

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
                _host.UpdateStatus Resources.NormalMode_NoPreviousSearch
            elif count > 1 then
                doSearchWithCount searchData (count-1)
            else
                ()

        match _lastSearch with 
        | None -> _host.UpdateStatus Resources.NormalMode_NoPreviousSearch
        | Some(searchData) -> doSearchWithCount searchData count

    interface IIncrementalSearch with
        member x.InSearch = Option.isSome _data
        member x.CurrentSearch = 
            match _data with 
            | Some(data) -> Some { Pattern = data.Pattern; Kind=data.Kind; Flags=x.SearchReplaceFlags }
            | None -> None
        member x.LastSearch 
            with get() = _lastSearch 
            and set value = _lastSearch <- value
        member x.Process ki = x.ProcessCore ki
        member x.Begin kind = x.Begin kind
        member x.FindNextMatch count = x.FindNextMatch count
    
    
