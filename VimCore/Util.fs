#light

namespace Vim

[<AbstractClass>]
type internal ToggleHandler() =
    abstract Add : unit -> unit
    abstract Remove : unit -> unit
   
    static member Create<'T> (source:System.IObservable<'T>) (func: 'T -> unit) = ToggleHandler<'T>(source,func)
    static member Empty = 
        { new ToggleHandler() with 
            member x.Add() = ()
            member x.Remove() = () }

and internal ToggleHandler<'T> 
    ( 
        _source : System.IObservable<'T>,
        _func : 'T -> unit) =  
    inherit ToggleHandler()
    let mutable _handler : System.IDisposable option = None
    override x.Add() = 
        match _handler with
        | Some(_) -> failwith "Already subcribed"
        | None -> _handler <- _source |> Observable.subscribe _func |> Option.Some
    override x.Remove() =
        match _handler with
        | Some(actual) -> 
            actual.Dispose()
            _handler <- None
        | None -> ()
    
module internal Utils =

    let IsFlagSet value flag = 
        let intValue = LanguagePrimitives.EnumToValue value
        let flagValue = LanguagePrimitives.EnumToValue flag
        0 <> (intValue &&& flagValue)

    let UnsetFlag value flag =
        let intValue = LanguagePrimitives.EnumToValue value
        let flagValue = LanguagePrimitives.EnumToValue flag
        let value = intValue &&& (~~~flagValue)
        LanguagePrimitives.EnumOfValue value

    
    /// Create a regex.  Returns None if the regex has invalid characters
    let TryCreateRegex pattern options =
        try
            let r = new System.Text.RegularExpressions.Regex(pattern, options)
            Some r
        with 
            | :? System.ArgumentException -> None

    /// Determine if the given character is any line break character.  This matches up with 
    /// the editors understanding of line break characters
//    let IsAnyLineBreakCharacter c =
//        // TODO: \x0085
//        let special = char 133 // '\x0085'
//        match c with 
//        | '\n' -> true
//        | '\r' -> true
//        | '\u2028' -> true
//        | '\u2029' -> true
//        | _ -> false
//
//    let IsAnyLineBreakCharacterExcept c except = 
//        if c = except then false
//        else IsAnyLineBreakCharacter c
//
//    /// Get the set of Span's for line breaks over the given text
//    let GetLineBreakSpans (text:string) = 
//        let rec inner index l = 
//            if index < 0 then l
//            elif index = 0 && IsAnyLineBreakCharacter text.[index] then
//                Span(index,1) @ l 
//            elif text.[index] = '\n' && text.[index-1] = '\r' then
//                inner (index-2) (Span(index-1,2) @ l)
//            elif IsAnyLineBreakCharacterExcept text.[index] '\n' then
//                inner (index-1) (Span(index,1)@@ l)
//            else inner (index-1) l
//        inner (text.Length-1) []

module internal ListUtil =

    let divide l = (l |> List.head), (l |> List.tail)

    /// Try and get the head of the list.  Will return the head of list and tail 
    /// as separate elements.  Returns None if the list is empty
    let tryHead l = 
        if List.isEmpty l then None
        else Some (divide l)

    let tryHeadOnly l = 
        if List.isEmpty l then None
        else Some (List.head l)

    let tryProcessHead l ifNonEmpty ifEmpty =
        if List.isEmpty l then 
            ifEmpty()
        else
            let head,tail = divide l
            ifNonEmpty head tail

module internal SeqUtil =
    
    /// Try and get the head of the Sequence.  Will return the head of list and tail 
    /// as separate elements.  Returns None if the list is empty
    let tryHead l = 
        if Seq.isEmpty l then None
        else 
            let head = Seq.head l
            let tail = l |> Seq.skip 1 
            Some (head,tail)

    let tryHeadOnly l = 
        if Seq.isEmpty l then None
        else Some (Seq.head l)
        
