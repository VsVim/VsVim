﻿#light

namespace Vim.Interpreter
open Vim
open System.Collections.Generic
open StringBuilderExtensions

[<RequireQualifiedAccess>]
type NextConditionalKind =
    | Else
    | ElseIf of Expression
    | EndIf

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

    member x.Return (value : 'T) =
        ParseResult.Succeeded value

    member x.Return (parseResult : ParseResult<'T>) =
        parseResult

    member x.Return (msg : string) = 
        ParseResult.Failed msg

    member x.ReturnFrom value = 
        value

    member x.Zero () = 
        ParseResult.Failed _errorMessage

[<Sealed>]
type Parser
    (
        _vimData : IVimData
    ) = 

    let _parserBuilder = ParserBuilder()
    let _tokenizer = Tokenizer("", TokenizerFlags.None)
    let mutable _lines = [|""|] 
    let mutable _lineIndex = 0

    /// The set of supported line commands paired with their abbreviation
    static let s_LineCommandNamePair = [
        ("autocmd", "au")
        ("behave", "be")
        ("cd", "cd")
        ("chdir", "chd")
        ("close", "clo")
        ("cnext", "cn")
        ("cprevious", "cp")
        ("copy", "co")
        ("delete","d")
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
        ("tabfirst", "tabfir")
        ("tablast", "tabl")
        ("tabnext", "tabn")
        ("tabNext", "tabN")
        ("tabprevious", "tabp")
        ("tabrewind", "tabr")
        ("undo", "u")
        ("unlet", "unl")
        ("vglobal", "v")
        ("version", "ve")
        ("vscmd", "vsc")
        ("vsplit", "vsp")
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

    new (vimData, lines) as this =
        Parser(vimData)
        then
            this.Reset lines

    member x.IsDone = _tokenizer.IsAtEndOfLine && _lineIndex  + 1 >= _lines.Length

    member x.IsTokenSequence texts = 
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

    /// Move to the next line of the input.  This will move past blank lines and return true if 
    /// the result is a non-blank line which can be processed
    member x.MoveToNextLine() = 

        let doMove () =
            if _lineIndex + 1 >= _lines.Length then
                // If this is the last line we should at least move the tokenizer to the end
                // of the line
                x.MoveToEndOfLine()
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

    /// Move to the end of the current line
    member x.MoveToEndOfLine() = 
        while not _tokenizer.IsAtEndOfLine do
            _tokenizer.MoveNextToken()

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
                LineCommand.ClearKeyMap ([KeyRemapMode.Insert; KeyRemapMode.Command], mapArgumentList) |> ParseResult.Succeeded
            else
                ParseResult.Failed Resources.Parser_NoBangAllowed
        else
            LineCommand.ClearKeyMap (keyRemapModes, mapArgumentList) |> ParseResult.Succeeded

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
        | None -> 
            LineCommand.DisplayKeyMap (keyRemapModes, None) |> ParseResult.Succeeded
        | Some leftKeyNotation -> 
            x.SkipBlanks()

            let rightKeyNotation = x.ParseWhileEx TokenizerFlags.AllowDoubleQuote (fun _ -> true)
            let rightKeyNotation = OptionUtil.getOrDefault "" rightKeyNotation
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
                match x.ParseSingleCommandCore() with
                | ParseResult.Failed _ -> 
                    _tokenizer.MoveToMark mark
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
            let mark = _tokenizer.Mark

            // Finish without changinging anything.
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
    member x.ParseRegisterName () = 
        let c = _tokenizer.CurrentChar 
        if CharUtil.IsDigit c then
            None
        else
            let name = RegisterName.OfChar c
            if Option.isSome name then
                _tokenizer.MoveNextChar()
            name

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

        let rec inner flags index  = 
            if index >= flagString.Length then
                ParseResult.Succeeded flags
            else
                let newFlag = 
                    match flagString.[index] with
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
                match newFlag, index = 0 with
                | None, _ -> 
                    // Illegal character was used 
                    ParseResult.Failed Resources.CommandMode_TrailingCharacters
                | Some SubstituteFlags.UsePreviousFlags, false ->
                    // The '&' flag is only legal in the first position.  After that
                    // it terminates the flag notation
                    ParseResult.Failed Resources.CommandMode_TrailingCharacters
                | Some newFlag, _ -> 
                    inner (flags ||| newFlag) (index + 1)

        inner SubstituteFlags.None 0

    /// Parse out :autocommand
    member x.ParseAutoCommand() = 

        let isRemove = x.ParseBang()
        let standardError = "Values missing"
        let onError msg = ParseResult.Failed msg
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
                ParseResult.Succeeded (LineCommand.RemoveAutoCommands autoCommandDefinition)

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
            | ParseResult.Failed msg -> ParseResult.Failed msg
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

                let lineCommand = LineCommand.AddAutoCommand autoCommandDefinition
                ParseResult.Succeeded lineCommand

    /// Parse out the :behave command.  The mode argument is required
    member x.ParseBehave() =
        x.SkipBlanks()
        if _tokenizer.IsAtEndOfLine then
            ParseResult.Failed Resources.Parser_Error
        else
            let mode = _tokenizer.CurrentToken.TokenText
            _tokenizer.MoveNextToken()
            ParseResult.Succeeded (LineCommand.Behave mode)

    /// Parse out the change directory command.  The path here is optional
    member x.ParseChangeDirectory() =
        // Bang is allowed but has no effect
        x.ParseBang() |> ignore
        x.SkipBlanks()
        let path = 
            if _tokenizer.IsAtEndOfLine then
                None
            else
                x.ParseRestOfLine() |> Some
        ParseResult.Succeeded (LineCommand.ChangeDirectory path)

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
        x.SkipBlanks()
        let count = x.ParseNumber()
        match destinationLineRange with
        | LineRangeSpecifier.None -> ParseResult.Failed Resources.Common_InvalidAddress
        | _ -> LineCommand.CopyTo (sourceLineRange, destinationLineRange, count) |> ParseResult.Succeeded

    /// Parse out the :copy command.  It has a single required argument that is the destination
    /// address
    member x.ParseMoveTo sourceLineRange = 
        x.SkipBlanks()
        let destinationLineRange = x.ParseLineRange()
        x.SkipBlanks()
        let count = x.ParseNumber()
        match destinationLineRange with
        | LineRangeSpecifier.None -> ParseResult.Failed Resources.Common_InvalidAddress
        | _ -> LineCommand.MoveTo (sourceLineRange, destinationLineRange, count) |> ParseResult.Succeeded

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
        let fileName = x.ParseRestOfLine()

        LineCommand.Edit (hasBang, fileOptionList, commandOption, fileName) |> ParseResult.Succeeded

    /// Parse out the :function command
    member x.ParseFunction() = 

        // Parse out the name of the function.  It must start with a capitol letter or
        // be preceded by the s: prefix 
        let parseFunctionName () = 

            // Lower case names are allowed when the name is prefixed with <SID> or s: 
            let allowLowerCase = 
                x.IsTokenSequence [| "<"; "SID"; ">" |] ||
                x.IsTokenSequence [| "s"; ":" |]
                
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Word word ->
                if not allowLowerCase && CharUtil.IsLower word.[0] then
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

        // Is the current line the "endfunction" line
        let isCurrentLineEndFunction () = 
            let name = x.TryExpand _tokenizer.CurrentToken.TokenText
            name = "endfunction"

        // Parse out the lines in the function.  If any of the lines inside the function register as a 
        // parse error we still need to continue parsing the function (even though it should ultimately
        // fail).  If we bail out of parsing early then it will cause the "next" command to be a 
        // line which is a part of the function.  
        let parseLines () = 

            let lines = List<LineCommand>()
            let mutable anyFailed = false
            let mutable foundEndFunction = false
            while not x.IsDone && not foundEndFunction do
                if isCurrentLineEndFunction () then
                    foundEndFunction <- true
                    x.MoveToEndOfLine()
                else
                    match x.ParseSingleCommandCore() with
                    | ParseResult.Failed _ -> anyFailed <- true
                    | ParseResult.Succeeded lineCommand -> lines.Add lineCommand

                    x.MoveToNextLine() |> ignore

            if anyFailed || not foundEndFunction then
                ParseResult.Failed Resources.Parser_Error
            else
                let allLines = List.ofSeq lines
                ParseResult.Succeeded allLines

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
                    x.MoveToEndOfLine()
                    (isAbort, isDict, isRange, true)

            inner false false false

        let hasBang = x.ParseBang()

        // Start ignoring blanks after parsing out the '!'.  The '!' must appear directly next to the 
        // function name or it is not a valid construct
        use flags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.SkipBlanks

        _parserBuilder { 
            let! name = parseFunctionName ()
            let! args = parseFunctionArguments ()
            let isAbort, isDict, isRange, isError = parseModifiers ()

            if x.MoveToNextLine() then
                let! lines = parseLines ()

                if isError then
                    return Resources.Parser_Error
                else
                    let func = { 
                        Name = name
                        Arguments = args
                        IsRange = isRange
                        IsAbort = isAbort
                        IsDictionary = isDict
                        IsForced = hasBang
                        LineCommands = lines
                    }
                    return LineCommand.DefineFunction func
            else
                return Resources.Parser_Error
        }

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
                _tokenizer.MoveNextToken()

                match Mark.OfChar _tokenizer.CurrentChar with
                | None -> None
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
                    let command = LineCommand.Substitute (lineRange, pattern, "", SubstituteFlags.None)
                    ParseResult.Succeeded command
                else
                    let replace, _ = x.ParsePattern delimiter
                    x.SkipBlanks()
                    match x.ParseSubstituteFlags() with
                    | ParseResult.Failed message -> ParseResult.Failed message
                    | ParseResult.Succeeded flags ->
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
        match x.ParseSubstituteFlags() with
        | ParseResult.Failed message -> ParseResult.Failed message
        | ParseResult.Succeeded flags ->
            let flags = processFlags flags

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
        let pattern = x.ParseRestOfLine()
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
        use resetFlags = _tokenizer.SetTokenizerFlagsScoped TokenizerFlags.AllowDoubleQuote
        let command = x.ParseRestOfLine()
        LineCommand.ShellCommand command |> ParseResult.Succeeded

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
                let unlet = LineCommand.Unlet (hasBang, list)
                ParseResult.Succeeded unlet
            | _ -> ParseResult.Failed "Error"
        getNames (fun x -> x)

    member x.ParseQuickFixNext count =
        let hasBang = x.ParseBang()
        ParseResult.Succeeded (LineCommand.QuickFixNext (count, hasBang))

    member x.ParseQuickFixPrevious count =
        let hasBang = x.ParseBang()
        ParseResult.Succeeded (LineCommand.QuickFixPrevious (count, hasBang))

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

        LineCommand.QuitWithWrite (lineRange, hasBang, fileOptionList, fileName) |> ParseResult.Succeeded

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
            if _tokenizer.IsAtEndOfLine then
                None
            else
                x.ParseRestOfLine() |> Some

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

    /// Parse out the :history command
    member x.ParseHistory() =
        ParseResult.Succeeded LineCommand.History

    /// Parse out the core global information. 
    member x.ParseGlobalCore lineRange matchPattern =
        match _tokenizer.CurrentTokenKind with
        | TokenKind.Character '\\' -> ParseResult.Failed Resources.Parser_InvalidArgument
        | TokenKind.Character '"' -> ParseResult.Failed Resources.Parser_InvalidArgument
        | TokenKind.Character delimiter ->
            _tokenizer.MoveNextToken()
            let pattern, foundDelimiter = x.ParsePattern delimiter
            if foundDelimiter then
                let command = x.ParseSingleCommandCore()
                match command with 
                | ParseResult.Failed msg -> ParseResult.Failed msg
                | ParseResult.Succeeded command -> LineCommand.Global (lineRange, pattern, matchPattern, command) |> ParseResult.Succeeded
            else
                ParseResult.Failed Resources.Parser_InvalidArgument
        | _ -> ParseResult.Failed Resources.Parser_InvalidArgument

    /// Parse out the :if command from the buffer
    member x.ParseIf() = 

        let standardError = "Unmatched Conditional Block"
        let onError msg = ParseResult.Failed msg

        // Parse out the conditional kind of the current line
        let parseConditionalKind onSuccess = 
            match _tokenizer.CurrentTokenKind with
            | TokenKind.Word name ->
                let name = x.TryExpand name
                match name with
                | "else" -> onSuccess (Some NextConditionalKind.Else)
                | "endif" -> onSuccess (Some NextConditionalKind.EndIf)
                | "elseif" -> 
                    _tokenizer.MoveNextToken()
                    x.SkipBlanks()
                    match x.ParseExpressionCore() with
                    | ParseResult.Failed msg -> onError msg 
                    | ParseResult.Succeeded expr -> onSuccess (Some (NextConditionalKind.ElseIf expr))
                | _ -> onSuccess None
            | _ -> onSuccess None

        // Parse out a conditional block from the 
        let parseBlockCommands onSuccess = 
            let builder = System.Collections.Generic.List<LineCommand>()
            let onSuccess nextConditionalKind = 
                let list = List.ofSeq builder
                onSuccess list nextConditionalKind

            let rec inner () = 
                parseConditionalKind (fun nextConditionalKind -> 
                    match nextConditionalKind with
                    | Some nextConditionalKind -> onSuccess nextConditionalKind
                    | None -> 
                        match x.ParseSingleCommandCore() with
                        | ParseResult.Failed msg -> onError msg
                        | ParseResult.Succeeded lineCommand ->
                            builder.Add lineCommand
                            if x.MoveToNextLine() then
                                inner ()
                            else
                                onError standardError)
        
            inner ()

        let rec parseNextConditional nextConditionalKind onSuccess = 

            let onEndIf () = 
                x.MoveToEndOfLine()
                x.MoveToNextLine() |> ignore

            match nextConditionalKind with
            | NextConditionalKind.EndIf ->
                onEndIf ()
                onSuccess ConditionalBlock.Empty
            | NextConditionalKind.ElseIf expr ->
                if x.MoveToNextLine() then
                    parseBlockCommands (fun lineCommands nextConditionalKind -> 
                        parseNextConditional nextConditionalKind (fun conditionalBlock ->
                            onSuccess (ConditionalBlock.Conditional (expr, lineCommands, conditionalBlock))))
                else
                    onError standardError
            | NextConditionalKind.Else ->
                if x.MoveToNextLine() then
                    parseBlockCommands (fun lineCommands nextConditionalKind ->
                        match nextConditionalKind with
                        | NextConditionalKind.Else -> onError standardError
                        | NextConditionalKind.ElseIf _ -> onError standardError
                        | NextConditionalKind.EndIf -> 
                            onEndIf ()
                            onSuccess (ConditionalBlock.Unconditional lineCommands))
                else
                    onError standardError

        x.SkipBlanks()
        match x.ParseExpressionCore() with
        | ParseResult.Failed msg -> ParseResult.Failed msg
        | ParseResult.Succeeded expr -> 
            if x.MoveToNextLine() then
                parseBlockCommands (fun lineCommands nextConditionalKind -> 
                    parseNextConditional nextConditionalKind (fun conditionalBlock ->
                        let block = ConditionalBlock.Conditional (expr, lineCommands, conditionalBlock)
                        let lineCommand = LineCommand.If block
                        ParseResult.Succeeded lineCommand))
            else
                onError standardError
        
    /// Parse out the join command
    member x.ParseJoin lineRange =  
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let lineRange = LineRangeSpecifier.Join (lineRange, x.ParseNumber())
        let joinKind = if hasBang then JoinKind.KeepEmptySpaces else JoinKind.RemoveEmptySpaces
        LineCommand.Join (lineRange, joinKind) |> ParseResult.Succeeded

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
                    | ParseResult.Failed msg -> ParseResult.Failed msg

            inner (fun rest ->
                let names = firstName :: rest
                ParseResult.Succeeded (LineCommand.DisplayLet names))

        match _tokenizer.CurrentTokenKind with
        | TokenKind.Word name ->
            match x.ParseVariableName() with
            | ParseResult.Succeeded name ->
                if _tokenizer.CurrentChar = '=' then
                    _tokenizer.MoveNextToken()
                    match x.ParseSingleValue() with
                    | ParseResult.Succeeded value -> LineCommand.Let (name, value) |> ParseResult.Succeeded
                    | ParseResult.Failed msg -> ParseResult.Failed msg
                else
                    parseDisplayLet name
            | ParseResult.Failed msg -> ParseResult.Failed msg
        | TokenKind.EndOfLine -> ParseResult.Succeeded (LineCommand.DisplayLet [])
        | _ -> ParseResult.Failed "Error"

    /// Parse out the :make command.  The arguments here other than ! are undefined.  Just
    /// get the text blob and let the interpreter / host deal with it 
    member x.ParseMake () = 
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let arguments = x.ParseRestOfLine()
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
            if _tokenizer.CurrentChar = '!' then
                _tokenizer.MoveNextToken()
                let command = x.ParseRestOfLine()
                LineCommand.ReadCommand (lineRange, command) |> ParseResult.Succeeded
            else
                let filePath = x.ParseRestOfLine()
                LineCommand.ReadFile (lineRange, [], filePath) |> ParseResult.Succeeded
        | _ ->
            // Can only be the file variety.
            x.SkipBlanks()
            let filePath = x.ParseRestOfLine()
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
            let parseNext argument = parseOption (fun list -> argument :: list |> withArgument)

            // Parse out an operator.  Parse out the value and use the specified setting name
            // and argument function as the argument
            let parseSetValue name argumentFunc = 

                let empty () = 
                    _tokenizer.MoveNextToken()
                    parseNext (SetArgument.AssignSetting (name, ""))

                if _tokenizer.IsAtEndOfLine || _tokenizer.CurrentChar = ' ' then
                    empty ()
                else
                    let c = _tokenizer.CurrentChar
                    if CharUtil.IsLetterOrDigit c then
                        let value = _tokenizer.CurrentToken.TokenText
                        _tokenizer.MoveNextToken()
                        parseNext (argumentFunc (name, value))
                    else
                        ParseResult.Failed Resources.Parser_Error

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
                    ParseResult.Failed Resources.Parser_Error

            match _tokenizer.CurrentTokenKind with
            | TokenKind.EndOfLine ->
                let list = withArgument []
                ParseResult.Succeeded (LineCommand.Set list)
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
                    | _ -> ParseResult.Failed Resources.Parser_Error
            | _ ->
                 ParseResult.Failed Resources.Parser_Error                   

        parseOption (fun x -> x)

    /// Parse out the :source command.  It can have an optional '!' following it then a file
    /// name 
    member x.ParseSource() =
        let hasBang = x.ParseBang()
        x.SkipBlanks()
        let fileName = x.ParseRestOfLine()
        ParseResult.Succeeded (LineCommand.Source (hasBang, fileName))

    /// Parse out the :split command
    member x.ParseSplit splitType lineRange =
        x.SkipBlanks()
        let fileOptionList = x.ParseFileOptions()

        x.SkipBlanks()
        let commandOption = x.ParseCommandOption()

        ParseResult.Succeeded (splitType (lineRange, fileOptionList, commandOption))

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
            | None -> LineCommand.DisplayMarks (List.ofSeq list) |> ParseResult.Succeeded
            | Some message -> ParseResult.Failed message
        | _ ->
            // Simple case.  No marks to parse out.  Just return them all
            LineCommand.DisplayMarks List.empty |> ParseResult.Succeeded

    /// Parse out a single expression
    member x.ParseSingleCommandCore() = 

        x.SkipBlanks()
        let lineRange = x.ParseLineRange()

        let noRange parseFunc = 
            match lineRange with
            | LineRangeSpecifier.None -> parseFunc()
            | _ -> ParseResult.Failed Resources.Parser_NoRangeAllowed

        let handleParseResult parseResult =
            match parseResult with
            | ParseResult.Failed _ ->
                // If there is already a failure don't look any deeper.
                parseResult
            | ParseResult.Succeeded _ ->
                x.SkipBlanks()

                // If there are still characters then it's illegal trailing characters
                if not _tokenizer.IsAtEndOfLine then
                    ParseResult.Failed Resources.CommandMode_TrailingCharacters
                else
                    parseResult

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
                | "display" -> noRange x.ParseDisplayRegisters 
                | "edit" -> noRange x.ParseEdit
                | "exit" -> x.ParseQuitAndWrite lineRange
                | "fold" -> x.ParseFold lineRange
                | "function" -> noRange (fun () -> x.ParseFunction())
                | "global" -> x.ParseGlobal lineRange
                | "history" -> noRange (fun () -> x.ParseHistory())
                | "if" -> noRange x.ParseIf
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
                | "split" -> x.ParseSplit LineCommand.HorizontalSplit lineRange
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
                | "unlet" -> noRange x.ParseUnlet
                | "unmap" -> noRange (fun () -> x.ParseMapUnmap true [KeyRemapMode.Normal;KeyRemapMode.Visual; KeyRemapMode.Select;KeyRemapMode.OperatorPending])
                | "version" -> noRange (fun () -> ParseResult.Succeeded LineCommand.Version)
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
                | _ -> ParseResult.Failed Resources.Parser_Error

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
            | LineRangeSpecifier.None -> ParseResult.Succeeded LineCommand.Nop
            | _ -> x.ParseJumpToLine lineRange |> handleParseResult
        | _ -> 
            x.ParseJumpToLine lineRange |> handleParseResult

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
        _parserBuilder {
            let! expr = x.ParseSingleExpression()
            x.SkipBlanks()

            // Parsee out a binary expression
            let parseBinary binaryKind =
                _tokenizer.MoveNextToken()
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

    member x.ParseNextLineCommand() = 
        let parseResult = x.ParseSingleCommandCore()
        x.MoveToNextLine() |> ignore
        parseResult

    member x.ParseRange rangeText = 
        x.Reset [|rangeText|]
        x.ParseLineRange(), x.ParseRestOfLine()

    member x.ParseExpression (expressionText : string) : ParseResult<Expression> = 
        x.Reset [|expressionText|]
        x.ParseExpressionCore()

    member x.ParseLineCommand commandText =
        x.Reset [|commandText|]
        x.ParseSingleCommandCore()

    member x.ParseLineCommands lines =
        x.Reset lines
        let rec inner rest =    
            match x.ParseSingleCommandCore() with
            | ParseResult.Failed msg -> ParseResult.Failed msg
            | ParseResult.Succeeded lineCommand -> 
                if x.MoveToNextLine() then    
                    inner (fun item -> rest (lineCommand :: item))
                else
                    rest [lineCommand]
        inner (fun all -> ParseResult.Succeeded all)
                

