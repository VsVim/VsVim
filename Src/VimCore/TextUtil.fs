#light
namespace Vim
open System
open Microsoft.VisualStudio.Text

module TextUtil =

    let IsBigWordChar c = not (Char.IsWhiteSpace(c))
    let IsNormalWordChar c = Char.IsLetterOrDigit(c) || c = '_'
    let IsNormalWordOtherChar c = (not (IsNormalWordChar c)) && (not (Char.IsWhiteSpace(c)))

    let IsWordChar kind c =
        match kind with
        | WordKind.BigWord -> IsBigWordChar c
        | WordKind.NormalWord -> IsNormalWordChar c 

    /// Get the word spans on the text in the given direction
    let GetWordSpans kind path text =

        // Function to determine if this is a word start and if return a predicate to
        // match the remainder of the word
        let isWordStart c = 
            match kind with
            | WordKind.NormalWord -> 
                if IsNormalWordChar c then Some IsNormalWordChar
                elif IsNormalWordOtherChar c then Some IsNormalWordOtherChar
                else None
            | WordKind.BigWord -> 
                if IsBigWordChar c then Some IsBigWordChar
                else None

        // Build up a sequence to get the words in the line
        let limit = (StringUtil.Length text) - 1
        let wordsForward = 
            0
            |> Seq.unfold (fun index -> 

                // Get the start index of the word and the predicate to keep matching
                // the word
                let rec getWord index = 
                    if index <= limit then
                        match isWordStart text.[index] with
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


    let rec private GetNormalWordPredicate text index searchPath = 
        let nextIndex index = 
            match searchPath with
            | SearchPath.Backward -> index - 1
            | SearchPath.Forward -> index + 1

        match StringUtil.CharAtOption index text with 
            | None -> IsNormalWordChar
            | Some c -> 
                if IsNormalWordChar c then
                    IsNormalWordChar
                else if IsNormalWordOtherChar  c then
                    IsNormalWordOtherChar
                else
                    GetNormalWordPredicate text (nextIndex index) searchPath

    // Get the predicate function for matching the particular WordKind value 
    // that is passed in as the first parameter
    let GetWordPred kind text index dir =
        match kind with 
            | WordKind.NormalWord -> GetNormalWordPredicate text index dir 
            | WordKind.BigWord -> IsBigWordChar

    // Find the span of the current word
    let private FindCurrentSpanCore (text: string) index pred = 
        let rec goWhile i =
            match i < text.Length with
            | false -> Span(index, text.Length - index)
            | true -> 
                match pred text.[i] with 
                | true -> goWhile (i + 1)
                | false -> Span(index, i - index)
        match (StringUtil.IsValidIndex index text) && (pred text.[index]) with 
        | true -> Some (goWhile (index + 1))
        | false -> None

    // Find the full span of the current word
    let private FindFullSpanCore (text: string) index pred =
        let rec goBack i = 
            match (StringUtil.IsValidIndex (i - 1) text) && pred (text.[i - 1]) with
            | true -> goBack (i - 1)
            | false -> i
        match (StringUtil.IsValidIndex index text) && (pred text.[index]) with
            | true -> FindCurrentSpanCore text (goBack index) pred
            | false -> None
    
    // Move back to the start of the previous word
    let rec private FindPreviousSpanCore (text: string) index pred  =
        let rec findNotStartOnWord i =
            match (pred text.[i],i) with
            | (true, _) -> FindFullSpanCore text i pred
            | (false, 0) -> None
            | (false, _) -> findNotStartOnWord (i-1)
        let findStartOnWord =
            match (index>0 && pred text.[index-1],index) with 
            | (true, _) -> FindFullSpanCore text index pred  // Middle of word, get the start
            | (false, 0) -> None
            | (false, _) -> findNotStartOnWord (index-1)
        match (index,StringUtil.IsValidIndex index text) with 
            | (0, _) -> None
            | (_, false) -> None
            | _ -> // Valid non-zero index
                match pred text.[index] with 
                    | true -> findStartOnWord 
                    | false -> findNotStartOnWord index
    
    let rec private FindNextSpanCore (text: string) index (pred: char->bool) = 
        let filter (i,c) = 
            match pred c with 
            | true -> Some i
            | false -> None
        let found = 
            text 
                |> Seq.mapi (fun i c -> i,c)
                |> Seq.skip index
                |> Seq.skipWhile (fun (i, c) -> pred c)
                |> Seq.tryPick filter
        match found with 
        | Some i -> FindCurrentSpanCore text i pred
        | None -> None
    
    let FindSpanCore spanFunc kind text index dir =
        let pred = GetWordPred kind text index dir 
        spanFunc text index pred 
            
    let GetCurrentWordSpan kind text index = 
        let f = FindCurrentSpanCore
        FindSpanCore f kind text index SearchPath.Forward
    let GetFullWordSpan kind text index = FindSpanCore FindFullSpanCore kind text index SearchPath.Forward
    let GetPreviousWordSpan kind text index = FindSpanCore FindPreviousSpanCore kind text index SearchPath.Backward
    let GetNextWordSpan kind text index = FindSpanCore FindNextSpanCore kind text index SearchPath.Forward

