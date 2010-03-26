#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

type internal MotionResult = 
    | Complete of (SnapshotSpan * MotionKind * OperationKind)
    
    /// Motion needs more input to be completed
    | NeedMoreInput of (KeyInput -> MotionResult)
    
    /// Indicates the motion is currently in an invalid state and 
    /// won't ever complete.  But the utility will still provide a 
    /// function to capture input until the motion action is completed
    /// with a completing key
    | InvalidMotion of string * (KeyInput -> MotionResult) 
    | Error of string
    | Cancel
       
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
            | VimKey.EnterKey -> Error("Invalid Motion")
            | _ -> InvalidMotion("Invalid Motion", inner)
        InvalidMotion("Invalid Motion",inner)
    
    /// Implement the w/W motion
    let private WordMotion start kind originalCount =
        let rec inner curPoint curCount = 
            let next = TssUtil.FindNextWordPosition curPoint kind
            match curCount with 
            | 1 -> 
                // When the next word crosses a line boundary for the last count then 
                // we stop the motion on the current line.  This does not appear to be 
                // caled out in the documentation but is evident in the behavior
                let span = 
                    if next.GetContainingLine().LineNumber <> curPoint.GetContainingLine().LineNumber then
                        new SnapshotSpan(start, curPoint.GetContainingLine().End)
                    else
                        new SnapshotSpan(start,next)
                Complete(span, MotionKind.Exclusive, OperationKind.CharacterWise)
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
                | 'w' -> func WordKind.NormalWord
                | 'W' -> func WordKind.BigWord
                | _ -> HitInvalidMotion
        NeedMoreInputWithEscape inner

    /// Implement the 'e' motion.  This goes to the end of the current word.  If we're
    /// not currently on a word it will find the next word and then go to the end of that
    let private EndOfWordMotion (start:SnapshotPoint) count kind =
        let snapshotEnd = TssUtil.GetEndPoint start.Snapshot
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
                    | None -> TssUtil.GetEndPoint start.Snapshot
                    | Some(s) -> inner s.End (count-1)

        let endPoint = inner start count
        let span = SnapshotSpan(start,endPoint)
        Complete (span, MotionKind.Inclusive, OperationKind.CharacterWise)
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    let private EndOfLineMotion (start:SnapshotPoint) count = 
        let span = TssUtil.GetLineRangeSpan start count
        Complete (span, MotionKind.Inclusive, OperationKind.CharacterWise)

    /// Find the first non-whitespace character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward
    let private BeginingOfLineMotion (start:SnapshotPoint) =
        let line = start.GetContainingLine()
        let found = TssUtil.GetPoints line
                        |> Seq.filter (fun x -> x.Position < start.Position)
                        |> Seq.tryFind (fun x-> x.GetChar() <> ' ')
        let span = match found with 
                    | Some p -> new SnapshotSpan(p, start)
                    | None -> new SnapshotSpan(start,0)
        Complete (span, MotionKind.Exclusive, OperationKind.CharacterWise)                    
    
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
            match ki.Char with
                | 'w' -> WordMotion start WordKind.NormalWord count
                | 'W' -> WordMotion start WordKind.BigWord count
                | '$' -> EndOfLineMotion start count
                | '^' -> BeginingOfLineMotion start 
                | 'a' -> AllWordMotion start count
                | 'e' -> EndOfWordMotion start count WordKind.NormalWord
                | 'E' -> EndOfWordMotion start count WordKind.BigWord
                
                /// Simple left right motions
                | 'h' -> 
                    let span = MotionUtil.CharLeft start count
                    Complete (span, MotionKind.Exclusive, OperationKind.CharacterWise)
                | 'l' ->
                    let span = MotionUtil.CharRight start count
                    Complete(span, MotionKind.Exclusive, OperationKind.CharacterWise)
                | 'k' ->
                    let span = MotionUtil.LineUp start count
                    Complete(span, MotionKind.Inclusive, OperationKind.LineWise)
                | 'j' ->
                    let span = MotionUtil.LineDown start count
                    Complete(span, MotionKind.Inclusive, OperationKind.LineWise)
                | _ -> HitInvalidMotion

    let ProcessView (view:ITextView) (ki:KeyInput) count = 
        let start = ViewUtil.GetCaretPoint view
        ProcessInput start ki count

      
    
    
