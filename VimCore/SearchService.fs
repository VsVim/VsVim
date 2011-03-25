#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService 
    (
        _search : ITextSearchService,
        _settings : IVimGlobalSettings
    ) = 

    let _factory = VimRegexFactory(_settings)

    /// Convert the Vim SearchData to the editor FindData structure
    member x.ConvertToFindData (searchData : SearchData) snapshot wordNavigator =

        // First get the text and possible text based options for the pattern.  We special
        // case a search of whole words that is not a regex for efficiency reasons
        let pattern = searchData.Pattern
        let text, textOptions = 
            let useRegex () =
                let text = _factory.Create pattern |> Option.map (fun p -> p.RegexPattern)
                text, FindOptions.UseRegularExpressions
            match PatternUtil.GetUnderlyingWholeWord pattern with
            | None -> 
                useRegex ()
            | Some word ->
                // If it's just letters and numbers then it's a straight text search. 
                let any = Seq.exists (fun c -> not (CharUtil.IsLetterOrDigit c || CharUtil.IsWhiteSpace c)) word
                if any then 
                    useRegex()
                else
                    Some word, FindOptions.WholeWord

        // Get the options related to case
        let caseOptions = 
            let searchOptions = searchData.Options
            if Util.IsFlagSet searchOptions SearchOptions.ConsiderIgnoreCase && _settings.IgnoreCase then
                let hasUpper () = pattern |> Seq.filter CharUtil.IsLetter |> Seq.filter CharUtil.IsUpper |> SeqUtil.isNotEmpty
                if Util.IsFlagSet searchOptions SearchOptions.ConsiderSmartCase && _settings.SmartCase && hasUpper() then FindOptions.MatchCase
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

    interface ISearchService with
        member x.FindNext searchData point nav = x.FindNext searchData point nav
        member x.FindNextMultiple searchData point nav count = x.FindNextMultiple searchData point nav count


