#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type ServiceSearchData = {

    SearchData: SearchData

    VimRegexOptions: VimRegexOptions

    Navigator: ITextStructureNavigator
}

/// An entry in our cache.  This type must be *very* careful to not hold the ITextBuffer in
/// question in memory.  This is why a WeakReference is used.  We don't want a cached search
/// entry creating a memory leak 
type EditorServiceCacheEntry = { 
    SearchString: string
    Options: FindOptions
    Snapshot: WeakReference<ITextSnapshot>
    Navigator: WeakReference<ITextStructureNavigator>
    StartPosition: int 
    FoundSpan: Span
} with 

    member x.Matches (findData: FindData) (position: int) =
        if findData.FindOptions = x.Options && findData.SearchString = x.SearchString && position = x.StartPosition then
            match x.Snapshot.Target, x.Navigator.Target with
            | Some snapshot, Some navigator ->
                findData.TextSnapshotToSearch = snapshot && findData.TextStructureNavigator = navigator
            | _, _ -> false
        else
            false

    static member Create (findData: FindData) (position: int) (foundSpan: SnapshotSpan) =
        {
            SearchString = findData.SearchString
            Options = findData.FindOptions
            Snapshot = WeakReferenceUtil.Create findData.TextSnapshotToSearch
            Navigator = WeakReferenceUtil.Create findData.TextStructureNavigator
            StartPosition = position
            FoundSpan = foundSpan.Span
        }

// This classes is used for searching are accessed from multiple threads.  In general this 
// is fine because searching an ITextSnapshot is an operation which is mostly readonly.  The 
// lack of mutation eliminates many race condition possibilities.  There are 2 cases we 
// need to be on the watch for within this type
//
//  1. The caching solution does mutate shared state.  All use of this data must occur
//     within a lock(_cacheArray) guard
//  2. The use of _vimRegexOptions.  This is a value which is updated via the UI thread 
//     via a user action that changes any value it depends on.  A single API initiated 
//     search may involve several actual searches of the data.  To be consistent we need
//     to use the same _vimRegexOptions throughout the same search
//
//     This is achieved by wrapping all uses of SearchData with ServiceSearchData at 
//     the API entry points.  

/// Wrapper around the core editor search service that has caching 
[<UsedInBackgroundThread()>]
type internal EditorSearchService 
    (
        _textSearchService: ITextSearchService
    ) =

    /// Vim semantics make repeated searches for the exact same string a very common 
    /// operation.  Incremental search is followed by taggers, next, etc ...  Caching
    /// here can provide a clear win to ensure the searches aren't unnecessarily 
    /// duplicated as searching is a relatively expensive operation.  
    ///
    /// This is used from multiple threads and all access must be inside a 
    /// lock(_cacheArray) guard
    let _cacheArray: EditorServiceCacheEntry option [] = Array.init 10 (fun _ -> None)
    let mutable _cacheArrayIndex = 0

    /// Look for the find information in the cache
    member private x.FindNextInCache (findData: FindData) (position: int) =
        lock (_cacheArray) (fun () -> 
            _cacheArray
            |> SeqUtil.filterToSome
            |> Seq.tryFind (fun cacheEntry -> cacheEntry.Matches findData position)
            |> Option.map (fun cacheEntry -> SnapshotSpan(findData.TextSnapshotToSearch, cacheEntry.FoundSpan)))

    member private x.AddIntoCache (findData: FindData) (position: int) (foundSpan: SnapshotSpan) = 
        lock (_cacheArray) (fun () -> 
            let cacheEntry = EditorServiceCacheEntry.Create findData position foundSpan
            _cacheArray.[_cacheArrayIndex] <- Some cacheEntry
            _cacheArrayIndex <- 
                let index = _cacheArrayIndex + 1
                if index >= _cacheArray.Length then 0 
                else index)

    /// Invoke the search service to find the next match
    member private x.FindNextRaw (findData: FindData) (position: int) =
        try
            _textSearchService.FindNext(position, true, findData) |> NullableUtil.ToOption
        with 
        | :? System.InvalidOperationException ->
            // Happens when we provide an invalid regular expression.  Just return None
            None

    /// Find the next occurrence of FindData in the snapshot at the given position.  This
    /// will always do a search (never consults the cache)
    member private x.FindNextCore (findData: FindData) (position: int) =
        let snapshot = findData.TextSnapshotToSearch
        let lastLine = SnapshotUtil.GetLastLine snapshot
        let isForward = not (Util.IsFlagSet findData.FindOptions FindOptions.SearchReverse)
        let mutable currentPosition = position;
        let mutable result = x.FindNextRaw findData currentPosition
        let mutable needsValidation = true

        let firstMatchPosition =
            match result with
            | None -> position
            | Some span -> span.Start.Position

        // Repeatedly try to find the next match, ignoring phantom lines and spurious matches.
        while needsValidation do
            needsValidation <- false

            match result with
            | None -> ()
            | Some span ->
                let matchPosition = span.Start.Position
                if span.Start = lastLine.Start && SnapshotLineUtil.IsPhantomLine lastLine then

                    // Search again from outside the phantom line.
                    let newPosition =
                        if isForward then
                            0
                        else
                            let point = SnapshotUtil.GetEndPointOfLastLine snapshot
                            point.Position

                    currentPosition <- newPosition
                    result <- x.FindNextRaw findData currentPosition
                    needsValidation <- true

                else

                    // Verify that it actually matches to work around a TextSearchService bug
                    // that can return spurious matches for patterns involving dollar.
                    // Specifically, at the time of this writing, the '$' pattern can
                    // produce a spurious match at the beginning of a nonempty line.
                    // The pattern actually matches if a forward search directly at the match
                    // position returns the same span.
                    let matchesResult =
                        x.GetMatchAtPosition findData matchPosition
                        |> Option.isSome

                    if not matchesResult then
                        let endPoint = SnapshotUtil.GetEndPoint snapshot
                        let newPosition = matchPosition
                        let newPosition =
                            if isForward then
                                if newPosition = endPoint.Position then
                                    0
                                else
                                    newPosition + 1
                            else
                                let newPosition =
                                    if newPosition = 0 then
                                        endPoint.Position
                                    else
                                        newPosition - 1
                                let newPoint = SnapshotPoint(snapshot, newPosition)
                                let newPosition =
                                    if SnapshotPointUtil.IsInsideLineBreak newPoint then
                                        newPosition - 1
                                    else
                                        newPosition
                                newPosition

                        currentPosition <- newPosition
                        result <- x.FindNextRaw findData currentPosition
                        needsValidation <- true

            // Make sure we didn't wrap to the first match.
            if needsValidation then
                match result with
                | None -> ()
                | Some span ->
                    let matchPosition = span.Start.Position
                    if matchPosition = firstMatchPosition then

                        // We can't wrap past the initial position.
                        result <- None


        result

    /// Get the match, if any, at the specified position
    member x.GetMatchAtPosition (findData: FindData) (position: int) =
        let snapshot = findData.TextSnapshotToSearch
        let checkOptions = findData.FindOptions &&& (~~~FindOptions.SearchReverse)
        let checkFindData = FindData(findData.SearchString, snapshot, checkOptions, findData.TextStructureNavigator)
        match x.FindNextRaw checkFindData position with
        | Some matchSpan ->
            if matchSpan.Start.Position = position then
                Some matchSpan
            else
                None
        | None ->
            None

    /// Find the next occurrence of FindData at the given position.  This will use the cache 
    /// if possible
    member x.FindNext (findData: FindData) (position: int) =
        match x.FindNextInCache findData position with
        | Some foundSpan -> Some foundSpan
        | None -> 
            match x.FindNextCore findData position with
            | Some foundSpan -> 
                x.AddIntoCache findData position foundSpan
                Some foundSpan
            | None -> None

[<UsedInBackgroundThread()>]
type internal SearchService 
    (
        _textSearchService: ITextSearchService,
        _globalSettings: IVimGlobalSettings
    ) =

    let _editorSearchService = EditorSearchService(_textSearchService)
    let mutable _vimRegexOptions = VimRegexOptions.Default

    do
        // It's not safe to use IVimGlobalSettings from multiple threads.  It will
        // only raise it's changed event from the main thread.  Use that call back
        // to calculate our new SearhServiceData and store it.  That can be safely
        // used from a background thread since it's a container of appropriate types
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Event.add (fun _ -> _vimRegexOptions <- VimRegexFactory.CreateRegexOptions _globalSettings)

    member x.GetServiceSearchData searchData navigator = 
        { SearchData = searchData; VimRegexOptions = _vimRegexOptions; Navigator = navigator }

    member x.ApplySearchOffsetDataLine (span: SnapshotSpan) count = 
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

    member x.ApplySearchOffsetDataSearch (serviceSearchData: ServiceSearchData) point (patternData: PatternData) = 
        let searchData = SearchData(patternData.Pattern, patternData.Path, true)
        let serviceSearchData = { serviceSearchData with SearchData = searchData }
        match x.FindNextCore serviceSearchData point 1 with
        | SearchResult.Found (_, span, _, _) -> Some span
        | SearchResult.NotFound _ -> None
        | SearchResult.Cancelled _ -> None
        | SearchResult.Error _ -> None

    member x.ApplySearchOffsetData (serviceSearchData: ServiceSearchData) (span: SnapshotSpan): SnapshotSpan option =
        match serviceSearchData.SearchData.Offset with
        | SearchOffsetData.None -> Some span
        | SearchOffsetData.Line count -> x.ApplySearchOffsetDataLine span count |> Some
        | SearchOffsetData.End count -> x.ApplySearchOffsetDataStartEnd (SnapshotSpanUtil.GetLastIncludedPointOrStart span) count |> Some
        | SearchOffsetData.Start count -> x.ApplySearchOffsetDataStartEnd span.Start count |> Some
        | SearchOffsetData.Search patternData -> x.ApplySearchOffsetDataSearch serviceSearchData span.End patternData

    /// This method is called from multiple threads.  Made static to help promote safety
    static member ConvertToFindDataCore (serviceSearchData: ServiceSearchData) snapshot = 

        // First get the text and possible text based options for the pattern.  We special
        // case a search of whole words that is not a regex for efficiency reasons
        let options = serviceSearchData.VimRegexOptions
        let searchData = serviceSearchData.SearchData
        let pattern = searchData.Pattern
        let textResult, textOptions, hadCaseSpecifier = 
            let useRegex () =
                match VimRegexFactory.CreateEx pattern options with
                | VimResult.Error msg -> 
                    VimResult.Error msg, FindOptions.None, false
                | VimResult.Result regex ->
                    let options = FindOptions.UseRegularExpressions
                    let options, hadCaseSpecifier = 
                        match regex.CaseSpecifier with
                        | CaseSpecifier.None -> options, false
                        | CaseSpecifier.IgnoreCase -> options, true
                        | CaseSpecifier.OrdinalCase -> options ||| FindOptions.MatchCase, true
                    VimResult.Result regex.RegexPattern, options, hadCaseSpecifier
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
                    VimResult.Result word, FindOptions.WholeWord, false

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

        let multilineOptions = FindOptions.Multiline

        let options = textOptions ||| caseOptions ||| revOptions ||| multilineOptions

        try
            match textResult with 
            | VimResult.Error msg -> 
                // Happens with a bad regular expression
                VimResult.Error msg
            | VimResult.Result text ->
                // Can throw in cases like having an invalidly formed regex.  Occurs
                // a lot via incremental searching while the user is typing
                FindData(text, snapshot, options, serviceSearchData.Navigator) |> VimResult.Result
        with 
        | :? System.ArgumentException as ex -> VimResult.Error ex.Message

    /// This is the core find function.  It will repeat the FindData search 'count' times.  
    member x.FindCore (serviceSearchData: ServiceSearchData) (findData: FindData) (startPoint: SnapshotPoint) count includeStartPoint: SearchResult =

        let searchData = serviceSearchData.SearchData
        let isForward = searchData.Kind.IsAnyForward 
        let mutable isFirstSearch = true
        let mutable count = max 1 count
        let mutable position = startPoint.Position
        let mutable wrapPosition = position
        let mutable searchResult = SearchResult.NotFound (searchData, false)
        let mutable didWrap = false

        // If we should include the start point, do a preliminary check for
        // a match at the start point and adjust accordingly.
        if includeStartPoint then
            match _editorSearchService.GetMatchAtPosition findData position with
            | Some patternSpan ->
                if count = 1 then
                    searchResult <-
                        match x.ApplySearchOffsetData serviceSearchData patternSpan with
                        | Some span -> SearchResult.Found (searchData, span, patternSpan, didWrap)
                        | None -> SearchResult.NotFound (searchData, true)
                count <- count - 1
            | None ->
                ()

        // Need to adjust the start point. If we are searching forward and the start
        // point is the last character on the line, the first search occurs on
        // the next line.  If we are searching backward, the first search occurs
        // before the start point.  
        if count = 0 then
            ()
        elif isForward then
            let startLine = SnapshotPointUtil.GetContainingLine startPoint
            let endOfLine = SnapshotLineUtil.GetEnd startLine
            if startPoint.Position = endOfLine.Position - 1 then
                let lineBreakLength = SnapshotLineUtil.GetLineBreakLength startLine
                position <- position + 1 + lineBreakLength
                wrapPosition <- position
            else
                wrapPosition <- position + 1
        else
            if position = 0 then
                position <- (SnapshotUtil.GetEndPoint startPoint.Snapshot).Position
                didWrap <- true
            else
                position <- position - 1
            wrapPosition <- position

        // Get the next search position given the search result SnapshotSpan
        let getNextSearchPosition (span: SnapshotSpan) = 
            let endPoint = SnapshotUtil.GetEndPoint span.Snapshot
            if isForward then

                // If the search matched an empty span, we need to
                // advance the starting position.
                if span.Length = 0 then
                    if span.End.Position = endPoint.Position then
                        0
                    else
                        (span.Start.Add 1).Position
                else
                    span.End.Position
            else
                if span.Start.Position = 0 then 
                    endPoint.Position
                else
                    (span.Start.Subtract 1).Position

        while count > 0 do
            match _editorSearchService.FindNext findData position with
            | None -> 
                // The pattern wasn't found so we are done searching 
                searchResult <- SearchResult.NotFound (searchData, false)
                count <- 0
            | Some patternSpan -> 

                // Calculate whether this search is wrapping or not
                didWrap <- 
                    if didWrap then
                        // Once wrapped, always wrapped
                        true
                    elif searchData.Kind.IsAnyForward && patternSpan.Start.Position < wrapPosition then
                        not (isFirstSearch && patternSpan.Start.Position = startPoint.Position)
                    elif searchData.Kind.IsAnyBackward && patternSpan.Start.Position > wrapPosition then 
                        true
                    else
                        false

                if didWrap && not searchData.Kind.IsWrap then
                    // If the search was started without wrapping and a wrap occurred then we are done.  Just
                    // return the bad data
                    searchResult <- SearchResult.NotFound (searchData, true)
                    count <- 0
                elif isForward && isFirstSearch && patternSpan.Start = startPoint then
                    // If the first match is on the search point going forward then it is not counted as a 
                    // match.  Otherwise searches like 'N' would go nowhere.  We just ignore this result and 
                    // continue forward from the end
                    position <- getNextSearchPosition patternSpan
                elif count > 1 then
                    // Need to keep searching.  Just increment the position and count and keep going 
                    position <- getNextSearchPosition patternSpan
                    count <- count - 1
                else
                    count <- count - 1
                    searchResult <-  
                        match x.ApplySearchOffsetData serviceSearchData patternSpan with
                        | Some span -> SearchResult.Found (searchData, span, patternSpan, didWrap)
                        | None -> SearchResult.NotFound (searchData, true)

                isFirstSearch <- false 

        searchResult

    /// Perform a search using the specified ServiceSearchData from the specified point
    member x.FindNextCore (serviceSearchData: ServiceSearchData) (startPoint: SnapshotPoint) count =
        let searchData = serviceSearchData.SearchData
        let snapshot = startPoint.Snapshot
        let includeStartPoint =
            Util.IsFlagSet serviceSearchData.SearchData.Options SearchOptions.IncludeStartPoint
        match SearchService.ConvertToFindDataCore serviceSearchData snapshot with
        | VimResult.Error msg -> SearchResult.Error (searchData, msg)
        | VimResult.Result findData -> x.FindCore serviceSearchData findData startPoint count includeStartPoint

    /// Perform a search using the specified SearchData from the specified point with
    /// the specified navigator
    member x.FindNext searchData startPoint navigator = 
        let searchServiceData = x.GetServiceSearchData searchData navigator
        x.FindNextCore searchServiceData startPoint 1

    /// Search for the given pattern from the specified point
    member x.FindNextPattern searchData startPoint navigator count = 
        let searchServiceData = x.GetServiceSearchData searchData navigator
        x.FindNextCore searchServiceData startPoint count

    interface ISearchService with
        member x.FindNext point searchData navigator = x.FindNext searchData point navigator
        member x.FindNextPattern point searchData navigator count = x.FindNextPattern searchData point navigator count


