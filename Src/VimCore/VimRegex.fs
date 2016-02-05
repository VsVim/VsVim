﻿#light

namespace Vim
open System
open System.Text
open System.Text.RegularExpressions
open StringBuilderExtensions

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

    let Escape c = c |> StringUtil.OfChar |> Regex.Escape 

[<RequireQualifiedAccess>]
[<NoComparison>]
type VimReplaceCaseState = 
    | None
    | UpperChar
    | UpperUntil
    | LowerChar
    | LowerUtil


/// Type responsible for generating replace strings.
[<Sealed>]
type VimRegexReplaceUtil
    (
        _input : string,
        _matchCollection : MatchCollection,
        _registerMap : IRegisterMap
    ) =

    let mutable _replaceCount = 0
    let mutable _replacement = ""
    let mutable _index = 0
    let mutable _caseState = VimReplaceCaseState.None
    let mutable _builder = StringBuilder()
    let mutable _replaceData = VimRegexReplaceData.Default

    member private x.AppendReplaceChar c = 
        match _caseState with
        | VimReplaceCaseState.None -> 
            _builder.AppendChar c
        | VimReplaceCaseState.LowerChar -> 
            _builder.AppendChar (CharUtil.ToLower c)
            _caseState <- VimReplaceCaseState.None
        | VimReplaceCaseState.LowerUtil -> 
            _builder.AppendChar (CharUtil.ToLower c)
        | VimReplaceCaseState.UpperChar ->
            _builder.AppendChar (CharUtil.ToUpper c)
            _caseState <- VimReplaceCaseState.None
        | VimReplaceCaseState.UpperUntil ->
            _builder.AppendChar (CharUtil.ToUpper c)
    
    member private x.AppendReplaceString str = 
        str |> Seq.iter x.AppendReplaceChar

    member private x.AppendGroup (m : Match) digit = 
        Contract.Requires (digit <> 0)
        if digit < m.Groups.Count then
            let group = m.Groups.[digit]
            x.AppendReplaceString group.Value

    member private x.AppendRegister (c : char) = 
        match RegisterName.OfChar c with
        | None -> ()
        | Some name ->
            let register = _registerMap.GetRegister name
            x.AppendReplaceString register.StringValue

        _index <- _index + 2

    /// Append the next element from the replace string.  This is typically a character but can 
    /// be more if the character is an escape sequence.  
    ///
    /// This method is responsible for updating _index based on the value consumed
    member private x.AppendNextElement (m : Match) =  
        Contract.Requires (_index < _replacement.Length)

        match _replacement.[_index] with
        | '&' when _replaceData.Magic ->
            x.AppendReplaceString m.Value
            _index <- _index + 1
        | '\\' when (_index + 1) < _replacement.Length ->
            match _replacement.[_index + 1] with
            | '\\' -> _builder.AppendChar '\\'
            | 't' -> _builder.AppendChar '\t'
            | 'n' -> _builder.AppendChar (char 0)
            | '0' -> x.AppendReplaceString m.Value
            | '&' -> 
                if _replaceData.Magic then
                    _builder.AppendChar '&'
                else
                    _builder.AppendChar '&'
            | 'u' -> _caseState <- VimReplaceCaseState.UpperChar
            | 'U' -> _caseState <- VimReplaceCaseState.UpperUntil
            | 'l' -> _caseState <- VimReplaceCaseState.LowerChar
            | 'L' -> _caseState <- VimReplaceCaseState.LowerUtil
            | 'e' -> _caseState <- VimReplaceCaseState.None
            | 'E' -> _caseState <- VimReplaceCaseState.None
            | 'r' -> _builder.AppendString _replaceData.NewLine
            | c -> 
                if c = CharCodes.Enter then
                    _builder.AppendString _replaceData.NewLine
                elif c = '=' && _index + 3 < _replacement.Length && _replacement.[_index + 2] = '@' then
                    x.AppendRegister _replacement.[_index + 3]
                else
                    match CharUtil.GetDigitValue c with 
                    | Some d-> x.AppendGroup m d
                    | None ->
                        _builder.AppendChar '\\'
                        _builder.AppendChar c
            _index <- _index + 2
        | c -> 
            if c = CharCodes.Enter then
                _builder.AppendString _replaceData.NewLine
            else
                x.AppendReplaceChar c
            _index <- _index + 1

    member private x.AppendReplacementCore (m : Match) =
        _caseState <- VimReplaceCaseState.None
        _index <- 0
        while _index < _replacement.Length do
            x.AppendNextElement m

    member private x.AppendReplacement (m : Match) =
        match _replaceData.Count with
        | VimRegexReplaceCount.All -> x.AppendReplacementCore m
        | VimRegexReplaceCount.One when _replaceCount = 0 -> x.AppendReplacementCore m
        | _ -> _builder.AppendString m.Value

        _replaceCount <- _replaceCount + 1

    /// Append the text which occurred before the match specified by this index.
    member private x.AppendInputBefore (matchIndex : int) =
        if matchIndex = 0 then
            let m = _matchCollection.[matchIndex]
            _builder.AppendSubstring _input 0 m.Index
        else
            let current = _matchCollection.[matchIndex]
            let before = _matchCollection.[matchIndex - 1]
            let charSpan = CharSpan.FromBounds _input (before.Index + before.Length) current.Index CharComparer.Exact
            _builder.AppendCharSpan charSpan

    member private x.AppendInputEnd() = 
        let last = _matchCollection.[_matchCollection.Count - 1]
        let charSpan = CharSpan.FromBounds _input (last.Index + last.Length) _input.Length CharComparer.Exact
        _builder.AppendCharSpan charSpan

    member x.Replace (replacement : string) (replaceData : VimRegexReplaceData) = 
        _replaceCount <- 0
        _replacement <- replacement
        _replaceData <- replaceData
        _builder.Length <- 0
        _caseState <- VimReplaceCaseState.None
        _index <- 0

        let mutable matchIndex = 0 
        while matchIndex < _matchCollection.Count do
            let m = _matchCollection.[matchIndex]
            x.AppendInputBefore matchIndex
            x.AppendReplacement m 
            matchIndex <- matchIndex + 1

        x.AppendInputEnd()
        _builder.ToString()

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
    member x.Replace (input : string) (replacement : string) (replaceData : VimRegexReplaceData) (registerMap : IRegisterMap) = 
        let collection = _regex.Matches(input)
        if collection.Count > 0 then
            let util = VimRegexReplaceUtil(input, collection, registerMap)
            util.Replace replacement replaceData
        else
            input

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

/// This type is responsible for converting a vim style regex into a .Net based
/// one.  
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
    let mutable _groupCount = 0
    let mutable _isStartOfPattern = true
    let mutable _isStartOfCollection = false
    let mutable _isRangeOpen = false
    let mutable _isRangeGreedy = false
    let mutable _isCollectionOpen = false
    let mutable _includesNewLine = false
    let mutable _caseSpecifier = CaseSpecifier.None

    member x.Pattern = _pattern

    member x.Index 
        with get () =  _index
        and set value = _index <- value

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

    /// Is this the first character inside of a collection [] construct
    member x.IsStartOfCollection 
        with get() = _isStartOfCollection
        and set value = _isStartOfCollection <- value

    /// Is this in the middle of a range? 
    member x.IsRangeOpen = _isRangeOpen

    /// Is this in the middle of a collection?
    member x.IsCollectionOpen = _isCollectionOpen

    /// Is this in the middle of a group?
    member x.IsGroupOpen = _groupCount > 0

    /// Includes a \n reference
    member x.IncludesNewLine
        with get() = _includesNewLine
        and set value = _includesNewLine <- value

    member x.IsEndOfPattern = x.Index >= x.Pattern.Length

    member x.IncrementGroupCount() = 
        _groupCount <- _groupCount + 1

    member x.DecrementGroupCount() = 
        _groupCount <- _groupCount - 1

    member x.IncrementIndex count =
        _index <- _index + count

    member x.DecrementIndex count = 
        _index <- _index - count

    member x.CharAtIndex = 
        StringUtil.CharAtOption x.Index x.Pattern

    member x.CharAtIndexOrDefault = 
        match StringUtil.CharAtOption x.Index x.Pattern with
        | Some c -> c
        | None -> char 0

    member x.CharAt index = 
        StringUtil.CharAtOption index x.Pattern

    member x.CharAtOrDefault index = 
        match StringUtil.CharAtOption index x.Pattern with
        | None -> char 0
        | Some c -> c

    member x.AppendString str = 
        _builder.AppendString str

    member x.AppendChar c =
        x.Builder.AppendChar c

    member x.AppendEscapedChar c = 
        c |> StringUtil.OfChar |> Regex.Escape |> x.AppendString

    member x.BeginCollection() = 
        x.AppendChar '['
        _isCollectionOpen <- true
        _isStartOfCollection <- true

    member x.EndCollection() = 
        x.AppendChar ']'
        _isCollectionOpen <- false

    member x.BeginRange isGreedy = 
        x.AppendChar '{'
        _isRangeOpen <- true
        _isRangeGreedy <- isGreedy

    member x.EndRange() =
        x.AppendChar '}'
        if not _isRangeGreedy then
            x.AppendChar '?'
        _isRangeOpen <- false

    member x.Break() =
        _isBroken <- true

module VimRegexFactory =

    /// Generates strings based on a char filter func.  Easier than hand writing out
    /// the values
    let GenerateCharString filterFunc = 
        [0 .. 255]
        |> Seq.map (fun x -> char x)
        |> Seq.filter filterFunc
        |> StringUtil.OfCharSeq

    let ControlCharString = GenerateCharString CharUtil.IsControl
    let PunctuationCharString = GenerateCharString Char.IsPunctuation
    let SpaceCharString = GenerateCharString Char.IsWhiteSpace

    let PrintableGroupPattern = 
        let str = [0 .. 255] |> Seq.map (fun i -> char i) |> Seq.filter (fun c -> not (Char.IsControl c)) |> StringUtil.OfCharSeq
        "[" + str + "]"

    let PrintableGroupNoDigitsPattern = 
        let str = [0 .. 255] |> Seq.map (fun i -> char i) |> Seq.filter (fun c -> not (Char.IsControl c) && not (Char.IsDigit c)) |> StringUtil.OfCharSeq
        "[" + str + "]"

    /// These are the named collections specified out inside of :help E769.  These are always
    /// appended inside a .Net [] collection and hence need to be valid in that context
    let NamedCollectionMap = 
        [|
            ("alnum", "A-Za-z0-9") 
            ("alpha", "A-Za-z") 
            ("blank", " \t")
            ("cntrl", ControlCharString)
            ("digit", "0-9")
            ("lower", "a-z")
            ("punct", PunctuationCharString)
            ("space", SpaceCharString)
            ("upper", "A-Z")
            ("xdigit", "A-Fa-f0-9")
            ("return", StringUtil.OfChar CharCodes.Enter)
            ("tab", "`t")
            ("escape", StringUtil.OfChar CharCodes.Escape)
            ("backspace", StringUtil.OfChar CharCodes.Backspace)
        |] |> Map.ofArray

    /// In Vim if a collection is unmatched then it is appended literally into the match 
    /// stream.  Can't determine if it's unmatched though until the string is fully 
    /// processed.  At this point we just go backwards and esacpe it
    let FixOpenCollection (data : VimRegexBuilder) = 
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

        if data.IsCollectionOpen then
            FixOpenCollection data

        if data.IsBroken then
            VimResult.Error Resources.Regex_Unknown
        elif data.IsRangeOpen then
            VimResult.Error Resources.Regex_UnmatchedBrace
        elif data.IsGroupOpen then
            VimResult.Error Resources.Regex_UnmatchedParen
        else 
            let bclPattern = data.Builder.ToString()
            let regex = VimRegexUtils.TryCreateRegex bclPattern regexOptions
            match regex with
            | None -> VimResult.Error Resources.Regex_Unknown
            | Some regex -> VimRegex(data.Pattern, data.CaseSpecifier, bclPattern, regex, data.IncludesNewLine) |> VimResult.Result

    /// Convert the given character as a special character.  Interpretation
    /// may depend on the type of magic that is currently being employed
    let ConvertCharAsSpecial (data : VimRegexBuilder) c = 
        match c with
        | '.' -> data.AppendChar '.'
        | '+' -> data.AppendChar '+'
        | '=' -> data.AppendChar '?'
        | '?' -> data.AppendChar '?'
        | '*' -> data.AppendChar '*'
        | '(' -> 
            data.IncrementGroupCount()
            data.AppendChar '('
        | ')' -> 
            data.DecrementGroupCount()
            data.AppendChar ')'
        | '{' -> 
            if data.IsRangeOpen then
                data.Break()
            elif data.CharAtIndexOrDefault = '-' && data.CharAtOrDefault (data.Index + 1) = '}' then
                data.AppendString "*?"
                data.IncrementIndex 2
            elif data.CharAtIndexOrDefault = '-' then
                data.BeginRange false
                data.IncrementIndex 1
            else
                data.BeginRange true
        | '}' -> if data.IsRangeOpen then data.EndRange() else data.AppendChar '}'
        | '|' -> data.AppendChar '|'
        | '^' -> if data.IsStartOfPattern || data.IsStartOfCollection then data.AppendChar '^' else data.AppendEscapedChar '^'
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
                data.BeginCollection()
        | ']' -> if data.IsCollectionOpen then data.EndCollection() else data.AppendEscapedChar(']')
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
        | 'i' -> data.AppendString @"[0-9_a-zA-Zàáâãäåæçèéêë@]"
        | 'I' -> data.AppendString @"[_a-zA-Zàáâãäåæçèéêë@]"
        | 'k' -> data.AppendString @"[0-9_a-zA-Zàáâãäåæçèéêë@]"
        | 'K' -> data.AppendString @"[_a-zA-Zàáâãäåæçèéêë@]"
        | 'f' -> data.AppendString @"[0-9a-zA-Z@/\.-_+,#$%{}[\]:!~=]"
        | 'F' -> data.AppendString @"[a-zA-Z@/\.-_+,#$%{}[\]:!~=]"
        | 'p' -> data.AppendString PrintableGroupPattern
        | 'P' -> data.AppendString PrintableGroupNoDigitsPattern
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
        | 'i' -> ConvertCharAsSpecial data c
        | 'I' -> ConvertCharAsSpecial data c
        | 'k' -> ConvertCharAsSpecial data c
        | 'K' -> ConvertCharAsSpecial data c
        | 'f' -> ConvertCharAsSpecial data c
        | 'F' -> ConvertCharAsSpecial data c
        | 'p' -> ConvertCharAsSpecial data c
        | 'P' -> ConvertCharAsSpecial data c
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

    /// Try and parse out the name of a named collection.  This is called when the 
    /// index points to ':' assuming this is a valid named collection
    let TryParseNamedCollectionName (data : VimRegexBuilder) = 
        let index = data.Index
        if data.CharAtOrDefault index = ':' then
            let mutable endIndex = index + 1
            while endIndex < data.Pattern.Length && data.CharAtOrDefault endIndex <> ':' do
                endIndex <- endIndex + 1

            if data.CharAtOrDefault (endIndex + 1) = ']' then
                // It's a named collection
                let startIndex = index + 1
                let name = data.Pattern.Substring(startIndex, endIndex - startIndex)
                Some name
            else
                None
        else
            None

    /// Try and append one of the named collections.  These are covered in :help E769.  
    let TryAppendNamedCollection (data : VimRegexBuilder) c = 
        if data.IsCollectionOpen && c = '[' then
            match TryParseNamedCollectionName data with
            | None -> false
            | Some name -> 
                match Map.tryFind name NamedCollectionMap with
                | Some value -> 
                    // Length of the name + characters in the named "::]"
                    data.Index <- data.Index + (name.Length + 3)
                    data.AppendString value
                    true
                | None -> false
        else
            false

    /// Convert a normal unescaped char 
    let ProcessNormalChar (data : VimRegexBuilder) c = 
        if not (TryAppendNamedCollection data c) then

            // Process the normal character based on our current magic kind
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
        let rec inner () : VimResult<VimRegex> =
            if data.IsBroken then 
                VimResult.Error Resources.Regex_Unknown 
            else
                match data.CharAtIndex with
                | None -> CreateVimRegex data 
                | Some '\\' -> 
                    let wasStartOfCollection = data.IsStartOfCollection
                    data.IncrementIndex 1
                    match data.CharAtIndex with 
                    | None -> ProcessNormalChar data '\\'
                    | Some c -> 
                        data.IncrementIndex 1
                        ProcessEscapedChar data c

                    // If we were at the start of a collection before processing this 
                    // char then we no longer are afterwards
                    if wasStartOfCollection then 
                        data.IsStartOfCollection <- false

                    inner ()
                | Some c -> 
                    data.IncrementIndex 1
                    ProcessNormalChar data c |> inner
        inner ()

    let CreateEx pattern options = 

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

    let Create pattern options = 
        match CreateEx pattern options with
        | VimResult.Result vimRegex -> Some vimRegex
        | VimResult.Error _ -> None

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

