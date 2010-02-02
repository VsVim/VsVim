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
        Count=1;
        Register=_bufferData.RegisterMap.DefaultRegister;
    }

    let mutable _operationMap : Map<KeyInput,Operation> = Map.empty

    /// Function used to process the next piece of input
    let mutable _runFunc : NormalModeData -> KeyInput -> NormalModeResult = (fun _ _ -> NormalModeResult.Complete)

    /// Whether or not we are waiting for at least the second keystroke
    /// of a given command
    let mutable _waitingForMoreInput = false

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
                    
    member this.WaitForMotion ki (d:NormalModeData) doneFunc = 
        let rec f (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (span) -> 
                    _bufferData.VimHost.UpdateStatus(System.String.Empty)
                    doneFunc span
                | NeedMoreInput (moreFunc) ->
                    _bufferData.VimHost.UpdateStatus("Waiting for motion")
                    let inputFunc _ ki = f (moreFunc ki)
                    NormalModeResult.NeedMore2 inputFunc
                | InvalidMotion (msg,moreFunc) ->
                    _bufferData.VimHost.UpdateStatus(msg)
                    let inputFunc _ ki = f (moreFunc ki)
                    NormalModeResult.NeedMore2 inputFunc 
                | Error (msg) ->
                    _bufferData.VimHost.UpdateStatus(msg)
                    NormalModeResult.Complete
                | Cancel -> 
                    _bufferData.VimHost.UpdateStatus(System.String.Empty)
                    NormalModeResult.Complete
        f (MotionCapture.ProcessView _bufferData.TextView ki d.Count)
        
        
    // Respond to the d command.  Need the finish motion
    member this.WaitDelete =
        let inner (d:NormalModeData) (ki:KeyInput) =
            match ki.Key with 
                | Key.D -> 
                    let point = ViewUtil.GetCaretPoint _bufferData.TextView
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
                    let point = ViewUtil.GetCaretPoint _bufferData.TextView
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
                    let point = ViewUtil.GetCaretPoint _bufferData.TextView
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
            | true ->
                host.Beep()

    member private x.ReplaceChar (d:NormalModeData) = 
        let inner (d:NormalModeData) (ki:KeyInput) =
            if not (_operations.ReplaceChar ki d.Count) then
                _bufferData.VimHost.Beep()
            _bufferData.BlockCaret.Show()
            NormalModeResult.Complete
        _bufferData.BlockCaret.Hide()
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
        _bufferData.EditorOperations.ResetSelection()
        this.TextView.Caret.MoveTo(newCaret) |> ignore
        this.TextView.Caret.EnsureVisible()

    /// Handles commands which begin with g in normal mode.  This should be called when the g char is
    /// already processed
    member x.CharGCommand (d:NormalModeData) =
        let inner (d:NormalModeData) (ki:KeyInput) =  
            match ki.Char with
            | 'J' -> 
                let view = _bufferData.TextView
                let caret = ViewUtil.GetCaretPoint view
                _operations.Join caret Modes.JoinKind.KeepEmptySpaces d.Count |> ignore
            | 'p' -> _operations.PasteAfterCursor d.Register.StringValue 1 d.Register.Value.OperationKind true |> ignore
            | 'P' -> _operations.PasteBeforeCursor d.Register.StringValue 1 true |> ignore
            | '_' -> _bufferData.EditorOperations.MoveToLastNonWhiteSpaceCharacter(false)
            | _ ->
                _bufferData.VimHost.Beep()
                ()
            NormalModeResult.Complete
        NeedMore2(inner)

    /// Implement the commands associated with the z prefix in normal mode
    member x.CharZCommand (d:NormalModeData) =
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
            let res = _operations.JumpToMark ki.Char _bufferData.MarkMap _bufferData.VimHost
            match res with 
            | Modes.Failed(msg) -> _bufferData.VimHost.UpdateStatus(msg)
            | _ -> ()
            NormalModeResult.Complete
        NormalModeResult.NeedMore2 waitForKey

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.Mark (d:NormalModeData) =
        let waitForKey (d2:NormalModeData) (ki:KeyInput) =
            let cursor = ViewUtil.GetCaretPoint _bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> _bufferData.VimHost.Beep()
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
        _bufferData.EditorOperations.ResetSelection()
        runCount count

    member private this.BuildMotionOperationsMap =
        let wrap func = 
            fun (d:NormalModeData) -> 
                func d.Count
                _bufferData.EditorOperations.ResetSelection()
                NormalModeResult.Complete
        let factory = Vim.Modes.CommandFactory(_operations)
        factory.CreateMovementCommands() 
            |> Seq.map (fun (ki,com) -> {KeyInput=ki;RunFunc=(wrap com)})

    member this.BuildOperationsMap = 
        let waitOps = seq {
            yield (InputUtil.CharToKeyInput('d'), this.WaitDelete)
            yield (InputUtil.CharToKeyInput('y'), this.WaitYank)
            yield (InputUtil.CharToKeyInput('<'), this.ShiftLeft)
            yield (InputUtil.CharToKeyInput('>'), this.ShiftRight)
            yield (InputUtil.CharToKeyInput('c'), this.WaitChange)
        }

        let completeOps : seq<KeyInput * (int -> Register -> unit)> = seq {
            yield (InputUtil.CharToKeyInput('x'), (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
            yield (InputUtil.KeyToKeyInput(Key.Delete), (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
            yield (InputUtil.CharToKeyInput('X'),  (fun count reg -> _operations.DeleteCharacterBeforeCursor count reg))
            yield (InputUtil.CharToKeyInput('p'), (fun count reg -> _operations.PasteAfterCursor reg.StringValue count reg.Value.OperationKind false))
            yield (InputUtil.CharToKeyInput('P'), (fun count reg -> _operations.PasteBeforeCursor reg.StringValue count false))
            yield (InputUtil.CharToKeyInput('$'), (fun _ _ -> _bufferData.EditorOperations.MoveToEndOfLine(false)))
            yield (InputUtil.CharToKeyInput('^'), (fun _ _ -> _bufferData.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false)))
            yield (InputUtil.CharToKeyInput('0'), (fun _ _ -> _bufferData.EditorOperations.MoveToStartOfLine(false))) 
            yield (InputUtil.CharToKeyInput('n'), (fun count _ -> _incrementalSearch.FindNextMatch count))
            yield (InputUtil.CharToKeyInput('*'), (fun count _ -> this.FindNextWordUnderCursor count SearchKind.ForwardWithWrap))
            yield (InputUtil.CharToKeyInput('#'), (fun count _ -> this.FindNextWordUnderCursor count SearchKind.BackwardWithWrap))
            yield (InputUtil.CharToKeyInput('u'), (fun count _ -> _bufferData.VimHost.Undo this.TextBuffer count))
            yield (KeyInput('r', Key.R, ModifierKeys.Control), (fun count _ -> _bufferData.VimHost.Redo this.TextBuffer count))
            yield (InputUtil.KeyToKeyInput(Key.Enter), (fun _ _ -> this.MoveForEnter this.TextView _bufferData.VimHost))
            yield (KeyInput('u', Key.U, ModifierKeys.Control), (fun count _ -> this.ScrollCore ScrollDirection.Up count))
            yield (KeyInput('d', Key.D, ModifierKeys.Control), (fun count _ -> this.ScrollCore ScrollDirection.Down count))
            yield (KeyInput('J', Key.J, ModifierKeys.Shift),
                (fun count _-> 
                    let start = ViewUtil.GetCaretPoint this.TextView
                    let kind = Vim.Modes.JoinKind.RemoveEmptySpaces
                    let res = _operations.Join start kind count
                    if not res then
                        this.VimHost.Beep() ) )
            yield (KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control),
                (fun _ _ ->
                    match _operations.GoToDefinition this.VimHost with
                    | Vim.Modes.Succeeded -> ()
                    | Vim.Modes.Failed(msg) ->
                        this.VimHost.UpdateStatus(msg)
                        () ) )
            yield (InputUtil.CharToKeyInput('Y'), (fun count reg -> _operations.YankLines count reg))
        }

        let doNothing _ _ = ()
        let changeOpts = seq {
            yield (InputUtil.CharToKeyInput('i'), ModeKind.Insert, doNothing)
            yield (InputUtil.CharToKeyInput(':'), ModeKind.Command, doNothing)
            yield (InputUtil.CharToKeyInput('A'), ModeKind.Insert, (fun _ _ -> _bufferData.EditorOperations.MoveToEndOfLine(false)))
            yield (InputUtil.CharToKeyInput('o'), ModeKind.Insert, (fun _ _ -> _operations.InsertLineBelow() |> ignore))
            yield (InputUtil.CharToKeyInput('O'), ModeKind.Insert, (fun _ _ -> _operations.InsertLineAbove() |> ignore))
            yield (InputUtil.CharToKeyInput('v'), ModeKind.VisualCharacter, doNothing)
            yield (InputUtil.CharToKeyInput('V'), ModeKind.VisualLine, doNothing)
            yield (KeyInput('q', Key.Q, ModifierKeys.Control), ModeKind.VisualBlock, doNothing)
            yield (InputUtil.CharToKeyInput('s'), ModeKind.Insert,
                (fun count reg -> 
                    let tss = this.TextView.TextSnapshot
                    let start = ViewUtil.GetCaretPoint this.TextView
                    let endPoint = 
                        if start.Position + count > tss.Length then SnapshotPoint(tss, tss.Length)
                        else start.Add(count)
                    let span = SnapshotSpan(start,endPoint)
                    _operations.DeleteSpan span MotionKind.Exclusive OperationKind.CharacterWise reg |> ignore ))
            yield (InputUtil.CharToKeyInput('C'), ModeKind.Insert, 
                (fun count reg ->
                    let point = ViewUtil.GetCaretPoint this.TextView
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
                    let span = SnapshotSpan(point, span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.CharacterWise reg |> ignore))
            yield (InputUtil.CharToKeyInput('S'), ModeKind.Insert,
                (fun count reg ->
                    let point = ViewUtil.GetCaretPoint this.TextView
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore) )
        }

        let l : list<Operation> = [
            {   KeyInput=InputUtil.CharToKeyInput('/');
                RunFunc=(fun d -> this.BeginIncrementalSearch SearchKind.ForwardWithWrap) } 
            {   KeyInput=InputUtil.CharToKeyInput('?');
                RunFunc=(fun d -> this.BeginIncrementalSearch SearchKind.BackwardWithWrap) } 
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
            ]

        let l =
            (waitOps |> Seq.map (fun (ki,func) -> {KeyInput=ki;RunFunc=(fun _ -> NeedMore2(func)) }))
            |> Seq.append (completeOps |> Seq.map (fun (ki,func) -> {KeyInput=ki;RunFunc=(fun d -> func d.Count d.Register; NormalModeResult.Complete)}))
            |> Seq.append (changeOpts |> Seq.map (fun (ki,kind,func) -> {KeyInput=ki;RunFunc=(fun d -> func d.Count d.Register; NormalModeResult.SwitchMode kind)}))
            |> Seq.append (l |> Seq.ofList )
            |> Seq.append (this.BuildMotionOperationsMap)
            |> Seq.map (fun d -> d.KeyInput,d)
            |> Map.ofSeq
        l
   
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
        if ki.IsDigit && ki.Char <> '0' then this.GetCount d ki
        elif ki.Key = Key.OemQuotes && ki.ModifierKeys = ModifierKeys.Shift then 
            let f (d:NormalModeData) ki = this.GetRegister (_bufferData.RegisterMap) ki
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
                Register=_bufferData.RegisterMap.DefaultRegister }
        _runFunc <- this.StartCore
        _waitingForMoreInput <- false
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
            if _waitingForMoreInput then 
                true
            else if ki.IsDigit then
                true
            else
                _operationMap.ContainsKey ki

        member this.Process ki = 
            match _runFunc _data ki with
                | NormalModeResult.Complete -> 
                    this.ResetData
                    Processed
                | NeedMore2(f) ->
                    _runFunc <- f
                    _waitingForMoreInput <- true
                    Processed
                | NormalModeResult.SwitchMode (kind) -> 
                    this.ResetData // Make sure to reset information when switching modes
                    ProcessResult.SwitchMode kind
                | CountComplete (count,nextKi) ->
                    _data <- {_data with Count=count; }
                    _runFunc <- this.StartCore
                    (this :> IMode).Process nextKi
                | RegisterComplete (reg) ->     
                    _data <- {_data with Register=reg; }
                    _runFunc <- this.StartCore
                    _waitingForMoreInput <- false
                    Processed
        member this.OnEnter ()  =
            _bufferData.BlockCaret.Show()
            this.ResetData
        member this.OnLeave () = 
            _bufferData.BlockCaret.Hide()
    

