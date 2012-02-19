#light

namespace Vim.Interpreter
open Vim
open StringBuilderExtensions
open System.Diagnostics

type NextTokenFlags = 
    | None = 0

    // In almost all cases a double quote is a comment and will cause the token stream to
    // terminate.  There are a few exceptions like string constants but those are all 
    // contextual and driven by the parser
    | AllowDoubleQuote = 0x1

type internal TokenStream
    (
        _text : string
    ) =

    let mutable _index = 0

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
        if _index <= _text.Length then
            _text.[_index] |> Some
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
                if Util.IsFlagSet flags NextTokenFlags.AllowDoubleQuote then
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


[<Sealed>]
[<Class>]
[<DebuggerDisplay("{ToString(),nq}")>]
type internal Tokenizer
    (
        _text : string
    ) as this =

    let _tokenStream = TokenStream(_text)

    /// The current Token the tokenizer is looking at
    let mutable _currentToken = Token(_text, 0, 0, TokenKind.EndOfLine)

    do
        this.MakeCurrentToken 0 NextTokenFlags.None

    member x.CurrentToken = _currentToken

    member x.CurrentTokenKind = _currentToken.TokenKind

    member x.IsAtEndOfLine = x.CurrentTokenKind = TokenKind.EndOfLine

    member x.Index = _currentToken.StartIndex

    member x.MakeCurrentToken startIndex flags = 
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
            let tokenKind = _tokenStream.GetCurrentTokenKind flags
            let length = _tokenStream.Index - startIndex
            _currentToken <- Token(
                _text,
                startIndex,
                length,
                tokenKind)

    member x.MoveNextTokenEx flags = 
        let index = _currentToken.StartIndex + _currentToken.Length
        x.MakeCurrentToken index flags

    member x.MoveNextToken() = x.MoveNextTokenEx NextTokenFlags.None

    member x.Rewind index = x.MakeCurrentToken index NextTokenFlags.None

