#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media


/// Operation in the normal mode
type internal Operation =  {
    KeyInput : Vim.KeyInput;
    RunFunc : NormalModeData -> NormalModeResult
}

type internal NormalMode
    ( 
        _bufferData : IVimBuffer, 
        _operations : IOperations,
        _searchReplace : ISearchReplace,
        _incrementalSearch : IIncrementalSearch ) = 


    let mutable _data = {
        VimBufferData=_bufferData; 
        RunFunc = (fun _ _ -> NormalModeResult.Complete);
        Count=1;
        Register=_bufferData.RegisterMap.DefaultRegister;
        WaitingForMoreInput=false;
    }

    let mutable _operationMap : Map<KeyInput,Operation> = Map.empty

    member this.TextView = _bufferData.TextView
    member this.TextBuffer = _bufferData.TextBuffer
    member this.VimHost = _bufferData.VimHost
    member this.CaretPoint = _bufferData.TextView.Caret.Position.BufferPosition
    member this.Settings = _bufferData.Settings
    member this.IncrementalSearch = _incrementalSearch
    
    /// Begin an incremental search.  Called when the user types / into the editor
    member this.BeginIncrementalSearch (kind:SearchKind) =
        let rec inner (_:NormalModeData) (ki:KeyInput) = 
            if _incrementalSearch.Process ki then NormalModeResult.Complete
            else NormalModeResult.NeedMore2 inner
        _incrementalSearch.Begin kind
        NeedMore2 inner
    
    member this.FindNextWordUnderCursor (count:int) (kind:SearchKind) =
        let point = ViewUtil.GetCaretPoint this.TextView
        if point.Position = point.Snapshot.Length || System.Char.IsWhiteSpace (point.GetChar()) then
            this.VimHost.UpdateStatus Resources.NormalMode_NoStringUnderCursor
        else
            match _searchReplace.FindNextWord point WordKind.NormalWord kind this.Settings.IgnoreCase with
            | None -> ()
            | Some(span) ->
                ViewUtil.MoveCaretToPoint this.TextView span.Start |> ignore
        NormalModeResult.Complete
                    
    member this.WaitForMotion ki (d:NormalModeData) doneFunc = 
        let rec f (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (span) -> 
                    d.VimBufferData.VimHost.UpdateStatus(System.String.Empty)
                    doneFunc span
                | NeedMoreInput (moreFunc) ->
                    d.VimBufferData.VimHost.UpdateStatus("Waiting for motion")
                    let inputFunc _ ki = f (moreFunc ki)
                    NormalModeResult.NeedMore ({d with RunFunc = inputFunc })
                | InvalidMotion (msg,moreFunc) ->
                    _bufferData.VimHost.UpdateStatus(msg)
                    let inputFunc _ ki = f (moreFunc ki)
                    NormalModeResult.NeedMore ({d with RunFunc = inputFunc})
                | Error (msg) ->
                    d.VimBufferData.VimHost.UpdateStatus(msg)
                    NormalModeResult.Complete
                | Cancel -> 
                    d.VimBufferData.VimHost.UpdateStatus(System.String.Empty)
                    NormalModeResult.Complete
        f (MotionCapture.ProcessView d.VimBufferData.TextView ki d.Count)
        
        
    // Respond to the d command.  Need the finish motion
    member this.WaitDelete =
        let inner (d:NormalModeData) (ki:KeyInput) =
            match ki.Key with 
                | Key.D -> 
                    let point = ViewUtil.GetCaretPoint d.VimBufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise d.Register |> ignore
                    NormalModeResult.Complete
                | _ -> 
                    let func (span,motionKind,opKind)= 
                        _operations.DeleteSpan span motionKind opKind d.Register |> ignore
                        NormalModeResult.Complete
                    this.WaitForMotion ki d func
        inner
        
    // Respond to the y command.  Need to wait for a complete motion
    member this.WaitYank =
        let inner (d:NormalModeData) (ki:KeyInput) =
            match ki.Key with 
                | Key.Y -> 
                    let point = ViewUtil.GetCaretPoint d.VimBufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count
                    _operations.Yank span MotionKind.Inclusive OperationKind.LineWise d.Register
                    NormalModeResult.Complete
                | _ ->
                    let inner (ss:SnapshotSpan,motionKind,opKind) = 
                        _operations.Yank ss motionKind opKind d.Register
                        NormalModeResult.Complete
                    this.WaitForMotion ki d inner
        inner 

    // Respond to the c command.  Need the finish motion
    member this.WaitChange =
        let inner (d:NormalModeData) (ki:KeyInput) =
            match ki.Key with 
                | Key.C -> 
                    let point = ViewUtil.GetCaretPoint d.VimBufferData.TextView
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count 
                    let span = SnapshotSpan(point.GetContainingLine().Start,span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise d.Register |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert
                | _ -> 
                    let func (span,motionKind,opKind)= 
                        _operations.DeleteSpan span motionKind opKind d.Register |> ignore
                        NormalModeResult.SwitchMode ModeKind.Insert
                    this.WaitForMotion ki d func
        inner

    /// Implement the < operator        
    member this.ShiftLeft =
        let inner (d:NormalModeData) (ki:KeyInput) =
            match ki.Char with 
                | '<' ->
                    let span = TssUtil.GetLineRangeSpan (this.CaretPoint.GetContainingLine().Start) d.Count
                    _operations.ShiftLeft span _bufferData.Settings.ShiftWidth |> ignore
                    NormalModeResult.Complete
                | _ ->
                    let inner2 (span:SnapshotSpan,_,_) =
                        _operations.ShiftLeft span _bufferData.Settings.ShiftWidth |> ignore                                          
                        NormalModeResult.Complete
                    this.WaitForMotion ki d inner2
        inner                                            
                    
    /// Implements the > operator.  Check for the special > motion key and then default
    /// back to a standard motion                    
    member this.ShiftRight =
        let inner (d:NormalModeData) (ki:KeyInput) =
            match ki.Char with
                | '>' ->
                    let span = TssUtil.GetLineRangeSpan (this.CaretPoint.GetContainingLine().Start) d.Count
                    _operations.ShiftRight span _bufferData.Settings.ShiftWidth |> ignore
                    NormalModeResult.Complete
                | _ ->
                    let inner2 (span:SnapshotSpan,_,_) =
                        _operations.ShiftRight span _bufferData.Settings.ShiftWidth |> ignore
                        NormalModeResult.Complete
                    this.WaitForMotion ki d inner2
        inner
                            
    member this.MoveEndOfLine (d:NormalModeData) =
        ViewUtil.MoveCaretToEndOfLine d.VimBufferData.TextView |> ignore
        NormalModeResult.Complete
    
    member this.MoveStartOfLine (d:NormalModeData) =
        ViewUtil.MoveCaretToBeginingOfLine d.VimBufferData.TextView |> ignore
        NormalModeResult.Complete
    
    /// Move the caret as a result of the user hitting enter.  The caret should jump to the
    /// start of the next line unless we are at the end of the buffer
    member this.MoveForEnter (view:ITextView) (host:IVimHost)=
        let tss = view.TextSnapshot
        let point = ViewUtil.GetCaretPoint view
        let last = TssUtil.GetLastLine tss
        match last.LineNumber = point.GetContainingLine().LineNumber with
            | false -> 
                let next = tss.GetLineFromLineNumber(point.GetContainingLine().LineNumber+1)
                ViewUtil.MoveCaretToPoint view (next.Start) |> ignore
                NormalModeResult.Complete
            | true ->
                host.Beep()
                NormalModeResult.Complete

    member private x.ReplaceChar (d:NormalModeData) = 
        let inner (d:NormalModeData) (ki:KeyInput) =
            if not (_operations.ReplaceChar ki d.Count) then
                _bufferData.VimHost.Beep()
            d.VimBufferData.BlockCaret.Show()
            NormalModeResult.Complete
        d.VimBufferData.BlockCaret.Hide()
        NeedMore2(inner)
        
    /// Core method for scrolling the editor up or down
    member this.ScrollCore dir count =
        let lines = VimSettingsUtil.GetScrollLineCount this.Settings this.TextView
        let tss = this.TextBuffer.CurrentSnapshot
        let curLine = this.CaretPoint.GetContainingLine().LineNumber
        let newLine = 
            match dir with
            | ScrollDirection.Down -> min (tss.LineCount - 1) (curLine + lines)
            | ScrollDirection.Up -> max (0) (curLine - lines)
            | _ -> failwith "Invalid enum value"
        let newCaret = tss.GetLineFromLineNumber(newLine).Start
        ViewUtil.ClearSelection this.TextView
        this.TextView.Caret.MoveTo(newCaret) |> ignore
        this.TextView.Caret.EnsureVisible()
        NormalModeResult.Complete

    /// Handles commands which begin with g in normal mode.  This should be called when the g char is
    /// already processed
    member x.CharGCommand (d:NormalModeData) =
        let data = d.VimBufferData
        let inner (d:NormalModeData) (ki:KeyInput) =  
            match ki.Char with
            | 'J' -> 
                let view = data.TextView
                let caret = ViewUtil.GetCaretPoint view
                _operations.Join caret Modes.JoinKind.KeepEmptySpaces d.Count |> ignore
            | 'p' -> _operations.PasteAfterCursor d.Register.StringValue 1 d.Register.Value.OperationKind true |> ignore
            | 'P' -> _operations.PasteBeforeCursor d.Register.StringValue 1 true |> ignore
            | _ ->
                d.VimBufferData.VimHost.Beep()
                ()
            NormalModeResult.Complete
        NeedMore2(inner)

    /// Implement the commands associated with the z prefix in normal mode
    member x.CharZCommand (d:NormalModeData) =
        let data = d.VimBufferData
        let inner (d:NormalModeData) (ki:KeyInput) =  
            if ki.IsNewLine then 
                _bufferData.EditorOperations.ScrollLineTop()
                _bufferData.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) 
            else
                match ki.Char with
                | 't' ->  _bufferData.EditorOperations.ScrollLineTop() 
                | '.' -> 
                    _bufferData.EditorOperations.ScrollLineCenter() 
                    _bufferData.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) 
                | 'z' -> _bufferData.EditorOperations.ScrollLineCenter() 
                | '-' -> 
                    _bufferData.EditorOperations.ScrollLineBottom() 
                    _bufferData.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) 
                | 'b' -> _bufferData.EditorOperations.ScrollLineBottom() 
                | _ -> _bufferData.VimHost.Beep()
            NormalModeResult.Complete
        NeedMore2(inner)

    member x.JumpToMark (d:NormalModeData) =
        let waitForKey (d:NormalModeData) (ki:KeyInput) =
            let bufferData = d.VimBufferData
            let res = _operations.JumpToMark ki.Char bufferData.MarkMap _bufferData.VimHost
            match res with 
            | Modes.Failed(msg) -> bufferData.VimHost.UpdateStatus(msg)
            | _ -> ()
            NormalModeResult.Complete
        NormalModeResult.NeedMore2 waitForKey

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.Mark (d:NormalModeData) =
        let waitForKey (d2:NormalModeData) (ki:KeyInput) =
            let bufferData = d2.VimBufferData
            let cursor = ViewUtil.GetCaretPoint bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> bufferData.VimHost.Beep()
            | _ -> ()
            NormalModeResult.Complete
        NormalModeResult.NeedMore2 waitForKey

    /// Complete the specified motion function            
    member this.MotionFunc view count func =
        let rec runCount count =
            func view |> ignore
            match count with
                | 1 -> NormalModeResult.Complete
                | _ -> runCount (count-1) 
        ViewUtil.ClearSelection view                
        runCount count

    member private this.BuildMotionOperationsMap =
        let wrap func = 
            fun (d:NormalModeData) -> 
                func d.Count
                ViewUtil.ClearSelection _bufferData.TextView
                NormalModeResult.Complete
        let factory = Vim.Modes.CommandFactory(_operations)
        factory.CreateMovementCommands() 
            |> Seq.map (fun (ki,com) -> {KeyInput=ki;RunFunc=(wrap com)})

    member this.BuildOperationsMap = 
        let deleteCharAtCursor (d:NormalModeData) = 
            _operations.DeleteCharacterAtCursor d.Count d.Register
            NormalModeResult.Complete

        let l : list<Operation> = [
            {   KeyInput=InputUtil.CharToKeyInput('x');
                RunFunc=deleteCharAtCursor; }
            {   KeyInput=InputUtil.KeyToKeyInput(Key.Delete);
                RunFunc=deleteCharAtCursor; }
            {   KeyInput=InputUtil.CharToKeyInput('X');
                RunFunc=(fun d -> 
                    _operations.DeleteCharacterBeforeCursor d.Count d.Register
                    NormalModeResult.Complete); };
            {   KeyInput=InputUtil.CharToKeyInput('d');
                RunFunc=(fun d -> NeedMore2(this.WaitDelete)) };
            {   KeyInput=InputUtil.CharToKeyInput('y');
                RunFunc=(fun d -> NeedMore2(this.WaitYank)); };
            {   KeyInput=InputUtil.CharToKeyInput('<');
                RunFunc=(fun d -> NeedMore2(this.ShiftLeft)); };
            {   KeyInput=InputUtil.CharToKeyInput('>');
                RunFunc=(fun d -> NeedMore2(this.ShiftRight)); };
            {   KeyInput=InputUtil.CharToKeyInput('p');
                RunFunc=(fun d -> 
                            _operations.PasteAfterCursor d.Register.StringValue d.Count d.Register.Value.OperationKind false
                            NormalModeResult.Complete); };
            {   KeyInput=InputUtil.CharToKeyInput('P');
                RunFunc=(fun d -> 
                            _operations.PasteBeforeCursor d.Register.StringValue d.Count false
                            NormalModeResult.Complete); };
            {   KeyInput=InputUtil.CharToKeyInput('$');
                RunFunc=(fun d -> this.MoveEndOfLine d) };
            {   KeyInput=InputUtil.CharToKeyInput('^');
                RunFunc=(fun d -> this.MoveStartOfLine d) };
            {   KeyInput=InputUtil.CharToKeyInput('/');
                RunFunc=(fun d -> this.BeginIncrementalSearch SearchKind.ForwardWithWrap) };
            {   KeyInput=InputUtil.CharToKeyInput('?');
                RunFunc=(fun d -> this.BeginIncrementalSearch SearchKind.BackwardWithWrap) };
            {   KeyInput=InputUtil.CharToKeyInput('n');
                RunFunc=(fun d -> 
                            _incrementalSearch.FindNextMatch d.Count
                            NormalModeResult.Complete ) };
            {   KeyInput=InputUtil.CharToKeyInput('*');
                RunFunc=(fun d -> this.FindNextWordUnderCursor d.Count SearchKind.ForwardWithWrap) };
            {   KeyInput=InputUtil.CharToKeyInput('#');
                RunFunc=(fun d -> this.FindNextWordUnderCursor d.Count SearchKind.BackwardWithWrap) };
            {   KeyInput=InputUtil.CharToKeyInput('i');
                RunFunc=(fun _ -> NormalModeResult.SwitchMode (ModeKind.Insert)) };
            {   KeyInput=InputUtil.CharToKeyInput(':');
                RunFunc=(fun _ -> NormalModeResult.SwitchMode (ModeKind.Command)); };
            {   KeyInput=InputUtil.CharToKeyInput('A');
                RunFunc=(fun d ->
                    ViewUtil.MoveCaretToEndOfLine (d.VimBufferData.TextView) |> ignore  
                    NormalModeResult.SwitchMode (ModeKind.Insert)) };
            {   KeyInput=InputUtil.CharToKeyInput('u');
                RunFunc=(fun d -> 
                    this.VimHost.Undo this.TextBuffer d.Count
                    NormalModeResult.Complete) };
            {   KeyInput=InputUtil.CharToKeyInput('o');
                RunFunc=(fun d -> 
                    _operations.InsertLineBelow() |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert); }
            {   KeyInput=InputUtil.CharToKeyInput('O');
                RunFunc=(fun d -> 
                    _operations.InsertLineAbove() |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert); };
            {   KeyInput=InputUtil.KeyToKeyInput(Key.Enter);
                RunFunc=(fun d -> this.MoveForEnter this.TextView d.VimBufferData.VimHost) };
            {   KeyInput=KeyInput('u', Key.U, ModifierKeys.Control);
                RunFunc=(fun d -> this.ScrollCore ScrollDirection.Up d.Count) };
            {   KeyInput=KeyInput('d', Key.D, ModifierKeys.Control);
                RunFunc=(fun d -> this.ScrollCore ScrollDirection.Down d.Count) };
            {   KeyInput=KeyInput('J', Key.J, ModifierKeys.Shift);
                RunFunc=(fun d -> 
                    let start = ViewUtil.GetCaretPoint this.TextView
                    let kind = Vim.Modes.JoinKind.RemoveEmptySpaces
                    let res = _operations.Join start kind d.Count
                    if not res then
                        this.VimHost.Beep()
                    NormalModeResult.Complete) };
            {   KeyInput=KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control);
                RunFunc=(fun d ->
                    match _operations.GoToDefinition this.VimHost with
                    | Vim.Modes.Succeeded -> ()
                    | Vim.Modes.Failed(msg) ->
                        this.VimHost.UpdateStatus(msg)
                        ()
                    NormalModeResult.Complete) };
            {   KeyInput=InputUtil.CharToKeyInput('m');
                RunFunc=this.Mark };
            {   KeyInput=InputUtil.CharToKeyInput('\'');
                RunFunc=this.JumpToMark };
            {   KeyInput=InputUtil.CharToKeyInput('`');
                RunFunc=this.JumpToMark };
            {   KeyInput=InputUtil.CharToKeyInput('g');
                RunFunc=this.CharGCommand };
            {   KeyInput=InputUtil.CharToKeyInput('z');
                RunFunc=this.CharZCommand; };
            {   KeyInput=InputUtil.CharToKeyInput('r');
                RunFunc=this.ReplaceChar; }
            {   KeyInput=InputUtil.CharToKeyInput('Y');
                RunFunc=(fun d -> 
                        _operations.YankLines d.Count d.Register 
                        NormalModeResult.Complete); }
            {   KeyInput=InputUtil.CharToKeyInput('v');
                RunFunc=(fun _ -> NormalModeResult.SwitchMode ModeKind.VisualCharacter); }
            {   KeyInput=InputUtil.CharToKeyInput('V');
                RunFunc=(fun _ -> NormalModeResult.SwitchMode ModeKind.VisualLine); }
            {   KeyInput=KeyInput('q', Key.Q, ModifierKeys.Control);
                RunFunc=(fun _ -> NormalModeResult.SwitchMode ModeKind.VisualBlock) }
            {   KeyInput=InputUtil.CharToKeyInput('c');
                RunFunc=(fun d -> NeedMore2(this.WaitChange)); };
            {   KeyInput=InputUtil.CharToKeyInput('s');
                RunFunc=(fun d -> 
                    let tss = this.TextView.TextSnapshot
                    let start = ViewUtil.GetCaretPoint this.TextView
                    let endPoint = 
                        if start.Position + d.Count > tss.Length then SnapshotPoint(tss, tss.Length)
                        else start.Add(d.Count)
                    let span = SnapshotSpan(start,endPoint)
                    _operations.DeleteSpan span MotionKind.Exclusive OperationKind.CharacterWise d.Register |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert) }
            {   KeyInput=InputUtil.CharToKeyInput('C');
                RunFunc=(fun d ->
                    let point = ViewUtil.GetCaretPoint this.TextView
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count
                    let span = SnapshotSpan(point, span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.CharacterWise d.Register |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert) }
            {   KeyInput=InputUtil.CharToKeyInput('S');
                RunFunc=(fun d ->
                    let point = ViewUtil.GetCaretPoint this.TextView
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point d.Count
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise d.Register |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert) }
            ]
        let l = l @ (this.BuildMotionOperationsMap |> List.ofSeq)
        l |> List.map (fun d -> d.KeyInput,d) |> Map.ofList

   
    /// Repsonible for getting the count                
    member this.GetCount (d:NormalModeData) (ki:KeyInput) = 
        let rec inner ki (func: KeyInput -> CountResult) = 
            match func ki with
                | CountResult.Complete (count,nextKi) ->
                    CountComplete(count, nextKi)
                | CountResult.NeedMore(f) ->
                    let nextFunc _ ki2 = inner ki2 f
                    NeedMore2(nextFunc)
        inner ki (CountCapture.Process)                    
        
    /// Responsible for getting the register 
    member this.GetRegister (m:IRegisterMap) (ki:KeyInput) =
        let c = InputUtil.KeyInputToChar ki
        let reg = m.GetRegister c
        RegisterComplete(reg)

    member this.StartCore (d:NormalModeData) (ki:KeyInput) =
        if ki.IsDigit then this.GetCount d ki
        elif ki.Key = Key.OemQuotes && ki.ModifierKeys = ModifierKeys.Shift then 
            let f (d:NormalModeData) ki = this.GetRegister (d.VimBufferData.RegisterMap) ki
            NeedMore2(f)
        else
            match Map.tryFind ki _operationMap with
            | Some op -> op.RunFunc(d)
            | None -> 
                this.VimHost.Beep()
                NormalModeResult.Complete

    /// Reset the internal data for the NormalMode instance
    member this.ResetData = 
        _data <- {
            _data with 
                Count=1; 
                RunFunc=this.StartCore;
                WaitingForMoreInput=false;
                Register=_bufferData.RegisterMap.DefaultRegister }
        if _operationMap.Count = 0 then
            _operationMap <- this.BuildOperationsMap

    member this.Register = _data.Register
    member this.Count = _data.Count
    
    interface IMode with 
        member this.VimBuffer = _bufferData
        member this.Commands = 
            _operationMap
                |> Map.toSeq
                |> Seq.map (fun (k,v) -> k)

        member this.ModeKind = ModeKind.Normal
        member this.CanProcess (ki:KeyInput) =
            if _data.WaitingForMoreInput then
                true
            else if ki.IsDigit then
                true
            else
                _operationMap.ContainsKey ki

        member this.Process ki = 
            match _data.RunFunc _data ki with
                | NormalModeResult.Complete -> 
                    this.ResetData
                    Processed
                | NormalModeResult.NeedMore(d) -> 
                    _data <- { d with WaitingForMoreInput=true }
                    Processed
                | NeedMore2(f) ->
                    _data <- {_data with RunFunc=f;WaitingForMoreInput=true}
                    Processed
                | NormalModeResult.SwitchMode (kind) -> 
                    this.ResetData // Make sure to reset information when switching modes
                    ProcessResult.SwitchMode kind
                | CountComplete (count,nextKi) ->
                    _data <- {_data with Count=count; RunFunc=this.StartCore}
                    (this :> IMode).Process nextKi
                | RegisterComplete (reg) ->     
                    _data <- {_data with Register=reg; RunFunc=this.StartCore;  WaitingForMoreInput=false }
                    Processed
        member this.OnEnter ()  =
            _bufferData.BlockCaret.Show()
            this.ResetData
        member this.OnLeave () = 
            _bufferData.BlockCaret.Hide()
    

