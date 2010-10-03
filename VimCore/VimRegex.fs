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

    /// Text of the Regular expression
    member x.Text = _vimText

    /// Does the string match the text
    member x.IsMatch input = _regex.IsMatch(input)

[<RequireQualifiedAccess>]
type MagicKind = 
    | NoMagic
    | Magic
    | VeryMagic
    | VeryNoMagic

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

    member x.Create pattern = 
        let kind = if _settings.Magic then MagicKind.Magic else MagicKind.NoMagic
        let data = { 
            Pattern = pattern
            Index = 0
            Builder = new StringBuilder()
            MagicKind = kind
            MatchCase = not _settings.IgnoreCase
            HasCaseAtom = false }
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
                    | None -> x.ConvertNormalChar data '\\'
                    | Some(c) -> x.ConvertEscapedChar (data.IncrementIndex 1) c
                inner data
            | Some(c) -> x.ConvertNormalChar (data.IncrementIndex 1) c |> inner
        inner data
    
    /// Process an escaped character.  Look first for global options such as ignore 
    /// case or magic and then go for magic specific characters
    member x.ConvertEscapedChar data c  =
        match c with 
        | 'C' -> {data with MatchCase = true; HasCaseAtom = true}
        | 'c' -> {data with MatchCase = false; HasCaseAtom = true }
        | 'm' -> {data with MagicKind = MagicKind.Magic }
        | 'M' -> {data with MagicKind = MagicKind.NoMagic }
        | 'v' -> {data with MagicKind = MagicKind.VeryMagic }
        | 'V' -> {data with MagicKind = MagicKind.VeryNoMagic }
        | _ -> 
            match data.MagicKind with
            | MagicKind.NoMagic -> 
                match c with 
                | '.' -> data.AppendChar '.' 
                | _ -> x.ConvertNormalChar data c
            | MagicKind.Magic -> 
                match c with
                | '.' -> data.AppendString @"\."
                | _ -> x.ConvertNormalChar data c
            | MagicKind.VeryMagic -> x.ConvertNormalChar data c
            | MagicKind.VeryNoMagic -> 
                match c with 
                | '.' -> data.AppendChar '.'
                | _  -> x.ConvertNormalChar data c

    /// Convert a normal unescaped char based on the 
    member x.ConvertNormalChar (data:Data) c = 

        // Consider smart case here but only a case atom hasn't already 
        // appeared 
        let data = 
            if _settings.SmartCase && not data.HasCaseAtom && CharUtil.IsLetter c && CharUtil.IsUpper c then 
                {data with MatchCase = true }
            else 
                data

        match data.MagicKind with
        | MagicKind.NoMagic -> 
            match c with
            | '.' -> data.AppendString @"\."
            | _ -> data.AppendChar c
        | MagicKind.Magic -> 
            match c with 
            | _ -> data.AppendChar c
        | MagicKind.VeryMagic -> 
            match c with 
            | _ -> data.AppendChar c
        | MagicKind.VeryNoMagic -> 
            match c with 
            | '.' -> data.AppendString @"\."
            | _ -> data.AppendChar c



