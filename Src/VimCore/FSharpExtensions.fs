namespace Vim.Extensions

open System.Runtime.CompilerServices

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
