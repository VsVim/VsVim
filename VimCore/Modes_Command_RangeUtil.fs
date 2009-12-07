#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text

type internal Range =
    /// Valid input range.  SnapshotSpan has the actual range of the text while the KeyInput
    /// list is the remaining input after the range
    | ValidRange of SnapshotSpan * KeyInput list
    /// No range given, original list returned
    | NoRange of KeyInput list
    /// Invalid range.  String contains the error
    | Invalid of string * KeyInput list

module internal RangeUtil =

    /// Parse out a line number 
    let ParseLineNumber (tss:ITextSnapshot) (input:KeyInput list) =

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
        let numberArray = 
            digits 
                |> Seq.ofList
                |> Seq.map (fun x -> x.Char)
                |> Array.ofSeq
        let numberStr = new System.String(numberArray)
        let msg = sprintf "Invalid Range: \"%s\" is not a valid line number numberStr" numberStr
        let mutable number = 0
        match System.Int32.TryParse(numberStr, &number) with
        | false -> Invalid(msg, input)
        | true -> 
            let number = TssUtil.VimLineToTssLine number
            if number < tss.LineCount then 
                let line = tss.GetLineFromLineNumber(number)
                ValidRange(line.ExtentIncludingLineBreak, remaining)
            else
                Invalid(msg,input)


    /// Parse out a single item in the range
    let ParseItem (point:SnapshotPoint) (map:MarkMap) (list:KeyInput list) =
        let head = list |> List.head 
        if head.IsDigit then
            ParseLineNumber point.Snapshot list
        else if head.Char = '.' then
            let span = point.GetContainingLine().ExtentIncludingLineBreak
            ValidRange(span, list |> List.tail)
        else
            NoRange(list)

    let ParseRangeCore (point:SnapshotPoint) (map:MarkMap) (range:KeyInput list) =

        let parseRight (point:SnapshotPoint) map (leftSpan:SnapshotSpan) remainingInput = 
            let right = ParseItem point map remainingInput 
            match right with 
            | Invalid(msg,_) -> Invalid(msg,range)
            | NoRange(_) -> Invalid("Invalid right portion of range", range)
            | ValidRange(rightSpan,remainingInput) ->
                let fullSpan = new SnapshotSpan(leftSpan.Start,rightSpan.End)
                ValidRange(fullSpan, remainingInput)
        
        // Parse out the separator 
        let parseWithLeft (leftSpan:SnapshotSpan) (remainingInput:KeyInput list) =
            let msg = "Invalid Range: Expected , or ;"
            match remainingInput |> List.isEmpty with
            | true -> Invalid(msg,range)
            | false -> 
                let head = remainingInput |> List.head 
                let remainingInput = remainingInput |> List.tail
                if head.Char = ',' then parseRight point map leftSpan remainingInput
                else if head.Char = ';' then 
                    let point = leftSpan.End.GetContainingLine().Start
                    parseRight point map leftSpan remainingInput
                else Invalid(msg,range)
        
        let left = ParseItem point map range
        match left with 
        | Invalid(_) -> left
        | NoRange(_) -> left
        | ValidRange(leftSpan,range) -> parseWithLeft leftSpan range


    let ParseRange (point:SnapshotPoint) (map:MarkMap) (list:KeyInput list) = 
        match list |> List.isEmpty with
        | true -> NoRange(list)
        | false ->
            let head = list |> List.head
            if head.Char = '%' then 
                let tss = point.Snapshot
                let span = new SnapshotSpan(tss, 0, tss.Length)
                ValidRange(span, List.tail list)
            else
                ParseRangeCore point map list
