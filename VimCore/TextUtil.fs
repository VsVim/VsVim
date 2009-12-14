#light
namespace Vim
open System
open Microsoft.VisualStudio.Text

type internal Direction =
    | Neither = 0
    | Left = 1
    | Right = 2

module internal TextUtil =

    let private IsBigWordChar c = not (Char.IsWhiteSpace(c))
    let private IsWordChar c = Char.IsLetterOrDigit(c) || c = '_'
    let private IsWordOtherChar c = (not (IsWordChar c)) && (not (Char.IsWhiteSpace(c)))
    
    let rec private GetNormalWordPredicate input index dir = 
        let nextIndex index = 
            match dir with
                | Direction.Left -> index - 1
                | Direction.Right -> index + 1
                | Direction.Neither -> -1
                | _ -> failwith "Invalid Enum"
        
        match StringUtil.CharAtOption input index with 
            | None -> IsWordChar
            | Some c -> 
                if IsWordChar c then
                    IsWordChar
                else if IsWordOtherChar c then
                    IsWordOtherChar
                else
                    GetNormalWordPredicate input (nextIndex index) dir
    
    // Get the predicate function for matching the particular WordKind value 
    // that is passed in as the first parameter
    let GetWordPred kind input index dir =
        match kind with 
            | WordKind.NormalWord -> GetNormalWordPredicate input index dir 
            | WordKind.BigWord -> IsBigWordChar
            | _ -> failwith "Invalid enum value" 
    
    // Find the span of the current word
    let private FindCurrentSpanCore (input:string) index pred = 
        let rec goWhile i =
            match i < input.Length with
                | false -> Span(index, input.Length - index)
                | true -> 
                    match pred input.[i] with 
                        | true -> goWhile (i+1)
                        | false -> Span(index, i-index)
        match (StringUtil.IsValidIndex input index) && (pred input.[index]) with 
            | true -> Some (goWhile (index+1))
            | false -> None
            
    // Find the full span of the current word
    let private FindFullSpanCore (input:string) index pred =
        let rec goBack i = 
            match (StringUtil.IsValidIndex input (i-1)) && pred (input.[i-1]) with
                | true -> goBack (i-1)
                | false -> i
        match (StringUtil.IsValidIndex input index) && (pred input.[index]) with
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
        match (index,StringUtil.IsValidIndex input index) with 
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
        FindSpanCore f kind input index Direction.Right
    let FindCurrentWord kind input index = 
        let span = FindCurrentWordSpan kind input index
        FindWordCore input span
    let FindFullWordSpan kind input index = FindSpanCore FindFullSpanCore kind input index Direction.Right
    let FindFullWord kind input index = 
        let span = FindFullWordSpan kind input index
        FindWordCore input span
    let FindPreviousWordSpan kind input index = FindSpanCore FindPreviousSpanCore kind input index Direction.Left
    let FindPreviousWord kind input index = 
        let span = FindPreviousWordSpan kind input index
        FindWordCore input span
    let FindNextWordSpan kind input index = FindSpanCore FindNextSpanCore kind input index Direction.Right
    let FindNextWord kind input index = 
        let span = FindNextWordSpan kind input index
        FindWordCore input span
    
