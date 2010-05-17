namespace Vim.Extensions

open System.Runtime.CompilerServices

[<Extension>]
module public OptionExtensions =

    [<Extension>]
    let IsSome opt = Option.isSome opt

    [<Extension>]
    let IsNone opt = Option.isNone opt


module public FSharpOption =

    let Create value = value |> Some

