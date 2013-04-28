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

    member ParseNextLineCommand : unit -> ParseResult<LineCommand>

    member ParseRange : rangeText : string -> LineRangeSpecifier * string

    member ParseExpression : expressionText : string -> ParseResult<Expression>

    member ParseLineCommand : commandText : string -> ParseResult<LineCommand>

    member ParseLineCommands : lines : string[] -> ParseResult<LineCommand list>


