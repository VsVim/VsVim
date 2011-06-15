#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text

type internal ParseRangeResult =
    | Succeeded of SnapshotLineRange * char list
    | NoRange 
    | Failed of string 

type internal ItemRangeKind = 
    | LineNumber
    | CurrentLine
    | Mark

type internal ItemRange = 
    | ValidRange of SnapshotLineRange * ItemRangeKind * char list
    | NoRange
    | Error of string

type internal RangeParser() =
    member this.Bind (ir, rest) =
        match ir with 
        | ValidRange (r,kind,input) -> rest (r,kind,input)
        | NoRange -> ParseRangeResult.NoRange
        | Error(msg) -> Failed(msg)
    member this.Bind (pr, rest) = 
        match pr with
        | Succeeded (range,remaining) -> rest (range,remaining)
        | ParseRangeResult.NoRange -> pr
        | Failed(_) -> pr
    member this.Zero () = Failed "Invalid"
    member this.ReturnFrom (result:ParseRangeResult) = result

module internal RangeUtil =

    let private _parser = RangeParser()

    let ApplyCount count (range:SnapshotLineRange) =
        let count = if count <= 1 then 1 else count
        SnapshotLineRangeUtil.CreateForLineAndMaxCount range.EndLine count

    let TryApplyCount count range =
        match count with 
        | None -> range
        | Some(count) -> ApplyCount count range

    let RangeForCurrentLine view = view |> TextViewUtil.GetCaretLine |> SnapshotLineRangeUtil.CreateForLine

    let RangeOrCurrentLine view rangeOpt =
        match rangeOpt with
        | Some(range) -> range
        | None -> RangeForCurrentLine view

    let RangeOrCurrentLineWithCount view rangeOpt countOpt =
        let range = 
            match rangeOpt with
            | Some range -> range
            | None -> RangeForCurrentLine view
        TryApplyCount countOpt range

    /// Combine the two ranges
    let CombineRanges (left:SnapshotLineRange) (right:SnapshotLineRange) = 
        let startLine = 
            if left.StartLineNumber < right.StartLineNumber then left.StartLine
            else right.StartLine
        let endLine =
            if left.EndLineNumber > right.EndLineNumber then left.EndLine
            else right.EndLine
        SnapshotLineRangeUtil.CreateForLineRange startLine endLine

    let ParseNumber (input:char list) =

        // Parse out the input into the list of digits and remaining input
        let rec getDigits (input:char list) =
            let inner (head:char) tail = 
                if System.Char.IsDigit head then 
                    let restDigits,restInput = getDigits tail
                    (head :: restDigits, restInput)
                else ([],input)
            ListUtil.tryProcessHead input inner (fun() -> ([],input))
            
        let digits,remaining = getDigits input
        let numberStr = 
            digits 
                |> Seq.ofList
                |> Array.ofSeq
                |> StringUtil.ofCharArray
        let mutable number = 0
        match System.Int32.TryParse(numberStr, &number) with
        | false -> (None,input)
        | true -> (Some(number), remaining)

    /// Parse out a line number 
    let private ParseLineNumber (tss:ITextSnapshot) (input:char list) =
    
        let opt,remaining = ParseNumber input
        match opt with 
        | Some number ->
            let number = Util.VimLineToTssLine number
            if number < tss.LineCount then 
                let range = tss.GetLineFromLineNumber(number) |> SnapshotLineRangeUtil.CreateForLine
                ValidRange(range, LineNumber, remaining)
            else
                Error Resources.Range_Invalid
        | None -> 
            Error("Expected a line number")

    /// Parse out a mark 
    let private ParseMark (textBuffer: ITextBuffer) (map : IMarkMap) (list : char list) = 
        let inner head tail = 
            let opt = map.GetMark textBuffer head
            match opt with 
            | Some(point) -> 
                let range = point.Position |> SnapshotPointUtil.GetContainingLine |> SnapshotLineRangeUtil.CreateForLine
                ValidRange(range, Mark, tail)
            | None -> Error Resources.Range_MarkNotValidInFile
        ListUtil.tryProcessHead list inner (fun () -> Error Resources.Range_MarkMissingIdentifier)

    /// This will parse out a potential '+' or '-' out of the list and apply it to the 
    /// provided 'range' value.  If there is not a '+' or '-' at the start of the list
    /// the method will return the original range and char list unchanged
    let ParsePlusMinus (range : SnapshotLineRange) (list: char list) =
        let getCount list =
            let opt,list = ParseNumber list
            match opt with 
            | Some(num) -> num, list
            | None -> 1, list
        let inner head tail = 
            if head = '+' then 
                let count,tail = getCount tail
                let range = 
                    if range.Count = 1 then
                        let number = min (range.StartLineNumber + count) (range.Snapshot.LineCount - 1)
                        range.Snapshot.GetLineFromLineNumber(number) |> SnapshotLineRangeUtil.CreateForLine
                    else
                        SnapshotLineRangeUtil.CreateForLineAndMaxCount range.StartLine (range.Count + count)
                range,tail
            elif head = '-' then
                let count,tail = getCount tail
                let range =
                    if range.Count = 1 then
                        let number = max 0 (range.StartLineNumber - count)
                        range.Snapshot.GetLineFromLineNumber(number) |> SnapshotLineRangeUtil.CreateForLine 
                    else 
                        let endLineNumber = max 0 (range.EndLineNumber - count)
                        if endLineNumber = range.StartLineNumber then 
                            SnapshotLineRangeUtil.CreateForLine range.StartLine
                        elif endLineNumber < range.StartLineNumber then
                            range.Snapshot.GetLineFromLineNumber(endLineNumber) |> SnapshotLineRangeUtil.CreateForLine
                        else 
                            SnapshotLineRangeUtil.CreateForLineRange range.StartLine (range.Snapshot.GetLineFromLineNumber(endLineNumber))
                range, tail
            else 
                range, list
        ListUtil.tryProcessHead list inner (fun () -> range, list)

    /// Parse out a single item in the range.  This is used to parse out the left and right
    /// side of the range.  In the case we are parsing the right side of the range the left
    /// value will be provided so that the '+' and '-' can be properly applied
    let ParseItem (line : ITextSnapshotLine) (map : IMarkMap) (list : char list) (leftRange: SnapshotLineRange option)=
        let head = list |> List.head 
        if CharUtil.IsDigit head then
            ParseLineNumber line.Snapshot list
        else if head = '.' then
            let range = line |> SnapshotLineRangeUtil.CreateForLine
            ValidRange (range, CurrentLine, list |> List.tail)
        else if head = '\'' then
            ParseMark line.Snapshot.TextBuffer map (list |> List.tail)
        else if head = '$' then 
            let range = line.Snapshot |> SnapshotUtil.GetLastLine |> SnapshotLineRangeUtil.CreateForLine
            ValidRange (range, LineNumber, list |> List.tail)
        else if head = '+' || head = '-' then
            // The range item begins with a '+' or a '-'.  In the case of the left hand
            // side of the range this is applied to the current line and in the right
            // side it's applied to the left range 
            let range = 
                match leftRange with
                | Some (range) -> range
                | None -> SnapshotLineRangeUtil.CreateForLine line
            let range, list = ParsePlusMinus range list
            ValidRange (range, LineNumber, list)
        else
            NoRange

    let ParseRangeCore (line : ITextSnapshotLine) (map : IMarkMap) (originalInput : char list) =
        _parser {
            let! range, kind,remaining = ParseItem line map originalInput None
            let range, remaining = ParsePlusMinus range remaining
            match ListUtil.tryHead remaining with
            | None -> return! Succeeded(range, remaining)
            | Some (head,tail) ->
                if head = ',' then 
                    let! rightRange, _, remaining = ParseItem line map tail (Some range)
                    let rightRange, remaining = ParsePlusMinus rightRange remaining
                    let fullRange = CombineRanges range rightRange
                    return! Succeeded(fullRange, remaining)
                else if head = ';' then 
                    let point = range.Start
                    let! rightRange, _, remaining = ParseItem line map tail (Some range)
                    let rightRange, remaining = ParsePlusMinus rightRange remaining
                    let fullRange = CombineRanges range rightRange
                    return! Succeeded(fullRange, remaining)
                else if kind = LineNumber then
                    return! Succeeded(range, remaining)
                else if kind = CurrentLine then
                    return! Succeeded(range, remaining)
                else
                    return! Failed Resources.Range_ConnectionMissing
        }

    let ParseRange (line : ITextSnapshotLine) (map : IMarkMap) (list : char list) = 
        let inner head tail =
            if head = '%' then 
                let range = SnapshotLineRangeUtil.CreateForSnapshot line.Snapshot
                ParseRangeResult.Succeeded(range, tail)
            else
                ParseRangeCore line map list
        ListUtil.tryProcessHead list inner (fun() -> ParseRangeResult.NoRange)
