#light
namespace Vim.Interpreter
open System

type internal TokenizerFlags = 
    | None = 0

    /// In almost all cases a double quote is a comment and will cause the token stream to
    /// terminate.  There are a few exceptions like string constants but those are all 
    /// contextual and driven by the parser
    | AllowDoubleQuote = 0x1

    /// Skip blank tokens 
    | SkipBlanks = 0x2

[<Sealed>]
[<Class>]
type internal ResetTokenizerFlags = 

    /// The flags that will be set on reset
    member TokenizerFlags : TokenizerFlags

    /// Reset now 
    member Reset : unit -> unit

    interface IDisposable

[<Sealed>]
[<Class>]
type internal Tokenizer = 

    new: text : string * tokenizerFlags : TokenizerFlags -> Tokenizer

    /// Current mark into the token stream
    member Mark : int

    /// The flags the tokenizer is parsing under
    member TokenizerFlags : TokenizerFlags with get, set 

    /// Is it at the end of the stream
    member IsAtEndOfLine : bool

    /// Current Token 
    member CurrentToken : Token

    /// Current TokenKind
    member CurrentTokenKind : TokenKind

    /// Current character that the tokenizer is pointing at
    member CurrentChar : char

    /// Move to the next token in the stream.  Double quotes will be treated as a 
    /// comment
    member MoveNextToken : unit -> unit

    /// Move to the next character in the stream and reset the current token.  If the 
    /// current token is a word then this would move past the first character in the
    /// word
    member MoveNextChar : unit -> unit

    /// Rewind the token stream to the specified mark
    member MoveToMark : mark : int -> unit

    /// Move to the end of the current line
    member MoveToEndOfLine : unit -> unit

    /// Change the flags on the tokenizer and return a ResetTokenizerFlags instance
    /// that will reset the flags when done 
    member SetTokenizerFlagsScoped : tokenizerFlags : TokenizerFlags -> ResetTokenizerFlags

    /// Reset the tokenizer with the specified string and flags.  It will begin parsing
    /// this text from the beginning
    member Reset : text : string -> tokenizerFlags : TokenizerFlags -> unit

