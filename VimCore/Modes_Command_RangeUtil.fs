#light

namespace Vim.Modes.Command
open Vim
open Vim.Interpreter
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal ParseRangeResult =
    | Succeeded of SnapshotLineRange * char list
    | NoRange 
    | Failed of string 

type internal RangeUtil
    (
        _vimBufferData : VimBufferData
    ) =

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textView = _vimBufferData.TextView
    let _markMap = _vimTextBuffer.Vim.MarkMap
    let _interpreter = Interpreter(_vimBufferData)

    member x.RangeForCurrentLine = _textView |> TextViewUtil.GetCaretLine |> SnapshotLineRangeUtil.CreateForLine

    member x.ApplyCount count (range:SnapshotLineRange) =
        let count = if count <= 1 then 1 else count
        SnapshotLineRangeUtil.CreateForLineAndMaxCount range.EndLine count

    member x.TryApplyCount count range =
        match count with 
        | None -> range
        | Some count -> x.ApplyCount count range

    member x.RangeOrCurrentLine rangeOpt =
        match rangeOpt with
        | Some range -> range
        | None -> x.RangeForCurrentLine

    member x.RangeOrCurrentLineWithCount rangeOpt countOpt =
        let range = 
            match rangeOpt with
            | Some range -> range
            | None -> x.RangeForCurrentLine
        x.TryApplyCount countOpt range

    member x.ParseNumber (input : char list) =

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

    member x.ParseRange (list : char list) = 
        let text = StringUtil.ofCharList list
        match Parser.ParseRange text with
        | ParseResult.Failed _ ->
            ParseRangeResult.NoRange
        | ParseResult.Succeeded (lineRange, tail) ->
            let tail = List.ofSeq tail
            match _interpreter.GetLineRange lineRange with
            | None -> ParseRangeResult.Failed Resources.Range_Invalid
            | Some lineRange -> ParseRangeResult.Succeeded (lineRange, tail)
