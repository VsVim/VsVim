#light

// Types which need a temporary home as public until we can merge the VimCore and 
// VimCorePort assemblies.
// 
// All these go into FSharpUtil.fs
namespace Vim
open System

type internal Contract = 

    static member Requires test = 
        if not test then
            raise (System.Exception("Contract failed"))

    [<System.Diagnostics.Conditional("DEBUG")>]
    static member Assert test = 
        if not test then
            raise (System.Exception("Contract failed"))

    static member GetInvalidEnumException<'T> (value : 'T) : System.Exception =
        let msg = sprintf "The value %O is not a valid member of type %O" value typedefof<'T>
        System.Exception(msg)

    static member FailEnumValue<'T> (value : 'T) : unit = 
        raise (Contract.GetInvalidEnumException value)

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Method ||| AttributeTargets.Interface)>]
type internal UsedInBackgroundThread() =
    inherit Attribute()

type internal StandardEvent<'T when 'T :> System.EventArgs>() =

    let _event = new DelegateEvent<System.EventHandler<'T>>()

    member x.Publish = _event.Publish

    member x.Trigger (sender : obj) (args : 'T) = 
        let argsArray = [| sender; args :> obj |]
        _event.Trigger(argsArray)

type internal StandardEvent() =

    let _event = new DelegateEvent<System.EventHandler>()

    member x.Publish = _event.Publish

    member x.Trigger (sender : obj) =
        let argsArray = [| sender; System.EventArgs.Empty :> obj |]
        _event.Trigger(argsArray)

type internal DisposableBag() = 
    let mutable _toDispose : System.IDisposable list = List.empty
    member x.DisposeAll () = 
        _toDispose |> List.iter (fun x -> x.Dispose()) 
        _toDispose <- List.empty
    member x.Add d = _toDispose <- d :: _toDispose 

module internal NullableUtil = 

    let (|HasValue|Null|) (x : System.Nullable<_>) =
        if x.HasValue then
            HasValue (x.Value)
        else
            Null 

    let Create (x : 'T) =
        System.Nullable<'T>(x)

    let CreateNull<'T when 'T : (new : unit -> 'T) and 'T : struct and 'T :> System.ValueType> () =
        System.Nullable<'T>()

    let ToOption (x : System.Nullable<_>) =
        if x.HasValue then
            Some x.Value
        else
            None

