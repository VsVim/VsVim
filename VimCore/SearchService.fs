#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService 
    (
        _search : ITextSearchService,
        _globalSettings : IVimGlobalSettings
    ) = 

    let _factory = VimRegexFactory(_globalSettings)

    /// Convert the Vim SearchData to the editor FindData structure
    member x.ConvertToFindData (searchData : SearchData) snapshot wordNavigator =

        // First get the text and possible text based options for the pattern.  We special
        // case a search of whole words that is not a regex for efficiency reasons
        let pattern = searchData.Pattern
        let text, textOptions = 
            let useRegex () =
                match _factory.Create pattern with
                | None -> 
                    None, FindOptions.None
                | Some regex ->
                    let options = FindOptions.UseRegularExpressions
                    let options = 
                        match regex.CaseSpecifier with
                        | CaseSpecifier.None -> options
                        | CaseSpecifier.IgnoreCase -> options
                        | CaseSpecifier.OrdinalCase -> options ||| FindOptions.MatchCase
                    Some regex.RegexPattern, options
            match PatternUtil.GetUnderlyingWholeWord pattern with
            | None -> 
                useRegex ()
            | Some word ->
                // If possible we'd like to avoid the overhead of a regular expression here.  In general
                // if the pattern is jusnt letters and numbers then we can do a non-regex search on the 
                // buffer.  
                let isSimplePattern = Seq.forall (fun c -> CharUtil.IsLetterOrDigit c || CharUtil.IsBlank c) word

                // There is one exception to this rule though.  There is a bug in the Dev10 implementation
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
                    Some word, FindOptions.WholeWord

        // Get the options related to case
        let caseOptions = 
            let searchOptions = searchData.Options
            if Util.IsFlagSet searchOptions SearchOptions.ConsiderIgnoreCase && _globalSettings.IgnoreCase then
                let hasUpper () = pattern |> Seq.filter CharUtil.IsLetter |> Seq.filter CharUtil.IsUpper |> SeqUtil.isNotEmpty
                if Util.IsFlagSet searchOptions SearchOptions.ConsiderSmartCase && _globalSettings.SmartCase && hasUpper() then FindOptions.MatchCase
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

    member x.FindNextMultiple (searchData : SearchData) startPoint nav count =

        let snapshot = SnapshotPointUtil.GetSnapshot startPoint 
        match x.ConvertToFindData searchData snapshot nav with
        | None ->
            // Can't convert to a FindData so no way to search
            SearchResult.NotFound (searchData, false)
        | Some findData -> 

            // Recursive loop to perform the search "count" times
            let rec doFind findData count position didWrap = 

                let result = 
                    try
                        _search.FindNext(position, true, findData) |> NullableUtil.toOption
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

    member x.FindNext searchData point nav = x.FindNextMultiple searchData point nav 1

    /// Search for the given pattern from the specified point. 
    member x.FindNextPattern (patternData : PatternData) startPoint wordNavigator count = 

        // Find the real place to search.  When going forward we should start after
        // the caret and before should start before. This prevents the text 
        // under the caret from being the first match
        let snapshot = SnapshotPointUtil.GetSnapshot startPoint
        let startPoint, didStartWrap = Util.GetSearchPointAndWrap patternData.Path startPoint

        // Go ahead and run the search
        let searchData = SearchData.OfPatternData patternData _globalSettings.WrapScan
        let result = x.FindNextMultiple searchData startPoint wordNavigator count

        // Need to fudge the SearchResult here to account for the possible wrap the 
        // search start incurred when calculating the actual 'startPoint' value.  If it 
        // wrapped we need to get the SearchResult to account for that so we can 
        // process the messages properly and give back the appropriate value
        if didStartWrap then 
            match result with
            | SearchResult.Found (searchData, span, didWrap) ->
                if _globalSettings.WrapScan then
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

    interface ISearchService with
        member x.FindNext searchData point nav = x.FindNext searchData point nav
        member x.FindNextMultiple searchData point nav count = x.FindNextMultiple searchData point nav count
        member x.FindNextPattern patternData point nav count = x.FindNextPattern patternData point nav count


