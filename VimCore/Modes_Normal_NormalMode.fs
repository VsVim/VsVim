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
    RunFunc : int -> Register -> NormalModeResult
}

type internal NormalMode 
    ( 
        _bufferData : IVimBuffer, 
        _operations : IOperations,
        _searchReplace : ISearchReplace,
        _incrementalSearch : IIncrementalSearch ) = 


    /// Command specific data (count,register)
    let mutable _data = (1,_bufferData.RegisterMap.DefaultRegister)

    let mutable _operationMap : Map<KeyInput,Operation> = Map.empty

    /// Function used to process the next piece of input
    let mutable _runFunc : KeyInput -> int -> Register -> NormalModeResult = (fun _ _ _ -> NormalModeResult.Complete)

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
        let rec inner (ki:KeyInput) _ _ = 
            if _incrementalSearch.Process ki then NormalModeResult.Complete
            else NormalModeResult.NeedMore2 inner
        _incrementalSearch.Begin kind
        inner
    
    member this.WaitForMotion ki count doneFunc = 
        let rec f (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (span) -> 
                    _bufferData.VimHost.UpdateStatus(System.String.Empty)
                    doneFunc span
                | NeedMoreInput (moreFunc) ->
                    _bufferData.VimHost.UpdateStatus("Waiting for motion")
                    let inputFunc ki _ _ = f (moreFunc ki)
                    NormalModeResult.NeedMore2 inputFunc
                | InvalidMotion (msg,moreFunc) ->
                    _bufferData.VimHost.UpdateStatus(msg)
                    let inputFunc ki _ _ = f (moreFunc ki)
                    NormalModeResult.NeedMore2 inputFunc 
                | Error (msg) ->
                    _bufferData.VimHost.UpdateStatus(msg)
                    NormalModeResult.Complete
                | Cancel -> 
                    _bufferData.VimHost.UpdateStatus(System.String.Empty)
                    NormalModeResult.Complete
        f (MotionCapture.ProcessView _bufferData.TextView ki count)
        
        
    // Respond to the d command.  Need the finish motion
    member this.WaitDelete =
        let inner (ki:KeyInput) count reg =
            match ki.Key with 
                | Key.D -> 
                    let point = ViewUtil.GetCaretPoint _bufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore
                    NormalModeResult.Complete
                | _ -> 
                    let func (span,motionKind,opKind)= 
                        _operations.DeleteSpan span motionKind opKind reg |> ignore
                        NormalModeResult.Complete
                    this.WaitForMotion ki count func
        inner
        
    // Respond to the y command.  Need to wait for a complete motion
    member this.WaitYank =
        let inner (ki:KeyInput) count reg =
            match ki.Key with 
                | Key.Y -> 
                    let point = ViewUtil.GetCaretPoint _bufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
                    _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg 
                    NormalModeResult.Complete
                | _ ->
                    let inner (ss:SnapshotSpan,motionKind,opKind) = 
                        _operations.Yank ss motionKind opKind reg 
                        NormalModeResult.Complete
                    this.WaitForMotion ki count inner
        inner 

    // Respond to the c command.  Need the finish motion
    member this.WaitChange =
        let inner (ki:KeyInput) count reg =
            match ki.Key with 
                | Key.C -> 
                    let point = ViewUtil.GetCaretPoint _bufferData.TextView
                    let span = TssUtil.GetLineRangeSpanIncludingLineBreak point count
                    let span = SnapshotSpan(point.GetContainingLine().Start,span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert
                | _ -> 
                    let func (span,motionKind,opKind)= 
                        _operations.DeleteSpan span motionKind opKind reg |> ignore
                        NormalModeResult.SwitchMode ModeKind.Insert
                    this.WaitForMotion ki count func
        inner

    /// Implement the < operator        
    member this.ShiftLeft =
        let inner (ki:KeyInput) count reg =
            match ki.Char with 
                | '<' ->
                    let span = TssUtil.GetLineRangeSpan (this.CaretPoint.GetContainingLine().Start) count
                    _operations.ShiftLeft span _bufferData.Settings.ShiftWidth |> ignore
                    NormalModeResult.Complete
                | _ ->
                    let inner2 (span:SnapshotSpan,_,_) =
                        _operations.ShiftLeft span _bufferData.Settings.ShiftWidth |> ignore                                          
                        NormalModeResult.Complete
                    this.WaitForMotion ki count inner2
        inner                                            
                    
    /// Implements the > operator.  Check for the special > motion key and then default
    /// back to a standard motion                    
    member this.ShiftRight =
        let inner (ki:KeyInput) count reg =
            match ki.Char with
                | '>' ->
                    let span = TssUtil.GetLineRangeSpan (this.CaretPoint.GetContainingLine().Start) count
                    _operations.ShiftRight span _bufferData.Settings.ShiftWidth |> ignore
                    NormalModeResult.Complete
                | _ ->
                    let inner2 (span:SnapshotSpan,_,_) =
                        _operations.ShiftRight span _bufferData.Settings.ShiftWidth |> ignore
                        NormalModeResult.Complete
                    this.WaitForMotion ki count inner2
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

    member private x.ReplaceChar = 
        let inner (ki:KeyInput) count reg =
            if not (_operations.ReplaceChar ki count) then
                _bufferData.VimHost.Beep()
            _bufferData.BlockCaret.Show()
            NormalModeResult.Complete
        _bufferData.BlockCaret.Hide()
        inner
        
    /// Handles commands which begin with g in normal mode.  This should be called when the g char is
    /// already processed
    member x.CharGCommand =
        let inner (ki:KeyInput) count (reg:Register) =  
            match ki.Char with
            | 'J' -> 
                let view = _bufferData.TextView
                let caret = ViewUtil.GetCaretPoint view
                _operations.Join caret Modes.JoinKind.KeepEmptySpaces count |> ignore
            | 'p' -> _operations.PasteAfterCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore
            | 'P' -> _operations.PasteBeforeCursor reg.StringValue 1 true |> ignore
            | '_' -> _bufferData.EditorOperations.MoveToLastNonWhiteSpaceCharacter(false)
            | _ ->
                _bufferData.VimHost.Beep()
                ()
            NormalModeResult.Complete
        inner

    /// Implement the commands associated with the z prefix in normal mode
    member x.CharZCommand = 
        let inner (ki:KeyInput) coun reg =  
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
        inner

    member x.JumpToMark =
        let waitForKey (ki:KeyInput) _ _ =
            let res = _operations.JumpToMark ki.Char _bufferData.MarkMap _bufferData.VimHost
            match res with 
            | Modes.Failed(msg) -> _bufferData.VimHost.UpdateStatus(msg)
            | _ -> ()
            NormalModeResult.Complete
        waitForKey

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.Mark = 
        let waitForKey (ki:KeyInput) _ _ =
            let cursor = ViewUtil.GetCaretPoint _bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> _bufferData.VimHost.Beep()
            | _ -> ()
            NormalModeResult.Complete
        waitForKey

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
            fun count _ -> 
                func count
                _bufferData.EditorOperations.ResetSelection()
                NormalModeResult.Complete
        let factory = Vim.Modes.CommandFactory(_operations)
        factory.CreateMovementCommands() 
            |> Seq.map (fun (ki,com) -> {KeyInput=ki;RunFunc=(wrap com)})

    member this.BuildOperationsMap = 
        let waitOps = seq {
            yield (InputUtil.CharToKeyInput('d'), (fun () -> this.WaitDelete))
            yield (InputUtil.CharToKeyInput('y'), (fun () -> this.WaitYank))
            yield (InputUtil.CharToKeyInput('<'), (fun () -> this.ShiftLeft))
            yield (InputUtil.CharToKeyInput('>'), (fun () -> this.ShiftRight))
            yield (InputUtil.CharToKeyInput('c'), (fun () -> this.WaitChange))
            yield (InputUtil.CharToKeyInput('/'), (fun () -> this.BeginIncrementalSearch SearchKind.ForwardWithWrap))
            yield (InputUtil.CharToKeyInput('?'), (fun () -> this.BeginIncrementalSearch SearchKind.BackwardWithWrap))
            yield (InputUtil.CharToKeyInput('m'), (fun () -> this.Mark))
            yield (InputUtil.CharToKeyInput('\''), (fun () -> this.JumpToMark))
            yield (InputUtil.CharToKeyInput('`'), (fun () -> this.JumpToMark))
            yield (InputUtil.CharToKeyInput('g'), (fun () -> this.CharGCommand))
            yield (InputUtil.CharToKeyInput('z'), (fun () -> this.CharZCommand))
            yield (InputUtil.CharToKeyInput('r'), (fun () -> this.ReplaceChar))
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
            yield (InputUtil.CharToKeyInput('*'), (fun count _ -> _operations.MoveToNextOccuranceOfWordAtCursor true count))
            yield (InputUtil.CharToKeyInput('#'), (fun count _ -> _operations.MoveToPreviousOccuranceOfWordAtCursor true count))
            yield (InputUtil.CharToKeyInput('u'), (fun count _ -> _bufferData.VimHost.Undo this.TextBuffer count))
            yield (KeyInput('r', Key.R, ModifierKeys.Control), (fun count _ -> _bufferData.VimHost.Redo this.TextBuffer count))
            yield (InputUtil.KeyToKeyInput(Key.Enter), (fun _ _ -> this.MoveForEnter this.TextView _bufferData.VimHost))
            yield (KeyInput('u', Key.U, ModifierKeys.Control), (fun count _ -> _operations.Scroll ScrollDirection.Up count))
            yield (KeyInput('d', Key.D, ModifierKeys.Control), (fun count _ -> _operations.Scroll ScrollDirection.Down count))
            yield (KeyInput('J', Key.J, ModifierKeys.Shift), (fun count _ -> _operations.JoinAtCaret count))
            yield (KeyInput(']', Key.OemCloseBrackets, ModifierKeys.Control), (fun _ _ -> _operations.GoToDefinitionWrapper()))
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
            yield (InputUtil.CharToKeyInput('s'), ModeKind.Insert, (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
            yield (InputUtil.CharToKeyInput('C'), ModeKind.Insert, (fun count reg -> _operations.DeleteLinesFromCursor count reg))
            yield (InputUtil.CharToKeyInput('S'), ModeKind.Insert, (fun count reg -> _operations.DeleteLines count reg))
        }

        let l =
            (waitOps |> Seq.map (fun (ki,func) -> {KeyInput=ki;RunFunc=(fun _ _ -> NeedMore2(func())) }))
            |> Seq.append (completeOps |> Seq.map (fun (ki,func) -> {KeyInput=ki;RunFunc=(fun count reg -> func count reg; NormalModeResult.Complete)}))
            |> Seq.append (changeOpts |> Seq.map (fun (ki,kind,func) -> {KeyInput=ki;RunFunc=(fun count reg -> func count reg; NormalModeResult.SwitchMode kind)}))
            |> Seq.append (this.BuildMotionOperationsMap)
            |> Seq.map (fun d -> d.KeyInput,d)
            |> Map.ofSeq
        l
   
    /// Repsonible for getting the count                
    member this.GetCount (ki:KeyInput) = 
        let rec inner ki (func: KeyInput -> CountResult) = 
            match func ki with
                | CountResult.Complete (count,nextKi) ->
                    CountComplete(count, nextKi)
                | CountResult.NeedMore(f) ->
                    let nextFunc ki2 _ _ = inner ki2 f
                    NeedMore2(nextFunc)
        inner ki (CountCapture.Process)                    
        
    /// Responsible for getting the register 
    member this.GetRegister (m:IRegisterMap) (ki:KeyInput) =
        let c = InputUtil.KeyInputToChar ki
        let reg = m.GetRegister c
        RegisterComplete(reg)

    member this.StartCore (ki:KeyInput) count reg =
        if ki.IsDigit && ki.Char <> '0' then this.GetCount ki
        elif ki.Key = Key.OemQuotes && ki.ModifierKeys = ModifierKeys.Shift then 
            let f ki _ _ = this.GetRegister (_bufferData.RegisterMap) ki
            NeedMore2(f)
        else
            match Map.tryFind ki _operationMap with
            | Some op -> op.RunFunc count reg
            | None -> 
                this.VimHost.Beep()
                NormalModeResult.Complete

    /// Reset the internal data for the NormalMode instance
    member this.ResetData = 
        _data <- (1, _bufferData.RegisterMap.DefaultRegister)
        _runFunc <- this.StartCore
        _waitingForMoreInput <- false
        if _operationMap.Count = 0 then
            _operationMap <- this.BuildOperationsMap

    member this.Register = 
        let _,reg = _data
        reg
    member this.Count = 
        let count,_ = _data
        count

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
            match _runFunc ki this.Count this.Register with
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
                    let _,reg = _data
                    _data <- (count,reg)
                    _runFunc <- this.StartCore
                    (this :> IMode).Process nextKi
                | RegisterComplete (reg) ->     
                    let count,_ = _data
                    _data <- (count,reg)
                    _runFunc <- this.StartCore
                    _waitingForMoreInput <- false
                    Processed
        member this.OnEnter ()  =
            _bufferData.BlockCaret.Show()
            this.ResetData
        member this.OnLeave () = 
            _bufferData.BlockCaret.Hide()
    

