#light

namespace Vim.Interpreter
open Vim
open System.Collections.Generic
open StringBuilderExtensions

[<RequireQualifiedAccess>]
type ParseRegisterName =
    | All
    | NoNumbered

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of Value: 'T
    | Failed of Error: string

    with 

    member x.Map (mapFunc: 'T -> ParseResult<'U>) =
        match x with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> mapFunc value

module ParseResultUtil =

    let Map (parseResult: ParseResult<'T>) mapFunc = 
        parseResult.Map mapFunc

    let ConvertToLineCommand (parseResult: ParseResult<LineCommand>) =
        match parseResult with
        | ParseResult.Failed msg -> LineCommand.ParseError msg
        | ParseResult.Succeeded lineCommand -> lineCommand

type ParseResultBuilder
    (
        _parser: Parser,
        _errorMessage: string
    ) = 

    new (parser) = ParseResultBuilder(parser, Resources.Parser_Error)

    /// Bind a ParseResult value
    member x.Bind (parseResult: ParseResult<'T>, (rest: 'T -> ParseResult<'U>)) = 
        match parseResult with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> rest value

    /// Bind an option value
    member x.Bind (parseValue: 'T option, (rest: 'T -> ParseResult<'U>)) = 
        match parseValue with
        | None -> ParseResult.Failed _errorMessage
        | Some value -> rest value

    member x.Return (value: 'T) =
        ParseResult.Succeeded value

    member x.Return (parseResult: ParseResult<'T>) =
        match parseResult with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> ParseResult.Succeeded value

    member x.Return (msg: string) = 
        ParseResult.Failed msg

    member x.ReturnFrom value = 
        value

    member x.Zero () = 
        ParseResult.Failed _errorMessage

and LineCommandBuilder
    (
        _parser: Parser,
        _errorMessage: string
    ) = 

    new (parser) = LineCommandBuilder(parser, Resources.Parser_Error)

    /// Bind a ParseResult value
    member x.Bind (parseResult: ParseResult<'T>, rest) = 
        match parseResult with
        | ParseResult.Failed msg -> _parser.ParseError msg
        | ParseResult.Succeeded value -> rest value

    /// Bind an option value
    member x.Bind (parseValue: 'T option, rest) = 
        match parseValue with
        | None -> _parser.ParseError _errorMessage
        | Some value -> rest value

    member x.Return (value: LineCommand) =
        value

    member x.Return (parseResult: ParseResult<LineCommand>) =
        match parseResult with
        | ParseResult.Failed msg -> _parser.ParseError msg
        | ParseResult.Succeeded lineCommand -> lineCommand

    member x.Return (msg: string) = 
        _parser.ParseError msg

    member x.ReturnFrom value = 
        value

    member x.Zero () = 
        _parser.ParseError _errorMessage

and [<Sealed>] Parser
    (
        _globalSettings: IVimGlobalSettings,
        _vimData: IVimData
    ) as this = 

    let _parseResultBuilder = ParseResultBuilder(this)
    let _lineCommandBuilder = LineCommandBuilder(this)
    let _tokenizer = Tokenizer("", TokenizerFlags.None)
    let mutable _lines = [|""|] 
    let mutable _lineIndex = 0

    /// The set of supported line commands paired with their abbreviation
    static let s_LineCommandNamePair = [
        ("autocmd", "au")
        ("behave", "be")
        ("call", "cal")
        ("cd", "cd")
        ("chdir", "chd")
        ("close", "clo")
        ("copy", "co")
        ("csx", "cs")
        ("csxe", "csxe")
        ("delete","d")
        ("delmarks", "delm")
        ("digraphs", "dig")
        ("display","di")
        ("echo", "ec")
        ("edit", "e")
        ("else", "el")
        ("execute", "exe")
        ("elseif", "elsei")
        ("endfunction", "endf")
        ("endif", "en")
        ("exit", "exi")
        ("fold", "fo")
        ("function", "fu")
        ("global", "g")
        ("help", "h")
        ("vimhelp", "vimh")
        ("history", "his")
        ("if", "if")
        ("join", "j")
        ("lcd", "lc")
        ("lchdir", "lch")
        ("list", "l")
        ("let", "let")
        ("move", "m")
        ("make", "mak")
        ("marks", "")
        ("nohlsearch", "noh")
        ("normal", "norm")
        ("number", "nu")
        ("only", "on")
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
        ("shell", "sh")
        ("sort", "sor")
        ("source","so")
        ("split", "sp")
        ("stopinsert", "stopi")
        ("substitute", "s")
        ("smagic", "sm")
        ("snomagic", "sno")
        ("t", "t")
        ("tabedit", "tabe")
        ("tabfirst", "tabfir")
        ("tablast", "tabl")
        ("tabnew", "tabnew")
        ("tabnext", "tabn")
        ("tabNext", "tabN")
        ("tabonly", "tabo")
        ("tabprevious", "tabp")
        ("tabrewind", "tabr")
        ("undo", "u")
        ("unlet", "unl")
        ("vglobal", "v")
        ("version", "ve")
        ("vscmd", "vsc")
        ("vsplit", "vs")
        ("wqall", "wqa")
        ("write","w")
        ("wq", "")
        ("wall", "wa")
        ("xall", "xa")
        ("xit", "x")
        ("yank", "y")
        ("/", "")
        ("?", "")
        ("<", "")
        (">", "")
        ("&", "")
        ("~", "")
        ("#", "")
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
        ("cwindow", "cw")
        ("cfirst", "cfir")
        ("clast", "cla")
        ("cnext", "cn")
        ("cNext", "cN")
        ("cprevious", "cp")
        ("crewind", "cr")
        ("lwindow", "lw")
        ("lfirst", "lfir")
        ("llast", "lla")
        ("lnext", "lne")
        ("lprevious", "lp")
        ("lNext", "lN")
        ("lrewind", "lr")
    ]

    /// Map of all autocmd events to the lower case version of the name
    static let s_NameToEventKindMap = 
        [
            ("bufnewfile", EventKind.BufNewFile)
            ("bufreadpre", EventKind.BufReadPre)
            ("bufread", EventKind.BufRead)
            ("bufreadpost", EventKind.BufReadPost)
            ("bufreadcmd", EventKind.BufReadCmd)
            ("filereadpre", EventKind.FileReadPre)
            ("filereadpost", EventKind.FileReadPost)
            ("filereadcmd", EventKind.FileReadCmd)
            ("filterreadpre", EventKind.FilterReadPre)
            ("filterreadpost", EventKind.FilterReadPost)
            ("stdinreadpre", EventKind.StdinReadPre)
            ("stdinreadpost", EventKind.StdinReadPost)
            ("bufwrite", EventKind.BufWrite)
            ("bufwritepre", EventKind.BufWritePre)
            ("bufwritepost", EventKind.BufWritePost)
            ("bufwritecmd", EventKind.BufWriteCmd)
            ("filewritepre", EventKind.FileWritePre)
            ("filewritepost", EventKind.FileWritePost)
            ("filewritecmd", EventKind.FileWriteCmd)
            ("fileappendpre", EventKind.FileAppendPre)
            ("fileappendpost", EventKind.FileAppendPost)
            ("fileappendcmd", EventKind.FileAppendCmd)
            ("filterwritepre", EventKind.FilterWritePre)
            ("filterwritepost", EventKind.FilterWritePost)
            ("bufadd", EventKind.BufAdd)
            ("bufcreate", EventKind.BufCreate)
            ("bufdelete", EventKind.BufDelete)
            ("bufwipeout", EventKind.BufWipeout)
            ("buffilepre", EventKind.BufFilePre)
            ("buffilepost", EventKind.BufFilePost)
            ("bufenter", EventKind.BufEnter)
            ("bufleave", EventKind.BufLeave)
            ("bufwinenter", EventKind.BufWinEnter)
            ("bufwinleave", EventKind.BufWinLeave)
            ("bufunload", EventKind.BufUnload)
            ("bufhidden", EventKind.BufHidden)
            ("bufnew", EventKind.BufNew)
            ("swapexists", EventKind.SwapExists)
            ("filetype", EventKind.FileType)
            ("syntax", EventKind.Syntax)
            ("encodingchanged", EventKind.EncodingChanged)
            ("termchanged", EventKind.TermChanged)
            ("vimenter", EventKind.VimEnter)
            ("guienter", EventKind.GUIEnter)
            ("termresponse", EventKind.TermResponse)
            ("vimleavepre", EventKind.VimLeavePre)
            ("vimleave", EventKind.VimLeave)
            ("filechangedshell", EventKind.FileChangedShell)
            ("filechangedshellpost", EventKind.FileChangedShellPost)
            ("filechangedro", EventKind.FileChangedRO)
            ("shellcmdpost", EventKind.ShellCmdPost)
            ("shellfilterpost", EventKind.ShellFilterPost)
            ("funcundefined", EventKind.FuncUndefined)
            ("spellfilemissing", EventKind.SpellFileMissing)
            ("sourcepre", EventKind.SourcePre)
            ("sourcecmd", EventKind.SourceCmd)
            ("vimresized", EventKind.VimResized)
            ("focusgained", EventKind.FocusGained)
            ("focuslost", EventKind.FocusLost)
            ("cursorhold", EventKind.CursorHold)
            ("cursorholdi", EventKind.CursorHoldI)
            ("cursormoved", EventKind.CursorMoved)
            ("cursormovedi", EventKind.CursorMovedI)
            ("winenter", EventKind.WinEnter)
            ("winleave", EventKind.WinLeave)
            ("tabenter", EventKind.TabEnter)
            ("tableave", EventKind.TabLeave)
            ("cmdwinenter", EventKind.CmdwinEnter)
            ("cmdwinleave", EventKind.CmdwinLeave)
            ("insertenter", EventKind.InsertEnter)
            ("insertchange", EventKind.InsertChange)
            ("insertleave", EventKind.InsertLeave)
            ("colorscheme", EventKind.ColorScheme)
            ("remotereply", EventKind.RemoteReply)
            ("quickfixcmdpre", EventKind.QuickFixCmdPre)
            ("quickfixcmdpost", EventKind.QuickFixCmdPost)
            ("sessionloadpost", EventKind.SessionLoadPost)
            ("menupopup", EventKind.MenuPopup)
            ("user", EventKind.User)
        ]
        |> Map.ofList

    new (globalSettings, vimData, lines) as this =
        Parser(globalSettings, vimData)
        then
            this.Reset lines

    member x.IsDone = _tokenizer.IsAtEndOfLine && _lineIndex  + 1 >= _lines.Length

    member x.ContextLineNumber = _lineIndex

    /// Parse out the token stream so long as it matches the input.  If everything matches
    /// the tokens will be consumed and 'true' will be returned.  Else 'false' will be 
    /// returned and the token stream will be unchanged
    member x.ParseTokenSequence texts = 
        let mark = _tokenizer.Mark
        let mutable all = true
        for text in texts do
            if _tokenizer.CurrentToken.TokenText = text then
                _tokenizer.MoveNextToken()
            else
                all <- false

        if not all then
            _tokenizer.MoveToMark mark

        all

    member x.ParseScriptLocalPrefix() = 
        x.ParseTokenSequence [| "<"; "SID"; ">" |] ||
        x.ParseTokenSequence [| "s"; ":" |]

    /// Reset the parser to the given set of input lines.  
    member x.Reset (lines: string[]) = 
        _lines <- 
            if lines.Length = 0 then
                [|""|]
            else
                lines
        _lineIndex <- 0
        _tokenizer.Reset _lines.[0] TokenizerFlags.None

        // It's possible the first line of the new input is blank.  Need to move past that and settle on 
        // a real line to be processed
        if x.IsCurrentLineBlank() then
            x.MoveToNextLine() |> ignore

    /// Is the current line blank 
    member x.IsCurrentLineBlank() = 
        let mark = _tokenizer.Mark
        let mutable allBlank = true
        while not _tokenizer.IsAtEndOfLine && allBlank do
            if _tokenizer.CurrentTokenKind = TokenKind.Blank then
                _tokenizer.MoveNextToken()
            else
                allBlank <- false

        _tokenizer.MoveToMark mark
        allBlank

    /// Parse the remainder of the line as a file path.  If there is nothing else on the line
    /// then None will be returned 
    member x.ParseRestOfLineAsFilePath() = 
        x.SkipBlanks()
        if _tokenizer.IsAtEndOfLine then
            []
        else
            x.ParseRestOfLine() |> x.ParseDirectoryPath

    /// Move to the next line of the input.  This will move past blank lines and return true if 
    /// the result is a non-blank line which can be processed
    member x.MoveToNextLine() = 

        let doMove () =
            if _lineIndex + 1 >= _lines.Length then
                // If this is the last line we should at least move the tokenizer to the end
                // of the line
                _tokenizer.MoveToEndOfLine()
            else
                _lineIndex <- _lineIndex + 1
                _tokenizer.Reset _lines.[_lineIndex] TokenizerFlags.None

        // Move at least one line
        doMove ()

        // Now move through all of the blank lines which could appear on the next line
        while not x.IsDone && x.IsCurrentLineBlank() do
            doMove ()

        not x.IsDone

    member x.Tokenizer = _tokenizer

    /// Move past the white space in the expression text
    member x.SkipBlanks () = 
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Blank -> _tokenizer.MoveNextToken()
        | _ -> ()

    /// Try and expand the possible abbreviation to a full line command name.  If it's 
    /// not an abbreviation then the original string will be returned
    member x.TryExpand name =

        // Is 'name' an abbreviation of the given command name and abbreviation
        let isAbbreviation (fullName: string) (abbreviation: string) = 
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
        if _tokenizer.CurrentChar = '!' then
            _tokenizer.MoveNextToken()
            true
        else
            false

    /// Parse out the text until the given predicate returns false or the end
    /// of the line is reached.  None is return if the current token when
    /// called doesn't match the predicate
    member x.ParseWhileEx flags predicate =
        use reset = _tokenizer.SetTokenizerFlagsScoped flags
        x.ParseWhile predicate

    member x.ParseWhile predicate = 
        let builder = System.Text.StringBuilder()
        let rec inner () =
            let token = _tokenizer.CurrentToken
            if token.TokenKind = TokenKind.EndOfLine then
                ()
            elif predicate token then
                builder.AppendString token.TokenText
                _tokenizer.MoveNextToken()
                inner ()
            else
                ()
        inner ()

        if builder.Length = 0 then
            None
        else
            builder.ToString() |> Some

    member x.ParseNumber() =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Number number ->
            _tokenizer.MoveNextToken()
            Some number
        | _ -> None


    member x.ParseLineRangeSpecifierEndCount lineRange = 
        match x.ParseNumber() with
        | Some count -> LineRangeSpecifier.WithEndCount (lineRange, count)
        | None -> lineRange

    /// Parse out a key notation argument.  Different than a word because it can accept items
    /// which are not letters such as numbers, <, >, etc ...
    member x.ParseKeyNotation() = 
        x.ParseWhileEx TokenizerFlags.AllowDoubleQuote (fun token -> 
            match token.TokenKind with 
            | TokenKind.Blank -> false
            | _ -> true)

    /// Parse out the remainder of the line including any trailing blanks
    member x.ParseRestOfLine() = 
        match x.ParseWhile (fun _ -> true) with
        | None -> StringUtil.Empty
        | Some text -> text

    /// Create a line number annotated parse error
    member x.ParseError message =
        if _lines.Length <> 1 then
            let lineMessage = _lineIndex + 1 |> Resources.Parser_OnLine
            sprintf "%s: %s" lineMessage message
        else
            message
        |> LineCommand.ParseError

    /// Parse out the mapclear variants. 
    member x.ParseMapClear allowBang keyRemapModes =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let mapArgumentList = x.ParseMapArguments()

        if hasBang then
            if allowBang then
                LineCommand.ClearKeyMap ([KeyRemapMode.Insert; KeyRemapMode.Command], mapArgumentList) 
            else
                x.ParseError Resources.Parser_NoBangAllowed
        else
            LineCommand.ClearKeyMap (keyRemapModes, mapArgumentList)

    /// Parse out a number from the stream
    member x.ParseNumberConstant() =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Number number ->
            _tokenizer.MoveNextToken()
            number |> VariableValue.Number |> Expression.ConstantValue |> ParseResult.Succeeded
        | _ -> ParseResult.Failed "Invalid Number"

    /// Parse out core portion of key mappings.
    member x.ParseMapKeysCore keyRemapModes allowRemap =

        x.SkipBlanks()
        let mapArgumentList = x.ParseMapArguments()
        match x.ParseKeyNotation() with
        | None -> LineCommand.DisplayKeyMap (keyRemapModes, None)
        | Some leftKeyNotation -> 
            x.SkipBlanks()

            let rightKeyNotation = x.ParseWhileEx TokenizerFlags.AllowDoubleQuote (fun _ -> true)
            let rightKeyNotation = OptionUtil.getOrDefault "" rightKeyNotation
            if StringUtil.IsBlanks rightKeyNotation then
                LineCommand.DisplayKeyMap (keyRemapModes, Some leftKeyNotation)
            else
                LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList)

    /// Parse out the :map commands and all of it's variants (imap, cmap, etc ...)
    member x.ParseMapKeys allowBang keyRemapModes =

        if x.ParseBang() then
            if allowBang then
                x.ParseMapKeysCore [KeyRemapMode.Insert; KeyRemapMode.Command] true
            else
                x.ParseError Resources.Parser_NoBangAllowed
        else
            x.ParseMapKeysCore keyRemapModes true

    /// Parse out the :nomap commands
    member x.ParseMapKeysNoRemap allowBang keyRemapModes =

        if x.ParseBang() then
            if allowBang then
                x.ParseMapKeysCore [KeyRemapMode.Insert; KeyRemapMode.Command] false
            else
                x.ParseError Resources.Parser_NoBangAllowed
        else
            x.ParseMapKeysCore keyRemapModes false

    /// Parse out the unmap variants. 
    member x.ParseMapUnmap allowBang keyRemapModes =

        let inner modes = 
            x.SkipBlanks()
            let mapArgumentList = x.ParseMapArguments()
            match x.ParseKeyNotation() with
            | None -> x.ParseError Resources.Parser_InvalidArgument
            | Some keyNotation -> LineCommand.UnmapKeys (keyNotation, modes, mapArgumentList)

        if x.ParseBang() then
            if allowBang then
                inner [KeyRemapMode.Insert; KeyRemapMode.Command]
            else
                x.ParseError Resources.Parser_NoBangAllowed
        else
            inner keyRemapModes

    /// Parse out a CommandOption value if the caret is currently pointed at one.  If 
    /// there is no CommnadOption here then the index will not change
    member x.ParseCommandOption () = 
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '+' ->
            let mark = _tokenizer.Mark

            _tokenizer.MoveNextToken()
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Number number ->
                _tokenizer.MoveNextToken()
                CommandOption.StartAtLine number |> Some
            | TokenKind.Character '/' ->
                _tokenizer.MoveNextToken()
                let pattern = x.ParseRestOfLine()
                CommandOption.StartAtPattern pattern |> Some
            | TokenKind.Character c ->
                match x.ParseSingleLine() with
                | LineCommand.ParseError _ -> 
                    _tokenizer.MoveToMark mark
                    None
                | lineCommand -> 
                    CommandOption.ExecuteLineCommand lineCommand |> Some
            | _ -> 
                // At the end of the line so it's just a '+' option
                CommandOption.StartAtLastLine |> Some
        | _ ->
            None

    /// Parse out the '++opt' parameter to some commands.
    member x.ParseFileOptions (): FileOption list =

        // TODO: Need to implement parsing out FileOption list
        List.empty

    /// Parse out the arguments which can be applied to the various map commands.  If the 
    /// argument isn't there then the index into the line will remain unchanged
    member x.ParseMapArguments() = 

        let rec inner withResult = 
            let mark = _tokenizer.Mark

            // Finish without changing anything.
            let finish() =
                _tokenizer.MoveToMark mark
                withResult []

            // The argument is mostly parsed out.  Need the closing '>' and the jump to
            // the next element in the list
            let completeArgument mapArgument = 
                _tokenizer.MoveNextToken()
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Character '>' ->
                    // Skip the '>' and any trailing blanks.  The method was called with
                    // the index pointing past white space and it should end that way
                    _tokenizer.MoveNextToken()
                    x.SkipBlanks()
                    inner (fun tail -> withResult (mapArgument :: tail))
                | _ -> finish ()

            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character '<' ->
                _tokenizer.MoveNextToken()
                match _tokenizer.CurrentTokenKind with 
                | TokenKind.Word "buffer" -> completeArgument KeyMapArgument.Buffer
                | TokenKind.Word "silent" -> completeArgument KeyMapArgument.Silent
                | TokenKind.Word "special" -> completeArgument KeyMapArgument.Special
                | TokenKind.Word "script" -> completeArgument KeyMapArgument.Script
                | TokenKind.Word "expr" -> completeArgument KeyMapArgument.Expr 
                | TokenKind.Word "unique" -> completeArgument KeyMapArgument.Unique
                | _ -> finish()
            | _ -> finish ()

        inner (fun x -> x)

    /// Parse out a register value from the text.  This will not parse out numbered register
    member x.ParseRegisterName kind = 
        let c = _tokenizer.CurrentChar 
        let isGood = 
            match kind with 
            | ParseRegisterName.All -> true
            | ParseRegisterName.NoNumbered -> not (CharUtil.IsDigit c)
        
        if isGood then
            let name = RegisterName.OfChar c
            if Option.isSome name then
                _tokenizer.MoveNextChar()
            name
        else
            None

    /// Used to parse out the flags for the sort commands
    member x.ParseSortFlags () =

        // Get the string which we are parsing for flags
        let flagString = x.ParseWhile (fun token -> 
            match token.TokenKind with
            | TokenKind.Word _ -> true
            | _ -> false)
        let flagString = OptionUtil.getOrDefault "" flagString

        let mutable parseResult: ParseResult<SortFlags> option = None
        let mutable flags = SortFlags.None
        let mutable index = 0
        while index < flagString.Length && Option.isNone parseResult do
            match flagString.[index] with
            | 'i' -> flags <- flags ||| SortFlags.IgnoreCase
            | 'n' -> flags <- flags ||| SortFlags.Decimal
            | 'f' -> flags <- flags ||| SortFlags.Float
            | 'x' -> flags <- flags ||| SortFlags.Hexidecimal
            | 'o' -> flags <- flags ||| SortFlags.Octal
            | 'b' -> flags <- flags ||| SortFlags.Binary
            | 'u' -> flags <- flags ||| SortFlags.Unique
            | 'r' -> flags <- flags ||| SortFlags.MatchPattern
            | _  -> 
                // Illegal character in the flags string 
                parseResult <- ParseResult.Failed Resources.CommandMode_TrailingCharacters |> Some

            index <- index + 1

        match parseResult with
        | None -> ParseResult.Succeeded flags
        | Some p -> p

    /// Used to parse out the flags for substitute commands.  Will not modify the 
    /// stream if there are no flags
    member x.ParseSubstituteFlags () =

        // Get the string which we are parsing for flags
        let flagString = x.ParseWhile (fun token -> 
            match token.TokenKind with
            | TokenKind.Character '&' -> true
            | TokenKind.Character '#' -> true
            | TokenKind.Word _ -> true
            | _ -> false)
        let flagString = OptionUtil.getOrDefault "" flagString

        let mutable parseResult: ParseResult<SubstituteFlags> option = None
        let mutable flags = 
            if _globalSettings.GlobalDefault then SubstituteFlags.ReplaceAll
            else SubstituteFlags.None
        let mutable index = 0
        while index < flagString.Length && Option.isNone parseResult do
            match flagString.[index] with
            | 'c' -> flags <- flags ||| SubstituteFlags.Confirm
            | 'r' -> flags <- flags ||| SubstituteFlags.UsePreviousSearchPattern
            | 'e' -> flags <- flags ||| SubstituteFlags.SuppressError
            | 'i' -> flags <- flags ||| SubstituteFlags.IgnoreCase
            | 'I' -> flags <- flags ||| SubstituteFlags.OrdinalCase
            | 'n' -> flags <- flags ||| SubstituteFlags.ReportOnly
            | 'p' -> flags <- flags ||| SubstituteFlags.PrintLast
            | 'l' -> flags <- flags ||| SubstituteFlags.PrintLastWithList
            | '#' -> flags <- flags ||| SubstituteFlags.PrintLastWithNumber
            | 'g' -> 
                // This is a toggle flag that turns the value on / off
                if Util.IsFlagSet flags SubstituteFlags.ReplaceAll then
                    flags <- flags &&& ~~~SubstituteFlags.ReplaceAll
                else
                    flags <- flags ||| SubstituteFlags.ReplaceAll
            | '&' -> 
                // The '&' flag is only legal in the first position.  After that
                // it terminates the flag notation
                if index = 0 then 
                    flags <- flags ||| SubstituteFlags.UsePreviousFlags
                else
                    parseResult <- ParseResult.Failed Resources.CommandMode_TrailingCharacters |> Some
            | _  -> 
                // Illegal character in the flags string 
                parseResult <- ParseResult.Failed Resources.CommandMode_TrailingCharacters |> Some

            index <- index + 1

        match parseResult with
        | None -> ParseResult.Succeeded flags
        | Some p -> p

    /// Parse out an '@' command
    member x.ParseAtCommand lineRange =
        x.SkipBlanks()
        if _tokenizer.CurrentChar = ':' then
            _tokenizer.MoveNextChar()
            match _vimData.LastLineCommand with
            | None -> x.ParseError "Error"
            | Some lineCommand -> x.MergeLineRangeWithCommand lineRange lineCommand
        else
            x.ParseError "Error"

    /// Merge new line range with previous line command
    member x.MergeLineRangeWithCommand lineRange lineCommand =
        let noRangeCommand =
            match lineRange with
            | LineRangeSpecifier.None -> lineCommand
            | _ -> x.ParseError "Error"
        match lineRange with
        | LineRangeSpecifier.None -> lineCommand
        | _ ->
            match lineCommand with
            | LineCommand.AddAutoCommand _ -> noRangeCommand
            | LineCommand.Behave _ -> noRangeCommand
            | LineCommand.Call _ -> noRangeCommand
            | LineCommand.ChangeDirectory _ -> noRangeCommand
            | LineCommand.ChangeLocalDirectory _ -> noRangeCommand
            | LineCommand.ClearKeyMap _ -> noRangeCommand
            | LineCommand.Close _ -> noRangeCommand
            | LineCommand.Compose _ -> noRangeCommand
            | LineCommand.CopyTo (_, destLineRange, count) -> LineCommand.CopyTo (lineRange, destLineRange, count)
            | LineCommand.CSharpScript _ -> noRangeCommand
            | LineCommand.CSharpScriptCreateEachTime _ -> noRangeCommand
            | LineCommand.Delete (_, registerName) -> LineCommand.Delete (lineRange, registerName)
            | LineCommand.DeleteAllMarks -> noRangeCommand
            | LineCommand.DeleteMarks _ -> noRangeCommand
            | LineCommand.Digraphs _ -> noRangeCommand
            | LineCommand.DisplayKeyMap _ -> noRangeCommand
            | LineCommand.DisplayLet _ -> noRangeCommand
            | LineCommand.DisplayMarks _ -> noRangeCommand
            | LineCommand.DisplayRegisters _ -> noRangeCommand
            | LineCommand.Echo _ -> noRangeCommand
            | LineCommand.Edit _ -> noRangeCommand
            | LineCommand.Else -> noRangeCommand
            | LineCommand.ElseIf _ -> noRangeCommand
            | LineCommand.Execute _ -> noRangeCommand
            | LineCommand.Files -> noRangeCommand
            | LineCommand.Fold lineRange -> LineCommand.Fold lineRange
            | LineCommand.Function _ -> noRangeCommand
            | LineCommand.FunctionEnd _ -> noRangeCommand
            | LineCommand.FunctionStart _ -> noRangeCommand
            | LineCommand.Global (_, pattern, matchPattern, lineCommand) -> LineCommand.Global (lineRange, pattern, matchPattern, lineCommand)
            | LineCommand.GoToFirstTab -> noRangeCommand
            | LineCommand.GoToLastTab -> noRangeCommand
            | LineCommand.GoToNextTab _ -> noRangeCommand
            | LineCommand.GoToPreviousTab _ -> noRangeCommand
            | LineCommand.Help _ -> noRangeCommand
            | LineCommand.VimHelp _ -> noRangeCommand
            | LineCommand.History -> noRangeCommand
            | LineCommand.HorizontalSplit (_, fileOptions, commandOptions) -> LineCommand.HorizontalSplit (lineRange, fileOptions, commandOptions)
            | LineCommand.HostCommand _ -> noRangeCommand
            | LineCommand.If _ -> noRangeCommand
            | LineCommand.IfEnd -> noRangeCommand
            | LineCommand.IfStart _ -> noRangeCommand
            | LineCommand.Join (_, joinKind) -> LineCommand.Join (lineRange, joinKind)
            | LineCommand.JumpToLastLine _ -> LineCommand.JumpToLastLine lineRange
            | LineCommand.Let _ -> noRangeCommand
            | LineCommand.LetEnvironment _ -> noRangeCommand
            | LineCommand.LetRegister _ -> noRangeCommand
            | LineCommand.LocationNext _ -> noRangeCommand
            | LineCommand.LocationPrevious _ -> noRangeCommand
            | LineCommand.LocationRewind _ -> noRangeCommand
            | LineCommand.LocationWindow -> noRangeCommand
            | LineCommand.Make _ -> noRangeCommand
            | LineCommand.MapKeys _ -> noRangeCommand
            | LineCommand.MoveTo (_, destLineRange, count) -> LineCommand.MoveTo (lineRange, destLineRange, count)
            | LineCommand.NoHighlightSearch -> noRangeCommand
            | LineCommand.Nop -> noRangeCommand
            | LineCommand.Normal (_, command) -> LineCommand.Normal (lineRange, command)
            | LineCommand.Only -> noRangeCommand
            | LineCommand.ParseError _ -> noRangeCommand
            | LineCommand.DisplayLines (_, lineCommandFlags)-> LineCommand.DisplayLines (lineRange, lineCommandFlags)
            | LineCommand.PrintCurrentDirectory -> noRangeCommand
            | LineCommand.PutAfter (_, registerName) -> LineCommand.PutAfter (lineRange, registerName)
            | LineCommand.PutBefore (_, registerName) -> LineCommand.PutBefore (lineRange, registerName)
            | LineCommand.QuickFixNext _ -> noRangeCommand
            | LineCommand.QuickFixPrevious _ -> noRangeCommand
            | LineCommand.QuickFixRewind _ -> noRangeCommand
            | LineCommand.QuickFixWindow -> noRangeCommand
            | LineCommand.Quit _ -> noRangeCommand
            | LineCommand.QuitAll _ -> noRangeCommand
            | LineCommand.QuitWithWrite (_, hasBang, fileOptions, filePath) -> LineCommand.QuitWithWrite (lineRange, hasBang, fileOptions, filePath)
            | LineCommand.ReadCommand (_, command) -> LineCommand.ReadCommand (lineRange, command)
            | LineCommand.ReadFile (_, fileOptionList, filePath) -> LineCommand.ReadFile (lineRange, fileOptionList, filePath)
            | LineCommand.Redo -> noRangeCommand
            | LineCommand.RemoveAutoCommands _ -> noRangeCommand
            | LineCommand.Retab (_, hasBang, tabStop) -> LineCommand.Retab (lineRange, hasBang, tabStop)
            | LineCommand.Search (_, path, pattern) -> LineCommand.Search (lineRange, path, pattern)
            | LineCommand.Set _ -> noRangeCommand
            | LineCommand.Shell -> noRangeCommand
            | LineCommand.ShellCommand (_, command) -> LineCommand.ShellCommand (lineRange, command)
            | LineCommand.ShiftLeft (_, count) -> LineCommand.ShiftLeft (lineRange, count)
            | LineCommand.ShiftRight (_, count) -> LineCommand.ShiftRight (lineRange, count)
            | LineCommand.Sort (_, hasBang, flags, pattern) -> LineCommand.Sort (lineRange, hasBang, flags, pattern)
            | LineCommand.Source _ -> noRangeCommand
            | LineCommand.StopInsert -> noRangeCommand
            | LineCommand.Substitute (_, pattern, replace, flags) -> LineCommand.Substitute (lineRange, pattern, replace, flags)
            | LineCommand.SubstituteRepeat (_, substituteFlags) -> LineCommand.SubstituteRepeat (lineRange, substituteFlags)
            | LineCommand.TabNew _ -> noRangeCommand
            | LineCommand.TabOnly -> noRangeCommand
            | LineCommand.Undo -> noRangeCommand
            | LineCommand.Unlet _ -> noRangeCommand
            | LineCommand.UnmapKeys _ -> noRangeCommand
            | LineCommand.Version -> noRangeCommand
            | LineCommand.VerticalSplit (_, fileOptions, commandOptions) -> LineCommand.VerticalSplit (lineRange, fileOptions, commandOptions)
            | LineCommand.Write (_, hasBang, fileOptionList, filePath) -> LineCommand.Write (lineRange, hasBang, fileOptionList, filePath)
            | LineCommand.WriteAll _ -> noRangeCommand
            | LineCommand.Yank (_, registerName, count) -> LineCommand.Yank (lineRange, registerName, count)

    /// Parse out :autocommand
    member x.ParseAutoCommand() = 

        let isRemove = x.ParseBang()
        let standardError = "Values missing"
        let onError msg = x.ParseError msg
        let onStandardError () = onError standardError

        // Parse out the auto group name from the current point in the tokenizer
        let parseAutoCommandGroup () = 
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Word name -> 
                let found = 
                    _vimData.AutoCommandGroups 
                    |> Seq.tryFind (fun autoCommandGroup ->
                        match autoCommandGroup with
                        | AutoCommandGroup.Default -> false
                        | AutoCommandGroup.Named groupName -> name = groupName)
                match found with 
                | Some autoCommandGroup ->
                    _tokenizer.MoveNextToken()
                    autoCommandGroup
                | None -> AutoCommandGroup.Default
            | _ -> AutoCommandGroup.Default

        // Whether or not the first string is interpreted as a group name is based on whether it exists in the
        // set of defined autogroup values.  
        let getAutoCommandGroup name =
            _vimData.AutoCommandGroups 
            |> Seq.tryFind (fun autoCommandGroup ->
                match autoCommandGroup with
                | AutoCommandGroup.Default -> false
                | AutoCommandGroup.Named groupName -> name = groupName)

        // Parse out the pattern.  Consume everything up until the next blank.  This isn't a normal regex
        // pattern though (described in 'help autocmd-patterns').  Commas do represent pattern separation
        let parsePatternList () = 
            x.SkipBlanks()

            let rec inner rest = 
                let isNotBlankOrComma (token: Token) =
                    match token.TokenKind with
                    | TokenKind.Blank -> false
                    | TokenKind.Character ',' -> false
                    | _ -> true

                match x.ParseWhile isNotBlankOrComma with
                | None -> rest []
                | Some str -> 
                    match _tokenizer.CurrentTokenKind with
                    | TokenKind.Character ',' ->
                        _tokenizer.MoveNextToken()
                        inner (fun item -> rest (str :: item))
                    | _ -> rest [str]

            inner (fun x -> x)

        // Parse out the event list.  Every autocmd value can specify multiple events by 
        // separating the names with a comma 
        let parseEventKindList () = 

            // Parse out an EventKind value from the specified event name 
            let parseEventKind (word: string) = 
                let word = word.ToLower()
                Map.tryFind word s_NameToEventKindMap
            
            let rec inner rest = 
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Word word -> 
                    match parseEventKind word with
                    | None -> ParseResult.Failed standardError
                    | Some eventKind ->
                        _tokenizer.MoveNextToken()
                        if _tokenizer.CurrentChar = ',' then
                            _tokenizer.MoveNextToken()
                            inner (fun item -> rest (eventKind :: item))
                        else
                            rest [eventKind]
                | _ -> rest []
                    
            inner (fun list -> ParseResult.Succeeded list)

        x.SkipBlanks() 
        let autoCommandGroup = parseAutoCommandGroup ()
        x.SkipBlanks()

        if isRemove then

            // Other remove syntaxes 
            let onRemoveEx eventKindList patternList lineCommandText = 
                let autoCommandDefinition = {  
                    Group = autoCommandGroup
                    EventKinds = eventKindList
                    Patterns = patternList
                    LineCommandText = lineCommandText
                }
                LineCommand.RemoveAutoCommands autoCommandDefinition

            // Called for one of the variations of the remove all commands
            let onRemoveAll () = 
                onRemoveEx List.empty List.empty ""

            match _tokenizer.CurrentTokenKind with
            | TokenKind.EndOfLine -> onRemoveAll ()
            | TokenKind.Character '*' -> 
                // This is the pattern form of the tokenizer.  
                _tokenizer.MoveNextToken()
                x.SkipBlanks()
                let patternList = parsePatternList ()
                onRemoveEx List.empty patternList ""
            | _ -> 
                // This is the longer form of the event remove which can specify both event kinds
                // and patterns.  The next item in both cases is the events followed by the patterns
                match parseEventKindList () with
                | ParseResult.Failed msg -> onError msg
                | ParseResult.Succeeded eventKindList ->
                    x.SkipBlanks()
                    let patternList = 
                        if _tokenizer.IsAtEndOfLine then
                            List.empty
                        else
                            parsePatternList ()
                    onRemoveEx eventKindList patternList ""

        else
            // This is the add form of auto command.  It will be followed by the events, pattern
            // and actual command in that order
            match parseEventKindList () with
            | ParseResult.Failed msg -> x.ParseError msg
            | ParseResult.Succeeded eventKindList ->
                x.SkipBlanks()
                let patternList = parsePatternList ()
                x.SkipBlanks()
                let command = x.ParseRestOfLine()
                let autoCommandDefinition = { 
                    Group = autoCommandGroup 
                    EventKinds = eventKindList
                    LineCommandText = command
                    Patterns = patternList
                }

                LineCommand.AddAutoCommand autoCommandDefinition

    /// Parse out the :behave command.  The mode argument is required
    member x.ParseBehave() =
        x.SkipBlanks()
        if _tokenizer.IsAtEndOfLine then
            x.ParseError Resources.Parser_Error
        else
            let mode = _tokenizer.CurrentToken.TokenText
            _tokenizer.MoveNextToken()
            LineCommand.Behave mode

    member x.ParseCall lineRange = 
        x.SkipBlanks()

        let isScriptLocal = x.ParseScriptLocalPrefix()
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word name ->
            _tokenizer.MoveNextToken()
            let arguments = x.ParseRestOfLine()
            let callInfo = {
                LineRange = lineRange
                Name = name
                Arguments = arguments
                IsScriptLocal = isScriptLocal
            }
            LineCommand.Call callInfo 
        | _ -> x.ParseError Resources.Parser_Error

    /// Parse out the change directory command.  The path here is optional
    member x.ParseChangeDirectory() =
        // Bang is allowed but has no effect
        x.ParseBang() |> ignore
        let path = x.ParseRestOfLineAsFilePath()
        LineCommand.ChangeDirectory path

    /// Parse out the change local directory command.  The path here is optional
    member x.ParseChangeLocalDirectory() =
        // Bang is allowed but has no effect
        x.ParseBang() |> ignore
        let path = x.ParseRestOfLineAsFilePath()
        LineCommand.ChangeLocalDirectory path

    /// Parse out the :close command
    member x.ParseClose() = 
        let isBang = x.ParseBang()
        LineCommand.Close isBang

    /// Parse out the :copy command.  It has a single required argument that is the destination
    /// address
    member x.ParseCopyTo sourceLineRange = 
        x.SkipBlanks()
        let destinationLineRange = x.ParseLineRange()
        x.SkipBlanks()
        let count = x.ParseNumber()
        match destinationLineRange with
        | LineRangeSpecifier.None -> x.ParseError Resources.Common_InvalidAddress
        | _ -> LineCommand.CopyTo (sourceLineRange, destinationLineRange, count)

    member x.ParseCSharpScript(lineRange:LineRangeSpecifier, createEachTime:bool) = 
        x.SkipBlanks()

        let isScriptLocal = x.ParseScriptLocalPrefix()
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word name ->
            _tokenizer.MoveNextToken()
            let arguments = x.ParseRestOfLine()
            let callInfo = {
                LineRange = lineRange
                Name = name
                Arguments = arguments
                IsScriptLocal = isScriptLocal
            }
            if createEachTime then
               LineCommand.CSharpScriptCreateEachTime callInfo 
            else
               LineCommand.CSharpScript callInfo 
        | _ -> x.ParseError Resources.Parser_Error

    /// Parse out the :move command.  It has a single required argument that is the destination
    /// address
    member x.ParseMoveTo sourceLineRange = 
        x.SkipBlanks()
        let destinationLineRange = x.ParseLineRange()
        x.SkipBlanks()
        let count = x.ParseNumber()
        match destinationLineRange with
        | LineRangeSpecifier.None -> x.ParseError Resources.Common_InvalidAddress
        | _ -> LineCommand.MoveTo (sourceLineRange, destinationLineRange, count)

    /// Parse out the :delete command
    member x.ParseDelete lineRange = 
        x.SkipBlanks()
        let name = x.ParseRegisterName ParseRegisterName.NoNumbered
        x.SkipBlanks()
        let lineRange = x.ParseLineRangeSpecifierEndCount lineRange
        LineCommand.Delete (lineRange, name)

    /// Parse out the :delmarks command
    member x.ParseDeleteMarks() =
        x.SkipBlanks()
        if x.ParseBang() then
            LineCommand.DeleteAllMarks
        else
            // Don't care about blank chars here.  Keep moving
            let moveCore () = 
                _tokenizer.MoveNextChar()
                while not _tokenizer.IsAtEndOfLine && CharUtil.IsBlank _tokenizer.CurrentChar do
                    _tokenizer.MoveNextChar()

            let hadError = ref false
            let list = List<Mark>()

            // Parse out the range of marks.  Anything from the start to end character
            // inclusive 
            let parseRange (startChar: char) (endChar: char) =
                for i = int startChar to int endChar do
                    let c = char i 
                    match Mark.OfChar c with
                    | Some mark -> list.Add mark
                    | None -> hadError := true

            while not _tokenizer.IsAtEndOfLine && not hadError.Value do
                let c = _tokenizer.CurrentChar
                moveCore ()
                if _tokenizer.CurrentChar = '-' then
                    moveCore ()
                    let o = _tokenizer.CurrentChar
                    moveCore ()

                    if CharUtil.IsLetter c && CharUtil.IsLetter o && c < o then
                        parseRange c o
                    elif CharUtil.IsDigit c && CharUtil.IsDigit o && c < o then
                        parseRange c o
                    else
                        hadError := true
                else
                    match Mark.OfChar c with
                    | Some mark -> list.Add(mark)
                    | None -> hadError := true

            if hadError.Value then
                _tokenizer.MoveToEndOfLine()
                x.ParseError Resources.Parser_Error
            else
                LineCommand.DeleteMarks (List.ofSeq list)

    /// Parse out the :edit command
    member x.ParseEdit () = 
        let hasBang = x.ParseBang()

        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let commandOption = x.ParseCommandOption()

        x.SkipBlanks()
        let fileName = x.ParseRestOfLineAsFilePath()
        LineCommand.Edit (hasBang, fileOptionList, commandOption, fileName)

    /// Parse out the :function command
    ///
    /// TODO: having the option on the definition is wrong.  Just make it a non-option and
    /// hammer the parser in tests.  If there are cases we can't handle then return an incomplete
    /// parser definition.  For legitimate parse errors though we should error just like Vim
    member x.ParseFunctionStart() = 

        // Parse out the name of the function.  It must start with a capitol letter or
        // be preceded by the s: prefix 
        let parseFunctionName isScriptLocal =

            match _tokenizer.CurrentTokenKind with
            | TokenKind.Word word ->
                if not isScriptLocal && CharUtil.IsLower word.[0] then
                    ParseResult.Failed (Resources.Parser_FunctionName word)
                else
                    _tokenizer.MoveNextToken()
                    ParseResult.Succeeded word
            | _ -> ParseResult.Failed Resources.Parser_Error

        // Parse out the data between the () in the function definition
        let parseArgList () = 
            let rec inner cont = 
                match _tokenizer.CurrentTokenKind with 
                | TokenKind.Word word -> 
                    _tokenizer.MoveNextToken()
                    if _tokenizer.CurrentChar = ',' then   
                        _tokenizer.MoveNextChar()
                        inner (fun rest -> cont (word :: rest))
                    else
                        cont []
                | _ -> cont [] 
            inner (fun x -> x) 

        // Parse out the function arguments 
        let parseFunctionArguments () =
            if _tokenizer.CurrentChar <> '(' then
                ParseResult.Failed Resources.Parser_Error
            else
                _tokenizer.MoveNextToken()
                let args = parseArgList ()
                if _tokenizer.CurrentChar = ')' then
                    _tokenizer.MoveNextToken()
                    ParseResult.Succeeded args
                else
                    ParseResult.Failed Resources.Parser_Error

        // Parse out the abort, dict and range modifiers which can follow a function definition
        let parseModifiers () = 

            let rec inner isAbort isDict isRange = 
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Word "abort" -> 
                    _tokenizer.MoveNextToken()
                    inner true isDict isRange 
                | TokenKind.Word "dict" -> 
                    _tokenizer.MoveNextToken()
                    inner isAbort true isRange 
                | TokenKind.Word "range" -> 
                    _tokenizer.MoveNextToken()
                    inner isAbort isDict true
                | TokenKind.EndOfLine ->
                    (isAbort, isDict, isRange, false)
                | _ -> 
                    _tokenizer.MoveToEndOfLine()
                    (isAbort, isDict, isRange, true)

            inner false false false

        let hasBang = x.ParseBang()

        // Start ignoring blanks after parsing out the '!'.  The '!' must appear directly next to the 
        // function name or it is not a valid construct
        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.SkipBlanks

        let lineCommand = _lineCommandBuilder { 
            // Lower case names are allowed when the name is prefixed with <SID> or s: 
            let isScriptLocal = x.ParseScriptLocalPrefix()
            let! name = parseFunctionName isScriptLocal
            let! parameters = parseFunctionArguments ()
            let isAbort, isDict, isRange, isError = parseModifiers ()

            let func = { 
                Name = name
                Parameters = parameters
                IsRange = isRange
                IsAbort = isAbort
                IsDictionary = isDict
                IsForced = hasBang
                IsScriptLocal = isScriptLocal
            }
            return LineCommand.FunctionStart (Some func)
        }

        match lineCommand with 
        | LineCommand.ParseError _ -> 
            _tokenizer.MoveToEndOfLine()
            LineCommand.FunctionStart None
        | _ -> lineCommand

    /// Parse out the :endfunc command
    member x.ParseFunctionEnd() = 
        LineCommand.FunctionEnd

    /// Parse the rest of the function given the FuntionDefinition value.  This value will be None
    /// when there was an error parsing out the function header.  Even when there is an error we 
    /// still must parse out the statements inside of it.  If we don't then a simple parsing error
    /// on a function header can lead to all of the statements inside it being executed promptly
    member x.ParseFunction (functionDefinition: FunctionDefinition option) = 

        // Parse out the lines in the function.  If any of the lines inside the function register as a 
        // parse error we still need to continue parsing the function (even though it should ultimately
        // fail).  If we bail out of parsing early then it will cause the "next" command to be a 
        // line which is a part of the function.  
        let lines = List<LineCommand>()
        let mutable foundEndFunction = false
        while not x.IsDone && not foundEndFunction do
            match x.ParseSingleCommand() with
            | LineCommand.FunctionEnd -> foundEndFunction <- true
            | lineCommand -> 
                // Intentionally putting parse errors into the set of commands for a function.  The parse 
                // errors aren't emitted as errors until the function is run.  Hence they need to be stored
                // as errors in the function
                lines.Add(lineCommand)

        match functionDefinition, not foundEndFunction with
        | Some functionDefinition, false -> 
            let func = { 
                Definition = functionDefinition
                LineCommands = List.ofSeq lines
            }
            LineCommand.Function func
        | _ -> x.ParseError Resources.Parser_Error

    /// Parse a {pattern} out of the text.  The text will be consumed until the unescaped value 
    /// 'delimiter' is provided or the end of the input is reached.  The method will return a tuple
    /// of the pattern and a bool.  The bool will represent whether or not the delimiter was found.
    /// If the delimiter is found then it will be consumed
    member x.ParsePattern delimiter = 

        // Need to reset to account for the case where the pattern begins with a 
        // double quote
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        let moveNextChar () = _tokenizer.MoveNextChar()
        let builder = System.Text.StringBuilder()
        let rec inner () = 
            if _tokenizer.IsAtEndOfLine then
                // Hit the end without finding 'delimiter'. 
                builder.ToString(), false
            else    
                let c = _tokenizer.CurrentChar
                if c = delimiter then 
                    moveNextChar ()
                    builder.ToString(), true
                elif c = '\\' then
                    moveNextChar ()

                    if _tokenizer.IsAtEndOfLine then
                        ()
                    else
                        let c = _tokenizer.CurrentChar
                        if c <> delimiter then
                            // If the next char is not the delimiter then we have to assume the '\'
                            // is part of an escape for the pattern itself (\(, \1, etc ..) and we
                            // need to leave it in.  
                            builder.AppendChar '\\'

                        builder.AppendChar c
                        moveNextChar ()

                    inner()
                else
                    builder.AppendChar c
                    moveNextChar ()
                    inner()

        inner ()

    /// Parse out a LineSpecifier from the text.
    ///
    /// If there is no valid line specifier at the given place in the text then the 
    /// index should not be adjusted
    member x.ParseLineSpecifier () =

        let lineSpecifier = 
            if _tokenizer.CurrentChar = '.' then
                _tokenizer.MoveNextToken()
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Number _ ->
                    x.ParseNumber()
                    |> OptionUtil.getOrDefault 1
                    |> (fun number -> LineSpecifier.LineSpecifierWithAdjustment (LineSpecifier.CurrentLine, number))
                    |> Some
                | _ ->
                    LineSpecifier.CurrentLine |> Some

            elif _tokenizer.CurrentChar = '\'' then
                let mark = _tokenizer.Mark
                _tokenizer.MoveNextToken()

                match Mark.OfChar _tokenizer.CurrentChar with
                | None -> 
                    _tokenizer.MoveToMark mark
                    None
                | Some mark -> 
                    _tokenizer.MoveNextChar()
                    LineSpecifier.MarkLine mark |> Some

            elif _tokenizer.CurrentChar = '$' || _tokenizer.CurrentChar = '%' then
                _tokenizer.MoveNextToken()
                LineSpecifier.LastLine |> Some

            elif _tokenizer.CurrentChar = '\\' then

                // It's one of the previous pattern specifiers.
                let mark = _tokenizer.Mark
                _tokenizer.MoveNextChar()
                if _tokenizer.CurrentChar = '/' then
                    _tokenizer.MoveNextChar()
                    Some LineSpecifier.NextLineWithPreviousPattern
                elif _tokenizer.CurrentChar = '?' then
                    _tokenizer.MoveNextChar()
                    Some LineSpecifier.PreviousLineWithPreviousPattern
                elif _tokenizer.CurrentChar = '&' then
                    _tokenizer.MoveNextChar()
                    Some LineSpecifier.NextLineWithPreviousSubstitutePattern
                else
                    _tokenizer.MoveToMark mark
                    None

            elif _tokenizer.CurrentChar = '/' then

                // It's the / next search pattern.
                _tokenizer.MoveNextChar()
                let pattern, _ = x.ParsePattern '/'
                LineSpecifier.NextLineWithPattern pattern |> Some

            elif _tokenizer.CurrentChar = '?' then

                // It's the ? previous search pattern.
                _tokenizer.MoveNextChar()
                let pattern, _ = x.ParsePattern '?'
                LineSpecifier.PreviousLineWithPattern pattern |> Some

            elif _tokenizer.CurrentChar = '+' || _tokenizer.CurrentChar = '-' then
                LineSpecifier.CurrentLine |> Some

            else 
                match x.ParseNumber() with
                | None -> None
                | Some number -> LineSpecifier.Number number |> Some

        // Need to check for a trailing + or - 
        match lineSpecifier with
        | None ->
            None
        | Some lineSpecifier ->
            let parseAdjustment isNegative = 
                _tokenizer.MoveNextToken()

                // If no number is specified then 1 is used instead
                let number = x.ParseNumber() |> OptionUtil.getOrDefault 1
                let number = 
                    if isNegative then
                        -number
                    else
                        number

                Some (LineSpecifier.LineSpecifierWithAdjustment (lineSpecifier, number))

            if _tokenizer.CurrentChar = '+' then
                parseAdjustment false
            elif _tokenizer.CurrentChar = '-' then
                parseAdjustment true
            else
                Some lineSpecifier

    /// Parse out any valid range node.  This will consider % and any other 
    /// range expression
    member x.ParseLineRange (): LineRangeSpecifier =
        if _tokenizer.CurrentChar = '%' then
            _tokenizer.MoveNextToken()
            LineRangeSpecifier.EntireBuffer
        else
            let startLine = x.ParseLineSpecifier()
            let startLine =
                match startLine with
                | None ->
                    if _tokenizer.CurrentChar = ',' || _tokenizer.CurrentChar = ';' then
                        Some LineSpecifier.CurrentLine
                    else
                        None
                | Some left -> startLine
            match startLine with
            | None -> LineRangeSpecifier.None
            | Some left ->

                if _tokenizer.CurrentChar = ',' || _tokenizer.CurrentChar = ';' then
                    let isSemicolon = _tokenizer.CurrentChar = ';'
                    _tokenizer.MoveNextToken()
                    match x.ParseLineSpecifier() with
                    | None -> LineRangeSpecifier.Range (left, LineSpecifier.CurrentLine, isSemicolon)
                    | Some right -> LineRangeSpecifier.Range (left, right, isSemicolon)
                else
                    LineRangeSpecifier.SingleLine left 

    /// Parse out the valid ex-flags
    member x.ParseLineCommandFlags initialFlags = 
        let rec inner flags = 

            let withFlag setFlag unsetFlag =
                _tokenizer.MoveNextChar()
                inner (setFlag ||| (flags &&& ~~~unsetFlag))

            match _tokenizer.CurrentChar with
            | c when c = char(0) || CharUtil.IsBlank c -> ParseResult.Succeeded flags
            | 'l' -> withFlag LineCommandFlags.List LineCommandFlags.Print
            | '#' -> withFlag LineCommandFlags.AddLineNumber LineCommandFlags.None
            | 'p' -> withFlag LineCommandFlags.Print LineCommandFlags.List
            | _ -> ParseResult.Failed Resources.Parser_InvalidArgument

        inner initialFlags

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
                _tokenizer.MoveNextToken()
                let pattern, foundDelimeter = x.ParsePattern delimiter
                if not foundDelimeter then
                    // When there is no trailing delimeter then the replace string is empty
                    LineCommand.Substitute (lineRange, pattern, "", SubstituteFlags.None)
                else
                    let replace, _ = x.ParsePattern delimiter
                    x.SkipBlanks()
                    match x.ParseSubstituteFlags() with
                    | ParseResult.Failed message -> x.ParseError message
                    | ParseResult.Succeeded flags ->
                        let flags = processFlags flags
                        x.SkipBlanks()
                        let lineRange = x.ParseLineRangeSpecifierEndCount lineRange
                        LineCommand.Substitute (lineRange, pattern, replace, flags)
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
        match x.ParseSubstituteFlags() with
        | ParseResult.Failed message -> x.ParseError message
        | ParseResult.Succeeded flags ->
            let flags = processFlags flags

            // Parses out the optional trailing count
            x.SkipBlanks()
            let lineRange = x.ParseLineRangeSpecifierEndCount lineRange
            LineCommand.SubstituteRepeat (lineRange, flags)

    /// Parse out the repeat variety of the substitute command which is initiated
    /// by the '&' character.
    member x.ParseSubstituteRepeat lineRange extraFlags = 
        x.ParseSubstituteRepeatCore lineRange (fun flags -> flags ||| extraFlags)

    /// Parse out the search commands
    member x.ParseSearch lineRange path =
        let pattern = x.ParseRestOfLine()
        LineCommand.Search (lineRange, path, pattern)

    /// Parse out the shift left pattern
    member x.ParseShiftLeft lineRange = 
        let mutable count = 1
        while _tokenizer.CurrentChar = '<' do
            _tokenizer.MoveNextChar()
            count <- count + 1
        x.SkipBlanks()
        let lineRange = x.ParseLineRangeSpecifierEndCount lineRange
        LineCommand.ShiftLeft (lineRange, count)

    /// Parse out the shift right pattern
    member x.ParseShiftRight lineRange = 
        let mutable count = 1
        while _tokenizer.CurrentChar = '>' do
            _tokenizer.MoveNextChar()
            count <- count + 1
        x.SkipBlanks()
        let lineRange = x.ParseLineRangeSpecifierEndCount lineRange
        LineCommand.ShiftRight (lineRange, count)

    /// Parse out the ':shell' command
    member x.ParseShell () = 
        x.SkipBlanks()
        LineCommand.Shell

    /// Parse out the ':!' command
    member x.ParseShellCommand lineRange =
        use resetFlags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        let command = x.ParseRestOfLine()
        LineCommand.ShellCommand (lineRange, command)

    /// Parse out a string constant from the token stream.  Loads of special characters are
    /// possible here.  A complete list is available at :help expr-string
    member x.ParseStringConstant() = 
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        _tokenizer.MoveNextToken()

        let builder = System.Text.StringBuilder()
        let moveNextChar () = _tokenizer.MoveNextChar()
        let rec inner afterEscape = 
            if _tokenizer.IsAtEndOfLine then
                ParseResult.Failed Resources.Parser_MissingQuote
            else
                let c = _tokenizer.CurrentChar
                moveNextChar()
                if afterEscape then
                    match c with
                    | 'e' -> builder.AppendChar (char 0x1b)
                    | 't' -> builder.AppendChar '\t'
                    | 'b' -> builder.AppendChar '\b'
                    | 'f' -> builder.AppendChar '\f'
                    | 'n' -> builder.AppendChar '\n'
                    | 'r' -> builder.AppendChar '\r'
                    | '<' ->

                        // Escaped open angle bracket in a string literal
                        // starts key notation.
                        let notation = x.ParseWhile (fun token -> 
                            match token.TokenKind with 
                            | TokenKind.Character '>' -> false
                            | _ -> true)
                        if _tokenizer.CurrentChar = '>' then
                            _tokenizer.MoveNextToken()
                            match notation with
                            | None -> ()
                            | Some notation ->
                                let notation = "<" + notation + ">"
                                let keyInput = KeyNotationUtil.StringToKeyInput notation
                                match keyInput.RawChar with
                                | None -> ()
                                | Some rawChar -> builder.AppendChar rawChar

                    | _ -> builder.AppendChar c
                    inner false
                elif c = '\\' then
                    inner true
                elif c = '"' then
                    builder.ToString()
                    |> VariableValue.String
                    |> Expression.ConstantValue
                    |> ParseResult.Succeeded
                else
                    builder.AppendChar c
                    inner false

        inner false

    /// Parse out a string literal from the token stream.  The only special character here is
    /// an escaped '.  Everything else is taken literally 
    ///
    /// help literal-string
    member x.ParseStringLiteral() = 
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        _tokenizer.MoveNextToken()

        let builder = System.Text.StringBuilder()
        let mutable result = ParseResult.Failed Resources.Parser_MissingQuote
        let mutable isDone = false
        while not isDone && not _tokenizer.IsAtEndOfLine do
            match _tokenizer.CurrentChar with
            | '\\' ->
                // Need to peek ahead to see if this is a back slash to be inserted or if it's
                // escaping a single quote
                _tokenizer.MoveNextChar()
                match _tokenizer.CurrentChar with 
                | '\'' ->
                    builder.AppendChar '\''
                    _tokenizer.MoveNextChar()
                | _ ->
                    builder.AppendChar '\\'
            | '\'' ->
                // Found the terminating character
                _tokenizer.MoveNextChar()
                result <- builder.ToString()
                |> VariableValue.String
                |> Expression.ConstantValue
                |> ParseResult.Succeeded
                isDone <- true
            | c ->
                builder.AppendChar c
                _tokenizer.MoveNextChar()

        result

    /// Parse out a string option value from the token stream.  The string ends
    /// on whitespace or vertical bar. If the option is a filename, backslash
    // escapes only non-filename characters.  Otherwise, it escapes all characters.
    ///
    /// help option-backslash
    member x.ParseOptionBackslash isFileName = 
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote

        let builder = System.Text.StringBuilder()
        let mutable isDone = false
        while not isDone && not _tokenizer.IsAtEndOfLine do
            match _tokenizer.CurrentChar with
            | '\\' ->

                // Escape the next character.
                _tokenizer.MoveNextChar()
                if _tokenizer.IsAtEndOfLine then
                    builder.AppendChar '\\'
                    isDone <- true
                else
                    let char = _tokenizer.CurrentChar
                    if isFileName && CharUtil.IsFileNameChar char then
                        builder.AppendChar '\\'
                        builder.AppendChar char
                    else
                        builder.AppendChar char
                    _tokenizer.MoveNextChar()

            | char ->
                if CharUtil.IsBlank char || "|".Contains(char.ToString()) then
                    _tokenizer.MoveNextChar()
                    isDone <- true
                else
                    builder.AppendChar char
                    _tokenizer.MoveNextChar()

        // Expand environment variables for filenames.
        let value = builder.ToString()
        if isFileName then
            SystemUtil.ResolvePath value
        else
            value

    /// Parse out the 'tabnew' / 'tabedit' commands.  They have the same set of arguments
    member x.ParseTabNew() = 
        let filePath = x.ParseRestOfLineAsFilePath()
        LineCommand.TabNew filePath

    /// Parse out the 'tabnext' command
    member x.ParseTabNext() =   
        x.SkipBlanks()
        let count = x.ParseNumber()
        LineCommand.GoToNextTab count

    /// Parse out the 'tabprevious' command
    member x.ParseTabPrevious() =   
        x.SkipBlanks()
        let count = x.ParseNumber()
        LineCommand.GoToPreviousTab count

    /// Parse out the unlet command.  
    ///
    /// Currently only names are supported.  We don't support unletting specific dictionary
    /// or list entries
    member x.ParseUnlet() =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let rec getNames withValue = 
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Word name ->
                _tokenizer.MoveNextToken()
                x.SkipBlanks()
                getNames (fun list -> withValue (name :: list))
            | TokenKind.EndOfLine ->
                let list = withValue []
                LineCommand.Unlet (hasBang, list)
            | _ -> x.ParseError "Error"
        getNames (fun x -> x)

    member x.ParseQuickFixWindow _ =
        _tokenizer.MoveToEndOfLine()
        LineCommand.QuickFixWindow

    member x.ParseQuickFixNext count =
        let hasBang = x.ParseBang()
        LineCommand.QuickFixNext (count, hasBang)

    member x.ParseQuickFixPrevious count =
        let hasBang = x.ParseBang()
        LineCommand.QuickFixPrevious (count, hasBang)

    member x.ParseQuickFixRewind defaultToLast =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let number = x.ParseNumber()
        LineCommand.QuickFixRewind (number, defaultToLast, hasBang)

    member x.ParseLocationWindow _ =
        _tokenizer.MoveToEndOfLine()
        LineCommand.LocationWindow

    member x.ParseLocationNext count =
        let hasBang = x.ParseBang()
        LineCommand.LocationNext (count, hasBang)

    member x.ParseLocationPrevious count =
        let hasBang = x.ParseBang()
        LineCommand.LocationPrevious (count, hasBang)

    member x.ParseLocationRewind defaultToLast =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let number = x.ParseNumber()
        LineCommand.LocationRewind (number, defaultToLast, hasBang)

    /// Parse out the quit and write command.  This includes 'wq', 'xit' and 'exit' commands.
    member x.ParseQuitAndWrite lineRange = 
        let hasBang = x.ParseBang()

        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let fileName =
            if _tokenizer.IsAtEndOfLine then
                None
            else
                x.ParseRestOfLine() |> Some

        LineCommand.QuitWithWrite (lineRange, hasBang, fileOptionList, fileName)

    /// Parse out a variable name from the system.  This handles the scoping prefix 
    member x.ParseVariableName() = 

        let parseNameScope name = 
            match name with
            | "g" -> Some NameScope.Global
            | "b" -> Some NameScope.Buffer
            | "w" -> Some NameScope.Window
            | "t" -> Some NameScope.Tab
            | "s" -> Some NameScope.Script
            | "l" -> Some NameScope.Function
            | "v" -> Some NameScope.Vim
            | _ -> None

        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.SkipBlanks
        _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDigitsInWord |> ignore
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word word ->
            _tokenizer.MoveNextToken()
            if _tokenizer.CurrentChar = ':' then
                _tokenizer.MoveNextToken()
                match _tokenizer.CurrentTokenKind, parseNameScope word with
                | TokenKind.Word name, Some nameScope -> 
                    _tokenizer.MoveNextToken()
                    let name = { NameScope = nameScope; Name = name } 
                    ParseResult.Succeeded name
                | _ -> ParseResult.Failed Resources.Parser_Error
            else
                let name = { NameScope = NameScope.Global; Name = word }
                ParseResult.Succeeded name
        | _ -> ParseResult.Failed Resources.Parser_Error

    member x.ParseEnvironmentVariableName() =
        _tokenizer.MoveNextToken()
        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDigitsInWord
        let tokenKind = _tokenizer.CurrentTokenKind
        _tokenizer.MoveNextToken()
        match tokenKind with
        | TokenKind.Word word ->
            Expression.EnvironmentVariableName word |> ParseResult.Succeeded
        | _ -> ParseResult.Failed "Environment variable name missing"

    /// Parse out a visual studio command.  The format is "commandName argument".  The command
    /// name can use letters, numbers and a period.  The rest of the line after will be taken
    /// as the argument
    member x.ParseHostCommand() = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let command = x.ParseWhile (fun token -> 
            match token.TokenKind with 
            | TokenKind.Word _ -> true
            | TokenKind.Number _ -> true
            | TokenKind.Character '.' -> true
            | TokenKind.Character '_' -> true
            | _ -> false)
        match command with 
        | None -> x.ParseError Resources.Parser_Error
        | Some command ->
            x.SkipBlanks()
            /// we want to do: vsc Edit.FindinFiles "foo bar" /lookin:"Current Project"
            /// so we need to allow double quotes and parse them into argument
            _tokenizer.TokenizerFlags <- _tokenizer.TokenizerFlags ||| TokenizerFlags.AllowDoubleQuote
            let argument = x.ParseRestOfLine()
            LineCommand.HostCommand (hasBang, command, argument)

    member x.ParseWrite lineRange = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        // Pares out the final fine name if it's provided
        x.SkipBlanks()
        let fileName =
            if _tokenizer.IsAtEndOfLine then
                None
            else
                x.ParseRestOfLine() |> Some

        LineCommand.Write (lineRange, hasBang, fileOptionList, fileName)

    member x.ParseWriteAll andQuit =
        let hasBang = x.ParseBang()
        LineCommand.WriteAll (hasBang, andQuit)

    /// Parse out the yank command
    member x.ParseYank lineRange =
        x.SkipBlanks()
        let registerName = x.ParseRegisterName ParseRegisterName.NoNumbered

        x.SkipBlanks()
        let count = x.ParseNumber()

        LineCommand.Yank (lineRange, registerName, count)

    /// Parse out the files command
    member x.ParseFiles() =
        LineCommand.Files

    /// Parse out the fold command
    member x.ParseFold lineRange =
        LineCommand.Fold lineRange

    /// Parse out the :global command
    member x.ParseGlobal lineRange =
        let hasBang = x.ParseBang()
        x.ParseGlobalCore lineRange (not hasBang)

    /// Parse out the :normal command
    member x.ParseNormal lineRange =
        x.SkipBlanks ()
        _tokenizer.TokenizerFlags <- _tokenizer.TokenizerFlags ||| TokenizerFlags.AllowDoubleQuote
        let inputs = seq {
            while not _tokenizer.IsAtEndOfLine do
                yield KeyInputUtil.CharToKeyInput _tokenizer.CurrentChar
                _tokenizer.MoveNextChar()
        }
        LineCommand.Normal (lineRange, List.ofSeq inputs)

    /// Parse out the :help command
    member x.ParseHelp() =
        x.SkipBlanks ()
        let subject = x.ParseRestOfLine()
        LineCommand.Help subject

    /// Parse out the :vimhelp command
    member x.ParseVimHelp() =
        x.SkipBlanks ()
        let subject = x.ParseRestOfLine()
        LineCommand.VimHelp subject

    /// Parse out the :history command
    member x.ParseHistory() =
        LineCommand.History

    /// Parse out the core global information. 
    member x.ParseGlobalCore lineRange matchPattern =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '\\' -> x.ParseError Resources.Parser_InvalidArgument
        | TokenKind.Character '"' -> x.ParseError Resources.Parser_InvalidArgument
        | TokenKind.Character delimiter ->
            _tokenizer.MoveNextToken()
            let pattern, foundDelimiter = x.ParsePattern delimiter
            if foundDelimiter then
                let command = x.ParseSingleLine()
                LineCommand.Global (lineRange, pattern, matchPattern, command)
            else
                x.ParseError Resources.Parser_InvalidArgument
        | _ -> x.ParseError Resources.Parser_InvalidArgument

    /// Parse out the :if command from the buffer
    member x.ParseIfStart() = 
        x.ParseIfOrElseIf LineCommand.IfStart

    member x.ParseIfEnd() =
        _tokenizer.MoveToEndOfLine()
        LineCommand.IfEnd

    member x.ParseElse() = 
        _tokenizer.MoveToEndOfLine()
        LineCommand.Else

    /// Parse out the :elseif command from the buffer
    member x.ParseElseIf() = 
        x.ParseIfOrElseIf LineCommand.ElseIf

    member x.ParseIfOrElseIf onSuccess = 
        x.SkipBlanks()
        match x.ParseExpressionCore() with
        | ParseResult.Failed msg -> x.ParseError msg
        | ParseResult.Succeeded expr -> 
            let lineCommand = onSuccess expr 
            _tokenizer.MoveToEndOfLine()
            lineCommand

    /// Parse out the :if command from the buffer given the initial conditional expression
    /// for the if command
    member x.ParseIf expr =
        let conditionalParser = ConditionalParser(x, expr)
        conditionalParser.Parse()
        
    /// Parse out the join command
    member x.ParseJoin lineRange =  
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.Join (lineRange, x.ParseNumber())
        let joinKind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        LineCommand.Join (lineRange, joinKind)

    /// Parse out the :echo command
    member x.ParseEcho () = 
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        x.SkipBlanks()
        if _tokenizer.IsAtEndOfLine then
            LineCommand.Nop
        else
            match x.ParseExpressionCore() with
            | ParseResult.Failed msg -> x.ParseError msg
            | ParseResult.Succeeded expr -> LineCommand.Echo expr

    /// Parse out the :execute command
    member x.ParseExecute () = 
        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        x.SkipBlanks()
        if _tokenizer.IsAtEndOfLine then
            LineCommand.Nop
        else
            match x.ParseExpressionCore() with
            | ParseResult.Failed msg -> x.ParseError msg
            | ParseResult.Succeeded expr -> LineCommand.Execute expr

    /// Parse out the :let command
    member x.ParseLet () = 
        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.SkipBlanks

        // Parse an assignment of lhs to '= rhs'.
        let parseAssignment (lhs: 'T) (assign: 'T -> Expression -> LineCommand) =
            if _tokenizer.CurrentChar = '=' then
                _tokenizer.MoveNextToken()
                match x.ParseExpressionCore() with
                | ParseResult.Succeeded rhs -> assign lhs rhs
                | ParseResult.Failed msg -> x.ParseError msg
            else
                x.ParseError Resources.Parser_Error

        // Handle the case where let is being used for display.  
        //  let x y z 
        let parseDisplayLet firstName = 
            let rec inner cont = 
                if _tokenizer.IsAtEndOfLine then
                    cont []
                else
                    match x.ParseVariableName() with
                    | ParseResult.Succeeded name -> inner (fun rest -> cont (name :: rest))
                    | ParseResult.Failed msg -> x.ParseError msg

            inner (fun rest ->
                let names = firstName :: rest
                LineCommand.DisplayLet names)

        if _tokenizer.CurrentTokenKind = TokenKind.EndOfLine then
            LineCommand.DisplayLet []
        else
            match x.ParseExpressionCore() with
            | ParseResult.Succeeded (Expression.VariableName variableName) ->
                if _tokenizer.CurrentChar = '=' then
                    parseAssignment variableName (fun lhs rhs -> LineCommand.Let (lhs, rhs))
                else
                    parseDisplayLet variableName
            | ParseResult.Succeeded (Expression.EnvironmentVariableName variableName) ->
                parseAssignment variableName (fun lhs rhs -> LineCommand.LetEnvironment (lhs, rhs))
            | ParseResult.Succeeded (Expression.RegisterName registerName) ->
                parseAssignment registerName (fun lhs rhs -> LineCommand.LetRegister (lhs, rhs))
            | ParseResult.Failed msg ->
                x.ParseError msg
            | _ ->
                x.ParseError Resources.Parser_Error

    /// Parse out the :make command.  The arguments here other than ! are undefined.  Just
    /// get the text blob and let the interpreter / host deal with it 
    member x.ParseMake () = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let arguments = x.ParseRestOfLine()
        LineCommand.Make (hasBang, arguments)

    /// Parse out the :put command.  The presence of a bang indicates that we need
    /// to put before instead of after
    member x.ParsePut lineRange =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let registerName = x.ParseRegisterName ParseRegisterName.NoNumbered

        if hasBang then
            LineCommand.PutBefore (lineRange, registerName)
        else
            LineCommand.PutAfter (lineRange, registerName)

    member x.ParseDisplayLines lineRange initialFlags =
        x.SkipBlanks()
        let lineRange = x.ParseLineRangeSpecifierEndCount lineRange
        x.SkipBlanks()
        _lineCommandBuilder { 
            let! flags = x.ParseLineCommandFlags initialFlags
            return LineCommand.DisplayLines (lineRange, flags) }

    /// Parse out the :read command
    member x.ParseRead lineRange = 
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()
        match fileOptionList with
        | [] ->
            // Can still be the file or command variety.  The ! or lack there of will
            // differentiate it at this point
            x.SkipBlanks()
            if _tokenizer.CurrentChar = '!' then
                _tokenizer.MoveNextToken()
                use resetFlags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
                let command = x.ParseRestOfLine()
                LineCommand.ReadCommand (lineRange, command)
            else
                let filePath = x.ParseRestOfLine()
                LineCommand.ReadFile (lineRange, [], filePath)
        | _ ->
            // Can only be the file variety.
            x.SkipBlanks()
            let filePath = x.ParseRestOfLine()
            LineCommand.ReadFile (lineRange, fileOptionList, filePath)

    /// Parse out the :retab command
    member x.ParseRetab lineRange =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let newTabStop = x.ParseNumber()
        LineCommand.Retab (lineRange, hasBang, newTabStop)

    /// Whether the specified setting is a file name setting, e.g. shell
    /// TODO: A local setting might someday be a file name setting
    member x.IsFileNameSetting (name: string) =
        match _globalSettings.GetSetting name with
        | None -> false
        | Some setting -> setting.HasFileNameOption

    /// Parse out the :set command and all of it's variants
    member x.ParseSet () = 

        // Parse out an individual option and add it to the 'withArgument' continuation
        let rec parseOption withArgument = 
            x.SkipBlanks()

            // Parse out the next argument and use 'argument' as the value of the current
            // argument
            let parseNext argument = parseOption (fun list -> argument :: list |> withArgument)

            // Parse out an operator.  Parse out the value and use the specified setting name
            // and argument function as the argument
            let parseSetValue name argumentFunc = 
                if _tokenizer.IsAtEndOfLine || _tokenizer.CurrentChar = ' ' then
                    _tokenizer.MoveNextToken()
                    parseNext (SetArgument.AssignSetting (name, ""))
                else
                    let isFileName = x.IsFileNameSetting name
                    let value = x.ParseOptionBackslash isFileName
                    parseNext (argumentFunc (name, value))

            // Parse out a simple assignment.  Move past the assignment char and get the value
            let parseAssign name = 
                _tokenizer.MoveNextChar()
                parseSetValue name SetArgument.AssignSetting

            // Parse out a compound operator.  This is used for '+=' and such.  This will be called
            // with the index pointed at the first character
            let parseCompoundOperator name argumentFunc = 
                _tokenizer.MoveNextToken()
                if _tokenizer.CurrentChar = '=' then
                    _tokenizer.MoveNextChar()
                    parseSetValue name argumentFunc
                else
                    x.ParseError Resources.Parser_Error

            match _tokenizer.CurrentTokenKind with
            | TokenKind.EndOfLine ->
                let list = withArgument []
                LineCommand.Set list
            | TokenKind.Word "all" ->
                _tokenizer.MoveNextToken()
                if _tokenizer.CurrentChar = '&' then
                    _tokenizer.MoveNextToken()
                    parseNext SetArgument.ResetAllToDefault
                else
                    parseNext SetArgument.DisplayAllButTerminal
            | TokenKind.Word "termcap" ->
                _tokenizer.MoveNextToken()
                parseNext SetArgument.DisplayAllTerminal
            | TokenKind.Word name ->

                // An option name can have an '_' due to the vsvim extension names
                let name = x.ParseWhile (fun token -> 
                    match token.TokenKind with
                    | TokenKind.Word _ -> true
                    | TokenKind.Character '_' -> true
                    | _ -> false) |> OptionUtil.getOrDefault ""

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
                    | TokenKind.Character '?' -> _tokenizer.MoveNextToken(); parseNext (SetArgument.DisplaySetting name)
                    | TokenKind.Character '!' -> _tokenizer.MoveNextToken(); parseNext (SetArgument.InvertSetting name)
                    | TokenKind.Character ':' -> parseAssign name
                    | TokenKind.Character '=' -> parseAssign name
                    | TokenKind.Character '+' -> parseCompoundOperator name SetArgument.AddSetting
                    | TokenKind.Character '^' -> parseCompoundOperator name SetArgument.MultiplySetting
                    | TokenKind.Character '-' -> parseCompoundOperator name SetArgument.SubtractSetting
                    | TokenKind.Blank -> _tokenizer.MoveNextToken(); parseNext (SetArgument.UseSetting name)
                    | TokenKind.EndOfLine -> parseNext (SetArgument.UseSetting name)
                    | _ -> x.ParseError Resources.Parser_Error
            | _ ->
                 x.ParseError Resources.Parser_Error

        parseOption (fun x -> x)

    /// Parse out the :sort command
    member x.ParseSort lineRange =

        // Whether this valid as a sort string delimiter
        let isValidDelimiter c =
            let isBad = CharUtil.IsLetter c
            not isBad

        let hasBang = x.ParseBang()
        x.SkipBlanks()
        match x.ParseSortFlags() with
        | ParseResult.Failed message -> x.ParseError message
        | ParseResult.Succeeded flags ->
            x.SkipBlanks()
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character delimiter ->
                if isValidDelimiter delimiter then
                    _tokenizer.MoveNextToken()
                    let pattern, foundDelimiter = x.ParsePattern delimiter
                    if not foundDelimiter then
                        LineCommand.Sort (lineRange, hasBang, flags, Some pattern)
                    else
                        x.SkipBlanks()
                        match x.ParseSortFlags() with
                        | ParseResult.Failed message -> x.ParseError message
                        | ParseResult.Succeeded moreFlags ->
                            let flags = flags ||| moreFlags
                            LineCommand.Sort (lineRange, hasBang, flags, Some pattern)
                else
                    LineCommand.Sort (lineRange, hasBang, flags, None)
            | _ ->
                LineCommand.Sort (lineRange, hasBang, flags, None)

    /// Parse out the :source command.  It can have an optional '!' following it then a file
    /// name 
    member x.ParseSource() =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileName = x.ParseRestOfLine()
        let fileName = fileName.Trim()
        LineCommand.Source (hasBang, fileName)

    /// Parse out the :split command
    member x.ParseSplit splitType lineRange =
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let commandOption = x.ParseCommandOption()

        splitType (lineRange, fileOptionList, commandOption)

    member x.ParseStopInsert () =
        LineCommand.StopInsert

    /// Parse out the :qal and :quitall commands
    member x.ParseQuitAll () =
        let hasBang = x.ParseBang()
        LineCommand.QuitAll hasBang

    /// Parse out the :quit command.
    member x.ParseQuit () = 
        let hasBang = x.ParseBang()
        LineCommand.Quit hasBang

    /// Parse out the ':digraphs' command
    member x.ParseDigraphs () =

        let mutable digraphList: (char * char * int) list = List.Empty
        let mutable (result: LineCommand option) = None
        let mutable more = true
        while more do
            x.SkipBlanks()
            if _tokenizer.IsAtEndOfLine then
                more <- false
            else
                let char1 = _tokenizer.CurrentChar
                _tokenizer.MoveNextChar()
                if _tokenizer.IsAtEndOfLine then
                    result <- x.ParseError Resources.CommandMode_InvalidCommand |> Some
                else
                    let char2 = _tokenizer.CurrentChar
                    _tokenizer.MoveNextChar()
                    x.SkipBlanks()
                    match x.ParseNumber() with
                    | Some number ->
                        let digraph = (char1, char2, number)
                        digraphList <- digraph :: digraphList
                    | None ->
                        result <- x.ParseError Resources.CommandMode_InvalidCommand |> Some

        match result with
        | Some lineCommand ->
            lineCommand
        | None ->
            digraphList <- List.rev digraphList
            LineCommand.Digraphs digraphList

    /// Parse out the :display and :registers command.  Just takes a single argument 
    /// which is the register name
    member x.ParseDisplayRegisters () = 
        let mutable nameList: RegisterName list = List.Empty
        let mutable more = true
        while more do
            x.SkipBlanks()
            match x.ParseRegisterName ParseRegisterName.All with
            | Some name -> 
                nameList <- name :: nameList
                more <- true
            | None -> more <- false

        nameList <- List.rev nameList
        LineCommand.DisplayRegisters nameList

    /// Parse out the :marks command.  Handles both the no argument and argument
    /// case
    member x.ParseDisplayMarks () = 
        x.SkipBlanks()

        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word word ->

            _tokenizer.MoveNextToken()
            let mutable message: string option = None
            let list = System.Collections.Generic.List<Mark>()
            for c in word do
                match Mark.OfChar c with
                | None -> message <- Some (Resources.Parser_NoMarksMatching c)
                | Some mark -> list.Add(mark)

            match message with
            | None -> LineCommand.DisplayMarks (List.ofSeq list)
            | Some message -> x.ParseError message
        | _ ->
            // Simple case.  No marks to parse out.  Just return them all
            LineCommand.DisplayMarks List.empty

    /// Parse a single line.  This will not attempt to link related LineCommand values like :function
    /// and :endfunc.  Instead it will return the result of the current LineCommand
    member x.ParseSingleLine() =

        // Skip the white space and: at the beginning of the line
        while _tokenizer.CurrentChar = ':' || _tokenizer.CurrentTokenKind = TokenKind.Blank do
            _tokenizer.MoveNextChar()

        let lineRange = x.ParseLineRange()

        // Skip the white space after a valid line range.
        match lineRange with
        | LineRangeSpecifier.None -> ()
        | _ -> x.SkipBlanks()

        let noRange parseFunc = 
            match lineRange with
            | LineRangeSpecifier.None -> parseFunc()
            | _ -> x.ParseError Resources.Parser_NoRangeAllowed

        let handleParseResult (lineCommand: LineCommand) =
            let lineCommand = 
                if lineCommand.Failed then
                    // If there is already a failure don't look any deeper.
                    lineCommand 
                else
                    x.SkipBlanks()
                    if _tokenizer.IsAtEndOfLine then
                        lineCommand
                    elif _tokenizer.CurrentChar = '|' then
                        _tokenizer.MoveNextChar()
                        let nextCommand = x.ParseSingleLine()
                        if nextCommand.Failed then
                            nextCommand
                        else
                            LineCommand.Compose (lineCommand, nextCommand)
                    else

                        // If there are still characters then it's illegal
                        // trailing characters.
                        x.ParseError Resources.CommandMode_TrailingCharacters
            x.MoveToNextLine() |> ignore
            lineCommand

        let handleCount parseFunc = 
            match lineRange with
            | LineRangeSpecifier.SingleLine lineSpecifier ->
                match lineSpecifier with
                | LineSpecifier.Number count -> parseFunc (Some count)
                | _ -> parseFunc None
            | _ -> parseFunc None

        let doParse name = 
            let parseResult = 
                match name with
                | "autocmd" -> noRange x.ParseAutoCommand
                | "behave" -> noRange x.ParseBehave
                | "buffers" -> noRange x.ParseFiles
                | "call" -> x.ParseCall lineRange
                | "cd" -> noRange x.ParseChangeDirectory
                | "cfirst" -> noRange (fun () -> x.ParseQuickFixRewind false)
                | "chdir" -> noRange x.ParseChangeDirectory
                | "clast" -> noRange (fun () -> x.ParseQuickFixRewind true)
                | "close" -> noRange x.ParseClose
                | "cmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Command])
                | "cmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Command])
                | "cnext" -> handleCount x.ParseQuickFixNext
                | "cNext" -> handleCount x.ParseQuickFixPrevious
                | "cnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Command])
                | "copy" -> x.ParseCopyTo lineRange 
                | "cprevious" -> handleCount x.ParseQuickFixPrevious
                | "crewind" -> noRange (fun () -> x.ParseQuickFixRewind false)
                | "csx" -> x.ParseCSharpScript(lineRange, createEachTime = false)
                | "csxe" -> x.ParseCSharpScript(lineRange, createEachTime = true)
                | "cunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Command])
                | "cwindow" -> noRange x.ParseQuickFixWindow
                | "delete" -> x.ParseDelete lineRange
                | "delmarks" -> noRange (fun () -> x.ParseDeleteMarks())
                | "digraphs" -> noRange x.ParseDigraphs
                | "display" -> noRange x.ParseDisplayRegisters 
                | "echo" -> noRange x.ParseEcho
                | "edit" -> noRange x.ParseEdit
                | "else" -> noRange x.ParseElse
                | "execute" -> noRange x.ParseExecute
                | "elseif" -> noRange x.ParseElseIf
                | "endfunction" -> noRange x.ParseFunctionEnd
                | "endif" -> noRange x.ParseIfEnd
                | "exit" -> x.ParseQuitAndWrite lineRange
                | "files" -> noRange x.ParseFiles
                | "fold" -> x.ParseFold lineRange
                | "function" -> noRange x.ParseFunctionStart
                | "global" -> x.ParseGlobal lineRange
                | "normal" -> x.ParseNormal lineRange
                | "help" -> noRange x.ParseHelp
                | "vimhelp" -> noRange x.ParseVimHelp
                | "history" -> noRange (fun () -> x.ParseHistory())
                | "if" -> noRange x.ParseIfStart
                | "iunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Insert])
                | "imap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Insert])
                | "imapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Insert])
                | "inoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Insert])
                | "join" -> x.ParseJoin lineRange 
                | "lcd" -> noRange x.ParseChangeLocalDirectory
                | "lchdir" -> noRange x.ParseChangeLocalDirectory
                | "let" -> noRange x.ParseLet
                | "lfirst" -> noRange (fun () -> x.ParseLocationRewind false)
                | "list" -> x.ParseDisplayLines lineRange LineCommandFlags.List
                | "llast" -> noRange (fun () -> x.ParseLocationRewind true)
                | "lmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Language])
                | "lnext" -> handleCount x.ParseLocationNext
                | "lNext" -> handleCount x.ParseLocationPrevious
                | "lnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Language])
                | "lprevious" -> handleCount x.ParseLocationPrevious
                | "lrewind" -> noRange (fun () -> x.ParseLocationRewind false)
                | "ls" -> noRange x.ParseFiles
                | "lunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Language])
                | "lwindow" -> noRange x.ParseLocationWindow
                | "make" -> noRange x.ParseMake 
                | "marks" -> noRange x.ParseDisplayMarks
                | "map"-> noRange (fun () -> x.ParseMapKeys true [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Select; KeyRemapMode.OperatorPending])
                | "mapclear" -> noRange (fun () -> x.ParseMapClear true [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Command; KeyRemapMode.OperatorPending])
                | "move" -> x.ParseMoveTo lineRange 
                | "nmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Normal])
                | "nmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Normal])
                | "nnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Normal])
                | "number" -> x.ParseDisplayLines lineRange LineCommandFlags.AddLineNumber
                | "nunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Normal])
                | "nohlsearch" -> noRange (fun () -> LineCommand.NoHighlightSearch)
                | "noremap"-> noRange (fun () -> x.ParseMapKeysNoRemap true [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Select; KeyRemapMode.OperatorPending])
                | "omap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.OperatorPending])
                | "omapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.OperatorPending])
                | "only" -> noRange (fun () -> LineCommand.Only)
                | "onoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.OperatorPending])
                | "ounmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.OperatorPending])
                | "put" -> x.ParsePut lineRange
                | "print" -> x.ParseDisplayLines lineRange LineCommandFlags.Print
                | "pwd" -> noRange (fun () -> LineCommand.PrintCurrentDirectory)
                | "quit" -> noRange x.ParseQuit
                | "qall" -> noRange x.ParseQuitAll
                | "quitall" -> noRange x.ParseQuitAll
                | "read" -> x.ParseRead lineRange
                | "redo" -> noRange (fun () -> LineCommand.Redo)
                | "retab" -> x.ParseRetab lineRange
                | "registers" -> noRange x.ParseDisplayRegisters 
                | "set" -> noRange x.ParseSet
                | "shell" -> noRange x.ParseShell
                | "sort" -> x.ParseSort lineRange
                | "source" -> noRange x.ParseSource
                | "split" -> x.ParseSplit LineCommand.HorizontalSplit lineRange
                | "stopinsert" -> x.ParseStopInsert()
                | "substitute" -> x.ParseSubstitute lineRange (fun x -> x)
                | "smagic" -> x.ParseSubstituteMagic lineRange
                | "smap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Select])
                | "smapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Select])
                | "snomagic" -> x.ParseSubstituteNoMagic lineRange
                | "snoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Select])
                | "sunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Select])
                | "t" -> x.ParseCopyTo lineRange 
                | "tabedit" -> noRange x.ParseTabNew
                | "tabfirst" -> noRange (fun () -> LineCommand.GoToFirstTab)
                | "tabrewind" -> noRange (fun () -> LineCommand.GoToFirstTab)
                | "tablast" -> noRange (fun () -> LineCommand.GoToLastTab)
                | "tabnew" -> noRange x.ParseTabNew
                | "tabnext" -> noRange x.ParseTabNext 
                | "tabNext" -> noRange x.ParseTabPrevious
                | "tabonly" -> noRange (fun () -> LineCommand.TabOnly)
                | "tabprevious" -> noRange x.ParseTabPrevious
                | "undo" -> noRange (fun () -> LineCommand.Undo)
                | "unlet" -> noRange x.ParseUnlet
                | "unmap" -> noRange (fun () -> x.ParseMapUnmap true [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Select; KeyRemapMode.OperatorPending])
                | "version" -> noRange (fun () -> LineCommand.Version)
                | "vglobal" -> x.ParseGlobalCore lineRange false
                | "vmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Visual; KeyRemapMode.Select])
                | "vmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Visual; KeyRemapMode.Select])
                | "vscmd" -> x.ParseHostCommand()
                | "vsplit" -> x.ParseSplit LineCommand.VerticalSplit lineRange
                | "vnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Visual; KeyRemapMode.Select])
                | "vunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Visual; KeyRemapMode.Select])
                | "wall" -> noRange (fun () -> x.ParseWriteAll false)
                | "wqall" -> noRange (fun () -> x.ParseWriteAll true)
                | "write" -> x.ParseWrite lineRange
                | "wq" -> x.ParseQuitAndWrite lineRange
                | "xall"-> noRange (fun () -> x.ParseWriteAll true)
                | "xit" -> x.ParseQuitAndWrite lineRange
                | "xmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Visual])
                | "xmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Visual])
                | "xnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Visual])
                | "xunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Visual])
                | "yank" -> x.ParseYank lineRange
                | "/" -> x.ParseSearch lineRange SearchPath.Forward
                | "?" -> x.ParseSearch lineRange SearchPath.Backward
                | "<" -> x.ParseShiftLeft lineRange
                | ">" -> x.ParseShiftRight lineRange
                | "&" -> x.ParseSubstituteRepeat lineRange SubstituteFlags.None
                | "~" -> x.ParseSubstituteRepeat lineRange SubstituteFlags.UsePreviousSearchPattern
                | "!" -> x.ParseShellCommand lineRange
                | "@" -> x.ParseAtCommand lineRange
                | "#" -> x.ParseDisplayLines lineRange LineCommandFlags.AddLineNumber
                | _ -> x.ParseError Resources.Parser_Error

            handleParseResult parseResult

        // Get the command name and make sure to expand it to it's possible
        // full name.
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word word ->
            _tokenizer.MoveNextToken()
            x.TryExpand word |> doParse
        | TokenKind.Character c ->
            _tokenizer.MoveNextToken()
            c |> StringUtil.OfChar |> x.TryExpand |> doParse
        | TokenKind.EndOfLine ->
            match lineRange with
            | LineRangeSpecifier.None -> handleParseResult LineCommand.Nop
            | _ -> LineCommand.JumpToLastLine lineRange |> handleParseResult
        | _ -> 
            LineCommand.JumpToLastLine lineRange |> handleParseResult

    /// Parse out a single command.  Unlike ParseSingleLine this will parse linked commands.  So
    /// it won't ever return LineCommand.FuntionStart but instead will return LineCommand.Function
    /// instead
    member x.ParseSingleCommand() =
        match x.ParseSingleLine() with 
        | LineCommand.FunctionStart functionDefinition -> x.ParseFunction functionDefinition 
        | LineCommand.IfStart expr -> x.ParseIf expr
        | lineCommand -> lineCommand

    // Parse out the name of a setting/option
    member x.ParseOptionName() =
        _tokenizer.MoveNextToken()
        let tokenKind = _tokenizer.CurrentTokenKind
        _tokenizer.MoveNextToken()
        match tokenKind with
        | TokenKind.Word word ->
            Expression.OptionName word |> ParseResult.Succeeded
        | _ -> ParseResult.Failed "Option name missing"

    member x.ParseList() =
        let rec parseList atBeginning =
            let recursivelyParseItems() =
                match x.ParseSingleExpression() with
                | ParseResult.Succeeded item ->
                    match parseList false with
                    | ParseResult.Succeeded otherItems -> item :: otherItems |> ParseResult.Succeeded
                    | ParseResult.Failed msg -> ParseResult.Failed msg
                | ParseResult.Failed msg -> ParseResult.Failed msg
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character '[' ->
                _tokenizer.MoveNextToken()
                parseList true
            | TokenKind.Character ']' ->
                _tokenizer.MoveNextToken()
                ParseResult.Succeeded []
            | TokenKind.Character ',' ->
                _tokenizer.MoveNextToken()
                x.SkipBlanks()
                recursivelyParseItems()
            | _ ->
                if atBeginning then recursivelyParseItems()
                else ParseResult.Failed Resources.Parser_Error
        match parseList true with
        | ParseResult.Succeeded expressionList -> Expression.List expressionList |> ParseResult.Succeeded
        | ParseResult.Failed msg -> ParseResult.Failed msg

    member x.ParseDictionary() =
        _tokenizer.MoveNextToken()
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '}' ->
            _tokenizer.MoveNextToken()
            VariableValue.Dictionary Map.empty |> Expression.ConstantValue |> ParseResult.Succeeded
        | _ -> ParseResult.Failed Resources.Parser_Error

    /// Parse out a single expression
    member x.ParseSingleExpression() =
        // Re-examine the current token based on the knowledge that double quotes are
        // legal in this context as a real token
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '\"' ->
            x.ParseStringConstant()
        | TokenKind.Character '\'' -> 
            x.ParseStringLiteral()
        | TokenKind.Character '&' ->
            x.ParseOptionName()
        | TokenKind.Character '[' ->
            x.ParseList()
        | TokenKind.Character '{' ->
            x.ParseDictionary()
        | TokenKind.Character '$' ->
            x.ParseEnvironmentVariableName()
        | TokenKind.Character '@' ->
            _tokenizer.MoveNextToken()
            match x.ParseRegisterName ParseRegisterName.All with
            | Some name -> Expression.RegisterName name |>  ParseResult.Succeeded
            | None -> ParseResult.Failed Resources.Parser_UnrecognizedRegisterName
        | TokenKind.Number number -> 
            _tokenizer.MoveNextToken()
            VariableValue.Number number |> Expression.ConstantValue |> ParseResult.Succeeded
        | _ ->
            match x.ParseVariableName() with
            | ParseResult.Failed msg -> ParseResult.Failed msg
            | ParseResult.Succeeded variable -> // TODO the nesting is getting deep here; refactor
                x.SkipBlanks()
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Character '(' ->
                    match x.ParseFunctionArguments true with
                    | ParseResult.Succeeded args ->
                        Expression.FunctionCall(variable, args) |> ParseResult.Succeeded
                    | ParseResult.Failed msg -> ParseResult.Failed msg
                | _ -> Expression.VariableName variable |> ParseResult.Succeeded

    member x.ParseFunctionArguments atBeginning =
        let recursivelyParseArguments() =
            match x.ParseSingleExpression() with
            | ParseResult.Succeeded arg ->
                match x.ParseFunctionArguments false with
                | ParseResult.Succeeded otherArgs -> arg :: otherArgs |> ParseResult.Succeeded
                | ParseResult.Failed msg -> ParseResult.Failed msg
            | ParseResult.Failed msg -> ParseResult.Failed msg
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '(' ->
            _tokenizer.MoveNextToken()
            x.ParseFunctionArguments true
        | TokenKind.Character ')' ->
            _tokenizer.MoveNextToken()
            ParseResult.Succeeded []
        | TokenKind.Character ',' ->
            _tokenizer.MoveNextToken()
            x.SkipBlanks()
            recursivelyParseArguments()
        | _ ->
            if atBeginning then recursivelyParseArguments()
            else ParseResult.Failed "invalid arguments for function"

    /// Parse out a complete expression from the text.  
    member x.ParseExpressionCore() =
        _parseResultBuilder {
            let! expr = x.ParseSingleExpression()
            x.SkipBlanks()

            // Parsee out a binary expression
            let parseBinary binaryKind =
                _tokenizer.MoveNextToken()
                x.SkipBlanks()

                _parseResultBuilder {
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
            | TokenKind.Character '>' -> return! parseBinary BinaryKind.GreaterThan
            | TokenKind.Character '<' -> return! parseBinary BinaryKind.LessThan
            | TokenKind.ComplexOperator "==" -> return! parseBinary BinaryKind.Equal
            | TokenKind.ComplexOperator "!=" -> return! parseBinary BinaryKind.NotEqual
            | _ -> return expr
        }

    member x.ParseNextCommand() = 
        x.ParseSingleCommand()

    member x.ParseNextLine() = 
        let parseResult = x.ParseSingleLine()
        parseResult

    member x.ParseRange rangeText = 
        x.Reset [|rangeText|]
        x.ParseLineRange(), x.ParseRestOfLine()

    member x.ParseExpression (expressionText: string) =
        x.Reset [|expressionText|]
        x.ParseExpressionCore()

    member x.ParseLineCommand commandText =
        x.Reset [|commandText|]
        x.ParseSingleCommand()

    member x.ParseLineCommands lines =
        x.Reset lines
        let rec inner rest =    
            let lineCommand = x.ParseSingleCommand()
            if not x.IsDone then
                inner (fun item -> rest (lineCommand :: item))
            else
                rest [lineCommand]
        inner (fun all -> all) 
                
    member x.ParseFileNameModifiers : FileNameModifier list =
        let rec inner (modifiers:FileNameModifier list) : FileNameModifier list =
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Character ':' ->
                let mark = _tokenizer.Mark
                _tokenizer.MoveNextChar()
                let c = _tokenizer.CurrentChar
                _tokenizer.MoveNextChar()
                match FileNameModifier.OfChar c with
                | Some m ->
                    match m with 
                    | FileNameModifier.PathFull when modifiers.IsEmpty -> 
                        // Note that 'p' is only valid when it is the first modifier -- violations end the modifier sequence
                        inner (m::modifiers)
                    | FileNameModifier.Tail when not (List.exists (fun m -> m = FileNameModifier.Root || m = FileNameModifier.Extension || m = FileNameModifier.Tail) modifiers) ->
                        // 't' must precede 'r' and 'e' and cannot be repeated -- violations end the modifier sequence
                        inner (m::modifiers)
                    | FileNameModifier.Head when not (List.exists (fun m -> m = FileNameModifier.Root || m = FileNameModifier.Extension || m = FileNameModifier.Tail) modifiers) ->
                        // 'h' should not follow 'e', 't', or 'r'
                        inner (m::modifiers)
                    | FileNameModifier.Root -> inner (m::modifiers)
                    | FileNameModifier.Extension -> inner (m::modifiers)
                    | _ -> 
                        // Stop processing if we encounter an unrecognized modifier character. Unconsume the last character and yield the modifiers so far.
                        _tokenizer.MoveToMark mark
                        modifiers
                | None ->
                    _tokenizer.MoveToMark mark
                    modifiers
            | _ -> modifiers
        
        List.rev (inner List.Empty)
    
    member x.ParseDirectoryPath directoryPath : SymbolicPath =
        _tokenizer.Reset directoryPath TokenizerFlags.None
        let rec inner components =
            if _tokenizer.IsAtEndOfLine then
                components
            else
                match _tokenizer.CurrentTokenKind with
                | TokenKind.Character '\\' ->
                    // As per :help cmdline-special, '\' only acts as an escape character when it immediately preceeds '%' or '#'.
                    _tokenizer.MoveNextChar()
                    match _tokenizer.CurrentTokenKind with
                    | TokenKind.Character '#' 
                    | TokenKind.Character '%' ->
                        let c = _tokenizer.CurrentChar
                        _tokenizer.MoveNextChar()
                        inner (SymbolicPathComponent.Literal (StringUtil.OfChar c)::components)
                    | _ -> 
                        inner (SymbolicPathComponent.Literal "\\"::components)
                | TokenKind.Character '%' ->
                    _tokenizer.MoveNextChar()
                    let modifiers = SymbolicPathComponent.CurrentFileName x.ParseFileNameModifiers
                    inner (modifiers::components)
                | TokenKind.Character '#' ->
                    _tokenizer.MoveNextChar()
                    let n =
                        match _tokenizer.CurrentTokenKind with
                        | TokenKind.Number n ->
                            _tokenizer.MoveNextToken()
                            n
                        | _ -> 1
                    let modifiers = x.ParseFileNameModifiers
                    inner (SymbolicPathComponent.AlternateFileName (n, modifiers)::components)
                | _ ->
                    let literal = _tokenizer.CurrentToken.TokenText
                    _tokenizer.MoveNextToken()
                    let nextComponents = 
                        match components with
                        | SymbolicPathComponent.Literal lhead::tail -> (SymbolicPathComponent.Literal (lhead + literal))::tail
                        | _ -> (SymbolicPathComponent.Literal literal::components)
                    inner nextComponents
        
        List.rev (inner List.Empty)

and ConditionalParser
    (
        _parser: Parser,
        _initialExpr: Expression
    ) = 

    static let StateBeforeElse = 1
    static let StateAfterElse = 2

    let mutable _currentExpr = Some _initialExpr
    let mutable _currentCommands = List<LineCommand>()
    let mutable _builder = List<ConditionalBlock>()
    let mutable _state = StateBeforeElse

    member x.IsDone =
        _parser.IsDone

    member x.Parse() =

        let mutable error: string option = None
        let mutable isDone = false
        while not _parser.IsDone && not isDone && Option.isNone error do
            match _parser.ParseSingleCommand() with
            | LineCommand.Else -> 
                if _state = StateAfterElse then
                    error <- Some Resources.Parser_MultipleElse
                x.CreateConditionalBlock()
                _state <- StateAfterElse
            | LineCommand.ElseIf expr -> 
                if _state = StateAfterElse then
                    error <- Some Resources.Parser_ElseIfAfterElse
                x.CreateConditionalBlock()
                _currentExpr <- Some expr
            | LineCommand.IfEnd ->  
                x.CreateConditionalBlock()
                isDone <- true
            | lineCommand -> _currentCommands.Add(lineCommand)

        match isDone, error with
        | _, Some msg -> LineCommand.ParseError msg
        | false, None -> _parser.ParseError "Unmatched Conditional Block"
        | true, None -> _builder |> List.ofSeq |> LineCommand.If 
                
    member x.CreateConditionalBlock() = 
        let conditionalBlock = {
            Conditional = _currentExpr
            LineCommands = List.ofSeq _currentCommands
        }
        _builder.Add(conditionalBlock)
        _currentExpr <- None
        _currentCommands.Clear()

