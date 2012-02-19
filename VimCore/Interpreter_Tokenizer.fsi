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

    /// Get the next token from the stream and adavance the index
    member GetNextToken : unit -> Token

    /// Increment the token stream to the next token
    member IncrementIndex : unit -> unit

    /// Peek the next token from the stream 
    member PeekNextToken : unit -> Token

    /// Peek the next TokenKind from the stream 
    member PeekNextTokenKind : unit -> TokenKind

    /// Rewind the token stream to the specified index
    member Rewind : index : int -> unit

