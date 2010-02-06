#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input
open System.Windows.Media

type internal DefaultOperations
    (
    _textView : ITextView,
    _operations : IEditorOperations,
    _host : IVimHost,
    _settings : VimSettings,
    _normalWordNav : ITextStructureNavigator,
    _searchService : ITextSearchService,
    _jumpList : IJumpList ) =

    inherit CommonOperations(_textView, _operations, _host)

    member private x.CommonImpl = x :> ICommonOperations

    member private x.JumpCore count moveJump =
        let rec inner count = 
            if count >= 1 && moveJump() then inner (count-1)
            elif count = 0 then true
            else false
        if not (inner count) then _host.Beep()
        else
            match _jumpList.Current with
            | None -> _host.Beep()
            | Some(point) -> 
                let ret = x.CommonImpl.NavigateToPoint (VirtualSnapshotPoint(point))
                if not ret then _host.Beep()

    member private x.MoveToNextWordCore isWrap isWholeWord count = 
        let point = ViewUtil.GetCaretPoint _textView
        match TssUtil.FindCurrentFullWordSpan point WordKind.NormalWord with
        | None -> _host.UpdateStatus Resources.NormalMode_NoWordUnderCursor
        | Some(span) ->
            let options = if isWholeWord then FindOptions.WholeWord else FindOptions.None
            let options = if _settings.IgnoreCase then options else options ||| FindOptions.MatchCase
            let count = max 0 (count-1)
            let word = span.GetText()
            let rec doFind count pos = 
                let data = FindData(word, point.Snapshot, options,_normalWordNav)
                let found = _searchService.FindNext(pos, isWrap, data) 
                if found.HasValue && count > 1 then
                    let nextPos = found.Value.End.Position
                    doFind (count-1) nextPos 
                elif found.HasValue then
                    ViewUtil.MoveCaretToPoint _textView found.Value.Start |> ignore
            doFind count span.End.Position

    member x.MoveToPreviousWordCore isWrap isWholeWord count = 
        let point = ViewUtil.GetCaretPoint _textView
        match TssUtil.FindCurrentFullWordSpan point WordKind.NormalWord with
        | None -> _host.UpdateStatus Resources.NormalMode_NoWordUnderCursor
        | Some(span) ->
            let options = if isWholeWord then FindOptions.WholeWord else FindOptions.None
            let options = if _settings.IgnoreCase then options else options ||| FindOptions.MatchCase
            let options = options ||| FindOptions.SearchReverse
            let count = max 0 (count-1)
            let getNextPos (span:SnapshotSpan) = 
                let pos = span.Start.Position-1
                if pos >= 0 then pos 
                elif isWrap then point.Snapshot.Length
                else 0
            let word = span.GetText()
            let rec doFind count pos = 
                let data = FindData(word, point.Snapshot, options,_normalWordNav)
                let found = _searchService.FindNext(pos, isWrap, data) 
                if found.HasValue && count > 1 then
                    let nextPos = getNextPos found.Value
                    doFind (count-1) nextPos 
                elif found.HasValue then
                    ViewUtil.MoveCaretToPoint _textView found.Value.Start |> ignore
            doFind count (getNextPos span)

    interface IOperations with 
        
        /// Paste the given text after the cursor
        member x.PasteAfterCursor text count opKind moveCursor = 
            let text = StringUtil.Repeat text count 
            let caret = ViewUtil.GetCaretPoint _textView
            let span = x.CommonImpl.PasteAfter caret text opKind
            if moveCursor then
                ViewUtil.MoveCaretToPoint _textView span.End |> ignore
            else if opKind = OperationKind.LineWise then
                // For a LineWise paste we want to place the cursor at the start
                // of the next line
                let caretLineNumber = caret.GetContainingLine().LineNumber
                let nextLine = _textView.TextSnapshot.GetLineFromLineNumber(caretLineNumber+1)
                ViewUtil.MoveCaretToPoint _textView nextLine.Start |> ignore
 
        /// Paste the text before the cursor
        member x.PasteBeforeCursor text count moveCursor = 
            let text = StringUtil.Repeat text count 
            let caret = ViewUtil.GetCaretPoint _textView
            let span = x.CommonImpl.PasteBefore caret text 
            if moveCursor then
                ViewUtil.MoveCaretToPoint _textView span.End |> ignore

        member x.InsertLineBelow () =
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            let tss = buffer.Replace(new Span(line.End.Position,0), System.Environment.NewLine)
            let newLine = tss.GetLineFromLineNumber(line.LineNumber+1)
        
            // Move the caret to the same indent position as the previous line
            let indent = TssUtil.FindIndentPosition(line)
            let point = new VirtualSnapshotPoint(newLine, indent)
            ViewUtil.MoveCaretToVirtualPoint _textView point |> ignore
            newLine
    
        member x.InsertLineAbove () = 
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let buffer = line.Snapshot.TextBuffer
            let tss = buffer.Replace(new Span(line.Start.Position,0), System.Environment.NewLine)
            let line = tss.GetLineFromLineNumber(line.LineNumber)
            ViewUtil.MoveCaretToPoint _textView line.Start |> ignore
            line
                
        /// Implement the r command in normal mode.  
        member x.ReplaceChar (ki:KeyInput) count = 
            let point = ViewUtil.GetCaretPoint _textView

            // Make sure the replace string is valid
            if (point.Position + count) > point.GetContainingLine().End.Position then
                false
            else
                let replaceText = 
                    if ki.IsNewLine then System.Environment.NewLine
                    else new System.String(ki.Char, count)
                let span = new Span(point.Position, count)
                let tss = _textView.TextBuffer.Replace(span, replaceText) 

                // Reset the caret to the point before the edit
                let point = new SnapshotPoint(tss,point.Position)
                _textView.Caret.MoveTo(point) |> ignore
                true
    
        /// Yank lines from the buffer.  Implements the Y command
        member x.YankLines count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let point = point.GetContainingLine().Start
            let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
            x.CommonImpl.Yank span MotionKind.Inclusive OperationKind.LineWise reg |> ignore

        member x.DeleteLines count reg = 
            let point = ViewUtil.GetCaretPoint _textView
            let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
            let span = SnapshotSpan(point, span.End)
            x.CommonImpl.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore

        member x.DeleteLinesFromCursor count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let span = TssUtil.GetLineRangeSpan point count
            let span = SnapshotSpan(point, span.End)
            x.CommonImpl.DeleteSpan span MotionKind.Inclusive OperationKind.CharacterWise reg |> ignore
    
        /// Implement the normal mode x command
        member x.DeleteCharacterAtCursor count reg =
            let point = ViewUtil.GetCaretPoint _textView
            let line = point.GetContainingLine()
            let count = min (count) (line.End.Position-point.Position)
            let span = new SnapshotSpan(point, count)
            x.CommonImpl.DeleteSpan span MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
        /// Implement the normal mode X command
        member x.DeleteCharacterBeforeCursor count reg = 
            let point = ViewUtil.GetCaretPoint _textView
            let range = TssUtil.GetReverseCharacterSpan point count
            x.CommonImpl.DeleteSpan range MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore
    
        member x.JoinAtCaret count =     
            let start = ViewUtil.GetCaretPoint _textView
            let kind = Vim.Modes.JoinKind.RemoveEmptySpaces
            let res = x.CommonImpl.Join start kind count
            if not res then
                _host.Beep()

        member x.GoToDefinitionWrapper () =
            match x.CommonImpl.GoToDefinition _host with
            | Vim.Modes.Succeeded -> ()
            | Vim.Modes.Failed(msg) ->
                _host.UpdateStatus(msg)

        member x.Scroll dir count =
            let lines = VimSettingsUtil.GetScrollLineCount _settings _textView
            let tss = _textView.TextSnapshot
            let caretPoint = ViewUtil.GetCaretPoint _textView
            let curLine = caretPoint.GetContainingLine().LineNumber
            let newLine = 
                match dir with
                | ScrollDirection.Down -> min (tss.LineCount - 1) (curLine + lines)
                | ScrollDirection.Up -> max (0) (curLine - lines)
                | _ -> failwith "Invalid enum value"
            let newCaret = tss.GetLineFromLineNumber(newLine).Start
            _operations.ResetSelection()
            _textView.Caret.MoveTo(newCaret) |> ignore
            _textView.Caret.EnsureVisible()
    
        member x.MoveToNextOccuranceOfWordAtCursor isWrap count = 
            x.MoveToNextWordCore isWrap true count

        member x.MoveToPreviousOccuranceOfWordAtCursor isWrap count = 
            x.MoveToPreviousWordCore isWrap true count

        member x.MoveToNextOccuranceOfPartialWordAtCursor count = 
            x.MoveToNextWordCore true false count
    
        member x.MoveToPreviousOccuranceOfPartialWordAtCursor count =
            x.MoveToPreviousWordCore true false count

        member x.JumpNext count = x.JumpCore count (fun () -> _jumpList.MoveNext())
        member x.JumpPrevious count = x.JumpCore count (fun() -> _jumpList.MovePrevious())
    
