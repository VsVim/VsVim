namespace Vim
open System.Diagnostics

/// Maintains the LineRange which was visited.  It's an effecient way of tracking discontiguous 
/// LineRange values which were visited as it will collapse them into larger LineRange values 
/// as contiguous regions are added
[<DebuggerDisplay("{ToString()}")>]
[<RequireQualifiedAccess>]
type LineRangeVisitedNode =

    /// Single contiguous region visited
    | Contiguous of LineRange

    /// Two discontiguous regions which were visited.  The left will always
    /// occur numerically before the right
    | Discontiguous of LineRangeVisitedNode * LineRangeVisitedNode

    with

    member x.StartLineNumber =  
        match x with
        | Contiguous lineRange -> lineRange.StartLineNumber
        | Discontiguous (left, _) -> left.StartLineNumber

    member x.LastLineNumber = 
        match x with
        | Contiguous lineRange -> lineRange.LastLineNumber
        | Discontiguous (_, right) -> right.LastLineNumber

    /// The overarching LineRange of the visited LineRange values
    member x.LineRange = 
        LineRange.CreateFromBounds x.StartLineNumber x.LastLineNumber

    /// The set of contiguous LineRange values which have been visited (in order)
    member x.LineRanges = 
        let found = System.Collections.Generic.List<LineRange>()
        let rec inner visited = 
            match visited with
            | Contiguous lineRange -> found.Add lineRange
            | Discontiguous (left, right) -> 
                inner left
                inner right
        inner x
        found |> List.ofSeq

    /// Does the structure contain the provided LineRange
    member x.Contains (lineRange : LineRange) = 
        match x with
        | Contiguous other -> other.Contains lineRange
        | Discontiguous (left, right) ->
            if lineRange.StartLineNumber < right.StartLineNumber then
                left.Contains lineRange
            else
                right.Contains lineRange

    member x.Add (lineRange : LineRange) = 
        match x with 
        | Contiguous other ->
            if lineRange.Intersects other then
                LineRange.CreateOverarching lineRange other |> Contiguous
            elif lineRange.StartLineNumber < other.StartLineNumber then
                Discontiguous ((Contiguous lineRange), x)
            else
                Discontiguous (x, (Contiguous lineRange))
        | Discontiguous (left, right) ->

            // See if the add created an oppurtunity for a collapse
            let collapse left right =
                match left, right with
                | Contiguous leftLineRange, Contiguous rightLineRange ->
                    if leftLineRange.Intersects rightLineRange then
                        LineRange.CreateOverarching leftLineRange rightLineRange |> Contiguous
                    else
                        Discontiguous (left, right)
                | _ -> Discontiguous (left, right)

            // The normal way of adding is to look at the set of LineRange values which
            // are represented in the LineRangeVisited structure.  Then collapse them down
            // one at a time
            let addNormal () = 
                let lineRanges = lineRange :: x.LineRanges
                LineRangeVisitedNode.OfSeq lineRanges |> Option.get

            if lineRange.Intersects left.LineRange && lineRange.Intersects right.LineRange then
                // If it intersects the left and the right then we need to do the normal
                // method of adding
                addNormal()
            else
                match left, right with
                | Contiguous leftLineRange, Contiguous rightLineRange ->
                    if lineRange.Intersects leftLineRange then
                        let left = left.Add lineRange
                        collapse left right
                    elif lineRange.Intersects rightLineRange then
                        let right = right.Add lineRange
                        collapse left right
                    else
                        addNormal()
                | _ -> addNormal()

    /// Create a LineRangeVisited structure from the sequence of LineRange values.  They
    /// do not need to be ordered.  If the provided Sequence is empty then None will be 
    /// returned
    static member OfSeq (lineRanges : seq<LineRange>) : LineRangeVisitedNode option =

        // First get it into a sorted LineRange list
        let lineRanges =
            lineRanges
            |> Seq.sortBy (fun lineRange -> lineRange.StartLineNumber)
            |> List.ofSeq

        // Now that we are sorted collapse down consequetive LineRange values which intersect
        // into ones that don't
        let lineRanges = 

            let rec collapseHead (lineRange : LineRange) rest = 
                match rest with
                | [] -> lineRange, []
                | head :: tail ->
                    if lineRange.Intersects head then
                        collapseHead (LineRange.CreateOverarching lineRange head) tail
                    else
                        lineRange, rest

            let rec inner rest withRange = 
                match rest with
                | [] -> withRange []
                | head :: tail ->
                    let head, tail = collapseHead head tail
                    let withRange = fun next -> withRange (head :: next)
                    inner tail withRange

            inner lineRanges (fun x -> x)

        lineRanges 
        |> Seq.fold (fun visited lineRange ->
            match visited with
            | None -> Contiguous lineRange |> Some
            | Some visited -> Discontiguous (visited, Contiguous lineRange) |> Some) None

    override x.ToString() = 
        match x with 
        | Contiguous lineRange -> sprintf "C %s" (lineRange.ToString())
        | Discontiguous (left, right) -> sprintf "D (%s %s)" (left.ToString()) (right.ToString())

type LineRangeVisited() = 

    let _list = System.Collections.Generic.List<LineRange>()

    member x.Add lineRange = 
        _list.Add lineRange
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

    member x.LineRange =
        if _list.Count = 0 then
            None
        else
            let left = _list.[0]
            let right = _list.[_list.Count - 1]
            LineRange.CreateOverarching left right |> Some

    member x.Clear() = 
        _list.Clear()

