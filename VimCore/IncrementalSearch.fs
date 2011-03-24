namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining
open NullableUtil

/// Records our current history search information
[<RequireQualifiedAccess>]
type HistoryState =
    | Empty 
    | Index of (string list) * int

type IncrementalSearchData = {
    /// The point from which the search needs to occur 
    StartPoint : ITrackingPoint;

    /// Most recent result of the search
    SearchResult : SearchResult

    /// This is the history state
    HistoryState : HistoryState
} with 

    member x.SearchData = x.SearchResult.SearchData

type internal IncrementalSearch
    (
        _operations : Modes.ICommonOperations,
        _settings : IVimLocalSettings,
        _navigator : ITextStructureNavigator,
        _search : ISearchService,
        _statusUtil : IStatusUtil,
        _vimData : IVimData
    ) =

    let _globalSettings = _settings.GlobalSettings
    let _textView = _operations.TextView
    let mutable _data : IncrementalSearchData option = None
    let _searchOptions = SearchOptions.ConsiderIgnoreCase ||| SearchOptions.ConsiderSmartCase
    let _currentSearchUpdated = Event<SearchResult>()
    let _currentSearchCompleted = Event<SearchResult>()
    let _currentSearchCancelled = Event<SearchData>()

    /// There is a big gap between the behavior and documentation of key mapping for an 
    /// incremental search operation.  The documentation properly documents the language
    /// mapping in "help language-mapping" and 'help imsearch'.  But it doesn't document
    /// that command mapping should used when 'imsearch' doesn't apply although it's
    /// the practice in implementation
    ///
    /// TODO: actually implement the 'imsearch' option and fix this
    member x.RemapMode = KeyRemapMode.Command

    /// Add the pattern to the incremental search history
    member x.AddToHistory (searchData : SearchData) = 
        let pattern = searchData.Text.RawText
        if not (StringUtil.isNullOrEmpty pattern) then
            let list = 
                _vimData.IncrementalSearchHistory
                |> Seq.filter (fun s -> not (StringUtil.isEqual s pattern))
                |> List.ofSeq
            _vimData.IncrementalSearchHistory <- pattern :: list

    member x.Begin kind = 
        let caret = TextViewUtil.GetCaretPoint _textView
        let start = Util.GetSearchPoint kind caret
        let searchData = {Text = SearchText.Pattern(StringUtil.empty); Kind = kind; Options = _searchOptions}
        let data = {
            StartPoint = start.Snapshot.CreateTrackingPoint(start.Position, PointTrackingMode.Negative)
            SearchResult = SearchResult.NotFound searchData
            HistoryState = HistoryState.Empty
        }

        _data <- Some data

        // Raise the event
        _currentSearchUpdated.Trigger data.SearchResult

        // There is a discrepancy between the documentation and implementation of key mapping
        // for searching.  If you look under "help language-mapping" it lists searching as one
        // of the items to which it should apply.  However in practice this isn't true.  Instead
        // command mode dictates the mappings for search
        { KeyRemapMode = Some x.RemapMode; BindFunction = x.Process data }

    /// Cancel the search.  Provide the last value searched for
    member x.CancelSearch oldData =
        x.ResetView ()
        _currentSearchCancelled.Trigger oldData
        BindResult.Cancelled

    // Reset the view to it's original state.  We should only be doing this if the
    // 'incsearch' option is set.  Otherwise the view shouldn't change during an 
    // incremental search
    member x.ResetView () =
        if _globalSettings.IncrementalSearch then
             _operations.EnsureCaretOnScreenAndTextExpanded()

    /// Run a history scroll at the specified index
    member x.RunHistoryScroll (data : IncrementalSearchData) (historyList : string list) index =
        if index < 0 || index >= historyList.Length then
            // Make sure we are searching at a valid index
            _operations.Beep()
            data
        else
            // Update the search to be this specific item
            let pattern = List.nth historyList index
            let text = SearchText.Pattern pattern
            let data = { data with HistoryState = HistoryState.Index (historyList, index) }
            x.RunSearch data text

    /// Run the search for the specified text.  Returns the new IncrementalSearchData resulting
    /// from the search
    member x.RunSearch (data : IncrementalSearchData) text =
        let searchData = { data.SearchData with Text = text }

        // Get the SearchResult value for the new text
        let searchResult =
            if StringUtil.isNullOrEmpty text.RawText then 
                // Searching for empty data resets the HistoryState.  Viewable by typing the following
                // sequence a, <Up>, <Back>, <Up>
                let data = { data with HistoryState = HistoryState.Empty }
                SearchResult.NotFound searchData
            else
                match TrackingPointUtil.GetPoint _textView.TextSnapshot data.StartPoint with
                | None ->
                    SearchResult.NotFound searchData
                | Some point ->
                    let options = SearchOptions.ConsiderIgnoreCase ||| SearchOptions.ConsiderIgnoreCase
                    _search.FindNext searchData point _navigator 

        // Update our state based on the SearchResult.  Only update the view if the 'incsearch'
        // option is set
        if _globalSettings.IncrementalSearch then
            match searchResult with
            | SearchResult.Found (_, span, _) -> _operations.EnsurePointOnScreenAndTextExpanded span.Start
            | SearchResult.NotFound _ -> x.ResetView ()

        _currentSearchUpdated.Trigger searchResult
        { data with SearchResult = searchResult }

    /// Process the next key stroke in the incremental search
    member x.Process (data : IncrementalSearchData) (keyInput : KeyInput) = 

        let remapMode = Some x.RemapMode
        let oldSearchData = data.SearchData

        let processCore () = 
            if keyInput = KeyInputUtil.EnterKey then
    
                let data =
                    if StringUtil.isNullOrEmpty data.SearchData.Text.RawText then
                        // When the user simply hits Enter on an empty incremental search then
                        // we should be re-using the 'LastSearch' value.
                        x.RunSearch data _vimData.LastSearchData.Text
                    else 
                        data
                x.AddToHistory data.SearchData
    
                // Need to update the status if the search wrapped around
                match data.SearchResult with
                | SearchResult.Found (_, _, didWrap) ->
                    if didWrap then
                        let message = 
                            if data.SearchData.Kind.IsAnyForward then Resources.Common_SearchForwardWrapped
                            else Resources.Common_SearchBackwardWrapped
                        _statusUtil.OnWarning message
                | SearchResult.NotFound _ ->
                    ()
    
                _vimData.LastSearchData <- oldSearchData
                _currentSearchCompleted.Trigger data.SearchResult
                None, BindResult.Complete data.SearchResult
            elif keyInput = KeyInputUtil.EscapeKey then
                // Escape cancels the current search.  It does update the history though
                x.AddToHistory oldSearchData
                None, x.CancelSearch oldSearchData
            elif keyInput.Key = VimKey.Back then
                let pattern = data.SearchData.Text.RawText
                match pattern.Length with
                | 0 -> 
                    None, x.CancelSearch oldSearchData
                | _ -> 
                    let pattern = pattern.Substring(0, pattern.Length - 1)
                    let text = SearchText.Pattern pattern
                    let data = x.RunSearch data text
                    Some data, BindResult<_>.CreateNeedMoreInput remapMode (x.Process data)
            elif keyInput.Key = VimKey.Up then
                x.ProcessUp data
            elif keyInput.Key = VimKey.Down then
                x.ProcessDown data
            else
                let c = keyInput.Char
                let pattern = data.SearchData.Text.RawText + (c.ToString())
                let text = SearchText.Pattern pattern
                let data = x.RunSearch data text
                Some data, BindResult<_>.CreateNeedMoreInput remapMode (x.Process data)

        let data, bindResult = processCore ()
        _data <- data
        bindResult

    /// Process the up key during an incremental search
    member x.ProcessUp (data : IncrementalSearchData) =
        let data = 
            match data.HistoryState with
            | HistoryState.Empty ->
                let pattern = data.SearchData.Text.RawText
                let list = 
                    if not (StringUtil.isNullOrEmpty data.SearchData.Text.RawText) then
                        _vimData.IncrementalSearchHistory 
                        |> Seq.filter (fun value -> StringUtil.startsWith pattern value)
                        |> List.ofSeq
                    else
                        _vimData.IncrementalSearchHistory
                x.RunHistoryScroll data list 0
            | HistoryState.Index (list, index) -> 
                x.RunHistoryScroll data list (index + 1)
        Some data, BindResult<_>.CreateNeedMoreInput (Some x.RemapMode) (x.Process data)

    /// Process the down key during an incremental search
    member x.ProcessDown (data : IncrementalSearchData) =
        let data = 
            match data.HistoryState with
            | HistoryState.Empty ->
                _operations.Beep()
                data
            | HistoryState.Index (list, index) -> 
                if index = 0 then
                    // Reset back to empty
                    let text = SearchText.Pattern ""
                    let data = { data with HistoryState = HistoryState.Empty }
                    x.RunSearch data text
                else
                    x.RunHistoryScroll data list (index - 1)

        Some data, BindResult<_>.CreateNeedMoreInput (Some x.RemapMode) (x.Process data)

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



