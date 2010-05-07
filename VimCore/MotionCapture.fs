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
            if ki.Key = VimKey.EscapeKey then ComplexMotionResult.Cancelled
            else func(ki)
        ComplexMotionResult.NeedMoreInput inner

    let ForwardCharMotionCore func = 
        let inner (ki:KeyInput) = ComplexMotionResult.Finished (func ki.Char)
        NeedMoreInputWithEscape inner

    let BackwardCharMotionCore count func = 
        let inner (ki:KeyInput) = ComplexMotionResult.Finished (func ki.Char)
        NeedMoreInputWithEscape inner

    let AllWordMotion () =        
        let inner (ki:KeyInput) = 
            let withKind kind = 
                let wrapped count = 
                    let count = CommandUtil.CountOrDefault count
                    _util.AllWord kind count |> Some
                ComplexMotionResult.Finished wrapped
            match ki.Char with
                | 'w' -> withKind WordKind.NormalWord
                | 'W' -> withKind WordKind.BigWord
                | _ -> ComplexMotionResult.Error Resources.MotionCapture_InvalidMotion
        NeedMoreInputWithEscape inner

    let WaitCharThen func =
        let inner (ki:KeyInput) = 
            let func count = 
                let count = CommandUtil.CountOrDefault count
                func ki.Char count
            ComplexMotionResult.Finished func
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
                yield (InputUtil.CharToKeyInput 'a', false, fun ()-> AllWordMotion() )
                yield (InputUtil.CharToKeyInput 'f', true, fun () -> WaitCharThen _util.ForwardChar)
                yield (InputUtil.CharToKeyInput 't', true, fun () -> WaitCharThen _util.ForwardTillChar)
                yield (InputUtil.CharToKeyInput 'F', true, fun () -> WaitCharThen _util.BackwardChar)
                yield (InputUtil.CharToKeyInput 'T', true, fun () -> WaitCharThen _util.BackwardTillChar)
            } |> Seq.map (fun (ki,isMovement,func) -> ComplexMotionCommand(OneKeyInput ki, isMovement,func))
        needCount
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.CommandName,command)) |> Map.ofSeq

    /// Run a normal motion function
    let RunMotionFunction func count =
        let res = func count
        match res with
        | None -> MotionResult.Error Resources.MotionCapture_InvalidMotion
        | Some(data) -> MotionResult.Complete (data,func)

    /// Run a complex motion operation
    let rec RunComplexMotion func count = 
        let rec inner result =
            match result with
            | ComplexMotionResult.Cancelled -> MotionResult.Cancelled
            | ComplexMotionResult.Error(msg) -> MotionResult.Error msg
            | ComplexMotionResult.NeedMoreInput(func) -> MotionResult.NeedMoreInput (fun ki -> func ki |> inner)
            | ComplexMotionResult.Finished(func) -> RunMotionFunction func count
        func() |> inner

    let rec WaitForCommandName count ki =
        let rec inner (previousName:CommandName) (ki:KeyInput) =
            if ki.Key = VimKey.EscapeKey then MotionResult.Cancelled 
            else
                let name = previousName.Add ki
                match Map.tryFind name MotionCommandsMap with
                | Some(command) -> 
                    match command with 
                    | SimpleMotionCommand(_,func) -> RunMotionFunction func count
                    | ComplexMotionCommand(_,_,func) -> RunComplexMotion func count
                | None -> 
                    let res = MotionCommandsMap |> Seq.filter (fun pair -> pair.Key.StartsWith name) 
                    if Seq.isEmpty res then MotionResult.Error Resources.MotionCapture_InvalidMotion
                    else MotionResult.NeedMoreInput (inner name)
        inner EmptyName ki
        
    /// Process a count prefix to the motion.  
    let ProcessCount (ki:KeyInput) (completeFunc:KeyInput -> int option -> MotionResult) startCount =
        let startCount = CommandUtil.CountOrDefault startCount
        let rec inner (processFunc: KeyInput->CountResult) (ki:KeyInput)  =               
            if ki.Key = VimKey.EscapeKey then MotionResult.Cancelled 
            else
                match processFunc ki with 
                    | CountResult.Complete(count,nextKi) -> 
                        let fullCount = startCount * count
                        completeFunc nextKi (Some fullCount)
                    | NeedMore(nextFunc) -> MotionResult.NeedMoreInput (inner nextFunc)
        inner (CountCapture.Process) ki

    let rec GetMotion (ki:KeyInput) count =
        if ki.Key = VimKey.EscapeKey then MotionResult.Cancelled
        elif ki.IsDigit && ki.Char <> '0' then ProcessCount ki GetMotion count
        else WaitForCommandName count ki

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionCommands = MotionCommands
        member x.GetMotion ki count = GetMotion ki count

      
    
    
