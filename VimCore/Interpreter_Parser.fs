#light

namespace Vim.Interpreter
open Vim
open StringBuilderExtensions

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of 'T
    | Failed of string

    with 

    member x.Map (mapFunc : 'T -> ParseResult<'U>) =
        match x with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> mapFunc value

module ParseResultUtil =

    let Map (parseResult : ParseResult<'T>) mapFunc = 
        parseResult.Map mapFunc

type ParserBuilder
    (
        _errorMessage : string
    ) = 

    new () = ParserBuilder(Resources.Parser_Error)

    /// Bind a ParseResult value
    member x.Bind (parseResult : ParseResult<'T>, rest) = 
        match parseResult with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> rest value

    /// Bind an option value
    member x.Bind (parseValue : 'T option, rest) = 
        match parseValue with
        | None -> ParseResult.Failed _errorMessage
        | Some value -> rest value

    member x.Return value =
        ParseResult.Succeeded value

    member x.ReturnFrom value = 
        value

    member x.Zero () = 
        ParseResult.Failed _errorMessage

// TODO: Look at every case of ParseResult.Failed and ensure we are using the appropriate error
// message
[<Sealed>]
type Parser
    (
        _text : string
    ) = 

    let _parserBuilder = ParserBuilder()
    let _tokenizer = Tokenizer(_text)

    /// The set of supported line commands paired with their abbreviation
    static let s_LineCommandNamePair = [
        ("cd", "cd")
        ("chdir", "chd")
        ("close", "clo")
        ("copy", "co")
        ("delete","d")
        ("display","di")
        ("edit", "e")
        ("exit", "exi")
        ("fold", "fo")
        ("global", "g")
        ("join", "j")
        ("lcd", "lc")
        ("lchdir", "lch")
        ("make", "mak")
        ("marks", "")
        ("nohlsearch", "noh")
        ("pwd", "pw")
        ("print", "p")
        ("Print", "P")
        ("put", "pu")
        ("quit", "q")
        ("qall", "qa")
        ("quitall", "quita")
        ("read", "r")
        ("redo", "red")
        ("registers", "reg")
        ("retab", "ret")
        ("set", "se")
        ("source","so")
        ("split", "sp")
        ("substitute", "s")
        ("smagic", "sm")
        ("snomagic", "sno")
        ("t", "t")
        ("tabfirst", "tabfir")
        ("tablast", "tabl")
        ("tabnext", "tabn")
        ("tabNext", "tabN")
        ("tabprevious", "tabp")
        ("tabrewind", "tabr")
        ("undo", "u")
        ("vglobal", "v")
        ("vscmd", "vsc")
        ("write","w")
        ("wq", "")
        ("wall", "wa")
        ("xit", "x")
        ("yank", "y")
        ("/", "")
        ("?", "")
        ("<", "")
        (">", "")
        ("&", "&")
        ("~", "~")
        ("mapclear", "mapc")
        ("nmapclear", "nmapc")
        ("vmapclear", "vmapc")
        ("xmapclear", "xmapc")
        ("smapclear", "smapc")
        ("omapclear", "omapc")
        ("imapclear", "imapc")
        ("cmapclear", "cmapc")
        ("unmap", "unm")
        ("nunmap", "nun")
        ("vunmap", "vu")
        ("xunmap", "xu")
        ("sunmap", "sunm")
        ("ounmap", "ou")
        ("iunmap", "iu")
        ("lunmap", "lu")
        ("cunmap", "cu")
        ("map", "")
        ("nmap", "nm")
        ("vmap", "vm")
        ("xmap", "xm")
        ("smap", "")
        ("omap", "om")
        ("imap", "im")
        ("lmap", "lm")
        ("cmap", "cm")
        ("noremap", "no")
        ("nnoremap", "nn")
        ("vnoremap", "vn")
        ("xnoremap", "xn")
        ("snoremap", "snor")
        ("onoremap", "ono")
        ("inoremap", "ino")
        ("lnoremap", "ln")
        ("cnoremap", "cno")
    ]

    // TODO: Delete.  Force the use of _tokenizer.IncrementIndex
    member x.IncrementIndex() =
        _tokenizer.IncrementIndex()

    // TODO: Delete.  Force the use of _tokenizer.IsAtEndOfLine
    member x.IsAtEndOfLine =
        _tokenizer.IsAtEndOfLine

    /// Move past the white space in the expression text
    member x.SkipBlanks () = 
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Blank -> x.IncrementIndex()
        | _ -> ()

    /// Try and expand the possible abbreviation to a full line command name.  If it's 
    /// not an abbreviation then the original string will be returned
    member x.TryExpand name =

        // Is 'name' an abbreviation of the given command name and abbreviation
        let isAbbreviation (fullName : string) (abbreviation : string) = 
            if name = fullName then
                true
            else 
                name.StartsWith(abbreviation) && fullName.StartsWith(name)

        s_LineCommandNamePair
        |> Seq.filter (fun (name, abbreviation) -> isAbbreviation name abbreviation)
        |> Seq.map fst
        |> SeqUtil.headOrDefault name

    /// Parse out the '!'.  Returns true if a ! was found and consumed
    /// actually skipped
    member x.ParseBang () = 
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '!' ->
            x.IncrementIndex()
            true
        | _ -> false

    /// Parse out the text until the given predicate returns false or the end
    /// of the line is reached.  None is return if the current token when
    /// called doesn't match the predicate
    member x.ParseWhile predicate =
        let builder = System.Text.StringBuilder()
        let rec inner () =
            let token = _tokenizer.CurrentToken
            if token.TokenKind = TokenKind.EndOfLine then
                ()
            elif predicate token then
                builder.AppendString token.TokenText
                x.IncrementIndex()
                inner ()
            else
                ()
        inner ()

        if builder.Length = 0 then
            None
        else
            builder.ToString() |> Some

    /// Parse out a single word from the text.  This will simply take the current cursor
    /// position and move while IsLetter is true.  This will return None if the resulting
    /// string is blank.  This will not skip any blanks
    member x.ParseWord() = 
        x.ParseWhile (fun token -> 
            match token.TokenKind with
            | TokenKind.Character c -> CharUtil.IsLetterOrDigit c
            | _ -> false)

    /// Try and parse out the given word from the text.  If the next word matches the
    /// given string then the parser moves past that word and returns true.  Else the 
    /// index is unchanged and false is returned
    member x.TryParseWord word = 
        let mark = _tokenizer.Index
        match x.ParseWord() with
        | None ->
            false
        | Some foundWord -> 
            if foundWord = word then
                true
            else
                _tokenizer.Rewind mark
                false

    member x.ParseNumber() =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Number number ->
            x.IncrementIndex()
            Some number
        | _ -> None

    /// TODO: Delete.  Or at least verify that 'c' isn't a token vs. TokenKind.Character
    member x.IsCurrentCharValue c = 
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character source -> c = source
        | _ -> false

    /// Parse out a key notation argument.  Different than a word because it can accept items
    /// which are not letters such as numbers, <, >, etc ...
    member x.ParseKeyNotation() = 
        x.ParseWhile (fun token -> 
            match token.TokenKind with 
            | TokenKind.Blank -> false
            | _ -> true)

    /// Parse out the remainder of the line including any trailing blanks
    member x.ParseRestOfLine() = 
        match x.ParseWhile (fun _ -> true) with
        | None -> StringUtil.empty
        | Some text -> text

    /// Parse out the mapclear variants. 
    member x.ParseMapClear allowBang keyRemapModes =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let mapArgumentList = x.ParseMapArguments()

        if hasBang then
            if allowBang then
                LineCommand.ClearKeyMap ([KeyRemapMode.Insert; KeyRemapMode.Command], mapArgumentList) |> ParseResult.Succeeded
            else
                ParseResult.Failed Resources.Parser_NoBangAllowed
        else
            LineCommand.ClearKeyMap (keyRemapModes, mapArgumentList) |> ParseResult.Succeeded

    /// Parse out a number from the stream
    member x.ParseNumberConstant() =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Number number ->
            x.IncrementIndex()
            number |> Value.Number |> Expression.ConstantValue |> ParseResult.Succeeded
        | _ -> ParseResult.Failed "Invalid Number"

    /// Parse out core portion of key mappings.
    member x.ParseMapKeysCore keyRemapModes allowRemap =

        x.SkipBlanks()
        let mapArgumentList = x.ParseMapArguments()
        match x.ParseKeyNotation() with
        | None -> 
            LineCommand.DisplayKeyMap (keyRemapModes, None) |> ParseResult.Succeeded
        | Some leftKeyNotation -> 
            x.SkipBlanks()
            let rightKeyNotation = x.ParseRestOfLine()
            if StringUtil.isBlanks rightKeyNotation then
                LineCommand.DisplayKeyMap (keyRemapModes, Some leftKeyNotation) |> ParseResult.Succeeded
            else
                LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList) |> ParseResult.Succeeded

    /// Parse out the :map commands and all of it's variants (imap, cmap, etc ...)
    member x.ParseMapKeys allowBang keyRemapModes =

        if x.ParseBang() then
            if allowBang then
                x.ParseMapKeysCore [KeyRemapMode.Insert; KeyRemapMode.Command] true
            else
                ParseResult.Failed Resources.Parser_NoBangAllowed
        else
            x.ParseMapKeysCore keyRemapModes true

    /// Parse out the :nomap commands
    member x.ParseMapKeysNoRemap allowBang keyRemapModes =

        if x.ParseBang() then
            if allowBang then
                x.ParseMapKeysCore [KeyRemapMode.Insert; KeyRemapMode.Command] false
            else
                ParseResult.Failed Resources.Parser_NoBangAllowed
        else
            x.ParseMapKeysCore keyRemapModes false

    /// Parse out the unmap variants. 
    member x.ParseMapUnmap allowBang keyRemapModes =

        let inner modes = 
            x.SkipBlanks()
            let mapArgumentList = x.ParseMapArguments()
            match x.ParseKeyNotation() with
            | None -> ParseResult.Failed Resources.Parser_InvalidArgument
            | Some keyNotation -> LineCommand.UnmapKeys (keyNotation, modes, mapArgumentList) |> ParseResult.Succeeded

        if x.ParseBang() then
            if allowBang then
                inner [KeyRemapMode.Insert; KeyRemapMode.Command]
            else
                ParseResult.Failed Resources.Parser_NoBangAllowed
        else
            inner keyRemapModes

    /// Parse out the rest of the text to the end of the line 
    ///
    /// TODO: Delete.  Combine with ParseRestOfLine
    member x.ParseToEndOfLine() = x.ParseRestOfLine()

    /// Parse out a CommandOption value if the caret is currently pointed at one.  If 
    /// there is no CommnadOption here then the index will not change
    member x.ParseCommandOption () = 
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '+' ->
            let mark = _tokenizer.Index

            x.IncrementIndex()
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Number number ->
                x.IncrementIndex()
                CommandOption.StartAtLine number |> Some
            | TokenKind.Character '/' ->
                x.IncrementIndex()
                let pattern = x.ParseToEndOfLine()
                CommandOption.StartAtPattern pattern |> Some
            | TokenKind.Character c ->
                match x.ParseSingleCommand() with
                | ParseResult.Failed _ -> 
                    _tokenizer.Rewind mark
                    None
                | ParseResult.Succeeded lineCommand ->
                    CommandOption.ExecuteLineCommand lineCommand |> Some
            | _ -> 
                // At the end of the line so it's just a '+' option
                CommandOption.StartAtLastLine |> Some
        | _ ->
            None

    /// Parse out the '++opt' paramter to some commands.
    member x.ParseFileOptions () : FileOption list =

        // TODO: Need to implement parsing out FileOption list
        List.empty

    /// Parse out the arguments which can be applied to the various map commands.  If the 
    /// argument isn't there then the index into the line will remain unchanged
    member x.ParseMapArguments() = 

        let rec inner withResult = 
            let mark = _tokenizer.Index

            // Finish without changinging anything.
            let finish() =
                _tokenizer.Rewind mark
                withResult []

            // The argument is mostly parsed out.  Need the closing '>' and the jump to
            // the next element in the list
            let completeArgument mapArgument = 
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Character '>' ->
                    // Skip the '>' and any trailing blanks.  The method was called with
                    // the index pointing past white space and it should end that way
                    x.IncrementIndex()
                    x.SkipBlanks()
                    inner (fun tail -> withResult (mapArgument :: tail))
                | _ -> finish ()

            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character '<' ->
                x.IncrementIndex()
                match x.ParseWord() with
                | None -> finish()
                | Some "buffer" -> completeArgument KeyMapArgument.Buffer
                | Some "silent" -> completeArgument KeyMapArgument.Silent
                | Some "special" -> completeArgument KeyMapArgument.Special
                | Some "script" -> completeArgument KeyMapArgument.Script
                | Some "expr" -> completeArgument KeyMapArgument.Expr 
                | Some "unique" -> completeArgument KeyMapArgument.Unique
                | Some _ -> finish ()
            | _ -> finish ()

        inner (fun x -> x)

    /// Parse out a register value from the text.  This will not parse out numbered register
    member x.ParseRegisterName () = 

        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character c ->
            let name = RegisterName.OfChar c
            if Option.isSome name then
                x.IncrementIndex()

            name
        | _ -> None

    /// Used to parse out the flags for substitute commands.  Will not modify the 
    /// stream if there are no flags
    member x.ParseSubstituteFlags () =

        let rec inner flags isFirst = 
            let newFlag = 
                match _tokenizer.CurrentTokenKind with 
                | TokenKind.Character 'c' -> Some SubstituteFlags.Confirm
                | TokenKind.Character 'r' -> Some SubstituteFlags.UsePreviousSearchPattern
                | TokenKind.Character 'e' -> Some SubstituteFlags.SuppressError
                | TokenKind.Character 'g' -> Some SubstituteFlags.ReplaceAll
                | TokenKind.Character 'i' -> Some SubstituteFlags.IgnoreCase
                | TokenKind.Character 'I' -> Some SubstituteFlags.OrdinalCase
                | TokenKind.Character 'n' -> Some SubstituteFlags.ReportOnly
                | TokenKind.Character 'p' -> Some SubstituteFlags.PrintLast
                | TokenKind.Character 'l' -> Some SubstituteFlags.PrintLastWithList
                | TokenKind.Character '#' -> Some SubstituteFlags.PrintLastWithNumber
                | TokenKind.Character '&' -> Some SubstituteFlags.UsePreviousFlags
                | _  -> None
            match newFlag, isFirst with
            | None, _ -> 
                // No more flags so we are done
                flags
            | Some SubstituteFlags.UsePreviousFlags, false ->
                // The '&' flag is only legal in the first position.  After that
                // it terminates the flag notation
                flags
            | Some newFlag, _ -> 
                x.IncrementIndex()
                inner (flags ||| newFlag) false

        inner SubstituteFlags.None true

    /// Parse out the change directory command.  The path here is optional
    member x.ParseChangeDirectory() =
        // Bang is allowed but has no effect
        x.ParseBang() |> ignore
        x.SkipBlanks()
        let path = 
            if x.IsAtEndOfLine then
                None
            else
                x.ParseToEndOfLine() |> Some
        ParseResult.Succeeded (LineCommand.ChangeDirectory path)

    /// Parse out the change local directory command.  The path here is optional
    member x.ParseChangeLocalDirectory() =
        // Bang is allowed but has no effect
        x.ParseBang() |> ignore

        x.SkipBlanks()
        let path = 
            if x.IsAtEndOfLine then
                None
            else
                x.ParseToEndOfLine() |> Some
        ParseResult.Succeeded (LineCommand.ChangeLocalDirectory path)

    /// Parse out the :close command
    member x.ParseClose() = 
        let isBang = x.ParseBang()
        LineCommand.Close isBang |> ParseResult.Succeeded

    /// Parse out the :copy command.  It has a single required argument that is the destination
    /// address
    member x.ParseCopyTo sourceLineRange = 
        x.SkipBlanks()
        let destinationLineRange = x.ParseLineRange()
        match destinationLineRange with
        | LineRangeSpecifier.None -> ParseResult.Failed Resources.Common_InvalidAddress
        | _ -> LineCommand.CopyTo (sourceLineRange, destinationLineRange) |> ParseResult.Succeeded

    /// Parse out the :delete command
    member x.ParseDelete lineRange = 
        x.SkipBlanks()
        let name = x.ParseRegisterName()
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        LineCommand.Delete (lineRange, name) |> ParseResult.Succeeded

    /// Parse out the :edit command
    member x.ParseEdit () = 
        let hasBang = x.ParseBang()

        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let commandOption = x.ParseCommandOption()

        x.SkipBlanks()
        let fileName = x.ParseToEndOfLine()

        LineCommand.Edit (hasBang, fileOptionList, commandOption, fileName) |> ParseResult.Succeeded

    /// Parse out the :[digit] command
    member x.ParseJumpToLine lineRange =
        match lineRange with
        | LineRangeSpecifier.SingleLine lineSpecifier ->
            match lineSpecifier with
            | LineSpecifier.Number number -> 
                ParseResult.Succeeded (LineCommand.JumpToLine number)
            | LineSpecifier.LastLine ->
                ParseResult.Succeeded LineCommand.JumpToLastLine
            | _ ->
                ParseResult.Failed Resources.Parser_Error
        | _ ->
            ParseResult.Failed Resources.Parser_Error

    /// Parse out the :$ command
    member x.ParseJumpToLastLine() =
        ParseResult.Succeeded (LineCommand.JumpToLastLine)

    /// Parse a {pattern} out of the text.  The text will be consumed until the unescaped value 
    /// 'delimiter' is provided or the end of the input is reached.  The method will return a tuple
    /// of the pattern and a bool.  The bool will represent whether or not the delimiter was found.
    /// If the delimeter is found then it will be consumed
    member x.ParsePattern delimiter = 
        let builder = System.Text.StringBuilder()
        let rec inner () = 
            let token = _tokenizer.CurrentToken
            match token.TokenKind with
            | TokenKind.Character c ->
                if c = delimiter then 
                    x.IncrementIndex()
                    builder.ToString(), true
                elif c = '\\' then
                    x.IncrementIndex()

                    match _tokenizer.CurrentTokenKind with
                    | TokenKind.Character c ->
                        if c <> delimiter then
                            // If the next char is not the delimeter then we have to assume the '\'
                            // is part of an escape for the pattern itself (\(, \1, etc ..) and we
                            // need to leave it in.  
                            builder.AppendChar '\\'

                        builder.AppendChar c
                        x.IncrementIndex()
                    | _ ->
                        ()

                    inner()
                else
                    builder.AppendChar c
                    x.IncrementIndex()
                    inner()
            | TokenKind.EndOfLine ->
                // Hit the end without finding 'delimiter'. 
                builder.ToString(), false
            | _ ->
                builder.AppendString token.TokenText
                x.IncrementIndex()
                inner()

        inner ()

    /// Parse out a LineSpecifier from the text.
    ///
    /// If there is no valid line specifier at the given place in the text then the 
    /// index should not be adjusted
    member x.ParseLineSpecifier () =

        let lineSpecifier = 
            if x.IsCurrentCharValue '.' then
                x.IncrementIndex()
                Some LineSpecifier.CurrentLine
            elif x.IsCurrentCharValue '\'' then
                x.IncrementIndex()
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Character c -> 
                    x.IncrementIndex()
                    c
                    |> Mark.OfChar 
                    |> Option.map LineSpecifier.MarkLine
                | _ -> None
            elif x.IsCurrentCharValue '$' || x.IsCurrentCharValue '%' then
                x.IncrementIndex()
                Some LineSpecifier.LastLine
            elif x.IsCurrentCharValue '/' then

                // It's one of the forward pattern specifiers
                x.IncrementIndex()
                if x.IsCurrentCharValue '/' then
                    Some LineSpecifier.NextLineWithPreviousPattern
                elif x.IsCurrentCharValue '?' then
                    Some LineSpecifier.PreviousLineWithPreviousPattern
                elif x.IsCurrentCharValue '&' then
                    Some LineSpecifier.NextLineWithPreviousSubstitutePattern
                else
                    // Parse out the pattern.  The closing delimeter is required her
                    let pattern, foundDelimeter = x.ParsePattern '/'
                    if foundDelimeter then
                        Some (LineSpecifier.NextLineWithPattern pattern)
                    else
                        None

            elif x.IsCurrentCharValue '?' then
                // It's the ? previous search pattern
                x.IncrementIndex()
                let pattern, foundDelimeter = x.ParsePattern '?'
                if foundDelimeter then
                    Some (LineSpecifier.PreviousLineWithPattern pattern)
                else
                    None

            elif x.IsCurrentCharValue '+' then
                x.IncrementIndex()
                x.ParseNumber() |> Option.map LineSpecifier.AdjustmentOnCurrent
            elif x.IsCurrentCharValue '-' then
                x.IncrementIndex()
                x.ParseNumber() |> Option.map (fun number -> LineSpecifier.AdjustmentOnCurrent -number)
            else 
                match x.ParseNumber() with
                | None -> None
                | Some number -> Some (LineSpecifier.Number number)

        // Need to check for a trailing + or - 
        match lineSpecifier with
        | None ->
            None
        | Some lineSpecifier ->
            let parseAdjustment isNegative = 
                x.IncrementIndex()

                // If no number is specified then 1 is used instead
                let number = x.ParseNumber() |> OptionUtil.getOrDefault 1
                let number = 
                    if isNegative then
                        -number
                    else
                        number

                Some (LineSpecifier.LineSpecifierWithAdjustment (lineSpecifier, number))
            if x.IsCurrentCharValue '+' then
                parseAdjustment false
            elif x.IsCurrentCharValue '-' then
                parseAdjustment true
            else
                Some lineSpecifier

    /// Parse out any valid range node.  This will consider % and any other 
    /// range expression
    member x.ParseLineRange () : LineRangeSpecifier =
        if x.IsCurrentCharValue '%' then
            x.IncrementIndex()
            LineRangeSpecifier.EntireBuffer
        else
            match x.ParseLineSpecifier() with
            | None -> LineRangeSpecifier.None
            | Some left ->

                if x.IsCurrentCharValue ',' || x.IsCurrentCharValue ';' then
                    let isSemicolon = x.IsCurrentCharValue ';'
                    x.IncrementIndex()
                    match x.ParseLineSpecifier() with
                    | None -> LineRangeSpecifier.SingleLine left
                    | Some right -> LineRangeSpecifier.Range (left, right, isSemicolon)
                else
                    LineRangeSpecifier.SingleLine left 

    /// Parse out the valid ex-flags
    member x.ParseLineCommandFlags() = 
        let rec inner flags = 

            let withFlag flag =
                x.IncrementIndex()
                inner (flag ||| flags)

            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character 'l' -> withFlag LineCommandFlags.List
            | TokenKind.Character '#' -> withFlag LineCommandFlags.AddLineNumber
            | TokenKind.Character 'p' -> withFlag LineCommandFlags.Print
            | TokenKind.Character c ->
                if CharUtil.IsBlank c then
                    ParseResult.Succeeded flags
                else 
                    ParseResult.Failed Resources.Parser_InvalidArgument
            | _ -> ParseResult.Succeeded flags

        inner LineCommandFlags.None

    /// Parse out the substitute command.  This should be called with the index just after
    /// the end of the :substitute word
    member x.ParseSubstitute lineRange processFlags = 
        x.SkipBlanks()

        // Is this valid as a search string delimiter
        let isValidDelimiter c = 
            let isBad = 
                CharUtil.IsLetter c ||
                CharUtil.IsDigit c ||
                c = '\\' ||
                c = '"' ||
                c = '|'
            not isBad

        // Need to look at the next char to know if we are parsing out a search string or not for
        // this particular :substitute command
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character delimiter -> 
            if isValidDelimiter delimiter then
                // If this is a valid delimiter then first try and parse out the pattern version
                // of substitute 
                x.IncrementIndex()
                let pattern, foundDelimeter = x.ParsePattern delimiter
                if not foundDelimeter then
                    // When there is no trailing delimeter then the replace string is empty
                    let command = LineCommand.Substitute (lineRange, pattern, "", SubstituteFlags.None)
                    ParseResult.Succeeded command
                else
                    let replace, _ = x.ParsePattern delimiter
                    x.SkipBlanks()
                    let flags = x.ParseSubstituteFlags()
                    let flags = processFlags flags
                    x.SkipBlanks()
                    let count = x.ParseNumber()
                    let lineRange = LineRangeSpecifier.WithEndCount (lineRange, count)
                    let command = LineCommand.Substitute (lineRange, pattern, replace, flags)
                    ParseResult.Succeeded command
            else
                // Without a delimiter it's the repeat variety of the substitute command
                x.ParseSubstituteRepeatCore lineRange processFlags
        | _ ->
            // Without a delimiter it's the repeat variety of the substitute command
            x.ParseSubstituteRepeatCore lineRange processFlags

    /// Parse out the :smagic command
    member x.ParseSubstituteMagic lineRange = 
        x.ParseSubstitute lineRange (fun flags ->
            let flags = Util.UnsetFlag flags SubstituteFlags.Nomagic
            flags ||| SubstituteFlags.Magic)

    /// Parse out the :snomagic command
    member x.ParseSubstituteNoMagic lineRange = 
        x.ParseSubstitute lineRange (fun flags ->
            let flags = Util.UnsetFlag flags SubstituteFlags.Magic
            flags ||| SubstituteFlags.Nomagic)

    /// Parse out the options to the repeat variety of the substitute command
    member x.ParseSubstituteRepeatCore lineRange processFlags =
        x.SkipBlanks()
        let flags = x.ParseSubstituteFlags() |> processFlags

        // Parses out the optional trailing count
        x.SkipBlanks()
        let count = x.ParseNumber()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, count)
        let command = LineCommand.SubstituteRepeat (lineRange, flags)
        ParseResult.Succeeded command

    /// Parse out the repeat variety of the substitute command which is initiated
    /// by the '&' character.
    member x.ParseSubstituteRepeat lineRange extraFlags = 
        x.ParseSubstituteRepeatCore lineRange (fun flags -> flags ||| extraFlags)

    /// Parse out the search commands
    member x.ParseSearch lineRange path =
        let pattern = x.ParseToEndOfLine()
        LineCommand.Search (lineRange, path, pattern) |> ParseResult.Succeeded

    /// Parse out the shift left pattern
    member x.ParseShiftLeft lineRange = 
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        LineCommand.ShiftLeft (lineRange) |> ParseResult.Succeeded

    /// Parse out the shift right pattern
    member x.ParseShiftRight lineRange = 
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        LineCommand.ShiftRight (lineRange) |> ParseResult.Succeeded

    /// Parse out the shell command
    member x.ParseShellCommand () =
        let command = x.ParseRestOfLine()
        LineCommand.ShellCommand command |> ParseResult.Succeeded

    /// Parse out the 'tabnext' command
    member x.ParseTabNext() =   
        x.SkipBlanks()
        let count = x.ParseNumber()
        ParseResult.Succeeded (LineCommand.GoToNextTab count)

    /// Parse out the 'tabprevious' command
    member x.ParseTabPrevious() =   
        x.SkipBlanks()
        let count = x.ParseNumber()
        ParseResult.Succeeded (LineCommand.GoToPreviousTab count)

    /// Parse out the quit and write command.  This includes 'wq', 'xit' and 'exit' commands.
    member x.ParseQuitAndWrite lineRange = 
        let hasBang = x.ParseBang()

        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let fileName =
            if x.IsAtEndOfLine then
                None
            else
                x.ParseToEndOfLine() |> Some

        LineCommand.QuitWithWrite (lineRange, hasBang, fileOptionList, fileName) |> ParseResult.Succeeded

    /// Parse out a visual studio command.  The format is "commandName argument".  The command
    /// name can use letters, numbers and a period.  The rest of the line after will be taken
    /// as the argument
    member x.ParseVisualStudioCommand() = 
        x.SkipBlanks()
        let command = x.ParseWhile (fun token -> 
            match token.TokenKind with 
            | TokenKind.Character c -> CharUtil.IsLetterOrDigit c || c = '.'
            | _ -> false)
        match command with 
        | None -> ParseResult.Failed Resources.Parser_Error
        | Some command ->
            x.SkipBlanks()
            let argument = x.ParseRestOfLine()
            LineCommand.VisualStudioCommand (command, argument) |> ParseResult.Succeeded

    member x.ParseWrite lineRange = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        // Pares out the final fine name if it's provided
        x.SkipBlanks()
        let fileName =
            if x.IsAtEndOfLine then
                None
            else
                x.ParseToEndOfLine() |> Some

        ParseResult.Succeeded (LineCommand.Write (lineRange, hasBang, fileOptionList, fileName))

    member x.ParseWriteAll() =
        let hasBang = x.ParseBang()
        ParseResult.Succeeded (LineCommand.WriteAll hasBang)

    /// Parse out the yank command
    member x.ParseYank lineRange =
        x.SkipBlanks()
        let registerName = x.ParseRegisterName()

        x.SkipBlanks()
        let count = x.ParseNumber()

        LineCommand.Yank (lineRange, registerName, count) |> ParseResult.Succeeded

    /// Parse out the fold command
    member x.ParseFold lineRange =
        LineCommand.Fold lineRange |> ParseResult.Succeeded

    /// Parse out the :global command
    member x.ParseGlobal lineRange =
        let hasBang = x.ParseBang()
        x.ParseGlobalCore lineRange (not hasBang)

    /// Parse out the core global information. 
    member x.ParseGlobalCore lineRange matchPattern =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '\\' -> ParseResult.Failed Resources.Parser_InvalidArgument
        | TokenKind.Character '"' -> ParseResult.Failed Resources.Parser_InvalidArgument
        | TokenKind.Character delimiter ->
            x.IncrementIndex()
            let pattern, foundDelimiter = x.ParsePattern delimiter
            if foundDelimiter then
                let command = x.ParseSingleCommand()
                match command with 
                | ParseResult.Failed msg -> ParseResult.Failed msg
                | ParseResult.Succeeded command -> LineCommand.Global (lineRange, pattern, matchPattern, command) |> ParseResult.Succeeded
            else
                ParseResult.Failed Resources.Parser_InvalidArgument
        | _ -> ParseResult.Failed Resources.Parser_InvalidArgument

    /// Parse out the join command
    member x.ParseJoin lineRange =  
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.Join (lineRange, x.ParseNumber())
        let joinKind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        LineCommand.Join (lineRange, joinKind) |> ParseResult.Succeeded

    /// Parse out the :make command.  The arguments here other than ! are undefined.  Just
    /// get the text blob and let the interpreter / host deal with it 
    member x.ParseMake () = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let arguments = x.ParseToEndOfLine()
        LineCommand.Make (hasBang, arguments) |> ParseResult.Succeeded

    /// Parse out the :put command.  The presence of a bang indicates that we need
    /// to put before instead of after
    member x.ParsePut lineRange =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let registerName = x.ParseRegisterName()

        if hasBang then
            LineCommand.PutBefore (lineRange, registerName) |> ParseResult.Succeeded
        else
            LineCommand.PutAfter (lineRange, registerName) |> ParseResult.Succeeded

    member x.ParsePrint lineRange : ParseResult<LineCommand> = 
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        x.SkipBlanks()
        _parserBuilder { 
            let! flags = x.ParseLineCommandFlags()
            return LineCommand.Print (lineRange, flags) }

    /// Parse out the :read command
    member x.ParseRead lineRange = 
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()
        match fileOptionList with
        | [] ->
            // Can still be the file or command variety.  The ! or lack there of will
            // differentiate it at this point
            x.SkipBlanks()
            if x.IsCurrentCharValue '!' then
                x.IncrementIndex()
                let command = x.ParseToEndOfLine()
                LineCommand.ReadCommand (lineRange, command) |> ParseResult.Succeeded
            else
                let filePath = x.ParseToEndOfLine()
                LineCommand.ReadFile (lineRange, [], filePath) |> ParseResult.Succeeded
        | _ ->
            // Can only be the file variety.
            x.SkipBlanks()
            let filePath = x.ParseToEndOfLine()
            LineCommand.ReadFile (lineRange, fileOptionList, filePath) |> ParseResult.Succeeded

    /// Parse out the :retab command
    member x.ParseRetab lineRange =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let newTabStop = x.ParseNumber()
        LineCommand.Retab (lineRange, hasBang, newTabStop) |> ParseResult.Succeeded

    /// Parse out the :set command and all of it's variants
    member x.ParseSet () = 

        // Parse out an individual option and add it to the 'withArgument' continuation
        let rec parseOption withArgument = 
            x.SkipBlanks()

            // Parse out the next argument and use 'argument' as the value of the current
            // argument
            let parseNext argument = parseOption (fun list -> argument :: list)

            // Parse out an operator.  Parse out the value and use the specified setting name
            // and argument function as the argument
            let parseOperator name argumentFunc = 
                x.IncrementIndex()
                match x.ParseWord() with
                | None -> ParseResult.Failed Resources.Parser_Error
                | Some value -> parseNext (argumentFunc (name, value))

            // Parse out a compound operator.  This is used for '+=' and such.  This will be called
            // with the index pointed at the first character
            let parseCompoundOperator name argumentFunc = 
                x.IncrementIndex()
                if x.IsCurrentCharValue '=' then
                    parseOperator name argumentFunc
                else
                    ParseResult.Failed Resources.Parser_Error

            if x.IsAtEndOfLine then
                let list = withArgument []
                ParseResult.Succeeded (LineCommand.Set list)
            elif x.TryParseWord "all" then
                if x.IsCurrentCharValue '&' then
                    x.IncrementIndex()
                    parseNext SetArgument.ResetAllToDefault
                else
                    parseNext SetArgument.DisplayAllButTerminal
            elif x.TryParseWord "termcap" then
                parseNext SetArgument.DisplayAllTerminal
            else
                match x.ParseWord() with
                | None ->
                     ParseResult.Failed Resources.Parser_Error                   
                | Some name ->
                    if name.StartsWith("no", System.StringComparison.Ordinal) then
                        let option = name.Substring(2)
                        parseNext (SetArgument.ToggleOffSetting option)
                    elif name.StartsWith("inv", System.StringComparison.Ordinal) then
                        let option = name.Substring(3)
                        parseNext (SetArgument.InvertSetting option)
                    else

                        // Need to look at the next character to decide what type of 
                        // argument this is
                        match _tokenizer.CurrentTokenKind with
                        | TokenKind.Character '?' -> x.IncrementIndex(); parseNext (SetArgument.DisplaySetting name)
                        | TokenKind.Character '!' -> x.IncrementIndex(); parseNext (SetArgument.InvertSetting name)
                        | TokenKind.Character ':' -> parseOperator name SetArgument.AssignSetting
                        | TokenKind.Character '=' -> parseOperator name SetArgument.AssignSetting
                        | TokenKind.Character '+' -> parseCompoundOperator name SetArgument.AddSetting
                        | TokenKind.Character '^' -> parseCompoundOperator name SetArgument.MultiplySetting
                        | TokenKind.Character '-' -> parseCompoundOperator name SetArgument.SubtractSetting
                        | TokenKind.Blank -> x.IncrementIndex(); parseNext (SetArgument.UseSetting name)
                        | TokenKind.EndOfLine -> parseNext (SetArgument.UseSetting name)
                        | _ -> ParseResult.Failed Resources.Parser_Error

        parseOption (fun x -> x)

    /// Parse out the :source command.  It can have an optional '!' following it then a file
    /// name 
    member x.ParseSource() =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileName = x.ParseToEndOfLine()
        ParseResult.Succeeded (LineCommand.Source (hasBang, fileName))

    /// Parse out the :split commnad
    member x.ParseSplit lineRange =
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let commandOption = x.ParseCommandOption()

        ParseResult.Succeeded (LineCommand.Split (lineRange, fileOptionList, commandOption))

    /// Parse out the :qal and :quitall commands
    member x.ParseQuitAll () =
        let hasBang = x.ParseBang()
        LineCommand.QuitAll hasBang |> ParseResult.Succeeded

    /// Parse out the :quit command.
    member x.ParseQuit () = 
        let hasBang = x.ParseBang()
        LineCommand.Quit hasBang |> ParseResult.Succeeded

    /// Parse out the :display and :registers command.  Just takes a single argument 
    /// which is the register name
    member x.ParseDisplayRegisters () = 
        x.SkipBlanks()
        let name = x.ParseRegisterName()
        LineCommand.DisplayRegisters name |> ParseResult.Succeeded

    /// Parse out the :marks command.  Handles both the no argument and argument
    /// case
    member x.ParseDisplayMarks () = 
        x.SkipBlanks()

        match x.ParseWord() with
        | None ->
            // Simple case.  No marks to parse out.  Just return them all
            LineCommand.DisplayMarks List.empty |> ParseResult.Succeeded
        | Some word ->

            let mutable message : string option = None
            let list = System.Collections.Generic.List<Mark>()
            for c in word do
                match Mark.OfChar c with
                | None -> message <- Some (Resources.Parser_NoMarksMatching c)
                | Some mark -> list.Add(mark)

            match message with
            | None -> LineCommand.DisplayMarks (List.ofSeq list) |> ParseResult.Succeeded
            | Some message -> ParseResult.Failed message

    /// Parse out a single expression
    member x.ParseSingleCommand () = 

        let lineRange = x.ParseLineRange()

        let noRange parseFunc = 
            match lineRange with
            | LineRangeSpecifier.None -> parseFunc()
            | _ -> ParseResult.Failed Resources.Parser_NoRangeAllowed

        // Get the command name and make sure to expand it to it's possible full
        // name
        let name = 
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character c ->
                if CharUtil.IsAlpha c then
                    x.ParseWhile (fun token ->
                        match token.TokenKind with
                        | TokenKind.Character c -> CharUtil.IsAlpha c
                        | _ -> false)
                    |> OptionUtil.getOrDefault ""
                    |> x.TryExpand
                else
                    x.IncrementIndex()
                    StringUtil.ofChar c
            | _ -> ""

        let parseResult = 
            match name with
            | "cd" -> noRange x.ParseChangeDirectory
            | "chdir" -> noRange x.ParseChangeDirectory
            | "close" -> noRange x.ParseClose
            | "cmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Command])
            | "cmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Command])
            | "cnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Command])
            | "copy" -> x.ParseCopyTo lineRange 
            | "cunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Command])
            | "delete" -> x.ParseDelete lineRange
            | "display" -> noRange x.ParseDisplayRegisters 
            | "edit" -> noRange x.ParseEdit
            | "exit" -> x.ParseQuitAndWrite lineRange
            | "fold" -> x.ParseFold lineRange
            | "global" -> x.ParseGlobal lineRange
            | "iunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Insert])
            | "imap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Insert])
            | "imapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Insert])
            | "inoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Insert])
            | "join" -> x.ParseJoin lineRange 
            | "lcd" -> noRange x.ParseChangeLocalDirectory
            | "lchdir" -> noRange x.ParseChangeLocalDirectory
            | "lmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Language])
            | "lunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Language])
            | "lnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Language])
            | "make" -> noRange x.ParseMake 
            | "marks" -> noRange x.ParseDisplayMarks
            | "map"-> noRange (fun () -> x.ParseMapKeys true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            | "mapclear" -> noRange (fun () -> x.ParseMapClear true [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Command; KeyRemapMode.OperatorPending])
            | "nmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Normal])
            | "nmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Normal])
            | "nnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Normal])
            | "nunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Normal])
            | "nohlsearch" -> noRange (fun () -> LineCommand.NoHighlightSearch |> ParseResult.Succeeded)
            | "noremap"-> noRange (fun () -> x.ParseMapKeysNoRemap true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            | "omap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.OperatorPending])
            | "omapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.OperatorPending])
            | "onoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.OperatorPending])
            | "ounmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.OperatorPending])
            | "put" -> x.ParsePut lineRange
            | "print" -> x.ParsePrint lineRange
            | "pwd" -> noRange (fun () -> ParseResult.Succeeded LineCommand.PrintCurrentDirectory)
            | "quit" -> noRange x.ParseQuit
            | "qall" -> noRange x.ParseQuitAll
            | "quitall" -> noRange x.ParseQuitAll
            | "read" -> x.ParseRead lineRange
            | "redo" -> noRange (fun () -> LineCommand.Redo |> ParseResult.Succeeded)
            | "retab" -> x.ParseRetab lineRange
            | "registers" -> noRange x.ParseDisplayRegisters 
            | "set" -> noRange x.ParseSet
            | "source" -> noRange x.ParseSource
            | "split" -> x.ParseSplit lineRange
            | "substitute" -> x.ParseSubstitute lineRange (fun x -> x)
            | "smagic" -> x.ParseSubstituteMagic lineRange
            | "smap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Select])
            | "smapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Select])
            | "snomagic" -> x.ParseSubstituteNoMagic lineRange
            | "snoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Select])
            | "sunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Select])
            | "t" -> x.ParseCopyTo lineRange 
            | "tabfirst" -> noRange (fun () -> ParseResult.Succeeded LineCommand.GoToFirstTab)
            | "tabrewind" -> noRange (fun () -> ParseResult.Succeeded LineCommand.GoToFirstTab)
            | "tablast" -> noRange (fun () -> ParseResult.Succeeded LineCommand.GoToLastTab)
            | "tabnext" -> noRange x.ParseTabNext 
            | "tabNext" -> noRange x.ParseTabPrevious
            | "tabprevious" -> noRange x.ParseTabPrevious
            | "undo" -> noRange (fun () -> LineCommand.Undo |> ParseResult.Succeeded)
            | "unmap" -> noRange (fun () -> x.ParseMapUnmap true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            | "vglobal" -> x.ParseGlobalCore lineRange false
            | "vmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Visual;KeyRemapMode.Select])
            | "vmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Visual; KeyRemapMode.Select])
            | "vscmd" -> noRange (fun () -> x.ParseVisualStudioCommand ())
            | "vnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Visual;KeyRemapMode.Select])
            | "vunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Visual;KeyRemapMode.Select])
            | "wall" -> noRange x.ParseWriteAll
            | "write" -> x.ParseWrite lineRange
            | "wq" -> x.ParseQuitAndWrite lineRange
            | "xit" -> x.ParseQuitAndWrite lineRange
            | "xmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Visual])
            | "xmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Visual])
            | "xnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Visual])
            | "xunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Visual])
            | "yank" -> x.ParseYank lineRange
            | "/" -> x.ParseSearch lineRange Path.Forward
            | "?" -> x.ParseSearch lineRange Path.Backward
            | "<" -> x.ParseShiftLeft lineRange
            | ">" -> x.ParseShiftRight lineRange
            | "&" -> x.ParseSubstituteRepeat lineRange SubstituteFlags.None
            | "~" -> x.ParseSubstituteRepeat lineRange SubstituteFlags.UsePreviousSearchPattern
            | "!" -> noRange (fun () -> x.ParseShellCommand())
            | "" -> x.ParseJumpToLine lineRange
            | _ -> ParseResult.Failed Resources.Parser_Error

        match parseResult with
        | ParseResult.Failed _ ->
            // If there is already a failure don't look any deeper.
            parseResult
        | ParseResult.Succeeded _ ->
            x.SkipBlanks()

            // If there are still characters then it's illegal trailing characters
            if not x.IsAtEndOfLine then
                ParseResult.Failed Resources.CommandMode_TrailingCharacters
            else
                parseResult

    /// Parse out a single expression
    member x.ParseSingleExpression() =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.String str ->
            x.IncrementIndex()
            Value.String str |> Expression.ConstantValue |> ParseResult.Succeeded
        | TokenKind.Number number ->
            x.IncrementIndex()
            Value.Number number |> Expression.ConstantValue |> ParseResult.Succeeded
        | _ -> ParseResult.Failed "Invalid expression"

    /// Parse out a complete expression from the text.  
    member x.ParseExpressionCore() =
        _parserBuilder {
            let! expr = x.ParseSingleExpression()
            x.SkipBlanks()

            // Parsee out a binary expression
            let parseBinary binaryKind =
                x.IncrementIndex()
                x.SkipBlanks()

                _parserBuilder {
                    let! rightExpr = x.ParseSingleExpression()
                    return Expression.Binary (binaryKind, expr, rightExpr)
                }

            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character '+' -> return! parseBinary BinaryKind.Add
            | TokenKind.Character '/' -> return! parseBinary BinaryKind.Divide
            | TokenKind.Character '*' -> return! parseBinary BinaryKind.Multiply
            | TokenKind.Character '.' -> return! parseBinary BinaryKind.Concatenate
            | TokenKind.Character '-' -> return! parseBinary BinaryKind.Subtract
            | TokenKind.Character '%' -> return! parseBinary BinaryKind.Modulo
            | _ -> return expr
        }

    static member ParseRange rangeText = 
        let parser = Parser(rangeText)
        (parser.ParseLineRange(), parser.ParseRestOfLine())

    static member ParseExpression (expressionText : string) : ParseResult<Expression> = 
        let parser = Parser(expressionText)
        parser.ParseExpressionCore()

    static member ParseLineCommand (commandText : string) = 
        let parser = Parser(commandText)
        parser.ParseSingleCommand()

