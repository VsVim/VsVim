#light

namespace Vim
open System.Text
open System.Text.RegularExpressions

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex 
    (
        _vimText : string,
        _regex : Regex option ) =

    member x.Text = _vimText
    member x.Regex = _regex
    member x.IsMatch input = 
        match _regex with
        | None -> false
        | Some(regex) -> regex.IsMatch(input)

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

    /// Is the match completely broken and should match nothing
    IsBroken : bool

    /// Is this the start of the pattern
    IsStartOfPattern : bool
}
    with
    member x.IsEndOfPattern = x.Index >= x.Pattern.Length
    member x.IncrementIndex count = { x with Index = x.Index + count }
    member x.DecrementIndex count = { x with Index = x.Index - count }
    member x.CharAtIndex = StringUtil.charAtOption x.Index x.Pattern
    member x.AppendString (str:string) = { x with Builder = x.Builder.Append(str) }
    member x.AppendChar (c:char) = { x with Builder = x.Builder.Append(c) }
    member x.AppendEscapedChar c = c |> StringUtil.ofChar |> Regex.Escape |> x.AppendString

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
            HasCaseAtom = false 
            IsBroken = false 
            IsStartOfPattern = true}

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
        if data.IsBroken then None
        else Regex(data.Builder.ToString(),options) |> Some

    member x.Convert (data:Data) =
        let rec inner (data:Data) : Regex option =
            if data.IsBroken then None
            else
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
            let data = 
                match data.MagicKind with
                | MagicKind.Magic -> x.ConvertEscapedCharAsMagicAndNoMagic data c 
                | MagicKind.NoMagic -> x.ConvertEscapedCharAsMagicAndNoMagic data c
                | MagicKind.VeryMagic -> data.AppendEscapedChar c
                | MagicKind.VeryNoMagic -> x.ConvertCharAsSpecial data c
            {data with IsStartOfPattern=false}
    
    /// Convert a normal unescaped char based on the 
    member x.ProcessNormalChar (data:Data) c = 
        let data = 
            match data.MagicKind with
            | MagicKind.Magic -> x.ConvertCharAsMagic data c
            | MagicKind.NoMagic -> x.ConvertCharAsNoMagic data c
            | MagicKind.VeryMagic -> 
                if CharUtil.IsLetter c || CharUtil.IsDigit c || c = '_' then data.AppendChar c
                else x.ConvertCharAsSpecial data c
            | MagicKind.VeryNoMagic -> data.AppendEscapedChar c
        {data with IsStartOfPattern=false}

    /// Convert the given char in the magic setting 
    member x.ConvertCharAsMagic (data:Data) c =
        match c with 
        | '*' -> data.AppendChar '*'
        | '.' -> data.AppendChar '.'
        | '^' -> x.ConvertCharAsSpecial data c
        | '$' -> x.ConvertCharAsSpecial data c
        | _ -> data.AppendEscapedChar c

    /// Convert the given char in the nomagic setting
    member x.ConvertCharAsNoMagic (data:Data) c =
        match c with 
        | '^' -> x.ConvertCharAsSpecial data c 
        | '$' -> x.ConvertCharAsSpecial data c
        | _ -> data.AppendEscapedChar c

    /// Convert the given escaped char in the magic and no magic settings.  The 
    /// differences here are minimal so it's convenient to put them in one method
    /// here
    member x.ConvertEscapedCharAsMagicAndNoMagic (data:Data) c =
        let isMagic = data.MagicKind = MagicKind.Magic
        match c with 
        | '.' -> if isMagic then data.AppendEscapedChar c else x.ConvertCharAsSpecial data c
        | '*' -> x.ConvertCharAsSpecial data c 
        | '?' -> x.ConvertCharAsSpecial data c 
        | '=' -> x.ConvertCharAsSpecial data c
        | '_' -> 
            match data.CharAtIndex with
            | None -> { data with IsBroken = true }
            | Some(c) -> 
                let data = data.IncrementIndex 1
                match c with 
                | '^' -> data.AppendChar '^'
                | '$' -> data.AppendChar '$'
                | _ -> { data with IsBroken = true }
        | _ -> data.AppendEscapedChar c

    /// Convert the given character as a special character.  Interpretation
    /// may depend on the type of magic that is currently being employed
    member x.ConvertCharAsSpecial (data:Data) c = 
        match c with
        | '.' -> data.AppendChar '.'
        | '=' -> data.AppendChar '?'
        | '?' -> data.AppendChar '?'
        | '*' -> data.AppendChar '*'
        | '^' -> if data.IsStartOfPattern then data.AppendChar '^' else data.AppendEscapedChar '^'
        | '$' -> if data.IsEndOfPattern then data.AppendChar '$' else data.AppendEscapedChar '$'
        | _ -> data.AppendEscapedChar c

