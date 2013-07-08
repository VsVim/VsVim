#light
namespace Vim
open System
open Microsoft.VisualStudio.Text

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type internal TextDirection =
    | Neither
    | Left 
    | Right

module internal TextUtil =

    let IsBigWordChar c = not (Char.IsWhiteSpace(c))
    let IsNormalWordChar c = Char.IsLetterOrDigit(c) || c = '_'
    let IsNormalWordOtherChar c = (not (IsNormalWordChar c)) && (not (Char.IsWhiteSpace(c)))

    let IsWordChar kind c =
        match kind with
        | WordKind.BigWord -> IsBigWordChar c
        | WordKind.NormalWord -> IsNormalWordChar c 

    /// Get the word spans on the input in the given direction
    let GetWordSpans kind path input =

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
        let limit = (StringUtil.length input) - 1
        let wordsForward = 
            0
            |> Seq.unfold (fun index -> 

                // Get the start index of the word and the predicate to keep matching
                // the word
                let rec getWord index = 
                    if index <= limit then
                        match isWordStart input.[index] with
                        | Some predicate -> 
                            // Now to find the end of the word
                            let endIndex = 
                                let rec inner index = 
                                    if index > limit || not (predicate input.[index]) then index
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
        | Path.Forward -> wordsForward
        | Path.Backward -> wordsForward |> List.ofSeq |> List.rev |> Seq.ofList


    let rec private GetNormalWordPredicate input index dir = 
        let nextIndex index = 
            match dir with
            | TextDirection.Left -> index - 1
            | TextDirection.Right -> index + 1
            | TextDirection.Neither -> -1

        match StringUtil.charAtOption index input with 
            | None -> IsNormalWordChar
            | Some c -> 
                if IsNormalWordChar c then
                    IsNormalWordChar
                else if IsNormalWordOtherChar  c then
                    IsNormalWordOtherChar
                else
                    GetNormalWordPredicate input (nextIndex index) dir

    // Get the predicate function for matching the particular WordKind value 
    // that is passed in as the first parameter
    let GetWordPred kind input index dir =
        match kind with 
            | WordKind.NormalWord -> GetNormalWordPredicate input index dir 
            | WordKind.BigWord -> IsBigWordChar

    // Find the span of the current word
    let private FindCurrentSpanCore (input:string) index pred = 
        let rec goWhile i =
            match i < input.Length with
            | false -> Span(index, input.Length - index)
            | true -> 
                match pred input.[i] with 
                | true -> goWhile (i+1)
                | false -> Span(index, i-index)
        match (StringUtil.isValidIndex index input) && (pred input.[index]) with 
        | true -> Some (goWhile (index+1))
        | false -> None

    // Find the full span of the current word
    let private FindFullSpanCore (input:string) index pred =
        let rec goBack i = 
            match (StringUtil.isValidIndex (i-1) input) && pred (input.[i-1]) with
            | true -> goBack (i-1)
            | false -> i
        match (StringUtil.isValidIndex index input) && (pred input.[index]) with
            | true -> FindCurrentSpanCore input (goBack index) pred
            | false -> None
    
    // Move back to the start of the previous word
    let rec private FindPreviousSpanCore (input:string) index pred  =
        let rec findNotStartOnWord i =
            match (pred input.[i],i) with
            | (true,_) -> FindFullSpanCore input i pred
            | (false,0) -> None
            | (false,_) -> findNotStartOnWord (i-1)
        let findStartOnWord =
            match (index>0 && pred input.[index-1],index) with 
            | (true,_) -> FindFullSpanCore input index pred  // Middle of word, get the start
            | (false,0) -> None
            | (false,_) -> findNotStartOnWord (index-1)
        match (index,StringUtil.isValidIndex index input) with 
            | (0,_) -> None
            | (_,false) -> None
            | _ -> // Valid non-zero index
                match pred input.[index] with 
                    | true -> findStartOnWord 
                    | false -> findNotStartOnWord index
    
    let rec private FindNextSpanCore (input:string) index (pred:char->bool) = 
        let filter (i,c) = 
            match pred c with 
            | true -> Some i
            | false -> None
        let found = 
            input 
                |> Seq.mapi (fun i c -> i,c)
                |> Seq.skip index
                |> Seq.skipWhile (fun (i,c) -> pred c)
                |> Seq.tryPick filter
        match found with 
        | Some i -> FindCurrentSpanCore input i pred
        | None -> None
    
    let FindSpanCore spanFunc kind input index dir =
        let pred = GetWordPred kind input index dir 
        spanFunc input index pred 
            
    let private FindWordCore (input:string) (span:option<Span>) =
        match span with 
        | None -> String.Empty
        | Some v -> input.Substring(v.Start, v.Length)

    let FindCurrentWordSpan kind input index = 
        let f = FindCurrentSpanCore
        FindSpanCore f kind input index TextDirection.Right
    let FindFullWordSpan kind input index = FindSpanCore FindFullSpanCore kind input index TextDirection.Right
    let FindPreviousWordSpan kind input index = FindSpanCore FindPreviousSpanCore kind input index TextDirection.Left
    let FindNextWordSpan kind input index = FindSpanCore FindNextSpanCore kind input index TextDirection.Right

