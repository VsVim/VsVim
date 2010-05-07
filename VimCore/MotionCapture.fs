#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

type internal MotionCapture 
    (
        _textView : ITextView,
        _util :IMotionUtil ) = 

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

    let ForwardCharMotionCore count func = 
        let inner (ki:KeyInput) =
            match func ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(data) -> Complete data 
        NeedMoreInputWithEscape inner

    let BackwardCharMotionCore count func = 
        let inner (ki:KeyInput) =
            match func ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(data) -> Complete data
        NeedMoreInputWithEscape inner

    let AllWordMotion count =        
        let inner (ki:KeyInput) = 
            match ki.Char with
                | 'w' -> _util.AllWord WordKind.NormalWord count |> Complete
                | 'W' -> _util.AllWord WordKind.BigWord count |> Complete
                | _ -> HitInvalidMotion
        NeedMoreInputWithEscape inner

    let WaitCharThen count func =
        let inner (ki:KeyInput) = 
            match func ki.Char count with
            | None -> MotionResult.Error(Resources.MotionCapture_InvalidMotion)
            | Some(data) -> Complete data
        NeedMoreInputWithEscape inner

    let SimpleMotions =  
        let needCount = 
            seq { 
                yield (InputUtil.CharToKeyInput 'w', fun count -> _util.WordForward WordKind.NormalWord count |> Some)
                yield (InputUtil.CharToKeyInput 'W', fun count -> _util.WordForward  WordKind.BigWord count |> Some)
                yield (InputUtil.CharToKeyInput 'b', fun count -> _util.WordBackward WordKind.NormalWord count |> Some)
                yield (InputUtil.CharToKeyInput 'B', fun count -> _util.WordBackward WordKind.BigWord count |> Some)
                yield (InputUtil.CharToKeyInput '$', fun count -> _util.EndOfLine count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.EndKey, fun count -> _util.EndOfLine count |> Some)
                yield (InputUtil.CharToKeyInput '^', fun count -> _util.FirstNonWhitespaceOnLine() |> Some)
                yield (InputUtil.CharToKeyInput '0', fun count -> _util.BeginingOfLine() |> Some)
                yield (InputUtil.CharToKeyInput 'e', fun count -> _util.EndOfWord WordKind.NormalWord count |> Some)
                yield (InputUtil.CharToKeyInput 'E', fun count -> _util.EndOfWord WordKind.BigWord count |> Some)
                yield (InputUtil.CharToKeyInput 'h', fun count -> _util.CharLeft count)
                yield (InputUtil.VimKeyToKeyInput VimKey.LeftKey, fun count -> _util.CharLeft count)
                yield (InputUtil.VimKeyToKeyInput VimKey.BackKey, fun count -> _util.CharLeft count)
                yield (InputUtil.CharAndModifiersToKeyInput 'h' KeyModifiers.Control, fun count -> _util.CharLeft count)
                yield (InputUtil.CharToKeyInput 'l', fun count -> _util.CharRight count)
                yield (InputUtil.VimKeyToKeyInput VimKey.RightKey, fun count -> _util.CharRight count)
                yield (InputUtil.CharToKeyInput ' ', fun count -> _util.CharRight count)
                yield (InputUtil.CharToKeyInput 'k', fun count -> _util.LineUp count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.UpKey, fun count -> _util.LineUp count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'p' KeyModifiers.Control, fun count -> _util.LineUp count |> Some)
                yield (InputUtil.CharToKeyInput 'j', fun count -> _util.LineDown count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.DownKey, fun count -> _util.LineDown count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'n' KeyModifiers.Control, fun count -> _util.LineDown count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'j' KeyModifiers.Control, fun count -> _util.LineDown count |> Some)
                yield (InputUtil.CharToKeyInput '+', fun count ->  _util.LineDownToFirstNonWhitespace count |> Some)
                yield (InputUtil.CharAndModifiersToKeyInput 'm' KeyModifiers.Control, fun count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield (InputUtil.VimKeyToKeyInput VimKey.EnterKey, fun count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield (InputUtil.CharToKeyInput '-', fun count -> _util.LineUpToFirstNonWhitespace count |> Some)
            } |> Seq.map (fun (ki,func) ->
                    let func2 count =
                        let count = CommandUtil.CountOrDefault count
                        func count
                    SimpleMotionCommand(OneKeyInput ki, func2)  )
        let needCount2 = 
            seq { 
                yield ("g_", fun count -> _util.LastNonWhitespaceOnLine count |> Some)
            } |> Seq.map (fun (str,func) ->
                    let name = CommandUtil.CreateCommandName str
                    let func2 count =
                        let count = CommandUtil.CountOrDefault count
                        func count
                    SimpleMotionCommand(name, func2)  )
        let needCountOpt =
            seq {
                yield (InputUtil.CharToKeyInput 'G', fun countOpt -> _util.LineOrLastToFirstNonWhitespace countOpt |> Some)
            } |> Seq.map (fun (ki,func) -> SimpleMotionCommand(OneKeyInput ki, func))

        let needCountOpt2 =
            seq {
                yield ( CommandUtil.CreateCommandName "gg", fun countOpt -> _util.LineOrFirstToFirstNonWhitespace countOpt |> Some)
            } |> Seq.map (fun (name,func) -> SimpleMotionCommand(name, func))
        Seq.append needCount needCountOpt |> Seq.append needCountOpt2 |> Seq.append needCount2
    
    let ComplexMotions = 
        let needCount = 
            seq {
                yield (InputUtil.CharToKeyInput 'a', false, fun count -> AllWordMotion count)
                yield (InputUtil.CharToKeyInput 'f', true, fun count -> WaitCharThen count _util.ForwardChar)
                yield (InputUtil.CharToKeyInput 't', true, fun count -> WaitCharThen count _util.ForwardTillChar)
                yield (InputUtil.CharToKeyInput 'F', true, fun count -> WaitCharThen count _util.BackwardChar)
                yield (InputUtil.CharToKeyInput 'T', true, fun count -> WaitCharThen count _util.BackwardTillChar)
            } |> Seq.map (fun (ki,isMovement,func) ->
                    let func2 count =
                        let count = CommandUtil.CountOrDefault count
                        func count
                    ComplexMotionCommand(OneKeyInput ki, isMovement,func2))
        needCount
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.CommandName,command)) |> Map.ofSeq

    let rec WaitForCommandName count ki =
        let rec inner (previousName:CommandName) ki =
            let runCommand command = 
                match command with 
                | SimpleMotionCommand(_,func) -> 
                    let res = func count
                    match res with
                    | None -> MotionResult.Error Resources.MotionCapture_InvalidMotion
                    | Some(data) -> Complete data
                | ComplexMotionCommand(_,_,func) -> func count
    
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

    let rec GetMotion (ki:KeyInput) count =
        if ki.Key = VimKey.EscapeKey then Cancel
        elif ki.IsDigit && ki.Char <> '0' then ProcessCount ki GetMotion count
        else WaitForCommandName count ki

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionCommands = MotionCommands
        member x.GetMotion ki count = GetMotion ki count

      
    
    
