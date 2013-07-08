#light

namespace Vim
open System.Text
open System.Text.RegularExpressions
open StringBuilderExtensions

[<System.Flags>]
type VimRegexOptions = 
    | Default = 0
    | NotCompiled = 0x1
    | IgnoreCase = 0x2
    | SmartCase = 0x4
    | NoMagic = 0x8

[<RequireQualifiedAccess>]
type CaseSpecifier =
    | None
    | IgnoreCase
    | OrdinalCase 

/// Data for a replace operation
type ReplaceData = {

    /// When the '\r' replace sequence is used what should the replace string be.  This
    /// is usually contextual to the point in the IVimBuffer
    NewLine : string

    /// Whether or not magic should apply
    Magic : bool

    /// The 'count' times it should be replaced.  Not considered in a replace all
    Count : int
}

module VimRegexUtils = 

    /// Profiling reveals that one of the biggest expenses of fast editing with :hlsearch
    /// enabled in large files is the creation of the regex.  With editting we never change
    /// the regex but waste up to 25% of the CPU cycles recreating it over and over 
    /// again.  Cache the most recent few Regex values here to remove the needless work.
    ///
    /// Note: This is shared amongst threads but not protected by a lock.  We don't want
    /// any contention issues here.
    let mutable _sharedRegexCache : (string * RegexOptions * Regex) list = List.empty

    let GetRegexFromCache pattern options = 
        let list = _sharedRegexCache
        list
        |> Seq.ofList
        |> Seq.tryFind (fun (cachedPattern, cachedOptions, _) -> pattern = cachedPattern && cachedOptions = options)
        |> Option.map (fun (_, _, regex) -> regex)

    let SaveRegexToCache pattern options regex = 
        let list = _sharedRegexCache
        let list = 
            let maxLength = 10
            if list.Length > maxLength then
    
                // Truncate the list to 5.  Don't want to be constantly copying the list
                // so cut it in half every time we need to reclaim values
                list |> Seq.truncate (maxLength / 2) |> List.ofSeq
            else
                list
        _sharedRegexCache <- (pattern, options, regex) :: list

    /// Create a regex.  Returns None if the regex has invalid characters
    let TryCreateRegex pattern options =
        match GetRegexFromCache pattern options with
        | Some regex -> Some regex
        | None ->
            try
                let r = new Regex(pattern, options)
                SaveRegexToCache pattern options r
                Some r
            with 
                | :? System.ArgumentException -> None

    let Escape c = c |> StringUtil.ofChar |> Regex.Escape 

    let ConvertReplacementString (replacement : string) (replaceData : ReplaceData) = 
        let builder = StringBuilder()
        let appendChar c = builder.AppendChar c
        let appendString str = builder.AppendString str
        let rec inner index = 

            // Process a character which follows an '\' in the string
            let handleEscapeChar c = 
                if CharUtil.IsDigit c then 
                    appendChar '$'
                    appendChar c
                elif c = '&' && not replaceData.Magic then
                    appendString "$0"
                elif c = '\\' then
                    appendChar '\\'
                elif c = 'r' then
                    appendString replaceData.NewLine
                elif c = 't' then
                    appendChar '\t'
                else 
                    Escape c |> appendString
                inner (index + 2)

            match StringUtil.charAtOption index replacement with
            | None -> 
                builder.ToString()
            | Some '&' -> 
                if replaceData.Magic then
                    // This is a special character in the replacement string and should 
                    // match the entire matched string
                    appendString "$0"
                else
                    // In no magic this is simply a normal character
                    appendChar '&'
                inner (index + 1)
            | Some '\\' -> 
                match StringUtil.charAtOption (index + 1) replacement with 
                | None -> 
                    Escape '\\' |> appendString
                    builder.ToString()
                | Some c -> 
                    handleEscapeChar c 
            | Some c -> 
                appendChar c
                inner (index + 1)

        inner 0


/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex 
    (
        _vimPattern : string,
        _caseSpecifier : CaseSpecifier,
        _regexPattern : string,
        _regex : Regex,
        _includesNewLine : bool
    ) =

    member x.CaseSpecifier = _caseSpecifier
    member x.VimPattern = _vimPattern
    member x.RegexPattern = _regexPattern
    member x.Regex = _regex
    member x.IncludesNewLine = _includesNewLine
    member x.IsMatch input = _regex.IsMatch(input)
    member x.ReplaceAll (input : string) (replacement : string) (replaceData : ReplaceData) = 
        let replacement = VimRegexUtils.ConvertReplacementString replacement replaceData
        _regex.Replace(input, replacement)
    member x.Replace (input : string) (replacement : string) (replaceData : ReplaceData) =
        let replacement = VimRegexUtils.ConvertReplacementString replacement replaceData
        _regex.Replace(input, replacement, replaceData.Count) 

[<RequireQualifiedAccess>]
[<NoComparison>]
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

type VimRegexBuilder 
    (
        _pattern : string,
        _magicKind : MagicKind,
        _matchCase : bool,
        _options : VimRegexOptions
    ) =

    let mutable _index = 0
    let mutable _magicKind = _magicKind
    let mutable _matchCase = _matchCase
    let mutable _builder = StringBuilder()
    let mutable _isBroken = false
    let mutable _isStartOfPattern = true
    let mutable _isStartOfGroup = false
    let mutable _isRangeOpen = false
    let mutable _isGroupOpen = false
    let mutable _includesNewLine = false
    let mutable _caseSpecifier = CaseSpecifier.None

    member x.Pattern = _pattern

    member x.Index = _index

    member x.Builder = _builder

    /// The original options 
    member x.Options = _options

    member x.MagicKind 
        with get() = _magicKind
        and set value = _magicKind <- value

    member x.MatchCase
        with get() = _matchCase
        and set value = _matchCase <- value

    /// Which case specifier appeared in the pattern
    member x.CaseSpecifier 
        with get() = _caseSpecifier
        and set value = _caseSpecifier <- value

    /// Is the match completely broken and should match nothing
    member x.IsBroken = _isBroken

    /// Is this the start of the pattern
    member x.IsStartOfPattern 
        with get() = _isStartOfPattern
        and set value = _isStartOfPattern <- value

    /// Is this the first character inside of a grouping [] construct
    member x.IsStartOfGroup 
        with get() = _isStartOfGroup
        and set value = _isStartOfGroup <- value

    /// Is this in the middle of a range? 
    member x.IsRangeOpen = _isRangeOpen

    /// Is this in the middle of a group?
    member x.IsGroupOpen = _isGroupOpen

    /// Includes a \n reference
    member x.IncludesNewLine
        with get() = _includesNewLine
        and set value = _includesNewLine <- value

    member x.IsEndOfPattern = x.Index >= x.Pattern.Length

    member x.IncrementIndex count =
        _index <- _index + count

    member x.DecrementIndex count = 
        _index <- _index - count

    member x.CharAtIndex = StringUtil.charAtOption x.Index x.Pattern

    member x.AppendString str = 
        _builder.AppendString str

    member x.AppendChar c =
        x.Builder.AppendChar c

    member x.AppendEscapedChar c = 
        c |> StringUtil.ofChar |> Regex.Escape |> x.AppendString

    member x.BeginGroup() = 
        x.AppendChar '['
        _isGroupOpen <- true
        _isStartOfGroup <- true

    member x.EndGroup() = 
        x.AppendChar ']'
        _isGroupOpen <- false

    member x.BeginRange() =
        x.AppendChar '{'
        _isRangeOpen <- true

    member x.EndRange() =
        x.AppendChar '}'
        _isRangeOpen <- false

    member x.Break() =
        _isBroken <- true

module VimRegexFactory =

    /// In Vim if a grouping is unmatched then it is appended literally into the match 
    /// stream.  Can't determine if it's unmatched though until the string is fully 
    /// processed.  At this point we just go backwards and esacpe it
    let FixOpenGroup (data : VimRegexBuilder) = 
        let builder = data.Builder
        let mutable i = builder.Length - 1
        while i >= 0 do
            if builder.[i] = '[' && (i = 0 || builder.[i - 1] <> '\\') then
                builder.Insert(i, '\\') |> ignore
                i <- -1
            else
                i <- i - 1

    // Create the actual Vim regex
    let CreateVimRegex (data : VimRegexBuilder) =

        let regexOptions = 
            let regexOptions = 
                if Util.IsFlagSet data.Options VimRegexOptions.NotCompiled then 
                    RegexOptions.None
                else 
                    RegexOptions.Compiled

            let regexOptions = 
                if data.IncludesNewLine then
                    regexOptions
                else
                    regexOptions ||| RegexOptions.Multiline
                    
            if data.MatchCase then
                regexOptions
            else
                regexOptions ||| RegexOptions.IgnoreCase

        if data.IsGroupOpen then
            FixOpenGroup data

        if data.IsBroken || data.IsRangeOpen then 
            None
        else 
            let bclPattern = data.Builder.ToString()
            let regex = VimRegexUtils.TryCreateRegex bclPattern regexOptions
            match regex with
            | None -> None
            | Some regex -> VimRegex(data.Pattern, data.CaseSpecifier, bclPattern, regex, data.IncludesNewLine) |> Some

    /// Convert the given character as a special character.  Interpretation
    /// may depend on the type of magic that is currently being employed
    let ConvertCharAsSpecial (data : VimRegexBuilder) c = 
        match c with
        | '.' -> data.AppendChar '.'
        | '+' -> data.AppendChar '+'
        | '=' -> data.AppendChar '?'
        | '?' -> data.AppendChar '?'
        | '*' -> data.AppendChar '*'
        | '(' -> data.AppendChar '('
        | ')' -> data.AppendChar ')'
        | '{' -> if data.IsRangeOpen then data.Break() else data.BeginRange()
        | '}' -> if data.IsRangeOpen then data.EndRange() else data.AppendChar '}'
        | '|' -> data.AppendChar '|'
        | '^' -> if data.IsStartOfPattern || data.IsStartOfGroup then data.AppendChar '^' else data.AppendEscapedChar '^'
        | '$' -> 
            if data.IsEndOfPattern then 
                data.AppendString @"\r?$" 
            else data.AppendEscapedChar '$'
        | '<' -> data.AppendString @"\b"
        | '>' -> data.AppendString @"\b"
        | '[' -> 
            match data.CharAtIndex with
            | Some ']' -> 
                data.AppendEscapedChar '['
                data.AppendEscapedChar ']'
                data.IncrementIndex 1
            | _ -> 
                data.BeginGroup()
        | ']' -> if data.IsGroupOpen then data.EndGroup() else data.AppendEscapedChar(']')
        | 'd' -> data.AppendString @"\d"
        | 'D' -> data.AppendString @"\D"
        | 's' -> data.AppendString @"\s"
        | 'S' -> data.AppendString @"\S"
        | 'w' -> data.AppendString @"\w"
        | 'W' -> data.AppendString @"\W"
        | 'x' -> data.AppendString @"[0-9A-Fa-f]"
        | 'X' -> data.AppendString @"[^0-9A-Fa-f]"
        | 'o' -> data.AppendString @"[0-7]"
        | 'O' -> data.AppendString @"[^0-7]"
        | 'h' -> data.AppendString @"[A-Za-z_]"
        | 'H' -> data.AppendString @"[^A-Za-z_]"
        | 'a' -> data.AppendString @"[A-Za-z]"
        | 'A' -> data.AppendString @"[^A-Za-z]"
        | 'l' -> data.AppendString @"[a-z]"
        | 'L' -> data.AppendString @"[^a-z]"
        | 'u' -> data.AppendString @"[A-Z]"
        | 'U' -> data.AppendString @"[^A-Z]"
        | _ -> data.AppendEscapedChar c

    /// Convert the given char in the magic setting 
    let ConvertCharAsMagic (data : VimRegexBuilder) c =
        match c with 
        | '*' -> data.AppendChar '*'
        | '.' -> data.AppendChar '.'
        | '}' -> if data.IsRangeOpen then data.EndRange() else data.AppendChar '}'
        | '^' -> ConvertCharAsSpecial data c
        | '$' -> ConvertCharAsSpecial data c
        | '[' -> ConvertCharAsSpecial data c
        | ']' -> ConvertCharAsSpecial data c
        | _ -> data.AppendEscapedChar c

    /// Convert the given char in the nomagic setting
    let ConvertCharAsNoMagic (data : VimRegexBuilder) c =
        match c with 
        | '^' -> ConvertCharAsSpecial data c 
        | '$' -> ConvertCharAsSpecial data c
        | ']' -> ConvertCharAsSpecial data c
        | _ -> data.AppendEscapedChar c

    /// Convert the given escaped char in the magic and no magic settings.  The 
    /// differences here are minimal so it's convenient to put them in one method
    /// here
    let ConvertEscapedCharAsMagicAndNoMagic (data : VimRegexBuilder) c =
        let isMagic = data.MagicKind = MagicKind.Magic
        match c with 
        | '.' -> if isMagic then data.AppendEscapedChar c else ConvertCharAsSpecial data c
        | '*' -> if isMagic then data.AppendEscapedChar c else ConvertCharAsSpecial data c 
        | '+' -> ConvertCharAsSpecial data c
        | '?' -> ConvertCharAsSpecial data c 
        | '=' -> ConvertCharAsSpecial data c
        | '<' -> ConvertCharAsSpecial data c
        | '>' -> ConvertCharAsSpecial data c
        | '(' -> ConvertCharAsSpecial data c 
        | ')' -> ConvertCharAsSpecial data c 
        | '{' -> ConvertCharAsSpecial data c
        | '}' -> if data.IsRangeOpen then data.EndRange() else data.AppendEscapedChar c
        | '|' -> ConvertCharAsSpecial data c
        | '[' -> if isMagic then data.AppendEscapedChar c else ConvertCharAsSpecial data c
        | ']' -> ConvertCharAsSpecial data c
        | 'a' -> ConvertCharAsSpecial data c
        | 'A' -> ConvertCharAsSpecial data c
        | 'd' -> ConvertCharAsSpecial data c
        | 'D' -> ConvertCharAsSpecial data c
        | 'h' -> ConvertCharAsSpecial data c
        | 'H' -> ConvertCharAsSpecial data c
        | 'l' -> ConvertCharAsSpecial data c
        | 'L' -> ConvertCharAsSpecial data c
        | 'o' -> ConvertCharAsSpecial data c
        | 'O' -> ConvertCharAsSpecial data c
        | 's' -> ConvertCharAsSpecial data c 
        | 'S' -> ConvertCharAsSpecial data c 
        | 'u' -> ConvertCharAsSpecial data c
        | 'U' -> ConvertCharAsSpecial data c
        | 'w' -> ConvertCharAsSpecial data c
        | 'W' -> ConvertCharAsSpecial data c
        | 'x' -> ConvertCharAsSpecial data c
        | 'X' -> ConvertCharAsSpecial data c
        | '_' -> 
            match data.CharAtIndex with
            | None -> data.Break()
            | Some c -> 
                data.IncrementIndex 1
                match c with 
                | '^' -> data.AppendChar '^'
                | '$' -> data.AppendChar '$'
                | '.' -> data.AppendString @"(.|\n)"
                | _ -> data.Break()
        | _ -> data.AppendEscapedChar c

    /// Process an escaped character.  Look first for global options such as ignore 
    /// case or magic and then go for magic specific characters
    let ProcessEscapedChar (data : VimRegexBuilder) c =
        let escape = VimRegexUtils.Escape
        match c with 
        | 'm' -> data.MagicKind <- MagicKind.Magic
        | 'M' -> data.MagicKind <- MagicKind.NoMagic
        | 'v' -> data.MagicKind <- MagicKind.VeryMagic
        | 'V' -> data.MagicKind <- MagicKind.VeryNoMagic
        | 't' -> data.AppendString "\t"
        | 'C' -> 
            data.MatchCase <- true
            data.CaseSpecifier <- CaseSpecifier.OrdinalCase
        | 'c' -> 
            data.MatchCase <- false
            data.CaseSpecifier <- CaseSpecifier.IgnoreCase
        | 'n' -> 
            // vim expects \n to match any kind of newline, regardless of platform. Think about it,
            // you can't see newlines, so why should you be expected to know the diff between them?
            // Also, use ?: for non-capturing group, so we don't cause any weird behavior
            data.AppendString "(?:\r?\n|\r)"
            data.IncludesNewLine <- true
        | _ -> 
            if CharUtil.IsDigit c then
                // Convert the \1 escape into the BCL \1 for any single digit
                let str = sprintf "\\%c"c
                data.AppendString str
            else
                match data.MagicKind with
                | MagicKind.Magic -> ConvertEscapedCharAsMagicAndNoMagic data c 
                | MagicKind.NoMagic -> ConvertEscapedCharAsMagicAndNoMagic data c
                | MagicKind.VeryMagic -> data.AppendEscapedChar c
                | MagicKind.VeryNoMagic -> ConvertCharAsSpecial data c

                data.IsStartOfPattern <- false

    /// Convert a normal unescaped char based on the magic kind
    let ProcessNormalChar (data : VimRegexBuilder) c = 
        match data.MagicKind with
        | MagicKind.Magic -> ConvertCharAsMagic data c
        | MagicKind.NoMagic -> ConvertCharAsNoMagic data c
        | MagicKind.VeryMagic -> 
            if CharUtil.IsLetter c || CharUtil.IsDigit c || c = '_' then 
                data.AppendChar c
            else
                ConvertCharAsSpecial data c
        | MagicKind.VeryNoMagic -> data.AppendEscapedChar c
        data.IsStartOfPattern <- false

    let Convert (data : VimRegexBuilder) = 
        let rec inner () : VimRegex option =
            if data.IsBroken then 
                None
            else
                match data.CharAtIndex with
                | None -> CreateVimRegex data 
                | Some '\\' -> 
                    let wasStartOfGroup = data.IsStartOfGroup
                    data.IncrementIndex 1
                    match data.CharAtIndex with 
                    | None -> ProcessNormalChar data '\\'
                    | Some c -> 
                        data.IncrementIndex 1
                        ProcessEscapedChar data c

                    // If we were at the start of a grouping before processing this 
                    // char then we no longer are afterwards
                    if wasStartOfGroup then 
                        data.IsStartOfGroup <- false

                    inner ()
                | Some c -> 
                    data.IncrementIndex 1
                    ProcessNormalChar data c |> inner
        inner ()

    let Create pattern options = 

        // Calculate the initial value for whether or not we display case
        let matchCase = 
            let ignoreCase = Util.IsFlagSet options VimRegexOptions.IgnoreCase
            let smartCase = Util.IsFlagSet options VimRegexOptions.SmartCase
            if smartCase then
                let isUpperLetter x = CharUtil.IsLetter x && CharUtil.IsUpper x
                if pattern |> Seq.exists isUpperLetter then
                    true
                else
                    not ignoreCase
            else
                not ignoreCase

        let magicKind = 
            if Util.IsFlagSet options VimRegexOptions.NoMagic then 
                MagicKind.NoMagic
            else
                MagicKind.Magic

        let data = VimRegexBuilder(pattern, magicKind, matchCase, options)

        Convert data

    let CreateCaseOptions (globalSettings : IVimGlobalSettings) =
        let options = VimRegexOptions.Default

        let options = 
            if globalSettings.IgnoreCase then options ||| VimRegexOptions.IgnoreCase
            else options

        let options = 
            if globalSettings.SmartCase then options ||| VimRegexOptions.SmartCase
            else options

        options

    let CreateRegexOptions (globalSettings : IVimGlobalSettings) =
        let options = VimRegexOptions.Default
        let options =
            if globalSettings.Magic then options
            else VimRegexOptions.NoMagic

        options ||| CreateCaseOptions globalSettings

    let CreateForSubstituteFlags pattern globalSettings (flags : SubstituteFlags) =

        let options = VimRegexOptions.Default

        let hasIgnoreCase = Util.IsFlagSet flags SubstituteFlags.IgnoreCase
        let hasOrdinalCase = Util.IsFlagSet flags SubstituteFlags.OrdinalCase

        // Get the case options
        let options = 
            if hasIgnoreCase then options ||| VimRegexOptions.IgnoreCase
            else options

        // If there was no case option specified then draw from the global settings
        let options = 
            if not hasIgnoreCase && not hasOrdinalCase then options ||| CreateCaseOptions globalSettings
            else options

        // Get the magic options
        let options = 
            if Util.IsFlagSet flags SubstituteFlags.Nomagic then options ||| VimRegexOptions.NoMagic
            else options 


        Create pattern options 

    let CreateForSettings pattern globalSettings =
        let options = CreateRegexOptions globalSettings
        Create pattern options

    let CreateBcl pattern options =
        VimRegexUtils.TryCreateRegex pattern options

