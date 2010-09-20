#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

[<AbstractClass>]
type internal CommonOperations ( _data : OperationsData ) =
    let _textView = _data.TextView
    let _operations = _data.EditorOperations
    let _outlining = _data.OutliningManager
    let _host = _data.VimHost
    let _jumpList = _data.JumpList
    let _settings = _data.LocalSettings
    let _undoRedoOperations = _data.UndoRedoOperations
    let _statusUtil = _data.StatusUtil

    /// The caret sometimes needs to be adjusted after an Up or Down movement.  Caret position
    /// and virtual space is actually quite a predicamite for VsVim because of how Vim standard 
    /// works.  Vim has no concept of Virtual Space and is designed to work in a fixed width
    /// font buffer.  Visual Studio has essentially the exact opposite.  Non-fixed width fonts are
    /// the most problematic because it makes the natural Vim motion of column based up and down
    /// make little sense visually.  Instead we rely on the core editor for up and down motions.
    ///
    /// The one exception has to do with the VirtualEdit setting.  By default the 'l' motion will 
    /// only move you to the last character on the line and no further.  Visual Studio up and down
    /// though acts like virtualedit=onemore.  We correct this here
    member private x.AdjustCaretAfterUpDownMove() =
        if not _settings.GlobalSettings.IsVirtualEditOneMore then 
            let point = TextViewUtil.GetCaretPoint _textView
            let line = SnapshotPointUtil.GetContainingLine point
            if point.Position >= line.End.Position && line.Length > 0 then 
                TextViewUtil.MoveCaretToPoint _textView (line.End.Subtract(1))

    member private x.NavigateToPoint (point:VirtualSnapshotPoint) = 
        let buf = point.Position.Snapshot.TextBuffer
        if buf = _textView.TextBuffer then 
            TextViewUtil.MoveCaretToPoint _textView point.Position
            TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining
            true
        else  _host.NavigateTo point 

    member private x.DeleteBlock col (reg:Register) =
        let data = StringData.OfNormalizedSnasphotSpanCollection col
        let value = {Value=data; MotionKind=MotionKind.Inclusive;OperationKind=OperationKind.CharacterWise}
        reg.UpdateValue value
        use edit = _textView.TextBuffer.CreateEdit()
        col |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
        edit.Apply() |> ignore

    member private x.DeleteSpan (span:SnapshotSpan) motionKind opKind (reg:Register) =
        let data = span |> SnapshotSpanUtil.GetText |> StringData.Simple
        let tss = span.Snapshot
        let regValue = {Value=data ;MotionKind=motionKind;OperationKind=opKind}
        reg.UpdateValue(regValue) 
        tss.TextBuffer.Delete(span.Span)

    member x.ShiftRightCore multiplier (col:SnapshotSpan seq) = 
        let text = new System.String(' ', _settings.GlobalSettings.ShiftWidth * multiplier)
        use edit = _data.TextView.TextBuffer.CreateEdit()
        col |> Seq.iter (fun span -> edit.Replace(span.Start.Position, 0, text) |> ignore)
        edit.Apply() |> ignore

    member x.ShiftSpanRight multiplier span = 
        let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
        let snapshot = span.Snapshot
        let col = 
            SnapshotUtil.GetLineRange snapshot startLine.LineNumber endLine.LineNumber
            |> Seq.map (fun line -> SnapshotLineUtil.GetExtentIncludingLineBreak line)
        x.ShiftRightCore multiplier col 

    member x.ShiftLeftCore multiplier (col:SnapshotSpan seq) =
        let count = _settings.GlobalSettings.ShiftWidth * multiplier
        use edit = _data.TextView.TextBuffer.CreateEdit()
        col 
        |> Seq.iter (fun span -> 
            let text = SnapshotSpanUtil.GetText span
            let toReplace = 
                let whiteSpaceLen = text |> Seq.takeWhile CharUtil.IsWhiteSpace |> Seq.length
                min whiteSpaceLen count
            edit.Replace(span.Start.Position, toReplace, StringUtil.empty) |> ignore )
        edit.Apply() |> ignore
        
    member x.ShiftSpanLeft multiplier (span:SnapshotSpan) = 
        let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
        let snapshot = span.Snapshot
        let col = 
            SnapshotUtil.GetLineRange snapshot startLine.LineNumber endLine.LineNumber
            |> Seq.map (fun line -> SnapshotLineUtil.GetExtentIncludingLineBreak line)
        x.ShiftLeftCore multiplier col 

    /// Change the letters on the given span by applying the specified function
    /// to each of them
    member x.ChangeLettersCore col changeFunc = 
        use edit = _textView.TextBuffer.CreateEdit()
        col 
        |> Seq.map SnapshotSpanUtil.GetPoints 
        |> Seq.concat
        |> Seq.map (fun x -> x.Position,x.GetChar())
        |> Seq.filter (fun (_,c) -> CharUtil.IsLetter c)
        |> Seq.map (fun (pos,c) -> (pos, changeFunc c))
        |> Seq.iter (fun (pos,c) -> edit.Replace(new Span(pos,1), StringUtil.ofChar c) |> ignore)
        edit.Apply() |> ignore

    member x.ChangeLettersOnSpan span changeFunc = x.ChangeLettersCore (Seq.singleton span) changeFunc

    member x.JoinCore span kind =

        let count = SnapshotSpanUtil.GetLineCount span
        if count > 1 then
            // Create a tracking point for the caret
            let snapshot = span.Snapshot
            let trackingPoint = 
                let point = span |> SnapshotSpanUtil.GetEndLine |> SnapshotLineUtil.GetStart
                snapshot.CreateTrackingPoint(point.Position, PointTrackingMode.Positive)

            use edit = _data.TextView.TextBuffer.CreateEdit()

            let replace = 
                match kind with
                | KeepEmptySpaces -> ""
                | RemoveEmptySpaces -> " "

            span 
            |> SnapshotSpanUtil.GetAllLines
            |> Seq.take (count-1) // Skip the last line 
            |> Seq.iter (fun line -> 

                // Delete the line break span
                let span = line |> SnapshotLineUtil.GetLineBreakSpan |> SnapshotSpanUtil.GetSpan
                edit.Replace(span, replace) |> ignore

                // Maybe strip the start of the next line as well
                if SnapshotUtil.IsLineNumberValid snapshot (line.LineNumber + 1) then
                    match kind with
                    | KeepEmptySpaces -> ()
                    | RemoveEmptySpaces ->
                        let nextLine = SnapshotUtil.GetLine snapshot (line.LineNumber+1)
                        let count =
                            nextLine.Extent 
                            |> SnapshotSpanUtil.GetText
                            |> Seq.takeWhile CharUtil.IsWhiteSpace
                            |> Seq.length
                        if count > 0 then
                            edit.Delete(nextLine.Start.Position,count) |> ignore
                )

            // Now position the caret on the new snapshot
            let snapshot = edit.Apply()
            match TrackingPointUtil.GetPoint snapshot trackingPoint with
            | None -> ()
            | Some(point) ->  TextViewUtil.MoveCaretToPoint _textView point 

    interface ICommonOperations with
        member x.TextView = _textView 
        member x.EditorOperations = _operations
        member x.FoldManager = _data.FoldManager
        member x.UndoRedoOperations = _data.UndoRedoOperations

        member x.Join start kind count =
    
            // Always joining at least 2 lines so we subtract to get the number of join
            // operations.  1 is a valid input though
            let count = if count > 1 then count-1 else 1
            let number = start |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetLineNumber
            if SnapshotUtil.IsLineNumberValid start.Snapshot (number + count) then
                let span =  start |> SnapshotSpanUtil.CreateEmpty 
                let span =  SnapshotSpanUtil.ExtendDown span count
                x.JoinCore span kind
                true
            else false
        member x.JoinSpan span kind = x.JoinCore span kind

        member x.GoToDefinition () = 
            let before = TextViewUtil.GetCaretPoint _textView
            if _host.GoToDefinition() then
                _jumpList.Add before |> ignore
                Succeeded
            else
                match TssUtil.FindCurrentFullWordSpan _textView.Caret.Position.BufferPosition Vim.WordKind.BigWord with
                | Some(span) -> 
                    let msg = Resources.Common_GotoDefFailed (span.GetText())
                    Failed(msg)
                | None ->  Failed(Resources.Common_GotoDefNoWordUnderCursor) 

        member x.GoToMatch () = _host.GoToMatch()
                
        member x.SetMark (vimBuffer:IVimBuffer) point c = 
            if System.Char.IsLetter(c) || c = '\'' || c = '`' then
                let map = vimBuffer.MarkMap
                map.SetMark point c
                Succeeded
            else
                Failed(Resources.Common_MarkInvalid)

        member x.NavigateToPoint point = x.NavigateToPoint point
                
        member x.JumpToMark ident (map:IMarkMap) = 
            let before = TextViewUtil.GetCaretPoint _textView
            let jumpLocal (point:VirtualSnapshotPoint) = 
                TextViewUtil.MoveCaretToPoint _textView point.Position
                TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining
                _jumpList.Add before |> ignore
                Succeeded
            if not (map.IsLocalMark ident) then 
                match map.GetGlobalMark ident with
                | None -> Failed Resources.Common_MarkNotSet
                | Some(point) -> 
                    match x.NavigateToPoint point with
                    | true -> 
                        _jumpList.Add before |> ignore
                        Succeeded
                    | false -> Failed Resources.Common_MarkInvalid
            else 
                match map.GetLocalMark _textView.TextBuffer ident with
                | Some(point) -> jumpLocal point
                | None -> Failed Resources.Common_MarkNotSet
    
        member x.YankText text motion operation (reg:Register) =
            let data = StringData.Simple text
            let regValue = {Value=data;MotionKind = motion; OperationKind = operation};
            reg.UpdateValue (regValue)

        member x.Yank (span:SnapshotSpan) motion operation (reg:Register) =
            let regValue = {Value=StringData.OfSpan span;MotionKind = motion; OperationKind = operation};
            reg.UpdateValue (regValue)
        
        member x.PasteAfter point text opKind = 
            let buffer = SnapshotPointUtil.GetBuffer point
            let line = SnapshotPointUtil.GetContainingLine point

            let doLineWise() = 
                let span = SnapshotSpan(line.EndIncludingLineBreak,0) 
                if line.LineBreakLength > 0 then 
                    (span, text, 0)
                else 
                    // when there is a 0 length line break we are at the end of
                    // the file and must insert an additional newline
                    let text = System.Environment.NewLine + text
                    (span, text, System.Environment.NewLine.Length)

            let doCharacterWise() = 
                let point =  if point.Position < line.End.Position then point.Add(1) else point
                let span =  SnapshotSpan(point,0)
                (span, text, 0)

            let replaceSpan, replaceText, offset = 
                match opKind with
                | OperationKind.LineWise -> doLineWise()
                | OperationKind.CharacterWise -> doCharacterWise()
                | _ -> failwith "Invalid Enum Value"
            let tss = buffer.Replace(replaceSpan.Span, replaceText)
            new SnapshotSpan(tss, replaceSpan.End.Position + offset , text.Length)
        
        member x.PasteBefore (point:SnapshotPoint) text opKind =
            let buffer = point.Snapshot.TextBuffer
            let span = 
                match opKind with
                | OperationKind.LineWise ->
                    let line = point.GetContainingLine()
                    new SnapshotSpan(line.Start, 0)
                | OperationKind.CharacterWise ->
                    new SnapshotSpan(point,0)
                | _ -> failwith "Invalid Enum Value"
            let tss = buffer.Replace(span.Span, text) 
            new SnapshotSpan(tss,span.End.Position, text.Length)

        member x.PasteOver span (reg:Register) =
            use edit = _data.TextView.TextBuffer.CreateEdit()
            edit.Replace(span.Span, reg.StringValue) |> ignore
            edit.Apply() |> ignore
    
        /// Move the cursor count spaces left
        member x.MoveCaretLeft count = 
            let caret = TextViewUtil.GetCaretPoint _textView
            let leftPoint = SnapshotPointUtil.GetPreviousPointOnLine caret count
            if caret <> leftPoint then
                _operations.ResetSelection()
                TextViewUtil.MoveCaretToPoint _textView leftPoint
    
        /// Move the cursor count spaces to the right
        member x.MoveCaretRight count =
            let caret = TextViewUtil.GetCaretPoint _textView
            let doMove point = 
                if point <> caret then
                    _operations.ResetSelection()
                    TextViewUtil.MoveCaretToPoint _textView point

            if SnapshotPointUtil.IsLastPointOnLine caret then

                // If we are an the last point of the line then only move if VirtualEdit=onemore
                let line = SnapshotPointUtil.GetContainingLine caret
                if _settings.GlobalSettings.IsVirtualEditOneMore && line.Length > 0 then 
                    doMove line.End
            else

                let rightPoint = SnapshotPointUtil.GetNextPointOnLine caret count
                doMove rightPoint
    
        /// Move the cursor count spaces up 
        member x.MoveCaretUp count =
            let caret = TextViewUtil.GetCaretPoint _textView
            let current = caret.GetContainingLine()
            let count = 
                if current.LineNumber - count > 0 then count
                else current.LineNumber 
            if count > 0 then _operations.ResetSelection()
            for i = 1 to count do   
                _operations.MoveLineUp(false)
            x.AdjustCaretAfterUpDownMove()

        /// Move the cursor count spaces down
        member x.MoveCaretDown count =
            let caret = TextViewUtil.GetCaretPoint _textView
            let line = caret.GetContainingLine()
            let tss = line.Snapshot
            let count = 
                if line.LineNumber + count < tss.LineCount then count
                else (tss.LineCount - line.LineNumber) - 1 
            if count > 0 then _operations.ResetSelection()
            for i = 1 to count do
                _operations.MoveLineDown(false)
            x.AdjustCaretAfterUpDownMove()

        member x.MoveWordForward kind count = 
            let caret = TextViewUtil.GetCaretPoint _textView
            let pos = TssUtil.FindNextWordStart caret count kind
            TextViewUtil.MoveCaretToPoint _textView pos 
            
        member x.MoveWordBackward kind count = 
            let caret = TextViewUtil.GetCaretPoint _textView
            let pos = TssUtil.FindPreviousWordStart caret count kind
            TextViewUtil.MoveCaretToPoint _textView pos 

        member x.ShiftSpanRight multiplier span = x.ShiftSpanRight multiplier span
        member x.ShiftBlockRight multiplier block = x.ShiftRightCore multiplier block 
        member x.ShiftSpanLeft multiplier span = x.ShiftSpanLeft multiplier span
        member x.ShiftBlockLeft multiplier block = x.ShiftLeftCore multiplier block

        member x.ShiftLinesRight count = 
            let point = TextViewUtil.GetCaretPoint _textView
            let span = SnapshotPointUtil.GetLineRangeSpan point count
            x.ShiftSpanRight 1 span

        member x.ShiftLinesLeft count =
            let point = TextViewUtil.GetCaretPoint _textView
            let span = SnapshotPointUtil.GetLineRangeSpan point count
            x.ShiftSpanLeft 1 span
            
        member x.InsertText text count = 
            let text = StringUtil.repeat text count 
            let point = TextViewUtil.GetCaretPoint _textView
            use edit = _textView.TextBuffer.CreateEdit()
            edit.Insert(point.Position, text) |> ignore
            edit.Apply() |> ignore
             
            // Need to adjust the caret to the end of the inserted text.  Very important
            // for operations like repeat
            if not (StringUtil.isNullOrEmpty text) then
                let snapshot = _textView.TextSnapshot
                let position = point.Position + text.Length - 1 
                let caret = SnapshotPoint(snapshot, position)
                _textView.Caret.MoveTo(caret) |> ignore
                _textView.Caret.EnsureVisible()
                

        member x.MoveCaretAndScrollLines dir count =
            let lines = _settings.Scroll
            let tss = _textView.TextSnapshot
            let caretPoint = TextViewUtil.GetCaretPoint _textView
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

        member x.ScrollLines dir count =
            for i = 1 to count do
                match dir with
                | ScrollDirection.Down -> _operations.ScrollDownAndMoveCaretIfNecessary()
                | ScrollDirection.Up -> _operations.ScrollUpAndMoveCaretIfNecessary()
                | _ -> failwith "Invalid enum value"
    
        member x.ScrollPages dir count = 
            let func,getLine =
                match dir with
                | ScrollDirection.Down -> (_operations.ScrollPageDown, fun () -> _textView.TextViewLines.LastVisibleLine)
                | ScrollDirection.Up -> (_operations.ScrollPageUp, fun () -> _textView.TextViewLines.FirstVisibleLine)
                | _ -> failwith "Invalid enum value"
            for i = 1 to count do
                func()

            // Scrolling itself does not move the caret.  Must be manually moved
            let line = getLine()
            _textView.Caret.MoveTo(line) |> ignore

        member x.DeleteSpan span motionKind opKind reg = x.DeleteSpan span motionKind opKind reg
        member x.DeleteBlock col reg = x.DeleteBlock col reg
        member x.DeleteLines count reg = 
            let point = TextViewUtil.GetCaretPoint _textView
            let point = point.GetContainingLine().Start
            let span = SnapshotPointUtil.GetLineRangeSpan point count
            let span = SnapshotSpan(point, span.End)
            x.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore
        member x.DeleteLinesInSpan span reg =
            let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
            let span = SnapshotSpan(startLine.Start,endLine.End)
            x.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore

        member x.DeleteLinesFromCursor count reg = 
            let point,line = TextViewUtil.GetCaretPointAndLine _textView
            let span = SnapshotPointUtil.GetLineRangeSpan point count
            let span = SnapshotSpan(point, span.End)
            x.DeleteSpan span MotionKind.Inclusive OperationKind.CharacterWise reg |> ignore

        /// Delete count lines from the cursor.  The last line is an unfortunate special case here 
        /// as it does not have a line break.  Hence in order to delete the line we must delete the 
        /// line break at the end of the preceeding line.  
        ///
        /// This cannot be normalized by always deleting the line break from the previous line because
        /// it would still break for the first line.  This is an unfortunate special case we must 
        /// deal with
        member x.DeleteLinesIncludingLineBreak count reg = 
            let point,line = TextViewUtil.GetCaretPointAndLine _textView
            let snapshot = point.Snapshot
            let span = 
                if 1 = count && line.LineNumber = SnapshotUtil.GetLastLineNumber snapshot && snapshot.LineCount > 1 then
                    let above = snapshot.GetLineFromLineNumber (line.LineNumber-1)
                    SnapshotSpan(above.End, line.End)
                else
                    let point = line.Start
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    SnapshotSpan(point, span.End)
            x.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore

        member x.DeleteLinesIncludingLineBreakFromCursor count reg = 
            let point = TextViewUtil.GetCaretPoint _textView
            let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
            let span = SnapshotSpan(point, span.End)
            x.DeleteSpan span MotionKind.Inclusive OperationKind.CharacterWise reg |> ignore

        member x.Undo count = _undoRedoOperations.Undo count
        member x.Redo count = _undoRedoOperations.Redo count
        member x.Save() = _host.Save _textView
        member x.SaveAs fileName = _host.SaveCurrentFileAs fileName
        member x.SaveAll() = _host.SaveAllFiles()
        member x.Close checkDirty = _host.Close _textView checkDirty
        member x.CloseAll checkDirty = _host.CloseAllFiles checkDirty
        member x.GoToNextTab count = _host.GoToNextTab count
        member x.GoToPreviousTab count = _host.GoToPreviousTab count
        member x.ChangeLetterCase span = x.ChangeLettersOnSpan span CharUtil.ChangeCase
        member x.ChangeLetterCaseBlock col = x.ChangeLettersCore col CharUtil.ChangeCase
        member x.MakeLettersLowercase span = x.ChangeLettersOnSpan span CharUtil.ToLower
        member x.MakeLettersUppercase span = x.ChangeLettersOnSpan span CharUtil.ToUpper
        member x.EnsureCaretOnScreen () = TextViewUtil.EnsureCaretOnScreen _textView 
        member x.EnsureCaretOnScreenAndTextExpanded () = TextViewUtil.EnsureCaretOnScreenAndTextExpanded _textView _outlining
        member x.MoveCaretToPoint point =  TextViewUtil.MoveCaretToPoint _textView point 
        member x.MoveCaretToMotionData (data:MotionData) =

            // Reduce the Span to the line we care about 
            let line = 
                if data.IsForward then SnapshotSpanUtil.GetEndLine data.Span
                else SnapshotSpanUtil.GetStartLine data.Span

            // Get the point which is the last or first point valid on the 
            // particular line / span 
            let getPointFromSpan () = 
                if data.OperationKind = OperationKind.LineWise then 
                    if data.IsForward then SnapshotLineUtil.GetEnd line
                    else SnapshotLineUtil.GetStart line
                else
                    if data.IsForward then
                        if data.MotionKind = MotionKind.Exclusive then data.Span.End
                        else SnapshotPointUtil.GetPreviousPointOnLine data.Span.End 1
                    else data.Span.Start

            let point = 
                match data.Column with
                | Some(col) -> 
                    let colLine = 
                        if col = 0 && data.MotionKind = MotionKind.Exclusive && SnapshotPointUtil.IsStartOfLine data.Span.End then
                            SnapshotPointUtil.GetContainingLine data.Span.End
                        else line
                    let _,endCol = colLine |> SnapshotLineUtil.GetEnd |> SnapshotPointUtil.GetLineColumn
                    if col < endCol then colLine.Start.Add(col)
                    else getPointFromSpan()
                | None -> getPointFromSpan()

            TextViewUtil.MoveCaretToPoint _textView point
            _operations.ResetSelection()
        member x.Beep () = if not _settings.GlobalSettings.VisualBell then _host.Beep()
        member x.OpenFold span count = 
            let regions = _outlining.GetCollapsedRegions(span) |> Seq.truncate count
            if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
            else  regions |> Seq.iter (fun x -> _outlining.Expand(x) |> ignore )
        member x.OpenAllFolds span =
            let regions = _outlining.GetCollapsedRegions(span) 
            if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
            else  regions |> Seq.iter (fun x -> _outlining.Expand(x) |> ignore )
        member x.CloseFold span count = 
            let pos = span |> SnapshotSpanUtil.GetStartPoint |> SnapshotPointUtil.GetPosition
            let temp = 
                _outlining.GetAllRegions(span) 
                |> Seq.filter (fun x -> not (x.IsCollapsed))
                |> Seq.map (fun x -> (TrackingSpanUtil.GetSpan _textView.TextSnapshot x.Extent) ,x)
                |> SeqUtil.filterToSome2
                |> Seq.sortBy (fun (span,_) -> pos - span.Start.Position )
                |> List.ofSeq
            let regions = temp  |> Seq.truncate count
            if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
            else regions |> Seq.iter (fun (_,x) -> _outlining.TryCollapse(x) |> ignore)
        member x.CloseAllFolds span =
            let regions = _outlining.GetAllRegions(span) 
            if Seq.isEmpty regions then _statusUtil.OnError Resources.Common_NoFoldFound
            else  regions |> Seq.iter (fun x -> _outlining.TryCollapse(x) |> ignore )
        member x.FoldLines count = 
            if count > 1 then 
                let caretLine = TextViewUtil.GetCaretLine _textView
                let span = SnapshotSpanUtil.ExtendDownIncludingLineBreak caretLine.Extent (count-1)
                _data.FoldManager.CreateFold span
        member x.DeleteOneFoldAtCursor () = 
            let point = TextViewUtil.GetCaretPoint _textView
            if not ( _data.FoldManager.DeleteFold point ) then
                _statusUtil.OnError Resources.Common_NoFoldFound
        member x.DeleteAllFoldsAtCursor () =
            let deleteAtCaret () = 
                let point = TextViewUtil.GetCaretPoint _textView
                _data.FoldManager.DeleteFold point
            if not (deleteAtCaret()) then
                _statusUtil.OnError Resources.Common_NoFoldFound
            else
                while deleteAtCaret() do
                    // Keep on deleteing 
                    ()
        member x.ChangeSpan (data:MotionData) reg =
            
            // For whatever reason the change commands will remove the trailing whitespace
            // for character wise motions
            let span = 
                if data.OperationKind = OperationKind.LineWise then data.OperationSpan
                else 
                    let point = 
                        data.OperationSpan
                        |> SnapshotSpanUtil.GetPointsBackward 
                        |> Seq.tryFind (fun x -> x.GetChar() |> CharUtil.IsWhiteSpace |> not)
                    match point with 
                    | Some(p) -> 
                        let endPoint = 
                            p
                            |> SnapshotPointUtil.TryAddOne 
                            |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint (p.Snapshot))
                        SnapshotSpan(data.OperationSpan.Start, endPoint)
                    | None -> data.OperationSpan
            x.DeleteSpan span data.MotionKind data.OperationKind reg |> ignore


