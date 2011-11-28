namespace Vim
open System.Diagnostics

type LineRangeVisited() = 

    let _list = System.Collections.Generic.List<LineRange>()

    member x.List = _list

    member x.LineRange = 
        match _list.Count with
        | 0 -> None
        | 1 -> _list.[0] |> Some
        | _ -> 
            let startLine = _list.[0].StartLineNumber
            let lastLine = _list.[_list.Count - 1].LastLineNumber
            LineRange.CreateFromBounds startLine lastLine |> Some

    member x.OrganizeLineRanges() =
        _list.Sort (fun (x : LineRange) (y : LineRange) -> x.StartLineNumber.CompareTo(y.StartLineNumber))

        // Now collapse any LineRange which intersects with the given value
        let mutable i = 0
        while i + 1 < _list.Count do
            let current = _list.[i]
            let next = _list.[i + 1]
            if current.Intersects next then
                _list.[i] <- LineRange.CreateOverarching current next
                _list.RemoveAt (i + 1)
            else
                i <- i + 1

    member x.Add lineRange = 
        _list.Add lineRange
        x.OrganizeLineRanges()

    member x.Contains lineRange =
        _list
        |> SeqUtil.any (fun other -> other.Contains lineRange)

    member x.GetUnvisited lineRange = 
        let found = 
            _list
            |> Seq.tryFind (fun current -> current.Intersects lineRange)
        match found with
        | None -> Some lineRange
        | Some found ->
            if found.Contains lineRange then
                // Already have a LineRange which completely contains the provided 
                // value.  No unvisited values
                None
            elif found.StartLineNumber <= lineRange.StartLineNumber then
                // The found range starts before and intersects.  The unvisited section 
                // is the range below
                LineRange.CreateFromBounds (found.LastLineNumber + 1) lineRange.LastLineNumber |> Some
            elif found.StartLineNumber > lineRange.StartLineNumber then
                // The found range starts below and intersects.  The unvisited section 
                // is the line range above
                LineRange.CreateFromBounds lineRange.StartLineNumber (found.StartLineNumber - 1) |> Some
            else
                Some lineRange

    member x.Clear() = 
        _list.Clear()

    static member OfSeq lineRanges = 
        let visited = LineRangeVisited()
        visited.List.AddRange lineRanges
        visited.OrganizeLineRanges()
        visited

