#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

/// This is the data which is passed between threads the SearhService is 
/// used on.
type SearchServiceData = {

    TextSearchService : ITextSearchService

    WrapScan : bool

    VimRegexOptions : VimRegexOptions
}

/// This class is useable from multiple threads.  Need to be careful because 
/// IVimGlobalSettings is not safe to use from multiple threads. 
[<UsedInBackgroundThread()>]
type internal SearchService 
    (
        _textSearchService : ITextSearchService,
        _globalSettings : IVimGlobalSettings
    ) =

    let mutable _searchServiceData = {
        TextSearchService = _textSearchService
        WrapScan = _globalSettings.WrapScan
        VimRegexOptions = VimRegexFactory.CreateRegexOptions _globalSettings
    }

    do
        // It's not safe to use IVimGlobalSettings from multiple threads.  It will
        // only raise it's changed event from the main thread.  Use that call back
        // to calcualet our new SearhServiceData and store it.  That can be safely
        // used from a background thread since it's a container of appropriate types
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Event.add (fun _ -> 
            _searchServiceData <- {
                TextSearchService = _textSearchService
                WrapScan = _globalSettings.WrapScan
                VimRegexOptions = VimRegexFactory.CreateRegexOptions _globalSettings
            })

    /// This method is callabla from multiple threads.  Made static to help promote safety
    [<UsedInBackgroundThread()>]
    static member ConvertToFindDataCore (searchServiceData : SearchServiceData) (searchData : SearchData) snapshot wordNavigator =

        // First get the text and possible text based options for the pattern.  We special
        // case a search of whole words that is not a regex for efficiency reasons
        let options = searchServiceData.VimRegexOptions
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
                FindData(text, snapshot, options, wordNavigator) |> Some
        with 
        | :? System.ArgumentException -> None

    /// This method is callabla from multiple threads.  Made static to help promote safety
    [<UsedInBackgroundThread()>]
    static member FindNextMultipleCore (searchServiceData : SearchServiceData) (searchData : SearchData) startPoint nav count =

        let textSearchService = searchServiceData.TextSearchService
        let snapshot = SnapshotPointUtil.GetSnapshot startPoint 
        match SearchService.ConvertToFindDataCore searchServiceData searchData snapshot nav with
        | None ->
            // Can't convert to a FindData so no way to search
            SearchResult.NotFound (searchData, false)
        | Some findData -> 

            // Recursive loop to perform the search "count" times
            let rec doFind findData count position didWrap = 

                let result = 
                    try
                        textSearchService.FindNext(position, true, findData) |> NullableUtil.ToOption
                    with 
                    | :? System.InvalidOperationException ->
                        // Happens when we provide an invalid regular expression.  Just return None
                        None

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
                    | Some span, false ->
                        SearchResult.Found (searchData, span, didWrap)
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

    /// Search for the given pattern from the specified point. 
    [<UsedInBackgroundThread()>]
    static member FindNextPatternCore (searchServiceData : SearchServiceData) patternData startPoint wordNavigator count =

        // Find the real place to search.  When going forward we should start after
        // the caret and before should start before. This prevents the text 
        // under the caret from being the first match
        let snapshot = SnapshotPointUtil.GetSnapshot startPoint
        let startPoint, didStartWrap = CommonUtil.GetSearchPointAndWrap patternData.Path startPoint

        // Go ahead and run the search
        let wrapScan = searchServiceData.WrapScan
        let searchData = SearchData.OfPatternData patternData wrapScan
        let result = SearchService.FindNextMultipleCore searchServiceData searchData startPoint wordNavigator count 

        // Need to fudge the SearchResult here to account for the possible wrap the 
        // search start incurred when calculating the actual 'startPoint' value.  If it 
        // wrapped we need to get the SearchResult to account for that so we can 
        // process the messages properly and give back the appropriate value
        if didStartWrap then 
            match result with
            | SearchResult.Found (searchData, span, didWrap) ->
                if wrapScan then
                    // If wrapping is enabled then we just need to update the 'didWrap' state
                    SearchResult.Found (searchData, span, true)
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

    /// Convert the Vim SearchData to the editor FindData structure
    member x.ConvertToFindData (searchData : SearchData) snapshot wordNavigator =
        SearchService.ConvertToFindDataCore _searchServiceData searchData snapshot wordNavigator

    member x.FindNextMultiple searchData startPoint wordNavigator count =
        SearchService.FindNextMultipleCore _searchServiceData searchData startPoint wordNavigator count 

    member x.FindNext searchData point nav = x.FindNextMultiple searchData point nav 1

    /// Search for the given pattern from the specified point. 
    member x.FindNextPattern patternData startPoint wordNavigator count = 
        SearchService.FindNextPatternCore _searchServiceData patternData startPoint wordNavigator count

    interface ISearchService with
        member x.FindNext searchData point nav = x.FindNext searchData point nav
        member x.FindNextMultiple searchData point nav count = x.FindNextMultiple searchData point nav count
        member x.FindNextPattern patternData point nav count = x.FindNextPattern patternData point nav count


