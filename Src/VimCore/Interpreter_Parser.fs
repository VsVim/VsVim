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

    let ConvertToLineCommand (parseResult : ParseResult<LineCommand>) =
        match parseResult with
        | ParseResult.Failed msg -> LineCommand.ParseError msg
        | ParseResult.Succeeded lineCommand -> lineCommand

type ParseResultBuilder
    (
        _errorMessage : string
    ) = 

    new () = ParseResultBuilder(Resources.Parser_Error)

    /// Bind a ParseResult value
    member x.Bind (parseResult : ParseResult<'T>, (rest : 'T -> ParseResult<'U>)) = 
        match parseResult with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> rest value

    /// Bind an option value
    member x.Bind (parseValue : 'T option, (rest : 'T -> ParseResult<'U>)) = 
        match parseValue with
        | None -> ParseResult.Failed _errorMessage
        | Some value -> rest value

    member x.Return (value : 'T) =
        ParseResult.Succeeded value

    member x.Return (parseResult : ParseResult<'T>) =
        match parseResult with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> ParseResult.Succeeded value

    member x.Return (msg : string) = 
        ParseResult.Failed msg

    member x.ReturnFrom value = 
        value

    member x.Zero () = 
        ParseResult.Failed _errorMessage

type LineCommandBuilder
    (
        _errorMessage : string
    ) = 

    new () = LineCommandBuilder(Resources.Parser_Error)

    /// Bind a ParseResult value
    member x.Bind (parseResult : ParseResult<'T>, rest) = 
        match parseResult with
        | ParseResult.Failed msg -> LineCommand.ParseError msg
        | ParseResult.Succeeded value -> rest value

    /// Bind an option value
    member x.Bind (parseValue : 'T option, rest) = 
        match parseValue with
        | None -> LineCommand.ParseError _errorMessage
        | Some value -> rest value

    member x.Return (value : LineCommand) =
        value

    member x.Return (parseResult : ParseResult<LineCommand>) =
        match parseResult with
        | ParseResult.Failed msg -> LineCommand.ParseError msg
        | ParseResult.Succeeded lineCommand -> lineCommand

    member x.Return (msg : string) = 
        LineCommand.ParseError msg

    member x.ReturnFrom value = 
        value

    member x.Zero () = 
        LineCommand.ParseError _errorMessage

[<Sealed>]
type Parser
    (
        _globalSettings : IVimGlobalSettings,
        _vimData : IVimData
    ) = 

    let _parseResultBuilder = ParseResultBuilder()
    let _lineCommandBuilder = LineCommandBuilder()
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
        ("cnext", "cn")
        ("cprevious", "cp")
        ("copy", "co")
        ("delete","d")
        ("delmarks", "delm")
        ("display","di")
        ("edit", "e")
        ("else", "el")
        ("elseif", "elsei")
        ("endfunction", "endf")
        ("endif", "en")
        ("exit", "exi")
        ("fold", "fo")
        ("function", "fu")
        ("global", "g")
        ("help", "h")
        ("history", "his")
        ("if", "if")
        ("join", "j")
        ("lcd", "lc")
        ("lchdir", "lch")
        ("let", "let")
        ("move", "m")
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
        ("tabedit", "tabe")
        ("tabfirst", "tabfir")
        ("tablast", "tabl")
        ("tabnew", "tabnew")
        ("tabnext", "tabn")
        ("tabNext", "tabN")
        ("tabprevious", "tabp")
        ("tabrewind", "tabr")
        ("undo", "u")
        ("unlet", "unl")
        ("vglobal", "v")
        ("version", "ve")
        ("vscmd", "vsc")
        ("vsplit", "vs")
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
    member x.Reset (lines : string[]) = 
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
            None
        else
            x.ParseRestOfLine() |> Some

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
        | None -> StringUtil.empty
        | Some text -> text

    /// Parse out the mapclear variants. 
    member x.ParseMapClear allowBang keyRemapModes =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let mapArgumentList = x.ParseMapArguments()

        if hasBang then
            if allowBang then
                LineCommand.ClearKeyMap ([KeyRemapMode.Insert; KeyRemapMode.Command], mapArgumentList) 
            else
                LineCommand.ParseError Resources.Parser_NoBangAllowed
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
            if StringUtil.isBlanks rightKeyNotation then
                LineCommand.DisplayKeyMap (keyRemapModes, Some leftKeyNotation)
            else
                LineCommand.MapKeys (leftKeyNotation, rightKeyNotation, keyRemapModes, allowRemap, mapArgumentList)

    /// Parse out the :map commands and all of it's variants (imap, cmap, etc ...)
    member x.ParseMapKeys allowBang keyRemapModes =

        if x.ParseBang() then
            if allowBang then
                x.ParseMapKeysCore [KeyRemapMode.Insert; KeyRemapMode.Command] true
            else
                LineCommand.ParseError Resources.Parser_NoBangAllowed
        else
            x.ParseMapKeysCore keyRemapModes true

    /// Parse out the :nomap commands
    member x.ParseMapKeysNoRemap allowBang keyRemapModes =

        if x.ParseBang() then
            if allowBang then
                x.ParseMapKeysCore [KeyRemapMode.Insert; KeyRemapMode.Command] false
            else
                LineCommand.ParseError Resources.Parser_NoBangAllowed
        else
            x.ParseMapKeysCore keyRemapModes false

    /// Parse out the unmap variants. 
    member x.ParseMapUnmap allowBang keyRemapModes =

        let inner modes = 
            x.SkipBlanks()
            let mapArgumentList = x.ParseMapArguments()
            match x.ParseKeyNotation() with
            | None -> LineCommand.ParseError Resources.Parser_InvalidArgument
            | Some keyNotation -> LineCommand.UnmapKeys (keyNotation, modes, mapArgumentList)

        if x.ParseBang() then
            if allowBang then
                inner [KeyRemapMode.Insert; KeyRemapMode.Command]
            else
                LineCommand.ParseError Resources.Parser_NoBangAllowed
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
    member x.ParseFileOptions () : FileOption list =

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

        let mutable parseResult : ParseResult<SubstituteFlags> option = None
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

    /// Parse out :autocommand
    member x.ParseAutoCommand() = 

        let isRemove = x.ParseBang()
        let standardError = "Values missing"
        let onError msg = LineCommand.ParseError msg
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
                let isNotBlankOrComma (token : Token) =
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
            let parseEventKind (word : string) = 
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
            | ParseResult.Failed msg -> LineCommand.ParseError msg
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
            LineCommand.ParseError Resources.Parser_Error
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
        | _ -> LineCommand.ParseError Resources.Parser_Error

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

        x.SkipBlanks()
        let path = 
            if _tokenizer.IsAtEndOfLine then
                None
            else
                x.ParseRestOfLine() |> Some
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
        | LineRangeSpecifier.None -> LineCommand.ParseError Resources.Common_InvalidAddress
        | _ -> LineCommand.CopyTo (sourceLineRange, destinationLineRange, count)

    /// Parse out the :copy command.  It has a single required argument that is the destination
    /// address
    member x.ParseMoveTo sourceLineRange = 
        x.SkipBlanks()
        let destinationLineRange = x.ParseLineRange()
        x.SkipBlanks()
        let count = x.ParseNumber()
        match destinationLineRange with
        | LineRangeSpecifier.None -> LineCommand.ParseError Resources.Common_InvalidAddress
        | _ -> LineCommand.MoveTo (sourceLineRange, destinationLineRange, count)

    /// Parse out the :delete command
    member x.ParseDelete lineRange = 
        x.SkipBlanks()
        let name = x.ParseRegisterName ParseRegisterName.NoNumbered
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
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
            let parseRange (startChar : char) (endChar : char) =
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
                LineCommand.ParseError Resources.Parser_Error
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
        let fileName = x.ParseRestOfLine()

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
            let! args = parseFunctionArguments ()
            let isAbort, isDict, isRange, isError = parseModifiers ()

            let func = { 
                Name = name
                Arguments = args
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
    member x.ParseFunction (functionDefinition : FunctionDefinition option) = 

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
        | _ -> LineCommand.ParseError Resources.Parser_Error

    /// Parse out the :[digit] command
    member x.ParseJumpToLine lineRange =
        match lineRange with
        | LineRangeSpecifier.SingleLine lineSpecifier ->
            match lineSpecifier with
            | LineSpecifier.Number number -> 
                LineCommand.JumpToLine number
            | LineSpecifier.LastLine ->
                LineCommand.JumpToLastLine
            | _ ->
                LineCommand.ParseError Resources.Parser_Error
        | _ ->
            LineCommand.ParseError Resources.Parser_Error

    /// Parse out the :$ command
    member x.ParseJumpToLastLine() =
        ParseResult.Succeeded (LineCommand.JumpToLastLine)

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
                Some LineSpecifier.CurrentLine
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
                Some LineSpecifier.LastLine
            elif _tokenizer.CurrentChar = '/' then

                // It's one of the forward pattern specifiers
                let mark = _tokenizer.Mark
                _tokenizer.MoveNextToken()
                if _tokenizer.CurrentChar = '/' then
                    Some LineSpecifier.NextLineWithPreviousPattern
                elif _tokenizer.CurrentChar = '?' then
                    Some LineSpecifier.PreviousLineWithPreviousPattern
                elif _tokenizer.CurrentChar = '&' then
                    Some LineSpecifier.NextLineWithPreviousSubstitutePattern
                else
                    // Parse out the pattern.  The closing delimiter is required her
                    let pattern, foundDelimeter = x.ParsePattern '/'
                    if foundDelimeter then
                        Some (LineSpecifier.NextLineWithPattern pattern)
                    else
                        _tokenizer.MoveToMark mark
                        None

            elif _tokenizer.CurrentChar = '?' then
                // It's the ? previous search pattern
                let mark = _tokenizer.Mark
                _tokenizer.MoveNextToken()
                let pattern, foundDelimeter = x.ParsePattern '?'
                if foundDelimeter then
                    Some (LineSpecifier.PreviousLineWithPattern pattern)
                else
                    _tokenizer.MoveToMark mark
                    None

            elif _tokenizer.CurrentChar = '+' then
                _tokenizer.MoveNextToken()
                x.ParseNumber() |> Option.map LineSpecifier.AdjustmentOnCurrent
            elif _tokenizer.CurrentChar = '-' then
                _tokenizer.MoveNextToken()
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
    member x.ParseLineRange () : LineRangeSpecifier =
        if _tokenizer.CurrentChar = '%' then
            _tokenizer.MoveNextToken()
            LineRangeSpecifier.EntireBuffer
        else
            match x.ParseLineSpecifier() with
            | None -> LineRangeSpecifier.None
            | Some left ->

                if _tokenizer.CurrentChar = ',' || _tokenizer.CurrentChar = ';' then
                    let isSemicolon = _tokenizer.CurrentChar = ';'
                    _tokenizer.MoveNextToken()
                    match x.ParseLineSpecifier() with
                    | None -> LineRangeSpecifier.SingleLine left
                    | Some right -> LineRangeSpecifier.Range (left, right, isSemicolon)
                else
                    LineRangeSpecifier.SingleLine left 

    /// Parse out the valid ex-flags
    member x.ParseLineCommandFlags() = 
        let rec inner flags = 

            let withFlag flag =
                _tokenizer.MoveNextToken()
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
                _tokenizer.MoveNextToken()
                let pattern, foundDelimeter = x.ParsePattern delimiter
                if not foundDelimeter then
                    // When there is no trailing delimeter then the replace string is empty
                    LineCommand.Substitute (lineRange, pattern, "", SubstituteFlags.None)
                else
                    let replace, _ = x.ParsePattern delimiter
                    x.SkipBlanks()
                    match x.ParseSubstituteFlags() with
                    | ParseResult.Failed message -> LineCommand.ParseError message
                    | ParseResult.Succeeded flags ->
                        let flags = processFlags flags
                        x.SkipBlanks()
                        let count = x.ParseNumber()
                        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, count)
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
        | ParseResult.Failed message -> LineCommand.ParseError message
        | ParseResult.Succeeded flags ->
            let flags = processFlags flags

            // Parses out the optional trailing count
            x.SkipBlanks()
            let count = x.ParseNumber()
            let lineRange = LineRangeSpecifier.WithEndCount (lineRange, count)
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
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        LineCommand.ShiftLeft (lineRange)

    /// Parse out the shift right pattern
    member x.ParseShiftRight lineRange = 
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        LineCommand.ShiftRight (lineRange)

    /// Parse out the shell command
    member x.ParseShellCommand () =
        use resetFlags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        let command = x.ParseRestOfLine()
        LineCommand.ShellCommand command

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
                    | 't' -> builder.AppendChar '\t'
                    | 'b' -> builder.AppendChar '\b'
                    | 'f' -> builder.AppendChar '\f'
                    | 'n' -> builder.AppendChar '\n'
                    | 'r' -> builder.AppendChar '\r'
                    | '\\' -> builder.AppendChar '\\'
                    | _ -> builder.AppendChar c
                    inner false
                elif c = '\\' then
                    inner true
                elif c = '"' then
                    builder.ToString()
                    |> VariableValue.String
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
                result <- builder.ToString() |> VariableValue.String |> ParseResult.Succeeded
                isDone <- true
            | c ->
                builder.AppendChar c
                _tokenizer.MoveNextChar()

        result

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
            | _ -> LineCommand.ParseError "Error"
        getNames (fun x -> x)

    member x.ParseQuickFixNext count =
        let hasBang = x.ParseBang()
        LineCommand.QuickFixNext (count, hasBang)

    member x.ParseQuickFixPrevious count =
        let hasBang = x.ParseBang()
        LineCommand.QuickFixPrevious (count, hasBang)

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
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word word ->
            _tokenizer.MoveNextToken()
            if _tokenizer.CurrentChar = ':' then
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

    /// Parse out a visual studio command.  The format is "commandName argument".  The command
    /// name can use letters, numbers and a period.  The rest of the line after will be taken
    /// as the argument
    member x.ParseVisualStudioCommand() = 
        x.SkipBlanks()
        let command = x.ParseWhile (fun token -> 
            match token.TokenKind with 
            | TokenKind.Word _ -> true
            | TokenKind.Character '.' -> true
            | TokenKind.Character '_' -> true
            | _ -> false)
        match command with 
        | None -> LineCommand.ParseError Resources.Parser_Error
        | Some command ->
            x.SkipBlanks()
            let argument = x.ParseRestOfLine()
            LineCommand.VisualStudioCommand (command, argument)

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

    member x.ParseWriteAll() =
        let hasBang = x.ParseBang()
        LineCommand.WriteAll hasBang

    /// Parse out the yank command
    member x.ParseYank lineRange =
        x.SkipBlanks()
        let registerName = x.ParseRegisterName ParseRegisterName.NoNumbered

        x.SkipBlanks()
        let count = x.ParseNumber()

        LineCommand.Yank (lineRange, registerName, count)

    /// Parse out the fold command
    member x.ParseFold lineRange =
        LineCommand.Fold lineRange

    /// Parse out the :global command
    member x.ParseGlobal lineRange =
        let hasBang = x.ParseBang()
        x.ParseGlobalCore lineRange (not hasBang)

    /// Parse out the :help command
    member x.ParseHelp() =
        _tokenizer.MoveToEndOfLine()
        LineCommand.Help

    /// Parse out the :history command
    member x.ParseHistory() =
        LineCommand.History

    /// Parse out the core global information. 
    member x.ParseGlobalCore lineRange matchPattern =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '\\' -> LineCommand.ParseError Resources.Parser_InvalidArgument
        | TokenKind.Character '"' -> LineCommand.ParseError Resources.Parser_InvalidArgument
        | TokenKind.Character delimiter ->
            _tokenizer.MoveNextToken()
            let pattern, foundDelimiter = x.ParsePattern delimiter
            if foundDelimiter then
                let command = x.ParseSingleLine()
                LineCommand.Global (lineRange, pattern, matchPattern, command)
            else
                LineCommand.ParseError Resources.Parser_InvalidArgument
        | _ -> LineCommand.ParseError Resources.Parser_InvalidArgument

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
        | ParseResult.Failed msg -> LineCommand.ParseError msg
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

    /// Parse out the :let command
    member x.ParseLet () = 
        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.SkipBlanks

        // Handle the case where let is being used for display.  
        //  let x y z 
        let parseDisplayLet firstName = 
            let rec inner cont = 
                if _tokenizer.IsAtEndOfLine then
                    cont []
                else
                    match x.ParseVariableName() with
                    | ParseResult.Succeeded name -> inner (fun rest -> cont (name :: rest))
                    | ParseResult.Failed msg -> LineCommand.ParseError msg

            inner (fun rest ->
                let names = firstName :: rest
                LineCommand.DisplayLet names)

        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word name ->
            match x.ParseVariableName() with
            | ParseResult.Succeeded name ->
                if _tokenizer.CurrentChar = '=' then
                    _tokenizer.MoveNextToken()
                    match x.ParseSingleValue() with
                    | ParseResult.Succeeded value -> LineCommand.Let (name, value)
                    | ParseResult.Failed msg -> LineCommand.ParseError msg
                else
                    parseDisplayLet name
            | ParseResult.Failed msg -> LineCommand.ParseError msg
        | TokenKind.EndOfLine -> LineCommand.DisplayLet []
        | _ -> LineCommand.ParseError "Error"

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

    member x.ParsePrint lineRange =
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.WithEndCount (lineRange, x.ParseNumber())
        x.SkipBlanks()
        _lineCommandBuilder { 
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
            if _tokenizer.CurrentChar = '!' then
                _tokenizer.MoveNextToken()
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
                    let value = x.ParseWhile (fun token -> 
                        match token.TokenKind with
                        | TokenKind.Word _ -> true
                        | TokenKind.Character c ->
                            CharUtil.IsLetterOrDigit c || @"-:\.,<>~[]".Contains(c.ToString())
                        | TokenKind.Number number -> true
                        | _ -> false)
                    match value with 
                    | None -> LineCommand.ParseError Resources.Parser_Error
                    | Some value -> parseNext (argumentFunc (name, value))

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
                    LineCommand.ParseError Resources.Parser_Error

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
                    | _ -> LineCommand.ParseError Resources.Parser_Error
            | _ ->
                 LineCommand.ParseError Resources.Parser_Error                   

        parseOption (fun x -> x)

    /// Parse out the :source command.  It can have an optional '!' following it then a file
    /// name 
    member x.ParseSource() =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileName = x.ParseRestOfLine()
        LineCommand.Source (hasBang, fileName)

    /// Parse out the :split command
    member x.ParseSplit splitType lineRange =
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let commandOption = x.ParseCommandOption()

        splitType (lineRange, fileOptionList, commandOption)

    /// Parse out the :qal and :quitall commands
    member x.ParseQuitAll () =
        let hasBang = x.ParseBang()
        LineCommand.QuitAll hasBang

    /// Parse out the :quit command.
    member x.ParseQuit () = 
        let hasBang = x.ParseBang()
        LineCommand.Quit hasBang

    /// Parse out the :display and :registers command.  Just takes a single argument 
    /// which is the register name
    member x.ParseDisplayRegisters () = 
        let mutable nameList : RegisterName list = List.Empty
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
            let mutable message : string option = None
            let list = System.Collections.Generic.List<Mark>()
            for c in word do
                match Mark.OfChar c with
                | None -> message <- Some (Resources.Parser_NoMarksMatching c)
                | Some mark -> list.Add(mark)

            match message with
            | None -> LineCommand.DisplayMarks (List.ofSeq list)
            | Some message -> LineCommand.ParseError message
        | _ ->
            // Simple case.  No marks to parse out.  Just return them all
            LineCommand.DisplayMarks List.empty

    /// Parse a single line.  This will not attempt to link related LineCommand values like :function
    /// and :endfunc.  Instead it will return the result of the current LineCommand
    member x.ParseSingleLine() =

        // Skip the white space and : at the beginning of the line
        while _tokenizer.CurrentChar = ':' || _tokenizer.CurrentTokenKind = TokenKind.Blank do
            _tokenizer.MoveNextChar()

        let lineRange = x.ParseLineRange()

        let noRange parseFunc = 
            match lineRange with
            | LineRangeSpecifier.None -> parseFunc()
            | _ -> LineCommand.ParseError Resources.Parser_NoRangeAllowed

        let handleParseResult (lineCommand : LineCommand) =
            let lineCommand = 
                if lineCommand.Failed then
                    // If there is already a failure don't look any deeper.
                    lineCommand 
                else
                    x.SkipBlanks()

                    // If there are still characters then it's illegal trailing characters
                    if not _tokenizer.IsAtEndOfLine then
                        LineCommand.ParseError Resources.CommandMode_TrailingCharacters
                    else
                        lineCommand
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
                | "call" -> x.ParseCall lineRange
                | "cd" -> noRange x.ParseChangeDirectory
                | "chdir" -> noRange x.ParseChangeDirectory
                | "close" -> noRange x.ParseClose
                | "cmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Command])
                | "cmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Command])
                | "cnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Command])
                | "cnext" -> handleCount x.ParseQuickFixNext
                | "cprevious" -> handleCount x.ParseQuickFixPrevious
                | "copy" -> x.ParseCopyTo lineRange 
                | "cunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Command])
                | "delete" -> x.ParseDelete lineRange
                | "delmarks" -> noRange (fun () -> x.ParseDeleteMarks())
                | "display" -> noRange x.ParseDisplayRegisters 
                | "edit" -> noRange x.ParseEdit
                | "else" -> noRange x.ParseElse
                | "elseif" -> noRange x.ParseElseIf
                | "endfunction" -> noRange x.ParseFunctionEnd
                | "endif" -> noRange x.ParseIfEnd
                | "exit" -> x.ParseQuitAndWrite lineRange
                | "fold" -> x.ParseFold lineRange
                | "function" -> noRange x.ParseFunctionStart
                | "global" -> x.ParseGlobal lineRange
                | "help" -> noRange x.ParseHelp
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
                | "lmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Language])
                | "lunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Language])
                | "lnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Language])
                | "make" -> noRange x.ParseMake 
                | "marks" -> noRange x.ParseDisplayMarks
                | "map"-> noRange (fun () -> x.ParseMapKeys true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
                | "mapclear" -> noRange (fun () -> x.ParseMapClear true [KeyRemapMode.Normal; KeyRemapMode.Visual; KeyRemapMode.Command; KeyRemapMode.OperatorPending])
                | "move" -> x.ParseMoveTo lineRange 
                | "nmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Normal])
                | "nmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Normal])
                | "nnoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.Normal])
                | "nunmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.Normal])
                | "nohlsearch" -> noRange (fun () -> LineCommand.NoHighlightSearch)
                | "noremap"-> noRange (fun () -> x.ParseMapKeysNoRemap true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
                | "omap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.OperatorPending])
                | "omapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.OperatorPending])
                | "onoremap"-> noRange (fun () -> x.ParseMapKeysNoRemap false [KeyRemapMode.OperatorPending])
                | "ounmap" -> noRange (fun () -> x.ParseMapUnmap false [KeyRemapMode.OperatorPending])
                | "put" -> x.ParsePut lineRange
                | "print" -> x.ParsePrint lineRange
                | "pwd" -> noRange (fun () -> LineCommand.PrintCurrentDirectory)
                | "quit" -> noRange x.ParseQuit
                | "qall" -> noRange x.ParseQuitAll
                | "quitall" -> noRange x.ParseQuitAll
                | "read" -> x.ParseRead lineRange
                | "redo" -> noRange (fun () -> LineCommand.Redo)
                | "retab" -> x.ParseRetab lineRange
                | "registers" -> noRange x.ParseDisplayRegisters 
                | "set" -> noRange x.ParseSet
                | "source" -> noRange x.ParseSource
                | "split" -> x.ParseSplit LineCommand.HorizontalSplit lineRange
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
                | "tabprevious" -> noRange x.ParseTabPrevious
                | "undo" -> noRange (fun () -> LineCommand.Undo)
                | "unlet" -> noRange x.ParseUnlet
                | "unmap" -> noRange (fun () -> x.ParseMapUnmap true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
                | "version" -> noRange (fun () -> LineCommand.Version)
                | "vglobal" -> x.ParseGlobalCore lineRange false
                | "vmap"-> noRange (fun () -> x.ParseMapKeys false [KeyRemapMode.Visual;KeyRemapMode.Select])
                | "vmapclear" -> noRange (fun () -> x.ParseMapClear false [KeyRemapMode.Visual; KeyRemapMode.Select])
                | "vscmd" -> x.ParseVisualStudioCommand()
                | "vsplit" -> x.ParseSplit LineCommand.VerticalSplit lineRange
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
                | _ -> LineCommand.ParseError Resources.Parser_Error

            handleParseResult parseResult

        // Get the command name and make sure to expand it to it's possible full
        // name
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word word ->
            _tokenizer.MoveNextToken()
            x.TryExpand word |> doParse
        | TokenKind.Character c ->
            _tokenizer.MoveNextToken()
            c |> StringUtil.ofChar |> x.TryExpand |> doParse
        | TokenKind.EndOfLine ->
            match lineRange with
            | LineRangeSpecifier.None -> handleParseResult LineCommand.Nop
            | _ -> x.ParseJumpToLine lineRange |> handleParseResult
        | _ -> 
            x.ParseJumpToLine lineRange |> handleParseResult

    /// Parse out a single command.  Unlike ParseSingleLine this will parse linked commands.  So
    /// it won't ever return LineCommand.FuntionStart but instead will return LineCommand.Function
    /// instead
    member x.ParseSingleCommand() =
        match x.ParseSingleLine() with 
        | LineCommand.FunctionStart functionDefinition -> x.ParseFunction functionDefinition 
        | LineCommand.IfStart expr -> x.ParseIf expr
        | lineCommand -> lineCommand

    /// Parse out a single expression
    member x.ParseSingleExpression() =
        match x.ParseSingleValue() with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded value -> Expression.ConstantValue value |> ParseResult.Succeeded

    /// Parse out a single expression
    member x.ParseSingleValue() =
        // Re-examine the current token based on the knowledge that double quotes are
        // legal in this context as a real token
        use reset = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '\"' ->
            x.ParseStringConstant()
        | TokenKind.Character '\'' -> 
            x.ParseStringLiteral()
        | TokenKind.Number number -> 
            _tokenizer.MoveNextToken()
            VariableValue.Number number |> ParseResult.Succeeded
        | _ -> ParseResult.Failed "Invalid expression"

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

    member x.ParseExpression (expressionText : string) =
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
                
and ConditionalParser
    (
        _parser : Parser,
        _initialExpr : Expression
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

        let mutable error : string option = None
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
        | false, None -> LineCommand.ParseError "Unmatched Conditional Block"
        | true, None -> _builder |> List.ofSeq |> LineCommand.If 
                
    member x.CreateConditionalBlock() = 
        let conditionalBlock = {
            Conditional = _currentExpr
            LineCommands = List.ofSeq _currentCommands
        }
        _builder.Add(conditionalBlock)
        _currentExpr <- None
        _currentCommands.Clear()

