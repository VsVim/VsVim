#light

namespace Vim.Interpreter
open Vim
open StringBuilderExtensions
open System.Diagnostics

type TokenizerUtil
    (
        _text : string
    ) = 

    /// Current index into the expression text
    let mutable _index = 0

    member x.CurrentChar =
        if _index >= _text.Length then
            None
        else
            Some _text.[_index]

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

    member x.IncrementIndex() =
        if _index < _text.Length then
            _index <- _index + 1

    member x.GetNextTokenKind() =
        match x.CurrentChar with
        | None -> TokenKind.EndOfLine
        | Some c ->
            match c with
            | '"' -> x.GetStringOrComment()
            | _ ->
                if CharUtil.IsDigit c then
                    x.GetNumber c
                elif CharUtil.IsBlank c then
                    x.GetBlanks()
                else
                    x.IncrementIndex()
                    TokenKind.Character c

                // TODO: Need todo strin literal

    member x.GetNextToken() =
        let startIndex = _index
        let tokenKind = x.GetNextTokenKind()
        let length = _index - startIndex
        Token(_text, startIndex, length, tokenKind)

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

    /// Move past a series of blanks
    member x.GetBlanks() =
        let isCurrentBlank () = 
            match x.CurrentChar with
            | None -> false
            | Some c -> CharUtil.IsBlank c

        while isCurrentBlank() do
            x.IncrementIndex()

        TokenKind.Blank

    /// This is called when the index is pointing at a doube-quote and we need to parse
    /// out a comment or a string
    ///
    /// Help expr-string
    /// TODO: Need to support all of the escapes
    member x.GetStringOrComment() =

        // Move past the '"'
        x.IncrementIndex()

        let startIndex = _index
        let rec inner () = 
            if x.IsCurrentChar '\\' then
                // In a string the '\' are escapes so just move past them so we don't 
                // conisder a '"' following it to be the close of the string.
                x.IncrementIndex()
                x.IncrementIndex()
                inner ()
            elif x.IsCurrentChar '"' then
                let str = _text.Substring(startIndex, _index - startIndex)
                x.IncrementIndex()
                TokenKind.String str
            elif _index >= _text.Length then
                // We're at the end of the line.  This was just a comment so go aheand and
                // return that we're at the end of the line
                TokenKind.EndOfLine
            else
                x.IncrementIndex()
                inner()

        inner ()

        (*
    member x.GetStringLiteral() = 
        let startIndex = _index
        let builder = System.Text.StringBuilder()
        let rec inner () = 
            if x.IsCurrentChar '\\' && x.IsPeekChar '\'' then
                builder.AppendChar('\'')
                x.IncrementIndex()
                x.IncrementIndex()
                inner ()
            elif x.IsCurrentChar '\'' then
                let str = builder.ToString()
                x.IncrementIndex()
                TokenKind.String str
            elif _index >= _text.Length then
                TokenKind.Error "Unterminated string literal"
            else
                x.IncrementIndex()
                inner()

        inner ()
        *)

[<Sealed>]
[<Class>]
[<DebuggerDisplay("{ToString(),nq}")>]
type internal Tokenizer
    (
        _text : string
    ) = 

    let _tokens = 
        let tokenizerUtil = TokenizerUtil(_text)
        Seq.unfold (fun isDone ->
            if isDone then
                None
            else
                let token = tokenizerUtil.GetNextToken()
                let isDone = 
                    match token.TokenKind with
                    | TokenKind.EndOfLine -> true
                    | _ -> false
                Some (token, isDone)) false
        |> List.ofSeq

    let mutable _index = 0

    member x.Index = _index

    member x.CurrentToken = 
        if _index >= _tokens.Length then
            Token.EndOfLine
        else
            _tokens.[_index]

    member x.CurrentTokenKind = x.CurrentToken.TokenKind

    member x.IsAtEndOfLine = x.CurrentTokenKind = TokenKind.EndOfLine

    member x.GetNextToken() = 
        x.IncrementIndex()
        x.CurrentToken

    member x.IncrementIndex() =
        if _index < _tokens.Length then
            _index <- _index + 1

    member x.Rewind index =
        _index <- index

    override x.ToString() = x.CurrentToken.ToString()

