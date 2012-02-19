#light
namespace Vim.Interpreter
open System.Diagnostics

open Vim

[<RequireQualifiedAccess>]
[<DebuggerDisplay("{ToString(),nq}")>]
type TokenKind = 

    /// A series of blank spaces
    | Blank

    /// A decimal number 
    | Number of int 

    /// A string constant bracketed by '"' 
    | String of string

    /// A single character
    | Character of char

    /// The end of the line
    | EndOfLine

    with

    override x.ToString() =
        match x with 
        | TokenKind.Blank -> "Blank"
        | TokenKind.Number number -> sprintf "Number %d" number
        | TokenKind.String str -> sprintf "String %s" str
        | TokenKind.Character c -> sprintf "Character %c" c
        | TokenKind.EndOfLine -> "EndOfLine"

/// The actual token information 
[<Struct>]
[<DebuggerDisplay("{ToString(),nq}")>]
type Token
    (
        _lineText : string,
        _startIndex : int,
        _length : int,
        _tokenKind : TokenKind
    ) = 

    member x.TokenKind = _tokenKind

    /// The actual original text of the token
    member x.TokenText = 
        if _startIndex + _length > _lineText.Length then
            // EndOfLine token doesn't have valid text
            StringUtil.empty
        else
            _lineText.Substring(_startIndex, _length)

    override x.ToString() = sprintf "%O - %s" x.TokenKind x.TokenText

    static member EndOfLine = Token(StringUtil.empty, 0, 0, TokenKind.EndOfLine)


