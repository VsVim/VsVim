#light
namespace Vim.Interpreter
open Vim
open Microsoft.VisualStudio.Text

[<Sealed>]
[<Class>]
type Interpreter
    (
        _vimBufferData : VimBufferData
    ) =

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView
    let _markMap = _vimTextBuffer.Vim.MarkMap

    /// The column of the caret
    member x.CaretColumn = SnapshotPointUtil.GetColumn x.CaretPoint

    /// The SnapshotPoint for the caret
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// The ITextSnapshotLine for the caret
    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The line number for the caret
    member x.CaretLineNumber = x.CaretLine.LineNumber

    /// The SnapshotLineRange for the caret line
    member x.CaretLineRange = x.CaretLine |> SnapshotLineRangeUtil.CreateForLine

    /// The SnapshotPoint and ITextSnapshotLine for the caret
    member x.CaretPointAndLine = TextViewUtil.GetCaretPointAndLine _textView

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    /// Get the ITextSnapshotLine specified by the given LineSpecifier
    member x.GetLineCore lineSpecifier currentLine = 

        // Get the ITextSnapshotLine specified by lineSpecifier and then apply the
        // given adjustment to the number.  Can fail if the line number adjustment
        // is invalid
        let getAdjustment adjustment (line : ITextSnapshotLine) = 
            let number = 
                let start = line.LineNumber + 1
                Util.VimLineToTssLine (start + adjustment)

            SnapshotUtil.TryGetLine x.CurrentSnapshot number

        match lineSpecifier with 
        | LineSpecifier.CurrentLine -> 
            x.CaretLine |> Some
        | LineSpecifier.LastLine ->
            SnapshotUtil.GetLastLine x.CurrentSnapshot |> Some
        | LineSpecifier.LineSpecifierWithAdjustment (lineSpecifier, adjustment) ->

            x.GetLine lineSpecifier |> OptionUtil.map2 (getAdjustment adjustment)
        | LineSpecifier.MarkLine mark ->

            // Get the line containing the mark in the context of this IVimTextBuffer
            _markMap.GetMark mark _vimBufferData
            |> Option.map VirtualSnapshotPointUtil.GetPoint
            |> Option.map SnapshotPointUtil.GetContainingLine
        | LineSpecifier.NextLineWithPattern pattern ->
            // TODO: Implement
            None
        | LineSpecifier.NextLineWithPreviousPattern ->
            // TODO: Implement
            None
        | LineSpecifier.NextLineWithPreviousSubstitutePattern ->
            // TODO: Implement
            None
        | LineSpecifier.Number number ->
            // Must be a valid number 
            let number = Util.VimLineToTssLine number
            SnapshotUtil.TryGetLine x.CurrentSnapshot number
        | LineSpecifier.PreviousLineWithPattern pattern ->
            // TODO: Implement
            None
        | LineSpecifier.PreviousLineWithPreviousPattern ->
            // TODO: Implement
            None

        | LineSpecifier.AdjustmentOnCurrent adjustment -> 
            getAdjustment adjustment currentLine

    /// Get the ITextSnapshotLine specified by the given LineSpecifier
    member x.GetLine lineSpecifier = 
        x.GetLineCore lineSpecifier x.CaretLine

    /// Get the specified LineRange in the IVimBuffer
    member x.GetLineRange lineRange =
        match lineRange with
        | LineRange.EntireBuffer -> 
            SnapshotLineRangeUtil.CreateForSnapshot x.CurrentSnapshot |> Some
        | LineRange.SingleLine lineSpecifier-> 
            x.GetLine lineSpecifier |> Option.map SnapshotLineRangeUtil.CreateForLine
        | LineRange.Range (leftLineSpecifier, rightLineSpecifier, adjustCaret) ->
            match x.GetLine leftLineSpecifier with
            | None ->
                None
            | Some leftLine ->
                // If the adjustCaret option was specified then we need to move the caret before
                // interpreting the next LineSpecifier.  The caret should remain moved after this 
                // completes
                if adjustCaret then
                    TextViewUtil.MoveCaretToPoint _textView leftLine.Start

                // Get the right line and combine the results
                match x.GetLineCore rightLineSpecifier leftLine with
                | None -> None
                | Some rightLine -> SnapshotLineRangeUtil.CreateForLineRange leftLine rightLine |> Some



