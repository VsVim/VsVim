namespace Vim.Interpreter

[<RequireQualifiedAccess>]
type ValueType =
    | Number
    | Float
    | String
    | FunctionRef
    | List
    | Dictionary
    | Error

[<RequireQualifiedAccess>]
type Value =
    | Number of int
    | Float of float
    | String of string
    | FunctionRef of string
    | List of Value list
    | Dictionary of Map<string, Value>
    | Error

    with

    member x.ValueType = 
        match x with
        | Number _ -> ValueType.Number
        | Float _ -> ValueType.Float
        | String _ -> ValueType.String
        | FunctionRef _ -> ValueType.FunctionRef
        | List _ -> ValueType.List
        | Dictionary _ -> ValueType.Dictionary
        | Error -> ValueType.Error