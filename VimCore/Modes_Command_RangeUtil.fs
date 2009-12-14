#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text

type internal Range = 
    | RawSpan of SnapshotSpan
    /// Start and End line of the range
    | Lines of ITextSnapshot * int * int

type internal ParseRangeResult =
    | Succeeded of Range * KeyInput list
    | NoRange 
    | Failed of string 

type internal ItemRangeKind = 
    | LineNumber
    | CurrentLine

type internal ItemRange = 
    | ValidRange of Range * ItemRangeKind * KeyInput list
    | NoRange
    | Error of string

module internal RangeUtil =

    /// Get the SnapshotSpan for the given Range
    let GetSnapshotSpan (r:Range) =
        match r with
        | RawSpan(span) -> span
        | Lines(tss,first,last) -> 
            new SnapshotSpan(
                tss.GetLineFromLineNumber(first).Start,
                tss.GetLineFromLineNumber(last).EndIncludingLineBreak)
        
    /// Get the range for the currently selected line
    let RangeForCurrentLine view =
        let point = ViewUtil.GetCaretPoint view
        let line = point.GetContainingLine().LineNumber
        let tss = point.Snapshot
        Range.Lines(tss,line,line)

    /// Apply the count to the given range
    let ApplyCount range count =
        let inner (tss:ITextSnapshot) startLine =
            let endLine = startLine + count
            let endLine = if endLine >= tss.LineCount then tss.LineCount-1 else endLine
            Range.Lines(tss,startLine,endLine)
            
        match range with 
        | Range.Lines(tss,_,endLine) ->
            // When a cuont is applied to a line range, the count of lines staring at the end 
            // line is used
            inner tss endLine
        | Range.RawSpan(span) -> 
            inner span.Snapshot (span.End.GetContainingLine().LineNumber)

    /// Combine the two ranges
    let CombineRanges left right = 
        let bestFit = 
            match left with
            | Range.Lines(tss,startLine,_) -> 
                match right with
                | Range.Lines(_,_,endLine) ->
                    Some(Range.Lines(tss,startLine,endLine))
                | _ -> None
            | _-> None
        match bestFit with
        | Some(range) -> range
        | None ->
            let left = GetSnapshotSpan left
            let right = GetSnapshotSpan right
            let span = new SnapshotSpan(left.Start, right.End)
            Range.RawSpan(span)

    /// Parse out a number from the input string
    let ParseNumber (input:KeyInput list) =

        // Parse out the input into the list of digits and remaining input
        let rec getDigits (input:KeyInput list) =
            match input |> List.isEmpty with
            | true ->([],input)
            | false -> 
                let head = input |> List.head
                if head.IsDigit then
                    let restDigits,restInput = getDigits (input |> List.tail)
                    (head :: restDigits, restInput)
                else 
                    ([],input)
            
        let digits,remaining = getDigits input
        let numberStr = 
            digits 
                |> Seq.ofList
                |> Seq.map (fun x -> x.Char)
                |> Array.ofSeq
                |> StringUtil.OfCharArray
        let mutable number = 0
        match System.Int32.TryParse(numberStr, &number) with
        | false -> (None,input)
        | true -> (Some(number), remaining)

    /// Parse out a line number 
    let ParseLineNumber (tss:ITextSnapshot) (input:KeyInput list) =
    
        let msg = "Invalid Range: Could not find a valid number"
        let opt,remaining = ParseNumber input
        match opt with 
        | Some(number) ->
            let number = TssUtil.VimLineToTssLine number
            if number < tss.LineCount then 
                let range = Range.Lines(tss,number,number)
                ValidRange(range, LineNumber, remaining)
            else
                let msg = sprintf "Invalid Range: Line Number %d is not a valid number in the file" number
                Error(msg)
        | None -> Error("Expected a line number")


    /// Parse out a single item in the range.
    let ParseItem (point:SnapshotPoint) (map:MarkMap) (list:KeyInput list) =
        let head = list |> List.head 
        if head.IsDigit then
            ParseLineNumber point.Snapshot list
        else if head.Char = '.' then
            let line = point.GetContainingLine().LineNumber
            let range = Range.Lines(point.Snapshot, line,line)
            ValidRange(range,CurrentLine, list |> List.tail)
        else
            NoRange

    let ParseRangeCore (point:SnapshotPoint) (map:MarkMap) (originalInput:KeyInput list) =

        let parseRight (point:SnapshotPoint) map (leftRange:Range) remainingInput : ParseRangeResult = 
            let right = ParseItem point map remainingInput 
            match right with 
            | NoRange -> Failed("Invalid Range: Right hand side is missing")
            | Error(msg) -> Failed(msg)
            | ValidRange(rightRange,_,remainingInput) ->
                let fullRange = CombineRanges leftRange rightRange
                Succeeded(fullRange, remainingInput)
        
        // Parse out the separator 
        let parseWithLeft (leftRange:Range) (remainingInput:KeyInput list) kind =
            match remainingInput |> List.isEmpty with
            | true ->  if kind = CurrentLine then Succeeded(leftRange,remainingInput) else ParseRangeResult.NoRange
            | false -> 
                let head = remainingInput |> List.head 
                let rest = remainingInput |> List.tail
                if head.Char = ',' then parseRight point map leftRange rest
                else if head.Char = ';' then 
                    let point = (GetSnapshotSpan leftRange).Start
                    parseRight point map leftRange rest
                else if kind = LineNumber then
                    ParseRangeResult.NoRange
                else if kind = CurrentLine then
                    ParseRangeResult.Succeeded(leftRange, remainingInput)
                else
                    let msg = "Invalid Range: Expected , or ;"
                    ParseRangeResult.Failed(msg)
        
        let left = ParseItem point map originalInput
        match left with 
        | NoRange -> ParseRangeResult.NoRange
        | Error(_) -> ParseRangeResult.NoRange
        | ValidRange(leftSpan,kind,range) -> parseWithLeft leftSpan range kind


    let ParseRange (point:SnapshotPoint) (map:MarkMap) (list:KeyInput list) = 
        match list |> List.isEmpty with
        | true -> ParseRangeResult.NoRange
        | false ->
            let head = list |> List.head
            if head.Char = '%' then 
                let tss = point.Snapshot
                let span = new SnapshotSpan(tss, 0, tss.Length)
                ParseRangeResult.Succeeded(RawSpan(span), List.tail list)
            else
                ParseRangeCore point map list


                
        
