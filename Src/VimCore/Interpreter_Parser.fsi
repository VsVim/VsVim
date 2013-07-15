#light

namespace Vim.Interpreter
open Vim

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of 'T
    | Failed of string

[<Sealed>]
[<Class>]
type Parser = 

    new: vimData : IVimData -> Parser

    new: vimData : IVimData * lines : string[] -> Parser

    member IsDone : bool

    /// Parse the next complete command from the source.  Command pairs like :func and :endfunc
    /// will be returned as a single Function command.  
    member ParseNextCommand : unit -> ParseResult<LineCommand>

    /// Parse the next line from the source.  Command pairs like :func and :endfunc will
    /// not be returned as a single command.  Instead they will be returned as the individual
    /// items
    member ParseNextLine : unit -> ParseResult<LineCommand>

    member ParseRange : rangeText : string -> LineRangeSpecifier * string

    member ParseExpression : expressionText : string -> ParseResult<Expression>

    member ParseLineCommand : commandText : string -> ParseResult<LineCommand>

    member ParseLineCommands : lines : string[] -> ParseResult<LineCommand list>


