#light

namespace Vim
open System.Text
open System.Text.RegularExpressions

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex 
    (
        _vimText : string,
        _regex : Regex ) =

    member x.Text = _vimText
    member x.Regex = _regex
    member x.IsMatch input = _regex.IsMatch(input)

[<RequireQualifiedAccess>]
type MagicKind = 
    | NoMagic
    | Magic
    | VeryMagic
    | VeryNoMagic

    with

    member x.IsAnyNoMagic =
        match x with 
        | MagicKind.NoMagic -> true
        | MagicKind.VeryNoMagic -> true
        | MagicKind.Magic -> false
        | MagicKind.VeryMagic -> false

    member x.IsAnyMagic = not x.IsAnyNoMagic

type Data = {
    Pattern : string 
    Index : int
    MagicKind : MagicKind 
    MatchCase : bool
    Builder : StringBuilder

    /// Do either the \c or \C atoms appear in the pattern
    HasCaseAtom : bool
}
    with
    member x.IncrementIndex count = { x with Index = x.Index + count }
    member x.DecrementIndex count = { x with Index = x.Index - count }
    member x.CharAtIndex = StringUtil.charAtOption x.Index x.Pattern
    member x.AppendString (str:string) = { x with Builder = x.Builder.Append(str) }
    member x.AppendChar (c:char) = { x with Builder = x.Builder.Append(c) }

[<Sealed>]
type VimRegexFactory
    (
        _settings : IVimGlobalSettings ) =

    /// Escape the given character
    let _escape c = c |> StringUtil.ofChar |> Regex.Escape 

    member x.Create pattern = 
        let kind = if _settings.Magic then MagicKind.Magic else MagicKind.NoMagic
        let data = { 
            Pattern = pattern
            Index = 0
            Builder = new StringBuilder()
            MagicKind = kind
            MatchCase = not _settings.IgnoreCase
            HasCaseAtom = false }

        // Check for smart case here
        let data = 
            let isUpperLetter x = CharUtil.IsLetter x && CharUtil.IsUpper x
            if _settings.SmartCase && data.Pattern |> Seq.filter isUpperLetter |> SeqUtil.isNotEmpty then 
                { data with MatchCase = true }
            else 
                data

        let regex = x.Convert data
        VimRegex(pattern, regex)

    member x.CreateRegex (data:Data) =
        let options = RegexOptions.Compiled;
        let options = if data.MatchCase then options else options ||| RegexOptions.IgnoreCase
        Regex(data.Builder.ToString(),options)

    member x.Convert (data:Data) =
        let rec inner (data:Data) = 
            match data.CharAtIndex with
            | None -> x.CreateRegex data 
            | Some('\\') -> 
                let data = data.IncrementIndex 1
                let data = 
                    match data.CharAtIndex with 
                    | None -> x.ProcessNormalChar data '\\'
                    | Some(c) -> x.ProcessEscapedChar (data.IncrementIndex 1) c
                inner data
            | Some(c) -> x.ProcessNormalChar (data.IncrementIndex 1) c |> inner
        inner data
    
    /// Process an escaped character.  Look first for global options such as ignore 
    /// case or magic and then go for magic specific characters
    member x.ProcessEscapedChar data c  =
        let escape c = c |> StringUtil.ofChar |> Regex.Escape 
        match c with 
        | 'C' -> {data with MatchCase = true; HasCaseAtom = true}
        | 'c' -> {data with MatchCase = false; HasCaseAtom = true }
        | 'm' -> {data with MagicKind = MagicKind.Magic }
        | 'M' -> {data with MagicKind = MagicKind.NoMagic }
        | 'v' -> {data with MagicKind = MagicKind.VeryMagic }
        | 'V' -> {data with MagicKind = MagicKind.VeryNoMagic }
        | _ ->
            match data.MagicKind with
            | MagicKind.Magic -> 
                match c with
                | '.' -> c |> _escape |> data.AppendString
                | _ -> x.ConvertCharAsSpecial data c
            | MagicKind.VeryMagic -> x.ConvertCharAsNonSpecial data c
            | MagicKind.NoMagic -> x.ConvertCharAsSpecial data c
            | MagicKind.VeryNoMagic -> x.ConvertCharAsSpecial data c

    /// Convert a normal unescaped char based on the 
    member x.ProcessNormalChar (data:Data) c = 

        let addEscaped() = c |> _escape |> data.AppendString

        match data.MagicKind with
        | MagicKind.NoMagic -> addEscaped()
        | MagicKind.Magic -> 
            match c with 
            | '*' -> data.AppendChar '*'
            | '.' -> data.AppendChar '.'
            | _ -> addEscaped()
        | MagicKind.VeryMagic -> x.ConvertCharAsSpecial data c
        | MagicKind.VeryNoMagic -> addEscaped()

    /// Convert the given character as a special character.  Interpretation
    /// may depend on the type of magic that is currently being employed
    member x.ConvertCharAsSpecial (data:Data) c = 
        match c with
        | '.' -> data.AppendChar '.'
        | '=' -> data.AppendChar '?'
        | '?' -> data.AppendChar '?'
        | '*' -> data.AppendChar '*'
        | _ -> c |> _escape |> data.AppendString

    /// Convert the given character as a non-special character.  Interpertation
    /// may depend on th etype of magic that is currently being employed
    member x.ConvertCharAsNonSpecial (data:Data) c = 
        match c with
        | '=' -> data.AppendChar '?'
        | '?' -> data.AppendChar '?'
        | '*' -> data.AppendChar '*'
        | _ -> c |> _escape |> data.AppendString


