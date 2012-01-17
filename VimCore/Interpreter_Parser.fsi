#light

namespace Vim.Interpreter

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of 'T
    | Failed of string

[<Sealed>]
[<Class>]
type Parser = 

    static member ParseRange : rangeText : string -> LineRangeSpecifier * string

    static member ParseExpression : expressionText : string -> ParseResult<Expression>

    static member ParseLineCommand : commandText : string -> ParseResult<LineCommand>

