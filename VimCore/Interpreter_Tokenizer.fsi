#light

namespace Vim.Interpreter

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

    /// Advance the token stream one and return the next Token
    member GetNextToken : unit -> Token

    /// Increment the token stream to the next token
    member IncrementIndex : unit -> unit

    /// Rewind the token stream to the specified index
    member Rewind : index : int -> unit

