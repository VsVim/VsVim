#light

namespace Vim
open System
open StringBuilderExtensions

module internal StringUtil =

    let empty = System.String.Empty

    let findFirst (input:seq<char>) index del =
        let found = 
            input 
                |> Seq.mapi ( fun i c -> (i,c) )
                |> Seq.skip index 
                |> Seq.skipWhile (fun (i,c) -> not (del c))
        match Seq.isEmpty found with
            | true -> None
            | false -> Some (Seq.head found)

    let isValidIndex index (input:string) = index >= 0 && index < input.Length
            
    let charAtOption index (input:string) = 
        match isValidIndex index input with
            | true -> Some input.[index]
            | false -> None
            
    let charAt index input =
        match charAtOption index input with 
            | Some c -> c
            | None -> failwith "Invalid index"
    
    let repeat count (value:string) =
        if 1 = count then value
        else
            let buffer = new System.Text.StringBuilder()
            for i = 1 to count do
                buffer.AppendString value
            buffer.ToString()

    let replaceNoCase (source : string) (toFind : string) (toReplace : string) = 
        let builder = System.Text.StringBuilder()
        let mutable lastIndex = 0
        let mutable index = source.IndexOf(toFind, StringComparison.OrdinalIgnoreCase)
        while index >= 0 do
            builder.AppendSubstring source lastIndex (index - lastIndex)
            builder.AppendString toReplace 
            lastIndex <- index + toFind.Length
            index <- source.IndexOf(toFind, lastIndex, StringComparison.OrdinalIgnoreCase)

        if lastIndex < source.Length then
            builder.AppendSubstring source lastIndex (source.Length - lastIndex)
        builder.ToString()

    let repeatChar count (value : char) =
        if 1 = count then (value.ToString())
        else
            let buffer = new System.Text.StringBuilder()
            for i = 1 to count do
                buffer.AppendChar value
            buffer.ToString()

    /// Create a String from an array of chars
    let ofCharArray (chars:char[]) = new System.String(chars)

    /// Create a String from a sequence of chars
    let ofCharSeq (chars : char seq) = chars |> Array.ofSeq |> ofCharArray

    let ofCharList (chars :char list) = chars |> Seq.ofList |> ofCharSeq

    /// Create a String from a single char
    let ofChar c = System.String(c,1)

    let ofStringSeq (strings : string seq) = 
        let builder = System.Text.StringBuilder()
        for value in strings do
            builder.AppendString value
        builder.ToString()

    let isNullOrEmpty str = System.String.IsNullOrEmpty(str)

    let indexOfChar (c : char) (str : string) = 
        let result = str.IndexOf(c)
        if result < 0 then None
        else Some result

    let indexOfCharAt (c : char) (index : int) (str : string) = 
        let result = str.IndexOf(c, index)
        if result < 0 then None
        else Some result

    let indexOfString (toFind : string) (str : string) = 
        let result = str.IndexOf(toFind)
        if result < 0 then None
        else Some result

    let indexOfStringAt (toFind : string) (index : int) (str : string) = 
        let result = str.IndexOf(toFind, index)
        if result < 0 then None
        else Some result

    let length (str:string) = 
        if str = null then 0
        else str.Length

    let isEqualIgnoreCase left right = 
        let comp = System.StringComparer.OrdinalIgnoreCase
        comp.Equals(left,right)

    let isEqual left right = 
        let comp = System.StringComparer.Ordinal
        comp.Equals(left,right)

    let split c (value:string) = value.Split( [| c |]) 

    let last value =
        let index = (length value) - 1
        value.[index]

    let endsWith suffix (value:string) = value.EndsWith(suffix, System.StringComparison.Ordinal)

    let startsWith prefix (value:string) = value.StartsWith(prefix, System.StringComparison.Ordinal)

    let startsWithIgnoreCase prefix (value:string) = value.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)

    let combineWith (arg:string) (values:string seq) =
        let builder = new System.Text.StringBuilder()
        values |> Seq.iteri (fun i str -> 
            if i <> 0 then 
                builder.AppendString arg
            builder.AppendString str)
        builder.ToString()

    let containsChar (arg:string) (c:char) = arg.IndexOf(c) >= 0

    let isWhiteSpace (arg : string) = not (Seq.exists CharUtil.IsNotWhiteSpace arg)

    let isBlanks (arg : string) = Seq.forall CharUtil.IsBlank arg

    /// Is the specified check string a substring of the given argument at the specified
    /// index
    let isSubstringAt (arg : string) (check : string) (index : int) (comparer : CharComparer) =
        if index + check.Length >= arg.Length then
            false
        else
            let mutable allMatch = true
            let mutable i = 0
            while i < check.Length && allMatch do
                let argIndex = index + i
                if not (comparer.IsEqual arg.[argIndex] check.[i]) then
                    allMatch <- false
                i <- i + 1
            allMatch
        

