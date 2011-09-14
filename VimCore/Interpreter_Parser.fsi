#light

namespace Vim.Interpreter

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of 'T
    | Failed of string

[<Sealed>]
[<Class>]
type Parser = 

    // TODO: Delete.  This is just a transition hack to allow us to use the new interpreter and parser
    // to replace RangeUtil.ParseRange
    static member ParseRange : rangeText : string -> ParseResult<LineRange * string>

    static member ParseExpression : expressionText : string -> ParseResult<Expression>

    static member ParseLineCommand : commandText : string -> ParseResult<LineCommand>

