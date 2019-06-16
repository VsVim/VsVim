#light
namespace Vim
open System
open System.Diagnostics
open Microsoft.VisualStudio.Text

module TextUtil =

    let IsBigWordChar c = not (Char.IsWhiteSpace(c))
    let IsNormalWordChar c = Char.IsLetterOrDigit(c) || c = '_'
    let IsBigWordOnlyChar c = (not (IsNormalWordChar c)) && (not (Char.IsWhiteSpace(c)))

    let IsWordChar wordKind c =
        match wordKind with
        | WordKind.BigWord -> IsBigWordChar c
        | WordKind.NormalWord -> IsNormalWordChar c 

    /// Get the word predicate given the current char and word wordKind. The returned predicate 
    /// will match all valid characters in the word based on the initial char. 
    let GetWordPredicateNonWhitespace wordKind c =
        Debug.Assert(not (CharUtil.IsWhiteSpace c))
        match wordKind with
        | WordKind.NormalWord -> 
            if IsNormalWordChar c then IsNormalWordChar
            else IsBigWordOnlyChar
        | WordKind.BigWord -> IsBigWordChar

    let GetWordPredicate wordKind c =
        if CharUtil.IsWhiteSpace c then None
        else GetWordPredicateNonWhitespace wordKind c |> Some

    /// Get the word spans on the text in the given direction
    let GetWordSpans wordKind path text =
        // Build up a sequence to get the words in the line
        let limit = (StringUtil.Length text) - 1
        let wordsForward = 
            0
            |> Seq.unfold (fun index -> 
                // Get the start index of the word and the predicate to keep matching
                // the word
                let rec getWord index = 
                    if index <= limit then
                        match GetWordPredicate wordKind text.[index] with
                        | Some predicate -> 
                            // Now to find the end of the word
                            let endIndex = 
                                let rec inner index = 
                                    if index > limit || not (predicate text.[index]) then index
                                    else inner (index + 1)
                                inner (index + 1)
                            Some (Span.FromBounds(index, endIndex), endIndex)
                        | None -> 
                            // Go to the next index
                            getWord (index + 1)
                    else
                        None
                getWord index)

        // Now return the actual sequence 
        match path with
        | SearchPath.Forward -> wordsForward
        | SearchPath.Backward -> wordsForward |> List.ofSeq |> List.rev |> Seq.ofList

    let private GetFullWordSpanCore (text: string) index predicate =
        Debug.Assert(not (CharUtil.IsWhiteSpace text.[index]))
        let mutable startIndex = index
        let mutable index = index
        while startIndex > 0 && predicate text.[startIndex - 1] do
            startIndex <- startIndex - 1
        while index < text.Length && predicate text.[index] do
            index <- index + 1
        let span = Span.FromBounds(startIndex, index)
        span

    let private GetFullWordSpan wordKind (text: string) index =
        match GetWordPredicate wordKind text.[index] with
        | Some predicate -> GetFullWordSpanCore text index predicate |> Some
        | None -> None

    let GetPreviousWordSpan wordKind (text: string) index = 
        let mutable index = index - 1
        while index >= 0 && CharUtil.IsWhiteSpace text.[index] do
            index <- index - 1

        if index < 0 then
            None
        else
            let predicate = GetWordPredicateNonWhitespace wordKind text.[index]
            GetFullWordSpanCore text index predicate |> Some

    let GetNextWordSpan wordKind (text: string) index = 
        let mutable index = index 
        while index < text.Length && CharUtil.IsWhiteSpace text.[index] do
            index <- index + 1
        
        if index >= text.Length then
            None
        else
            let predicate = GetWordPredicateNonWhitespace wordKind text.[index]
            GetFullWordSpanCore text index predicate |> Some
