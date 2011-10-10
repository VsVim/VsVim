#light

namespace Vim.Interpreter
open Vim

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of 'T
    | Failed of string

type ParseLineCommand = LineRange option -> ParseResult<LineCommand>

[<Sealed>]
type Parser
    (
        _text : string
    ) = 

    /// The set of supported line commands paired with their abbreviation
    static let s_LineCommandNamePair = [
        ("cd", "cd")
        ("chdir", "chd")
        ("close", "clo")
        ("delete","d")
        ("edit", "e")
        ("exit", "exi")
        ("display","di")
        ("fold", "fo")
        ("join", "j")
        ("lcd", "lc")
        ("lchdir", "lch")
        ("make", "mak")
        ("marks", "")
        ("nohlsearch", "noh")
        ("pwd", "pw")
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
        ("tabfirst", "tabfir")
        ("tablast", "tabl")
        ("tabnext", "tabn")
        ("tabNext", "tabN")
        ("tabprevious", "tabp")
        ("tabrewind", "tabr")
        ("undo", "u")
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

    /// Current index into the expression text
    let mutable _index = 0

    member x.CurrentChar =
        if _index >= _text.Length then
            None
        else
            Some _text.[_index]

    /// Is the parser at the end of the line
    member x.IsAtEndOfLine = _index = _text.Length

    member x.IsCurrentChar predicate = 
        match x.CurrentChar with
        | None -> false
        | Some c -> predicate c

    member x.IsCurrentCharValue value =
        match x.CurrentChar with
        | None -> false
        | Some c -> c = value

    member x.RemainingText =
        _text.Substring(_index)

    member x.IncrementIndex() =
        if _index < _text.Length then
            _index <- _index + 1

    /// Move past the white space in the expression text
    member x.SkipBlanks () = 
        if x.IsCurrentChar CharUtil.IsBlank then
            x.IncrementIndex()
            x.SkipBlanks()
        else
            ()

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

    /// Try and parse out the given word from the text.  If the next word matches the
    /// given string then the parser moves past that word and returns true.  Else the 
    /// index is unchanged and false is returned
    member x.TryParseWord word = 
        let mark = _index
        match x.ParseWord() with
        | None ->
            false
        | Some foundWord -> 
            if foundWord = word then
                true
            else
                _index <- mark
                false

    /// Parse out the '!'.  Returns true if a ! was found and consumed
    /// actually skipped
    member x.ParseBang () = 
        if x.IsCurrentChar (fun c -> c = '!') then
            x.IncrementIndex()
            true
        else
            false

    /// Parse out the text until the given predicate returns false or the end
    /// of the line is reached.  None is return if the current char when
    /// called doesn't match the predicate
    member x.ParseWhile predicate =
        if x.IsCurrentChar predicate then
            let startIndex = _index
            x.IncrementIndex()
            let length = 
                let rec inner () = 
                    if x.IsCurrentChar predicate then
                        x.IncrementIndex()
                        inner ()
                inner()
                _index - startIndex
            let text = _text.Substring(startIndex, length)
            Some text
        else
            None

    /// Parse out a single word from the text.  This will simply take the current cursor
    /// position and move while IsLetter is true.  This will return None if the resulting
    /// string is blank.  This will not skip any blanks
    member x.ParseWord() = x.ParseWhile CharUtil.IsLetterOrDigit

    /// Parse out a key notation argument.  Different than a word because it can accept items
    /// which are not letters such as numbers, <, >, etc ...
    member x.ParseKeyNotation() = x.ParseWhile CharUtil.IsNotBlank

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

    /// Parse out core portion of key mappings.
    member x.ParseMapKeysCore keyRemapModes allowRemap =

        x.SkipBlanks()
        let mapArgumentList = x.ParseMapArguments()
        match x.ParseKeyNotation() with
        | None -> 
            LineCommand.DisplayKeyMap (keyRemapModes, None) |> ParseResult.Succeeded
        | Some leftKeyNotation -> 
            x.SkipBlanks()
            match x.ParseKeyNotation() with
            | None ->
                LineCommand.DisplayKeyMap (keyRemapModes, Some leftKeyNotation) |> ParseResult.Succeeded
            | Some rightKeyNotation ->
                LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList) |> ParseResult.Succeeded

    /// Parse out the :map commands
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

    /// Parse out a number from the current text
    member x.ParseNumber () = 

        // If c is a digit char then return back the digit
        let toDigit c = 
            if CharUtil.IsDigit c then
                (int c) - (int '0') |> Some
            else
                None

        // Get the current char as a digit if it is one
        let currentAsChar () = 
            match x.CurrentChar with
            | None -> None
            | Some c -> toDigit c

        let rec inner value = 
            match currentAsChar() with
            | None -> 
                value
            | Some number ->
                let value = (value * 10) + number
                x.IncrementIndex()
                inner value

        match currentAsChar() with
        | None -> 
            None
        | Some number -> 
            x.IncrementIndex()
            inner number |> Some

    /// Parse out the rest of the text to the end of the line 
    member x.ParseToEndOfLine() =
        let text = x.RemainingText
        _index <- _text.Length
        text

    /// Parse out a CommandOption value if the caret is currently pointed at one.  If 
    /// there is no CommnadOption here then the index will not change
    member x.ParseCommandOption () = 
        if x.IsCurrentCharValue '+' then
            let mark = _index

            x.IncrementIndex()
            match x.CurrentChar with
            | None ->
                // At the end of the line so it's just a '+' option
                CommandOption.StartAtLastLine |> Some
            | Some c ->
                if CharUtil.IsDigit c then
                    let number = x.ParseNumber() |> Option.get
                    CommandOption.StartAtLine number |> Some
                elif c = '/' then
                    x.IncrementIndex()
                    let pattern = x.ParseToEndOfLine()
                    CommandOption.StartAtPattern pattern |> Some
                else
                    match x.ParseSingleCommand() with
                    | ParseResult.Failed _ -> 
                        _index <- mark
                        None
                    | ParseResult.Succeeded lineCommand ->
                        CommandOption.ExecuteLineCommand lineCommand |> Some
        else
            None

    /// Parse out the '++opt' paramter to some commands.
    member x.ParseFileOptions () : FileOption list =

        // TODO: Need to implement parsing out FileOption list
        List.empty

    /// Parse out the arguments which can be applied to the various map commands.  If the 
    /// argument isn't there then the index into the line will remain unchanged
    member x.ParseMapArguments() = 

        let rec inner withResult = 
            let mark = _index 

            // Finish without changinging anything.
            let finish() =
                _index <- mark
                withResult []

            // The argument is mostly parsed out.  Need the closing '>' and the jump to
            // the next element in the list
            let completeArgument mapArgument = 
                if x.IsCurrentCharValue '>' then
                    // Skip the '>' and any trailing blanks.  The method was called with
                    // the index pointing past white space and it should end that way
                    x.IncrementIndex()
                    x.SkipBlanks()
                    inner (fun tail -> withResult (mapArgument :: tail))
                else
                    finish()

            if x.IsCurrentCharValue '<' then
                x.IncrementIndex()
                match x.ParseWord() with
                | None -> finish()
                | Some "buffer" -> completeArgument MapArgument.Buffer
                | Some "silent" -> completeArgument MapArgument.Silent
                | Some "special" -> completeArgument MapArgument.Special
                | Some "script" -> completeArgument MapArgument.Script
                | Some "expr" -> completeArgument MapArgument.Expr 
                | Some "unique" -> completeArgument MapArgument.Unique
                | Some _ -> finish()
            else
                finish()

        inner (fun x -> x)

    /// Parse out a register value from the text.  This will not parse out numbered register
    member x.ParseRegisterName () = 

        if x.IsCurrentChar CharUtil.IsDigit then
            // Don't parse out numbered registers  Many commands can be followed by both
            // registers and counts.  Numbered registers are disallowed because they cause
            // an ambiguity with count
            None
        else

            let name = 
                x.CurrentChar
                |> OptionUtil.map2 RegisterName.OfChar
    
            if Option.isSome name then
                x.IncrementIndex()
    
            name

    /// Used to parse out the flags for substitute commands.  Will not modify the 
    /// stream if there are no flags
    member x.ParseSubstituteFlags () =

        let rec inner flags isFirst = 
            match x.CurrentChar with
            | None ->
                flags
            | Some c ->
                let newFlag = 
                    match c with 
                    | 'c' -> Some SubstituteFlags.Confirm
                    | 'r' -> Some SubstituteFlags.UsePreviousSearchPattern
                    | 'e' -> Some SubstituteFlags.SuppressError
                    | 'g' -> Some SubstituteFlags.ReplaceAll
                    | 'i' -> Some SubstituteFlags.IgnoreCase
                    | 'I' -> Some SubstituteFlags.OrdinalCase
                    | 'n' -> Some SubstituteFlags.ReportOnly
                    | 'p' -> Some SubstituteFlags.PrintLast
                    | 'l' -> Some SubstituteFlags.PrintLastWithList
                    | '#' -> Some SubstituteFlags.PrintLastWithNumber
                    | '&' -> Some SubstituteFlags.UsePreviousFlags
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

    /// Parse out the :delete command
    member x.ParseDelete lineRange = 
        x.SkipBlanks()
        let name = x.ParseRegisterName()
        x.SkipBlanks()
        let count = x.ParseNumber()
        LineCommand.Delete (lineRange, name, count) |> ParseResult.Succeeded

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
        | LineRange.SingleLine lineSpecifier ->
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

    /// Parse out a single char from the text
    member x.ParseChar() = 
        match x.CurrentChar with
        | None -> 
            None
        | Some c -> 
            x.IncrementIndex()
            Some c

    /// Parse a {pattern} out of the text.  The text will be consumed until the unescaped value 
    /// 'delimiter' is provided or the end of the input is reached.  The method will return a tuple
    /// of the pattern and a bool.  The bool will represent whether or not the delimiter was found.
    /// If the delimeter is found then it will be consumed
    member x.ParsePattern delimiter = 
        let builder = System.Text.StringBuilder()
        let rec inner () = 
            match x.CurrentChar with
            | None -> 
                // Hit the end without finding 'delimiter'. 
                builder.ToString(), false
            | Some c -> 
                if c = delimiter then 
                    x.IncrementIndex()
                    builder.ToString(), true
                elif c = '\\' then
                    x.IncrementIndex()

                    match x.CurrentChar with
                    | None ->
                        ()
                    | Some c ->
                        if c <> delimiter then
                            // If the next char is not the delimeter then we have to assume the '\'
                            // is part of an escape for the pattern itself (\(, \1, etc ..) and we
                            // need to leave it in.  
                            builder.Append('\\') |> ignore

                        builder.Append(c) |> ignore
                        x.IncrementIndex()

                    inner()
                else
                    builder.Append(c) |> ignore
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
                x.ParseChar() 
                |> OptionUtil.map2 Mark.OfChar
                |> Option.map LineSpecifier.MarkLine
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
    member x.ParseLineRange () =
        if x.IsCurrentCharValue '%' then
            x.IncrementIndex()
            LineRange.EntireBuffer |> Some
        else
            match x.ParseLineSpecifier() with
            | None ->
                None
            | Some left ->
                if x.IsCurrentCharValue ',' then
                    x.IncrementIndex()
                    x.ParseLineSpecifier()
                    |> Option.map (fun right -> LineRange.Range (left, right, false))
                elif x.IsCurrentCharValue ';' then
                    x.IncrementIndex()
                    x.ParseLineSpecifier()
                    |> Option.map (fun right -> LineRange.Range (left, right, true))
                else
                    LineRange.SingleLine left |> Some

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
        if x.IsCurrentChar isValidDelimiter then
            // If this is a valid delimiter then first try and parse out the pattern version
            // of substitute 
            let delimiter = Option.get x.CurrentChar
            x.IncrementIndex()
            let pattern, foundDelimeter = x.ParsePattern delimiter
            if not foundDelimeter then
                // When there is no trailing delimeter then the replace string is empty
                let command = LineCommand.Substitute (lineRange, pattern, "", SubstituteFlags.None, None)
                ParseResult.Succeeded command
            else
                let replace, _ = x.ParsePattern delimiter
                x.SkipBlanks()
                let flags = x.ParseSubstituteFlags()
                let flags = processFlags flags
                x.SkipBlanks()
                let count = x.ParseNumber()
                let command = LineCommand.Substitute (lineRange, pattern, replace, flags, count)
                ParseResult.Succeeded command
        else
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

        // Pares out the optional trailing count
        x.SkipBlanks()
        let count = x.ParseNumber()
        let command = LineCommand.SubstituteRepeat (lineRange, flags, count)
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
        let count = x.ParseNumber()
        LineCommand.ShiftLeft (lineRange, count) |> ParseResult.Succeeded

    /// Parse out the shift right pattern
    member x.ParseShiftRight lineRange = 
        x.SkipBlanks()
        let count = x.ParseNumber()
        LineCommand.ShiftRight (lineRange, count) |> ParseResult.Succeeded

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
            match x.CurrentChar with
            | None -> None
            | Some _ -> x.ParseToEndOfLine() |> Some

        LineCommand.QuitWithWrite (lineRange, hasBang, fileOptionList, fileName) |> ParseResult.Succeeded

    member x.ParseWrite lineRange = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        // Pares out the final fine name if it's provided
        x.SkipBlanks()
        let fileName =
            match x.CurrentChar with
            | None -> None
            | Some _ -> x.ParseToEndOfLine() |> Some

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

    /// Parse out the join command
    member x.ParseJoin lineRange =  
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let count = x.ParseNumber()
        let joinKind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        LineCommand.Join (lineRange, joinKind, count) |> ParseResult.Succeeded

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
                        match x.CurrentChar with
                        | None -> 
                            parseNext (SetArgument.UseSetting name)
                        | Some c ->
                            match c with 
                            | '?' -> x.IncrementIndex(); parseNext (SetArgument.DisplaySetting name)
                            | '!' -> x.IncrementIndex(); parseNext (SetArgument.InvertSetting name)
                            | ':' -> parseOperator name SetArgument.AssignSetting
                            | '=' -> parseOperator name SetArgument.AssignSetting
                            | '+' -> parseCompoundOperator name SetArgument.AddSetting
                            | '^' -> parseCompoundOperator name SetArgument.MultiplySetting
                            | '-' -> parseCompoundOperator name SetArgument.SubtractSetting
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
            | None -> parseFunc()
            | Some _ -> ParseResult.Failed Resources.Parser_NoRangeAllowed

        // Get the command name and make sure to expand it to it's possible full
        // name
        let name = 
            if x.IsCurrentChar CharUtil.IsAlpha then
                x.ParseWhile CharUtil.IsAlpha
                |> OptionUtil.getOrDefault ""
                |> x.TryExpand
            else
                match x.CurrentChar with
                | None -> 
                    ""
                | Some c -> 
                    x.IncrementIndex()
                    StringUtil.ofChar c

        let parseResult = 
            match name with
            | "cd" -> noRange x.ParseChangeDirectory
            | "chdir" -> noRange x.ParseChangeDirectory
            | "close" -> noRange x.ParseClose
            | "cmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Command])
            | "cmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Command])
            | "cnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Command])
            | "cunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Command])
            | "delete" -> x.ParseDelete lineRange
            | "display" -> noRange x.ParseDisplayRegisters 
            | "edit" -> noRange x.ParseEdit
            | "exit" -> x.ParseQuitAndWrite lineRange
            | "fold" -> x.ParseFold lineRange
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
            | "pwd" -> noRange (fun () -> ParseResult.Succeeded LineCommand.PrintCurrentDirectory)
            | "quit" -> noRange x.ParseQuit
            | "qall" -> noRange x.ParseQuitAll
            | "quitall" -> noRange x.ParseQuitAll
            | "read" -> x.ParseRead lineRange
            | "redo" -> noRange (fun () -> LineCommand.Redo |> ParseResult.Succeeded)
            | "retab" -> x.ParseRetab lineRange
            | "set" -> noRange x.ParseSet
            | "source" -> noRange x.ParseSource
            | "split" -> x.ParseSplit lineRange
            | "registers" -> noRange x.ParseDisplayRegisters 
            | "substitute" -> x.ParseSubstitute lineRange (fun x -> x)
            | "smagic" -> x.ParseSubstituteMagic lineRange
            | "smap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Select])
            | "smapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Select])
            | "snomagic" -> x.ParseSubstituteNoMagic lineRange
            | "snoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Select])
            | "sunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Select])
            | "tabfirst" -> noRange (fun () -> ParseResult.Succeeded LineCommand.GoToFirstTab)
            | "tabrewind" -> noRange (fun () -> ParseResult.Succeeded LineCommand.GoToFirstTab)
            | "tablast" -> noRange (fun () -> ParseResult.Succeeded LineCommand.GoToLastTab)
            | "tabnext" -> noRange x.ParseTabNext 
            | "tabNext" -> noRange x.ParseTabPrevious
            | "tabprevious" -> noRange x.ParseTabPrevious
            | "undo" -> noRange (fun () -> LineCommand.Undo |> ParseResult.Succeeded)
            | "unmap" -> noRange (fun () -> x.ParseMapUnmap true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
            | "vmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Visual;KeyRemapMode.Select])
            | "vmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Visual; KeyRemapMode.Select])
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
            | "" -> match lineRange with | Some lineRange -> x.ParseJumpToLine lineRange | None -> ParseResult.Failed Resources.Parser_Error
            | _ -> ParseResult.Failed Resources.Parser_Error

        match parseResult with
        | ParseResult.Failed _ ->
            // If there is already a failure don't look any deeper.
            parseResult
        | ParseResult.Succeeded _ ->
            x.SkipBlanks()

            // If there are still characters then it's illegal trailing characters
            if Option.isSome x.CurrentChar then
                ParseResult.Failed Resources.CommandMode_TrailingCharacters
            else
                parseResult

    // TODO: Delete.  This is just a transition hack to allow us to use the new interpreter and parser
    // to replace RangeUtil.ParseRange
    static member ParseRange rangeText = 
        let parser = Parser(rangeText)
        let lineRange = parser.ParseLineRange()
        match lineRange with 
        | None -> ParseResult.Failed Resources.Parser_Error
        | Some lineRange -> ParseResult.Succeeded (lineRange, parser.RemainingText) 

    static member ParseExpression (expressionText : string) : ParseResult<Expression> = 
        ParseResult.Failed Resources.Parser_Error

    static member ParseLineCommand (commandText : string) = 
        let parser = Parser(commandText)
        parser.ParseSingleCommand()

