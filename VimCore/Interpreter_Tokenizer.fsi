#light

namespace Vim.Interpreter

type NextTokenFlags = 
    | None = 0

    // In almost all cases a double quote is a comment and will cause the token stream to
    // terminate.  There are a few exceptions like string constants but those are all 
    // contextual and driven by the parser
    | AllowDoubleQuote = 0x1

[<Sealed>]
[<Class>]
type internal Tokenizer = 

    new: text : string -> Tokenizer

    /// Current index into the token stream
    member Index : int

    /// Is it at the end of the stream
    member IsAtEndOfLine : bool

    /// Current Token 
    member CurrentToken : Token

    /// Current TokenKind
    member CurrentTokenKind : TokenKind

    /// Move to the next token in the stream.  Double quotes will be treated as a 
    /// comment
    member MoveNextToken : unit -> unit

    /// Move to the next token in the stream.
    member MoveNextTokenEx : flags : NextTokenFlags -> unit

    /// Rewind the token stream to the specified index
    member Rewind : index : int -> unit

