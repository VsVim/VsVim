#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type ServiceSearchData = {

    SearchData : SearchData

    VimRegexOptions : VimRegexOptions

    Navigator : ITextStructureNavigator
}

/// An entry in our cache.  This type must be *very* careful to not hold the ITextBuffer in
/// question in memory.  This is why a WeakReference is used.  We don't want a cached search
/// entry creating a memory leak 
type ServiceCacheEntry = { 
    SearchString : string
    Options : FindOptions
    EditorData : WeakReference<ITextSnapshot * ITextStructureNavigator>
    StartPosition : int 
    FoundSpan : Span
} with 

    member x.Matches (findData : FindData) (position : int) =
        if findData.FindOptions = x.Options && findData.SearchString = x.SearchString && position = x.StartPosition then
            match x.EditorData.Target with
            | Some (snapshot, navigator) -> findData.TextSnapshotToSearch = snapshot && findData.TextStructureNavigator = navigator
            | None -> false
        else
            false

    static member Create (findData : FindData) (position : int) (foundSpan : SnapshotSpan) =
        let editorData = (findData.TextSnapshotToSearch, findData.TextStructureNavigator)
        {
            SearchString = findData.SearchString
            Options = findData.FindOptions
            EditorData = WeakReferenceUtil.Create editorData
            StartPosition = position
            FoundSpan = foundSpan.Span
        }

/// This class is used from multiple threads.  In general this is fine because searching 
/// an ITextSnapshot is an operation which is mostly readonly.  The lack of mutatino eliminates
/// many race condition possibilities.  There are 2 cases we need to be on the watch for
/// within this type
///
///  1. The caching solution does mutate shared state.  All use of this data must occur
///     within a lock(_cacheArray) guard
///  2. The use of _vimRegexOptions.  This is a value which is updated via the UI thread 
///     via a user action that changes any value it depends on.  A single API initiated 
///     search may involve several actual searches of the data.  To be consistent we need
///     to use the same _vimRegexOptions throughout the same search
///
///     This is achieved by wrapping all uses of SearchData with ServiceSearchData at 
///     the API entry points.  
[<UsedInBackgroundThread()>]
type internal SearchService 
    (
        _textSearchService : ITextSearchService,
        _globalSettings : IVimGlobalSettings
    ) =

    let mutable _vimRegexOptions = VimRegexOptions.Default

    /// Vim semantics make repeated searches for the exact same string a very common 
    /// operation.  Incremental search is followed by taggers, next, etc ...  Caching
    /// here can provide a clear win to ensure the searches aren't unnecessarily 
    /// duplicated as searching is a relatively expensive operation.  
    ///
    /// This is used from multiple threads and all access must be inside a 
    /// lock(_cacheArray) guard
    let _cacheArray : ServiceCacheEntry option [] = Array.init 10 (fun _ -> None)
    let mutable _cacheArrayIndex = 0

    do
        // It's not safe to use IVimGlobalSettings from multiple threads.  It will
        // only raise it's changed event from the main thread.  Use that call back
        // to calcualet our new SearhServiceData and store it.  That can be safely
        // used from a background thread since it's a container of appropriate types
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Event.add (fun _ -> _vimRegexOptions <- VimRegexFactory.CreateRegexOptions _globalSettings)

    member x.GetServiceSearchData searchData navigator = 
        { SearchData = searchData; VimRegexOptions = _vimRegexOptions; Navigator = navigator }

    member x.ApplySearchOffsetDataLine (span : SnapshotSpan) count = 
        let snapshot = span.Snapshot
        let startLine = SnapshotPointUtil.GetContainingLine span.Start
        let number = startLine.LineNumber + count
        let number = 
            if number < 0 then 0
            elif number >= snapshot.LineCount then snapshot.LineCount - 1
            else number
        let line = snapshot.GetLineFromLineNumber number
        SnapshotSpan(line.Start, 1)

    member x.ApplySearchOffsetDataStartEnd startPoint count = 
        let point = SnapshotPointUtil.GetRelativePoint startPoint count true
        SnapshotSpan(point, 1)

    member x.ApplySearchOffsetDataSearch (serviceSearchData : ServiceSearchData) point (patternData : PatternData) = 
        let searchData = SearchData(patternData.Pattern, patternData.Path, true)
        let serviceSearchData = { serviceSearchData with SearchData = searchData }
        match x.FindNextMultipleCore serviceSearchData point 1 with
        | SearchResult.Found (_, span, _, _) -> Some span
        | SearchResult.NotFound _ -> None

    member x.ApplySearchOffsetData (serviceSearchData : ServiceSearchData) (span : SnapshotSpan) : SnapshotSpan option =
        let snapshot = span.Snapshot
        match serviceSearchData.SearchData.Offset with
        | SearchOffsetData.None -> Some span
        | SearchOffsetData.Line count -> x.ApplySearchOffsetDataLine span count |> Some
        | SearchOffsetData.End count -> x.ApplySearchOffsetDataStartEnd (SnapshotSpanUtil.GetLastIncludedPointOrStart span) count |> Some
        | SearchOffsetData.Start count -> x.ApplySearchOffsetDataStartEnd span.Start count |> Some
        | SearchOffsetData.Search patternData -> x.ApplySearchOffsetDataSearch serviceSearchData span.End patternData

    /// This method is callabla from multiple threads.  Made static to help promote safety
    member x.ConvertToFindDataCore (serviceSearchData : ServiceSearchData) snapshot = 

        // First get the text and possible text based options for the pattern.  We special
        // case a search of whole words that is not a regex for efficiency reasons
        let options = serviceSearchData.VimRegexOptions
        let searchData = serviceSearchData.SearchData
        let pattern = searchData.Pattern
        let text, textOptions, hadCaseSpecifier = 
            let useRegex () =
                match VimRegexFactory.Create pattern options with
                | None -> 
                    None, FindOptions.None, false
                | Some regex ->
                    let options = FindOptions.UseRegularExpressions
                    let options, hadCaseSpecifier = 
                        match regex.CaseSpecifier with
                        | CaseSpecifier.None -> options, false
                        | CaseSpecifier.IgnoreCase -> options, true
                        | CaseSpecifier.OrdinalCase -> options ||| FindOptions.MatchCase, true
                    Some regex.RegexPattern, options, hadCaseSpecifier
            match PatternUtil.GetUnderlyingWholeWord pattern with
            | None -> 
                useRegex ()
            | Some word ->
                // If possible we'd like to avoid the overhead of a regular expression here.  In general
                // if the pattern is just letters and numbers then we can do a non-regex search on the 
                // buffer.  
                let isSimplePattern = Seq.forall (fun c -> CharUtil.IsLetterOrDigit c || CharUtil.IsBlank c) word

                // There is one exception to this rule though.  There is a bug in the Vs 2010 implementation
                // of ITextSearchService that causes it to hit an infinite loop if the following conditions
                // are met
                //
                //  1. Search is for a whole word
                //  2. Search is backwards 
                //  3. Search string is 1 or 2 characters long
                //  4. Any line above the search point starts with the search string but doesn't match
                //     it's contents
                // 
                // If 1-3 is true then we force a regex in order to avoid this bug
                let isBugPattern = 
                    searchData.Kind.IsAnyBackward &&
                    String.length word <= 2

                if isBugPattern || not isSimplePattern then
                    useRegex()
                else
                    Some word, FindOptions.WholeWord, false

        // Get the options related to case
        let caseOptions = 
            let searchOptions = searchData.Options
            let ignoreCase = Util.IsFlagSet options VimRegexOptions.IgnoreCase
            let smartCase = Util.IsFlagSet options VimRegexOptions.SmartCase
            if hadCaseSpecifier then
                // Case specifiers beat out any other options
                FindOptions.None
            elif Util.IsFlagSet searchOptions SearchOptions.ConsiderIgnoreCase && ignoreCase then
                let hasUpper () = pattern |> Seq.filter CharUtil.IsLetter |> Seq.filter CharUtil.IsUpper |> SeqUtil.isNotEmpty
                if Util.IsFlagSet searchOptions SearchOptions.ConsiderSmartCase && smartCase && hasUpper() then FindOptions.MatchCase
                else FindOptions.None
            else 
                FindOptions.MatchCase
        let revOptions = if searchData.Kind.IsAnyBackward then FindOptions.SearchReverse else FindOptions.None

        let options = textOptions ||| caseOptions ||| revOptions

        try
            match text with 
            | None ->
                // Happens with a bad regular expression
                None
            | Some text ->
                // Can throw in cases like having an invalidly formed regex.  Occurs
                // a lot via incremental searching while the user is typing
                FindData(text, snapshot, options, serviceSearchData.Navigator) |> Some
        with 
        | :? System.ArgumentException -> None

    member x.DoFindNext (findData : FindData) (position : int) =
        match x.DoFindNextInCache findData position with
        | Some foundSpan -> Some foundSpan
        | None -> 
            match x.DoFindNextCore findData position with
            | Some foundSpan -> 
                x.AddIntoCache findData position foundSpan
                Some foundSpan
            | None -> None

    member x.DoFindNextCore (findData : FindData) (position : int) =
        try
            _textSearchService.FindNext(position, true, findData) |> NullableUtil.ToOption
        with 
        | :? System.InvalidOperationException ->
            // Happens when we provide an invalid regular expression.  Just return None
            None

    member x.DoFindNextInCache (findData : FindData) (position : int) =
        lock (_cacheArray) (fun () -> 
            _cacheArray
            |> SeqUtil.filterToSome
            |> Seq.tryFind (fun cacheEntry -> cacheEntry.Matches findData position)
            |> Option.map (fun cacheEntry -> SnapshotSpan(findData.TextSnapshotToSearch, cacheEntry.FoundSpan)))

    member x.AddIntoCache (findData : FindData) (position : int) (foundSpan : SnapshotSpan) = 
        lock (_cacheArray) (fun () -> 
            let cacheEntry = ServiceCacheEntry.Create findData position foundSpan
            _cacheArray.[_cacheArrayIndex] <- Some cacheEntry
            _cacheArrayIndex <- 
                let index = _cacheArrayIndex + 1
                if index >= _cacheArray.Length then 0 
                else index)

    member x.FindNextMultipleCore (serviceSearchData : ServiceSearchData) (startPoint : SnapshotPoint) count : SearchResult =

        let snapshot = SnapshotPointUtil.GetSnapshot startPoint 
        let searchData = serviceSearchData.SearchData
        match x.ConvertToFindDataCore serviceSearchData snapshot with
        | None ->
            // Can't convert to a FindData so no way to search
            SearchResult.NotFound (searchData, false)
        | Some findData -> 

            // Recursive loop to perform the search "count" times
            let rec doFind findData count position didWrap = 
                let result = x.DoFindNext findData position

                // Calculate whether this search is wrapping or not
                let didWrap = 
                    match result with 
                    | Some span ->
                        if didWrap then
                            // Once wrapped, always wrapped
                            true
                        elif searchData.Kind.IsAnyForward && span.Start.Position < startPoint.Position then
                            true
                        elif searchData.Kind.IsAnyBackward && span.Start.Position > startPoint.Position then 
                            true
                        else
                            false
                    | None -> 
                        didWrap

                if didWrap && not searchData.Kind.IsWrap then
                    // If the search was started without wrapping and a wrap occurred then we are done.  Just
                    // return the bad data
                    SearchResult.NotFound (searchData, true)
                else
                    match result, count > 1 with
                    | Some patternSpan, false ->
                        match x.ApplySearchOffsetData serviceSearchData patternSpan with
                        | Some span -> SearchResult.Found (searchData, span, patternSpan, didWrap)
                        | None -> SearchResult.NotFound (searchData, true)
                    | Some span, true -> 
                        // Need to keep searching.  Get the next point to search for.  We always wrap 
                        // when searching so that we can give back accurate NotFound data.  
                        let point = 
                            if searchData.Kind.IsAnyForward then
                                span.End
                            elif span.Start.Position = 0 then 
                                SnapshotUtil.GetEndPoint snapshot
                            else
                                span.Start.Subtract 1
                        doFind findData (count-1) point.Position didWrap
                    | _ -> 
                        SearchResult.NotFound (searchData, false)

            let count = max 1 count
            let pos = startPoint.Position
            doFind findData count pos false

    member x.FindNextPatternCore (serviceSearchData : ServiceSearchData) startPoint count =

        // Find the real place to search.  When going forward we should start after
        // the caret and before should start before. This prevents the text 
        // under the caret from being the first match
        let snapshot = SnapshotPointUtil.GetSnapshot startPoint
        let searchData = serviceSearchData.SearchData
        let startPoint, didStartWrap = CommonUtil.GetSearchPointAndWrap searchData.Path startPoint

        // Go ahead and run the search
        let wrapScan = searchData.Kind.IsWrap
        let result = x.FindNextMultipleCore serviceSearchData startPoint count 

        // Need to fudge the SearchResult here to account for the possible wrap the 
        // search start incurred when calculating the actual 'startPoint' value.  If it 
        // wrapped we need to get the SearchResult to account for that so we can 
        // process the messages properly and give back the appropriate value
        if didStartWrap then 
            match result with
            | SearchResult.Found (searchData, span, patternSpan, didWrap) ->
                if wrapScan then
                    // If wrapping is enabled then we just need to update the 'didWrap' state
                    SearchResult.Found (searchData, span, patternSpan, true)
                else
                    // Wrapping is not enabled so change the result but it would've been present
                    // if wrapping was enabled
                    SearchResult.NotFound (searchData, true)
            | SearchResult.NotFound _ ->
                // No change
                result
        else
            // Nothing to fudge if the start didn't wrap 
            result

    member x.FindNextMultiple searchData startPoint navigator count =
        let serviceSearchData = x.GetServiceSearchData searchData navigator
        x.FindNextMultipleCore serviceSearchData startPoint count 

    member x.FindNext searchData point navigator = 
        x.FindNextMultiple searchData point navigator 1

    /// Search for the given pattern from the specified point. 
    member x.FindNextPattern searchData startPoint navigator count = 
        let searchServiceData = x.GetServiceSearchData searchData navigator
        x.FindNextPatternCore searchServiceData startPoint count

    interface ISearchService with
        member x.FindNext point searchData navigator = x.FindNext searchData point navigator
        member x.FindNextMultiple point searchData navigator count = x.FindNextMultiple searchData point navigator count
        member x.FindNextPattern point searchData navigator count = x.FindNextPattern searchData point navigator count


