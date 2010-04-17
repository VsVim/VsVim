#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

module internal MotionCapture = 

    let NeedMoreInputWithEscape func =
        let inner (ki:KeyInput) = 
            if ki.Key = VimKey.EscapeKey then Cancel
            else func(ki)
        NeedMoreInput(inner)

    /// When an invalid motion is given just wait for enter and then report and invalid 
    /// motion error.  Update the status to let the user know that we are currently
    /// in an invalid state
    let HitInvalidMotion =
        let rec inner (ki:KeyInput) =
            match ki.Key with 
            | VimKey.EscapeKey -> Cancel
            | VimKey.EnterKey -> Error(Resources.MotionCapture_InvalidMotion)
            | _ -> InvalidMotion("Invalid Motion", inner)
        InvalidMotion("Invalid Motion",inner)

    let private ForwardCharMotionCore start count func = 
        let inner (ki:KeyInput) =
            match func start ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(point:SnapshotPoint) -> 
                let span = SnapshotSpan(start, point.Add(1))
                let data = {Span=span;IsForward=true;OperationKind=OperationKind.CharacterWise;MotionKind=MotionKind.Inclusive}
                Complete data
        NeedMoreInputWithEscape inner

    /// Handle the 'f' motion.  Forward to the next occurrence of the specified character on
    /// this line
    let private ForwardCharMotion start count = ForwardCharMotionCore start count TssUtil.FindNextOccurranceOfCharOnLine

    /// Handle the 't' motion.  Forward till the next occurrence of the specified character on
    /// this line
    let private ForwardTillCharMotion start count = ForwardCharMotionCore start count TssUtil.FindTillNextOccurranceOfCharOnLine
        
    /// Implement the w/W motion
    let private WordMotion start kind originalCount =
        let rec inner curPoint curCount = 
            let next = TssUtil.FindNextWordPosition curPoint kind
            match curCount with 
            | 1 -> 
                // When the next word crosses a line boundary for the last count then 
                // we stop the motion on the current line.  This does not appear to be 
                // called out in the documentation but is evident in the behavior
                let span = 
                    if next.GetContainingLine().LineNumber <> curPoint.GetContainingLine().LineNumber then
                        new SnapshotSpan(start, curPoint.GetContainingLine().End)
                    else
                        new SnapshotSpan(start,next)
                {Span=span;IsForward=true;MotionKind=MotionKind.Exclusive;OperationKind=OperationKind.CharacterWise}
            | _ -> inner next (curCount-1)
        inner start originalCount      
        
    /// Implement the aw motion.  This is called once the a key is seen.
    let private AllWordMotion start count =        
        let func kind = 
            let start = match TssUtil.FindCurrentFullWordSpan start kind with 
                            | Some (s) -> s.Start
                            | None -> start
            WordMotion start kind count
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
                    if System.Char.IsWhiteSpace(start.GetChar()) then TssUtil.FindNextWordPosition start kind
                    else start

                if start = snapshotEnd then snapshotEnd
                else
                    // Get the span of the current word and the end completes the motion
                    match TssUtil.FindCurrentFullWordSpan start kind with
                    | None -> SnapshotUtil.GetEndPoint start.Snapshot
                    | Some(s) -> inner s.End (count-1)

        let endPoint = inner start count
        let span = SnapshotSpan(start,endPoint)
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise}
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    let private EndOfLineMotion (start:SnapshotPoint) count = 
        let span = SnapshotPointUtil.GetLineRangeSpan start count
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise}

    /// Find the first non-whitespace character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward
    let private BeginingOfLineMotion (start:SnapshotPoint) =
        let line = start.GetContainingLine()
        let found = SnapshotLineUtil.GetPoints line
                        |> Seq.filter (fun x -> x.Position < start.Position)
                        |> Seq.tryFind (fun x-> x.GetChar() <> ' ')
        let span = match found with 
                    | Some p -> new SnapshotSpan(p, start)
                    | None -> new SnapshotSpan(start,0)
        {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise} 

    let private CharLeftMotion start count = 
        let prev = SnapshotPointUtil.GetPreviousPointOnLine start count 
        if prev = start then None
        else {Span=SnapshotSpan(prev,start); IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise } |> Some

    let private CharRightMotion start count =
        let next = SnapshotPointUtil.GetNextPointOnLine start count 
        if next = start then None
        else {Span=SnapshotSpan(start,next); IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise } |> Some

    let private LineUpMotion start count =
        let span = MotionUtil.LineUp start count
        {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise } |> Some

    let private LineDownMotion start count =
        let span = MotionUtil.LineDown start count
        {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise } |> Some

    let SimpleMotions =  
        seq { 
            yield ('w', fun start count -> WordMotion start WordKind.NormalWord count |> Some)
            yield ('W', fun start count -> WordMotion start WordKind.BigWord count |> Some)
            yield ('$', fun start count -> EndOfLineMotion start count |> Some)
            yield ('^', fun start count -> BeginingOfLineMotion start |> Some)
            yield ('e', fun start count -> EndOfWordMotion start count WordKind.NormalWord |> Some)
            yield ('E', fun start count -> EndOfWordMotion start count WordKind.BigWord |> Some)
            yield ('h', fun start count -> CharLeftMotion start count )
            yield ('l', fun start count -> CharRightMotion start count )
            yield ('k', fun start count -> LineUpMotion start count)
            yield ('j', fun start count -> LineDownMotion start count)
        }

    let ComplexMotions = 
        seq {
            yield ('a', false, fun start count -> AllWordMotion start count)
            yield ('f', true, fun start count -> ForwardCharMotion start count)
            yield ('t', true, fun start count -> ForwardTillCharMotion start count)
        }

    let AllMotionsCore =
        let simple = SimpleMotions |> Seq.map (fun (c,func) -> (c,SimpleMotionCommand(c,func)))
        let complex = ComplexMotions |> Seq.map (fun (c,isMovement,func) -> (c,ComplexMotionCommand(c,isMovement,func)))
        simple |> Seq.append complex

    let MotionCommnads = AllMotionsCore |> Seq.map (fun (_,command) -> command)

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
        elif ki.IsDigit then ProcessCount ki (ProcessInput start) count
        else 
            match Map.tryFind ki.Char MotionCommandsMap with
            | Some(command) -> 
                match command with 
                | SimpleMotionCommand(_,func) -> 
                    let res = func start count
                    match res with
                    | None -> Error Resources.MotionCapture_InvalidMotion
                    | Some(data) -> Complete data
                | ComplexMotionCommand(_,_,func) -> func start count
            | None -> HitInvalidMotion

    let ProcessView (view:ITextView) (ki:KeyInput) count = 
        let start = TextViewUtil.GetCaretPoint view
        ProcessInput start ki count

      
    
    
