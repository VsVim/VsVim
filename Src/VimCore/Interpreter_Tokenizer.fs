#light

namespace Vim.Interpreter
open Vim
open StringBuilderExtensions
open System
open System.Diagnostics

type TokenizerFlags =
    | None = 0

    // In almost all cases a double quote is a comment and will cause the token stream to
    // terminate.  There are a few exceptions like string constants but those are all 
    // contextual and driven by the parser
    | AllowDoubleQuote = 0x1

    /// Skip blank tokens 
    | SkipBlanks = 0x2

type internal TokenStream() = 

    let mutable _index = 0
    let mutable _text = String.Empty

    member x.Index 
        with get () = _index
        and set value = _index <- value

    member x.Length = _text.Length

    member x.CurrentChar =
        if _index >= _text.Length then
            None
        else
            Some _text.[_index]

    member x.IncrementIndex () =
        _index <- _index + 1

    member x.PeekChar count = 
        let index = _index + count
        if index < _text.Length then
            _text.[index] |> Some
        else
            None

    member x.IsCurrentChar value =
        match x.CurrentChar with
        | None -> false
        | Some c -> c = value

    member x.IsPeekChar value =
        match x.PeekChar 1 with
        | None -> false
        | Some c -> c = value

    /// Tokenize the next item from the current position in the stream
    member x.GetCurrentTokenKind flags = 
        match x.CurrentChar with
        | None -> TokenKind.EndOfLine
        | Some c ->
            if CharUtil.IsDigit c then
                x.GetNumber c
            elif CharUtil.IsLetter c then
                x.GetWord()
            elif CharUtil.IsBlank c then
                x.GetBlanks()
            elif c = '\"' then
                if Util.IsFlagSet flags TokenizerFlags.AllowDoubleQuote then
                    x.IncrementIndex()
                    TokenKind.Character c
                else
                    _index <- _text.Length
                    TokenKind.EndOfLine
            else
                x.IncrementIndex()
                TokenKind.Character c

    /// Called when the current character is a digit.  Eat up the letters until we 
    /// exhaust all of the consequitive digit tokens
    /// 
    /// Help expr-number
    /// TODO: Floating point and octal
    member x.GetNumber c =

        // Parse out a base 10 number from the stream
        let parseDecimal () =
            let rec inner number = 
                match x.CurrentChar with
                | None -> number
                | Some c -> 
                    if CharUtil.IsDigit c then
                        x.IncrementIndex()
                        let digit = int (int c - int '0')
                        let number = number * 10
                        inner (digit + number)
                    else
                        number
            let number = inner 0
            TokenKind.Number number

        let parseHex () = 
            let rec inner number = 

                let withDigit digit = 
                    x.IncrementIndex()
                    let number = number * 16
                    inner (digit + number)

                match x.CurrentChar with
                | None -> number
                | Some 'a' -> withDigit 10
                | Some 'b' -> withDigit 11
                | Some 'c' -> withDigit 12
                | Some 'd' -> withDigit 13
                | Some 'e' -> withDigit 14
                | Some 'f' -> withDigit 15
                | Some c -> 
                    if CharUtil.IsDigit c then
                        let digit = int (int c - int '0')
                        withDigit digit
                    else
                        number
            let number = inner 0
            TokenKind.Number number

        if c = '0' && (x.IsPeekChar 'x' || x.IsPeekChar 'X') then
            x.IncrementIndex()
            x.IncrementIndex()
            parseHex ()
        else
            parseDecimal ()

    /// Move past the set of coniguous letter characters
    member x.GetWord() = 
        let startIndex = _index
        let isCurrentLetter () = 
            match x.CurrentChar with
            | None -> false
            | Some c -> CharUtil.IsLetter c

        while isCurrentLetter() do
            x.IncrementIndex()

        let word = _text.Substring(startIndex, _index - startIndex)
        TokenKind.Word word

    /// Move past a series of blanks
    member x.GetBlanks() =
        let isCurrentBlank () = 
            match x.CurrentChar with
            | None -> false
            | Some c -> CharUtil.IsBlank c

        while isCurrentBlank() do
            x.IncrementIndex()

        TokenKind.Blank

    member x.Reset text =
        _text <- text
        _index <- 0

[<Sealed>]
[<Class>]
[<DebuggerDisplay("{ToString(),nq}")>]
type internal Tokenizer
    (
        _text : string,
        _tokenizerFlags : TokenizerFlags
    ) as this =

    let _tokenStream = TokenStream()
    let mutable _text = _text
    let mutable _tokenizerFlags = _tokenizerFlags

    /// The current Token the tokenizer is looking at
    let mutable _currentToken = Token(_text, 0, 0, TokenKind.EndOfLine)

    do
        this.Reset _text _tokenizerFlags

    member x.TokenizerFlags 
        with get () = _tokenizerFlags
        and set value = 
            _tokenizerFlags <- value
            x.MoveToIndex _currentToken.StartIndex
            x.MaybeSkipBlank()

    member x.CurrentToken = _currentToken

    member x.CurrentTokenKind = _currentToken.TokenKind

    member x.CurrentChar = 
        if x.IsAtEndOfLine || _currentToken.StartIndex >= _text.Length then
            char 0
        else
            _text.[_currentToken.StartIndex]

    member x.IsAtEndOfLine = x.CurrentTokenKind = TokenKind.EndOfLine

    member x.Mark = _currentToken.StartIndex

    member x.MaybeSkipBlank() =
        if Util.IsFlagSet TokenizerFlags.SkipBlanks _tokenizerFlags && _currentToken.TokenKind = TokenKind.Blank then
            x.MoveNextToken()

    member x.MoveToIndex startIndex = 
        if startIndex >= _tokenStream.Length then

            // Make the current token the end of the line if it's not already so since
            // we're past the end if the line
            if _currentToken.TokenKind <> TokenKind.EndOfLine then
                _currentToken <- Token(
                    _text,
                    _text.Length,
                    0,
                    TokenKind.EndOfLine)
        else
            _tokenStream.Index <- startIndex
            let tokenKind = _tokenStream.GetCurrentTokenKind _tokenizerFlags
            let length = _tokenStream.Index - startIndex
            _currentToken <- Token(
                _text,
                startIndex,
                length,
                tokenKind)

    member x.MoveToMark mark = 
        x.MoveToIndex mark

    member x.MoveNextToken() = 
        let index = _currentToken.StartIndex + _currentToken.Length
        x.MoveToIndex index
        x.MaybeSkipBlank()

    member x.MoveNextChar() = 
        let index = _currentToken.StartIndex + 1
        x.MoveToIndex index

    member x.MoveToEndOfLine() =
        while not x.IsAtEndOfLine do
            x.MoveNextToken()

    member x.SetTokenizerFlagsScoped tokenizerFlags = 
        let reset = new ResetTokenizerFlags(x, _tokenizerFlags) 
        x.TokenizerFlags <- tokenizerFlags
        reset

    member x.Reset text tokenizerFlags = 
        _tokenizerFlags <- tokenizerFlags
        _text <- text
        _currentToken <- Token(_text, 0, 0, TokenKind.EndOfLine)

        _tokenStream.Reset text
        this.MoveToIndex 0
        this.MaybeSkipBlank()

and [<Sealed>] ResetTokenizerFlags
    (
        _tokenizer : Tokenizer,
        _tokenizerFlags : TokenizerFlags
    ) = 

    member x.TokenizerFlags = _tokenizerFlags

    /// Cancel the reset 
    member x.Reset() =
        _tokenizer.TokenizerFlags <- _tokenizerFlags

    interface IDisposable with
        member x.Dispose() = x.Reset()


