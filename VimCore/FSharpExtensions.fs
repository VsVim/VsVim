namespace Vim.Extensions

open System.Runtime.CompilerServices

[<Extension>]
module public OptionExtensions =

    [<Extension>]
    let IsSome opt = Option.isSome opt

    [<Extension>]
    let IsNone opt = Option.isNone opt

    [<Extension>]
    let Is (opt:'a option, value) =
        match opt with 
        | Some(toTest) -> toTest = value
        | None         -> false

    [<Extension>]
    let SomeOrDefault opt defaultValue = 
        match opt with 
        | Some(value) -> value
        | None -> defaultValue

module public FSharpOption =

    let Create value = value |> Some

    let CreateForReference value = 
        match box value with
        | null -> None
        | _ -> Some value

    let CreateForNullable (value : System.Nullable<'T>) =
        if value.HasValue then Some value.Value
        else None

[<Extension>]
module public SeqExtensions =

    [<Extension>]
    let ToFSharpList sequence = List.ofSeq sequence

    [<Extension>]
    let ToHistoryList sequence = 
        let historyList = Vim.HistoryList()
        sequence 
            |> List.ofSeq 
            |> List.rev
            |> Seq.iter historyList.Add
        historyList

[<Extension>]
type public FSharpFuncUtil = 

    [<Extension>] 
    static member ToFSharpFunc<'a> (func : System.Func<'a>) = fun () -> func.Invoke()

    [<Extension>] 
    static member ToFSharpFunc<'a,'b> (func : System.Converter<'a,'b>) = fun x -> func.Invoke(x)

    [<Extension>] 
    static member ToFSharpFunc<'a,'b> (func : System.Func<'a,'b>) = fun x -> func.Invoke(x)

    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c> (func : System.Func<'a,'b,'c>) = fun x y -> func.Invoke(x,y)

    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d> (func : System.Func<'a,'b,'c,'d>) = fun x y z -> func.Invoke(x,y,z)

    [<Extension>] 
    static member ToFSharpFunc<'a> (func : System.Action) = fun () -> func.Invoke()

    [<Extension>] 
    static member ToFSharpFunc<'a> (func : System.Action<'a>) = fun x -> func.Invoke(x)

    static member Create<'a,'b> (func : System.Func<'a,'b>) = FSharpFuncUtil.ToFSharpFunc func

    static member Create<'a,'b,'c> (func : System.Func<'a,'b,'c>) = FSharpFuncUtil.ToFSharpFunc func

    static member Create<'a,'b,'c,'d> (func : System.Func<'a,'b,'c,'d>) = FSharpFuncUtil.ToFSharpFunc func
