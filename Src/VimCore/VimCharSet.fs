namespace Vim

open System
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Diagnostics

[<RequireQualifiedAccess>]
[<NoComparison>]
type internal VimCharSetPart =
    | Char of Char: char * Exclude: bool
    | CharRange of StartChar: char * LastChar: char * Exclude: bool
    | Letters of Exclude: bool

[<Class>]
[<Sealed>]
type VimCharSet(_text: string, _partList: ReadOnlyCollection<VimCharSetPart>) =

    member x.Text = _text

    member x.Contains c =
        let mutable contains = false
        for part in _partList do
            match part with
            | VimCharSetPart.Char(partChar, exclude) when partChar = c -> contains <- not exclude
            | VimCharSetPart.CharRange(firstPartChar, secondPartChar, exclude) when c >= firstPartChar
                                                                                    && c <= secondPartChar ->
                contains <- not exclude
            | VimCharSetPart.Letters exclude when CharUtil.IsLetter c -> contains <- not exclude
            | _ -> ()
        contains

    static member TryParse(text: string): VimCharSet option =
        let charAt index =
            if index < text.Length then Some(text.[index]) else None

        let parseNumber startIndex =
            Debug.Assert(startIndex < text.Length)
            Debug.Assert(Char.IsDigit(text.[startIndex]))
            let mutable index = startIndex + 1
            while index < text.Length && Char.IsDigit(text.[index]) do
                index <- index + 1

            let numberText = text.Substring(startIndex, index - startIndex)
            let succeeded, number = Int32.TryParse(numberText)
            if succeeded then Some(number, index) else None

        // This parses out the individual char item in the char or digit form
        let parsePartItem startIndex =
            Debug.Assert(startIndex < text.Length)
            let firstChar = text.[startIndex]
            if Char.IsDigit firstChar then
                match parseNumber startIndex with
                | Some(number, index) -> Some(char number, index)
                | None -> None
            else
                Some(firstChar, startIndex + 1)

        // Parse out a full part: either the single item or the range version. This will not
        // handle excludes as that is handled by the caller.
        let parsePartCore startIndex exclude =
            Debug.Assert(startIndex < text.Length)

            let resolveSingleChar c =
                if c = '@' then VimCharSetPart.Letters exclude else VimCharSetPart.Char(c, exclude)

            match parsePartItem startIndex with
            | None -> None
            | Some(firstPartItem, index) ->
                match charAt index with
                | Some ',' -> Some(resolveSingleChar firstPartItem, index)
                | None -> Some(resolveSingleChar firstPartItem, index)
                | Some '-' when index + 1 < text.Length ->
                    match parsePartItem (index + 1) with
                    | None -> None
                    | Some(secondPartItem, index) ->
                        Some(VimCharSetPart.CharRange(firstPartItem, secondPartItem, exclude), index)
                | _ -> None

        // This handles parsing out a full part including the exclusion
        let parsePart startIndex =
            Debug.Assert(startIndex < text.Length)
            if startIndex + 1 < text.Length && text.[startIndex] = '^'
            then parsePartCore (startIndex + 1) true
            else parsePartCore startIndex false

        let list = List<VimCharSetPart>()

        let rec parse startIndex =
            match parsePart startIndex with
            | None -> None
            | Some(part, index) ->
                list.Add part
                match charAt index with
                | None ->
                    // Parse is complete now
                    let col = new ReadOnlyCollection<VimCharSetPart>(list)
                    let vimCharSet = VimCharSet(text, col)
                    Some vimCharSet
                | Some ',' -> parse (index + 1)
                | _ -> None

        parse 0
