namespace Vim

open System
open StringBuilderExtensions

module StringUtil =

    let Empty = System.String.Empty

    let GetLength(input: string) = input.Length

    let FindFirst (input: seq<char>) index del =
        let found =
            input
            |> Seq.mapi (fun i c -> (i, c))
            |> Seq.skip index
            |> Seq.skipWhile (fun (i, c) -> not (del c))
        match Seq.isEmpty found with
        | true -> None
        | false -> Some(Seq.head found)

    let IsValidIndex index (input: string) = index >= 0 && index < input.Length

    let CharAtOption index (input: string) =
        match IsValidIndex index input with
        | true -> Some input.[index]
        | false -> None

    let CharAt index input =
        match CharAtOption index input with
        | Some c -> c
        | None -> failwith "Invalid index"

    let Repeat count (value: string) =
        if 1 = count then
            value
        else
            let buffer = new System.Text.StringBuilder()
            for i = 1 to count do
                buffer.AppendString value
            buffer.ToString()

    let ReplaceNoCase (source: string) (toFind: string) (toReplace: string) =
        let builder = System.Text.StringBuilder()
        let mutable lastIndex = 0
        let mutable index = source.IndexOf(toFind, StringComparison.OrdinalIgnoreCase)
        while index >= 0 do
            builder.AppendSubstring source lastIndex (index - lastIndex)
            builder.AppendString toReplace
            lastIndex <- index + toFind.Length
            index <- source.IndexOf(toFind, lastIndex, StringComparison.OrdinalIgnoreCase)

        if lastIndex < source.Length then builder.AppendSubstring source lastIndex (source.Length - lastIndex)
        builder.ToString()

    let RepeatChar count (value: char) =
        if 1 = count then
            (value.ToString())
        else
            let buffer = new System.Text.StringBuilder()
            for i = 1 to count do
                buffer.AppendChar value
            buffer.ToString()

    /// Create a String from an array of chars
    let OfCharArray(chars: char []) = new System.String(chars)

    /// Create a String from a sequence of chars
    let OfCharSeq(chars: char seq) =
        chars
        |> Array.ofSeq
        |> OfCharArray

    let OfCharList(chars: char list) =
        chars
        |> Seq.ofList
        |> OfCharSeq

    /// Create a String from a single char
    let OfChar c = System.String(c, 1)

    let OfStringSeq(strings: string seq) =
        let builder = System.Text.StringBuilder()
        for value in strings do
            builder.AppendString value
        builder.ToString()

    let IsNullOrEmpty str = System.String.IsNullOrEmpty(str)

    let IndexOfChar (c: char) (str: string) =
        let result = str.IndexOf(c)
        if result < 0 then None else Some result

    let IndexOfCharAt (c: char) (index: int) (str: string) =
        let result = str.IndexOf(c, index)
        if result < 0 then None else Some result

    let IndexOfString (toFind: string) (str: string) =
        let result = str.IndexOf(toFind)
        if result < 0 then None else Some result

    let IndexOfStringAt (toFind: string) (index: int) (str: string) =
        let result = str.IndexOf(toFind, index)
        if result < 0 then None else Some result

    let Length(str: string) =
        if str = null then 0 else str.Length

    let IsEqualIgnoreCase left right =
        let comp = System.StringComparer.OrdinalIgnoreCase
        comp.Equals(left, right)

    let IsEqual left right =
        let comp = System.StringComparer.Ordinal
        comp.Equals(left, right)

    let Split c (value: string) = value.Split([| c |])

    let Last value =
        let index = (Length value) - 1
        value.[index]

    let EndsWith suffix (value: string) = value.EndsWith(suffix, System.StringComparison.Ordinal)

    let StartsWith prefix (value: string) = value.StartsWith(prefix, System.StringComparison.Ordinal)

    let StartsWithIgnoreCase prefix (value: string) =
        value.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)

    let CombineWith (arg: string) (values: string seq) =
        let builder = new System.Text.StringBuilder()
        values
        |> Seq.iteri (fun i str ->
            if i <> 0 then builder.AppendString arg
            builder.AppendString str)
        builder.ToString()

    let ContainsChar (arg: string) (c: char) = arg.IndexOf(c) >= 0

    let IsWhiteSpace(arg: string) = not (Seq.exists CharUtil.IsNotWhiteSpace arg)

    let IsBlanks(arg: string) = Seq.forall CharUtil.IsBlank arg

    let ExpandTabsForColumn (arg: string) (startColumn: int) (tabStop: int) =
        let builder = new System.Text.StringBuilder()
        let mutable column = startColumn
        for i = 0 to arg.Length - 1 do
            let c = arg.[i]
            if c = '\t' then
                builder.AppendChar ' '
                column <- column + 1
                while column % tabStop <> 0 do
                    builder.AppendChar ' '
                    column <- column + 1
            else
                builder.AppendChar c
                column <- column + 1
        builder.ToString()

    let GetDisplayString(arg: string) =
        let builder = new System.Text.StringBuilder()
        for i = 0 to arg.Length - 1 do
            match arg.[i] with
            | c when int (c) = 127 -> builder.AppendString "^?"
            | c when Char.IsControl(c) ->
                builder.AppendChar '^'
                builder.AppendChar(char (c + '@'))
            | c -> builder.AppendChar c
        builder.ToString()

    /// Is the specified check string a substring of the given argument at the specified
    /// index
    let IsSubstringAt (arg: string) (check: string) (index: int) (comparer: CharComparer) =
        if index + check.Length >= arg.Length then
            false
        else
            let mutable allMatch = true
            let mutable i = 0
            while i < check.Length && allMatch do
                let argIndex = index + i
                if not (comparer.IsEqual arg.[argIndex] check.[i]) then allMatch <- false
                i <- i + 1
            allMatch
