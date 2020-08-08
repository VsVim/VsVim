namespace Vim

open System.Collections.Generic

type internal DigraphMap() =

    let mutable _map = Dictionary<char * char, int>()

    interface IDigraphMap with
        member x.Map char1 char2 code = _map.[(char1, char2)] <- code
        member x.Unmap char1 char2 = _map.Remove((char1, char2)) |> ignore

        member x.GetMapping char1 char2 =
            if _map.ContainsKey((char1, char2)) then Some _map.[(char1, char2)] else None

        member x.Mappings =
            _map
            |> Seq.map (fun pair ->
                let char1, char2 = pair.Key
                (char1, char2, pair.Value))

        member x.Clear() = _map.Clear()
