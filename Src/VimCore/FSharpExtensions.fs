namespace Vim.Extensions

open System.Collections.ObjectModel
open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.VisualStudio.Utilities
open Vim

[<Extension>]
type public OptionExtensions =

    [<Extension>]
    static member IsSome(opt): bool = Option.isSome opt

    [<Extension>]
    static member IsSome(opt, func): bool =
        match opt with
        | Some value -> func value
        | None -> false

    [<Extension>]
    static member IsNone(opt) = Option.isNone opt

    [<Extension>]
    static member Is (opt:'a option, value) =
        match opt with 
        | Some(toTest) -> toTest = value
        | None         -> false

    [<Extension>]
    static member SomeOrDefault opt defaultValue = 
        match opt with 
        | Some(value) -> value
        | None -> defaultValue

module public FSharpOption =

    let True = Some true
    let False = Some false

    let Create value = value |> Some

    let CreateForReference value = 
        match box value with
        | null -> None
        | _ -> Some value

    let CreateForNullable (value: System.Nullable<'T>) =
        if value.HasValue then Some value.Value
        else None

[<Extension>]
type public FSharpFuncUtil = 

    [<Extension>] 
    static member ToFSharpFunc<'a> (func: System.Func<'a>) = fun () -> func.Invoke()

    [<Extension>] 
    static member ToFSharpFunc<'a,'b> (func: System.Converter<'a,'b>) = fun x -> func.Invoke(x)

    [<Extension>] 
    static member ToFSharpFunc<'a,'b> (func: System.Func<'a,'b>) = fun x -> func.Invoke(x)

    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c> (func: System.Func<'a,'b,'c>) = fun x y -> func.Invoke(x,y)

    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d> (func: System.Func<'a,'b,'c,'d>) = fun x y z -> func.Invoke(x,y,z)

    [<Extension>] 
    static member ToFSharpFunc<'a> (func: System.Action) = fun () -> func.Invoke()

    [<Extension>] 
    static member ToFSharpFunc<'a> (func: System.Action<'a>) = fun x -> func.Invoke(x)

    static member Create<'a,'b> (func: System.Func<'a,'b>) = FSharpFuncUtil.ToFSharpFunc func

    static member Create<'a,'b,'c> (func: System.Func<'a,'b,'c>) = FSharpFuncUtil.ToFSharpFunc func

    static member Create<'a,'b,'c,'d> (func: System.Func<'a,'b,'c,'d>) = FSharpFuncUtil.ToFSharpFunc func

[<Extension>]
module public CollectionExtensions =

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
    let ToReadOnlyCollectionShallow (list: List<'T>) = ReadOnlyCollection<'T>(list)

    [<Extension>]
    let ToReadOnlyCollection (e: 'T seq) = 
        let list = List<'T>(e)
        ReadOnlyCollection<'T>(list)

[<Extension>]
module public EditorExtensions =
    open System.Runtime.InteropServices

    [<Extension>]
    let TryGetPropertySafe<'T> propertyCollection (key: obj) (value: outref<'T>) =
        match PropertyCollectionUtil.GetValue<'T> key propertyCollection with
        | Some v ->
            value <- v
            true
        | None ->
            value <- Unchecked.defaultof<'T>
            false

[<Extension>]
module public DiscriminatedUnionExtensions =

    [<Extension>]
    let GetToggle settingValue = 
        match settingValue with
        | SettingValue.Toggle v -> v
        | _ -> failwith "Not a toggle"

    [<Extension>]
    let GetNumber settingValue = 
        match settingValue with
        | SettingValue.Number v -> v
        | _ -> failwith "Not a number"

    [<Extension>]
    let GetString settingValue =
        match settingValue with 
        | SettingValue.String v -> v
        | _ -> failwith "Not a string"
