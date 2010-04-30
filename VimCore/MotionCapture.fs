#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

type internal MotionCapture (_util :IMotionUtil ) = 

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

    let ForwardCharMotionCore start count func = 
        let inner (ki:KeyInput) =
            match func start ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(data) -> Complete data 
        NeedMoreInputWithEscape inner

    let BackwardCharMotionCore start count func = 
        let inner (ki:KeyInput) =
            match func start ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(data) -> Complete data
        NeedMoreInputWithEscape inner

    let AllWordMotion start count =        
        let inner (ki:KeyInput) = 
            match ki.Char with
                | 'w' -> _util.AllWord WordKind.NormalWord start count |> Complete
                | 'W' -> _util.AllWord WordKind.BigWord start count |> Complete
                | _ -> HitInvalidMotion
        NeedMoreInputWithEscape inner

    let WaitCharThen start count func =
        let inner (ki:KeyInput) = 
            match func ki.Char start count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(data) -> Complete data
        NeedMoreInputWithEscape inner

    let SimpleMotions =  
        let needCount = 
            seq { 
                yield (InputUtil.CharToKeyInput 'w', fun start count -> _util.WordForward WordKind.NormalWord start count |> Some)
                yield (InputUtil.CharToKeyInput 'W', fun start count -> _util.WordForward  WordKind.BigWord start count |> Some)
                yield (InputUtil.CharToKeyInput 'b', fun start count -> _util.WordBackward WordKind.NormalWord start count |> Some)
                yield (InputUtil.CharToKeyInput 'B', fun start count -> _util.WordBackward WordKind.BigWord start count |> Some)
                yield (InputUtil.CharToKeyInput '$', fun start count -> _util.EndOfLine start count |> Some)
                yield (InputUtil.CharToKeyInput '^', fun start count -> _util.FirstNonWhitespaceOnLine start |> Some)
                yield (InputUtil.CharToKeyInput '0', fun start count -> _util.BeginingOfLine start |> Some)
                yield (InputUtil.CharToKeyInput 'e', fun start count -> _util.EndOfWord WordKind.NormalWord start count |> Some)
                yield (InputUtil.CharToKeyInput 'E', fun start count -> _util.EndOfWord WordKind.BigWord start count |> Some)
                yield (InputUtil.CharToKeyInput 'h', fun start count -> _util.CharLeft start count)
                yield (InputUtil.VimKeyToKeyInput VimKey.LeftKey, fun start count -> _util.CharLeft start count)
                yield (InputUtil.VimKeyToKeyInput VimKey.BackKey, fun start count -> _util.CharLeft start count)
                yield (InputUtil.CharAndModifiersToKeyInput 'h' KeyModifiers.Control, fun start count -> _util.CharLeft start count)
                yield (InputUtil.CharToKeyInput 'l', fun start count -> _util.CharRight start count)
                yield (InputUtil.VimKeyToKeyInput VimKey.RightKey, fun start count -> _util.CharRight start count)
                yield (InputUtil.CharToKeyInput ' ', fun start count -> _util.CharRight start count)
                yield (InputUtil.CharToKeyInput 'k', fun start count -> _util.LineUp start count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.UpKey, fun start count -> _util.LineUp start count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'p' KeyModifiers.Control, fun start count -> _util.LineUp start count |> Some)
                yield (InputUtil.CharToKeyInput 'j', fun start count -> _util.LineDown start count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.DownKey, fun start count -> _util.LineDown start count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'n' KeyModifiers.Control, fun start count -> _util.LineDown start count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'j' KeyModifiers.Control, fun start count -> _util.LineDown start count |> Some)
                yield (InputUtil.CharToKeyInput '+', fun start count ->  _util.LineDownToFirstNonWhitespace start count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'm' KeyModifiers.Control, fun start count -> _util.LineDownToFirstNonWhitespace start count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.EnterKey, fun start count -> _util.LineDownToFirstNonWhitespace start count |> Some)
                yield (InputUtil.CharToKeyInput '-', fun start count -> _util.LineUpToFirstNonWhitespace start count |> Some)
            } |> Seq.map (fun (ki,func) ->
                    let func2 start count =
                        let count = CommandUtil.CountOrDefault count
                        func start count
                    SimpleMotionCommand(OneKeyInput ki, func2)  )
        let needCountOpt =
            seq {
                yield (InputUtil.CharToKeyInput 'G', fun start countOpt -> _util.LineOrLastToFirstNonWhitespace start countOpt |> Some)
            } |> Seq.map (fun (ki,func) -> SimpleMotionCommand(OneKeyInput ki, func))

        let needCountOpt2 =
            seq {
                yield ( CommandUtil.CreateCommandName "gg", fun start countOpt -> _util.LineOrFirstToFirstNonWhitespace start countOpt |> Some)
            } |> Seq.map (fun (name,func) -> SimpleMotionCommand(name, func))
        Seq.append needCount needCountOpt |> Seq.append needCountOpt2
    
    let ComplexMotions = 
        let needCount = 
            seq {
                yield (InputUtil.CharToKeyInput 'a', false, fun start count -> AllWordMotion start count)
                yield (InputUtil.CharToKeyInput 'f', true, fun start count -> WaitCharThen start count _util.ForwardChar)
                yield (InputUtil.CharToKeyInput 't', true, fun start count -> WaitCharThen start count _util.ForwardTillChar)
                yield (InputUtil.CharToKeyInput 'F', true, fun start count -> WaitCharThen start count _util.BackwardChar)
                yield (InputUtil.CharToKeyInput 'T', true, fun start count -> WaitCharThen start count _util.BackwardTillChar)
            } |> Seq.map (fun (ki,isMovement,func) ->
                    let func2 start count =
                        let count = CommandUtil.CountOrDefault count
                        func start count
                    ComplexMotionCommand(OneKeyInput ki, isMovement,func2))
        needCount
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.CommandName,command)) |> Map.ofSeq

    let rec WaitForCommandName start count ki =
        let rec inner (previousName:CommandName) ki =
            let runCommand command = 
                match command with 
                | SimpleMotionCommand(_,func) -> 
                    let res = func start count
                    match res with
                    | None -> MotionResult.Error Resources.MotionCapture_InvalidMotion
                    | Some(data) -> Complete data
                | ComplexMotionCommand(_,_,func) -> func start count
    
            let name = previousName.Add ki
            match Map.tryFind name MotionCommandsMap with
            | Some(command) -> runCommand command
            | None -> 
                let res = MotionCommandsMap |> Seq.filter (fun pair -> pair.Key.StartsWith name) 
                if Seq.isEmpty res then HitInvalidMotion
                else NeedMoreInputWithEscape (inner name)
        inner EmptyName ki
    
    /// Process a count prefix to the motion.  
    let ProcessCount (ki:KeyInput) (completeFunc:KeyInput -> int option -> MotionResult) startCount =
        let startCount = CommandUtil.CountOrDefault startCount
        let rec inner (processFunc: KeyInput->CountResult) (ki:KeyInput)  =               
            match processFunc ki with 
                | CountResult.Complete(count,nextKi) -> 
                    let fullCount = startCount * count
                    completeFunc nextKi (Some fullCount)
                | NeedMore(nextFunc) -> NeedMoreInputWithEscape (inner nextFunc)
        inner (CountCapture.Process) ki

    let rec ProcessInput start (ki:KeyInput) count =
        if ki.Key = VimKey.EscapeKey then Cancel
        elif ki.IsDigit && ki.Char <> '0' then ProcessCount ki (ProcessInput start) count
        else WaitForCommandName start count ki

    let ProcessView (view:ITextView) (ki:KeyInput) count = 
        let start = TextViewUtil.GetCaretPoint view
        ProcessInput start ki count

    interface IMotionCapture with
        member x.MotionCommands = MotionCommands
        member x.ProcessInput start ki count = ProcessInput start ki count
        member x.ProcessView view ki count = ProcessView view ki count

      
    
    
