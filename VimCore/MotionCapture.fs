#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

module internal MotionCapture = 

    let NeedMoreInputWithEscape func =
        let inner (ki:KeyInput) = 
            if ki.Key = VimKey.EscapeKey then Cancel
            else func(ki)
        MotionResult.NeedMoreInput(inner)

    /// When an invalid motion is given just wait for enter and then report and invalid 
    /// motion error.  Update the status to let the user know that we are currently
    /// in an invalid state
    let HitInvalidMotion =
        let rec inner (ki:KeyInput) =
            match ki.Key with 
            | VimKey.EscapeKey -> Cancel
            | VimKey.EnterKey -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | _ -> InvalidMotion("Invalid Motion", inner)
        InvalidMotion("Invalid Motion",inner)

    let private ForwardCharMotionCore start count func = 
        let inner (ki:KeyInput) =
            match func start ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(point:SnapshotPoint) -> 
                let span = SnapshotSpan(start, point.Add(1))
                let data = {Span=span; IsForward=true; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Inclusive; Column=None}
                Complete data
        NeedMoreInputWithEscape inner

    let private BackwardCharMotionCore (start:SnapshotPoint) count func = 
        let inner (ki:KeyInput) =
            match func start ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(point:SnapshotPoint) ->
                let span = SnapshotSpan(point, start)
                let data = {Span=span; IsForward=false; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Exclusive; Column=None}
                Complete data
        NeedMoreInputWithEscape inner

    /// Handle the 'f' motion.  Forward to the next occurrence of the specified character on
    /// this line
    let private ForwardCharMotion start count = ForwardCharMotionCore start count TssUtil.FindNextOccurranceOfCharOnLine

    /// Handle the 't' motion.  Forward till the next occurrence of the specified character on
    /// this line
    let private ForwardTillCharMotion start count = ForwardCharMotionCore start count TssUtil.FindTillNextOccurranceOfCharOnLine

    /// Handle the 'F' motion.  Backward to the previous occurrence of the specified character
    /// on this line
    let private BackwardCharMotion start count = BackwardCharMotionCore start count TssUtil.FindPreviousOccurranceOfCharOnLine

    /// Handle the 'T' motion.  Backward till to the previous occurrence of the specified character
    let private BackwardTillCharMotion start count = BackwardCharMotionCore start count TssUtil.FindTillPreviousOccurranceOfCharOnLine
        
    /// Implement the w/W motion
    let private WordMotionForward start count kind =
        let endPoint = TssUtil.FindNextWordStart start count kind  
        let span = SnapshotSpan(start,endPoint)
        {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}

    /// Implement the b/B motion
    let private WordMotionBackward start count kind =
        let startPoint = TssUtil.FindPreviousWordStart start count kind
        let span = SnapshotSpan(startPoint,start)
        {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        
    /// Implement the aw motion.  This is called once the a key is seen.
    let private AllWordMotion start count =        
        let func kind = 
            let start = match TssUtil.FindCurrentFullWordSpan start kind with 
                            | Some (s) -> s.Start
                            | None -> start
            WordMotionForward start count kind 
        let inner (ki:KeyInput) = 
            match ki.Char with
                | 'w' -> func WordKind.NormalWord |> Complete
                | 'W' -> func WordKind.BigWord |> Complete
                | _ -> HitInvalidMotion
        NeedMoreInputWithEscape inner

    /// Implement the 'e' motion.  This goes to the end of the current word.  If we're
    /// not currently on a word it will find the next word and then go to the end of that
    let private EndOfWordMotion (start:SnapshotPoint) count kind =
        let snapshotEnd = SnapshotUtil.GetEndPoint start.Snapshot
        let rec inner start count = 
            if count <= 0 || start = snapshotEnd then start
            else

                // Move start to the first word if we're currently on whitespace
                let start = 
                    if System.Char.IsWhiteSpace(start.GetChar()) then TssUtil.FindNextWordStart start 1 kind 
                    else start

                if start = snapshotEnd then snapshotEnd
                else
                    // Get the span of the current word and the end completes the motion
                    match TssUtil.FindCurrentFullWordSpan start kind with
                    | None -> SnapshotUtil.GetEndPoint start.Snapshot
                    | Some(s) -> inner s.End (count-1)

        let endPoint = inner start count
        let span = SnapshotSpan(start,endPoint)
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    let private EndOfLineMotion (start:SnapshotPoint) count = 
        let span = SnapshotPointUtil.GetLineRangeSpan start count
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}

    /// Find the first non-whitespace character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward
    let private FirstNonWhitespaceOnLine (start:SnapshotPoint) =
        let line = start.GetContainingLine()
        let found = SnapshotLineUtil.GetPoints line
                        |> Seq.filter (fun x -> x.Position < start.Position)
                        |> Seq.tryFind (fun x-> x.GetChar() <> ' ')
        let span = match found with 
                    | Some p -> new SnapshotSpan(p, start)
                    | None -> new SnapshotSpan(start,0)
        {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} 

    /// Move to the begining of the line.  Interestingly since this command is bound to the '0' it 
    /// can't be associated with a count.  Doing a command like 30 binds as count 30 vs. count 3 
    /// for command '0'
    let private BeginingOfLineMotion start =
        let line = SnapshotPointUtil.GetContainingLine start
        let span = SnapshotSpan(line.Start, start)
        {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}

    /// Handle the lines down to first non-whitespace motion
    let private LineDownToFirstNonWhitespace start count =
        let line = SnapshotPointUtil.GetContainingLine start
        let number = line.LineNumber + count
        let endLine = SnapshotUtil.GetValidLineOrLast line.Snapshot number
        let point = TssUtil.FindFirstNonWhitespaceCharacter endLine
        let span = SnapshotSpan(start, point.Add(1)) // Add 1 since it's inclusive
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None}

    let private CharLeftMotion start count = 
        let prev = SnapshotPointUtil.GetPreviousPointOnLine start count 
        if prev = start then None
        else {Span=SnapshotSpan(prev,start); IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} |> Some

    let private CharRightMotion start count =
        let next = SnapshotPointUtil.GetNextPointOnLine start count 
        if next = start then None
        else {Span=SnapshotSpan(start,next); IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None } |> Some

    /// Get the span of "count" lines upward careful not to run off the beginning of the
    /// buffer.  Implementation of the "k" motion
    let private LineUpMotion point count =     
        let endLine = SnapshotPointUtil.GetContainingLine point
        let startLineNumber = max 0 (endLine.LineNumber - count)
        let startLine = SnapshotUtil.GetLine endLine.Snapshot startLineNumber
        let span = SnapshotSpan(startLine.Start, endLine.End)
        {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } |> Some

    /// Get the span of "count" lines downward careful not to run off the end of the
    /// buffer.  Implementation of the "j" motion
    let private LineDownMotion point count = 
        let startLine = SnapshotPointUtil.GetContainingLine point
        let endLineNumber = startLine.LineNumber + count
        let endLine = SnapshotUtil.GetValidLineOrLast startLine.Snapshot endLineNumber
        let span = SnapshotSpan(startLine.Start, endLine.End)            
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } |> Some

    let SimpleMotions =  
        seq { 
            yield (InputUtil.CharToKeyInput 'w', fun start count -> WordMotionForward start count WordKind.NormalWord |> Some)
            yield (InputUtil.CharToKeyInput 'W', fun start count -> WordMotionForward start count WordKind.BigWord |> Some)
            yield (InputUtil.CharToKeyInput 'b', fun start count -> WordMotionBackward start count WordKind.NormalWord |> Some)
            yield (InputUtil.CharToKeyInput 'B', fun start count -> WordMotionBackward start count WordKind.BigWord |> Some)
            yield (InputUtil.CharToKeyInput '$', fun start count -> EndOfLineMotion start count |> Some)
            yield (InputUtil.CharToKeyInput '^', fun start count -> FirstNonWhitespaceOnLine start |> Some)
            yield (InputUtil.CharToKeyInput '0', fun start count -> BeginingOfLineMotion start |> Some)
            yield (InputUtil.CharToKeyInput 'e', fun start count -> EndOfWordMotion start count WordKind.NormalWord |> Some)
            yield (InputUtil.CharToKeyInput 'E', fun start count -> EndOfWordMotion start count WordKind.BigWord |> Some)
            yield (InputUtil.CharToKeyInput 'h', fun start count -> CharLeftMotion start count )
            yield (InputUtil.VimKeyToKeyInput VimKey.LeftKey, fun start count -> CharLeftMotion start count)
            yield (InputUtil.VimKeyToKeyInput VimKey.BackKey, fun start count -> CharLeftMotion start count)
            yield (InputUtil.CharAndModifiersToKeyInput 'h' KeyModifiers.Control, fun start count -> CharLeftMotion start count)
            yield (InputUtil.CharToKeyInput 'l', fun start count -> CharRightMotion start count )
            yield (InputUtil.VimKeyToKeyInput VimKey.RightKey, fun start count -> CharRightMotion start count)
            yield (InputUtil.CharToKeyInput ' ', fun start count -> CharRightMotion start count)
            yield (InputUtil.CharToKeyInput 'k', fun start count -> LineUpMotion start count)
            yield (InputUtil.VimKeyToKeyInput VimKey.UpKey, fun start count -> LineUpMotion start count)
            yield (InputUtil.CharAndModifiersToKeyInput 'p' KeyModifiers.Control, fun start count -> LineUpMotion start count)
            yield (InputUtil.CharToKeyInput 'j', fun start count -> LineDownMotion start count)
            yield (InputUtil.VimKeyToKeyInput VimKey.DownKey, fun start count -> LineDownMotion start count)
            yield (InputUtil.CharAndModifiersToKeyInput 'n' KeyModifiers.Control, fun start count -> LineDownMotion start count)
            yield (InputUtil.CharAndModifiersToKeyInput 'j' KeyModifiers.Control, fun start count -> LineDownMotion start count)
            yield (InputUtil.CharToKeyInput '+', fun start count -> LineDownToFirstNonWhitespace start count |> Some)
            yield (InputUtil.CharAndModifiersToKeyInput 'm' KeyModifiers.Control, fun start count -> LineDownToFirstNonWhitespace start count |> Some)
            yield (InputUtil.VimKeyToKeyInput VimKey.EnterKey, fun start count -> LineDownToFirstNonWhitespace start count |> Some)
        }

    let ComplexMotions = 
        seq {
            yield (InputUtil.CharToKeyInput 'a', false, fun start count -> AllWordMotion start count)
            yield (InputUtil.CharToKeyInput 'f', true, fun start count -> ForwardCharMotion start count)
            yield (InputUtil.CharToKeyInput 't', true, fun start count -> ForwardTillCharMotion start count)
            yield (InputUtil.CharToKeyInput 'F', true, fun start count -> BackwardCharMotion start count)
            yield (InputUtil.CharToKeyInput 'T', true, fun start count -> BackwardTillCharMotion start count)
        }

    let AllMotionsCore =
        let simple = SimpleMotions |> Seq.map (fun (c,func) -> (c,SimpleMotionCommand(OneKeyInput c,func)))
        let complex = ComplexMotions |> Seq.map (fun (c,isMovement,func) -> (c,ComplexMotionCommand(OneKeyInput c,isMovement,func)))
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore |> Seq.map (fun (_,command) -> command)

    let MotionCommandsMap = AllMotionsCore |> Map.ofSeq

    /// Process a count prefix to the motion.  
    let private ProcessCount (ki:KeyInput) (completeFunc:KeyInput -> int -> MotionResult) startCount =
        let rec inner (processFunc: KeyInput->CountResult) (ki:KeyInput)  =               
            match processFunc ki with 
                | CountResult.Complete(count,nextKi) -> 
                    let fullCount = startCount * count
                    completeFunc nextKi fullCount
                | NeedMore(nextFunc) -> NeedMoreInputWithEscape (inner nextFunc)
        inner (CountCapture.Process) ki               
        
    let rec ProcessInput start (ki:KeyInput) count =
        let count = if count < 1 then 1 else count
        if ki.Key = VimKey.EscapeKey then Cancel
        elif ki.IsDigit && ki.Char <> '0' then ProcessCount ki (ProcessInput start) count
        else 
            match Map.tryFind ki MotionCommandsMap with
            | Some(command) -> 
                match command with 
                | SimpleMotionCommand(_,func) -> 
                    let res = func start count
                    match res with
                    | None -> MotionResult.Error Resources.MotionCapture_InvalidMotion
                    | Some(data) -> Complete data
                | ComplexMotionCommand(_,_,func) -> func start count
            | None -> HitInvalidMotion

    let ProcessView (view:ITextView) (ki:KeyInput) count = 
        let start = TextViewUtil.GetCaretPoint view
        ProcessInput start ki count

      
    
    
