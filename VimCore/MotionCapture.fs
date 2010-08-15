#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

type internal MotionCaptureGlobalData() =
    let mutable _lastCharSearch : (MotionFunction * MotionFunction) option = None

    interface IMotionCaptureGlobalData with
        member x.LastCharSearch 
            with get() = _lastCharSearch
            and set value = _lastCharSearch <- value

type internal MotionCapture 
    (
        _host : IVimHost,
        _textView : ITextView,
        _util :IMotionUtil,
        _globalData : IMotionCaptureGlobalData ) = 

    let NeedMoreInputWithEscape func =
        let inner (ki:KeyInput) = 
            if ki.Key = VimKey.Escape then ComplexMotionResult.Cancelled
            else func(ki)
        ComplexMotionResult.NeedMoreInput inner

    let WaitCharThen func =
        let inner (ki:KeyInput) = 
            let func count = 
                let count = CommandUtil.CountOrDefault count
                func ki.Char count
            ComplexMotionResult.Finished func
        NeedMoreInputWithEscape inner

    let CharSearch func backwardFunc =
        let inner c count = 
            let result = func c count
            if Option.isSome result then
                let makeMotion func count = 
                    let count = CommandUtil.CountOrDefault count
                    func c count
                _globalData.LastCharSearch <- Some (makeMotion func, makeMotion backwardFunc)
            result
        WaitCharThen inner 
        
    /// Repeat the last f,F,t or T motion.  
    let RepeatLastCharSearch direction count =
        match _globalData.LastCharSearch with
        | None -> 
            _host.Beep()
            None
        | Some(forwardFunc,backwardFunc) ->
            if SearchKindUtil.IsForward direction then forwardFunc count
            else backwardFunc count

    let SimpleMotions =  
        let singleToKiSet = KeyNotationUtil.StringToKeyInput >> OneKeyInput
        let needCount = 
            seq { 
                yield ("w", fun count -> _util.WordForward WordKind.NormalWord count |> Some)
                yield ("W", fun count -> _util.WordForward  WordKind.BigWord count |> Some)
                yield ("b", fun count -> _util.WordBackward WordKind.NormalWord count |> Some)
                yield ("B", fun count -> _util.WordBackward WordKind.BigWord count |> Some)
                yield ("$", fun count -> _util.EndOfLine count |> Some)
                yield ("<End>", fun count -> _util.EndOfLine count |> Some)
                yield ("^", fun count -> _util.FirstNonWhitespaceOnLine() |> Some)
                yield ("0", fun count -> _util.BeginingOfLine() |> Some)
                yield ("e", fun count -> _util.EndOfWord WordKind.NormalWord count )
                yield ("E", fun count -> _util.EndOfWord WordKind.BigWord count )
                yield ("h", fun count -> _util.CharLeft count)
                yield ("<Left>", fun count -> _util.CharLeft count)
                yield ("<Bs>", fun count -> _util.CharLeft count)
                yield ("<C-h>", fun count -> _util.CharLeft count)
                yield ("l", fun count -> _util.CharRight count)
                yield ("<Right>", fun count -> _util.CharRight count)
                yield ("<Space>", fun count -> _util.CharRight count)
                yield ("k", fun count -> _util.LineUp count |> Some)
                yield ("<Up>", fun count -> _util.LineUp count |> Some)
                yield ("<C-p>", fun count -> _util.LineUp count |> Some)
                yield ("j", fun count -> _util.LineDown count |> Some)
                yield ("<Down>", fun count -> _util.LineDown count |> Some)
                yield ("<C-n>", fun count -> _util.LineDown count |> Some)
                yield ("<C-j>", fun count -> _util.LineDown count |> Some)
                yield ("+", fun count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield ("_", fun count -> _util.LineDownToFirstNonWhitespace (count-1) |> Some)
                yield ("<C-m>", fun count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield ("<Enter>", fun count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield ("-", fun count -> _util.LineUpToFirstNonWhitespace count |> Some)
            } |> Seq.map (fun (kiName,func) ->
                    let kiSet = singleToKiSet kiName 
                    let func2 count =
                        let count = CommandUtil.CountOrDefault count
                        func count
                    SimpleMotionCommand(kiSet, func2)  )
        let needCount2 = 
            seq { 
                yield ("g_", fun count -> _util.LastNonWhitespaceOnLine count |> Some)
                yield ("aw", fun count -> _util.AllWord WordKind.NormalWord count |> Some)
                yield ("aW", fun count -> _util.AllWord WordKind.BigWord count |> Some)
            } |> Seq.map (fun (str,func) ->
                    let name = KeyNotationUtil.StringToKeyInputSet str
                    let func2 count =
                        let count = CommandUtil.CountOrDefault count
                        func count
                    SimpleMotionCommand(name, func2)  )
        let needCountOpt =
            seq {
                yield ("G", fun countOpt -> _util.LineOrLastToFirstNonWhitespace countOpt |> Some)
                yield ("H", fun countOpt -> _util.LineFromTopOfVisibleWindow countOpt |> Some)
                yield ("L", fun countOpt -> _util.LineFromBottomOfVisibleWindow countOpt |> Some)
                yield ("M", fun _ -> _util.LineInMiddleOfVisibleWindow () |> Some)
                yield (";", fun count -> RepeatLastCharSearch SearchKind.Forward count )
                yield (",", fun count -> RepeatLastCharSearch SearchKind.Backward count )
            } |> Seq.map (fun (ki,func) -> SimpleMotionCommand(singleToKiSet ki, func))

        let needCountOpt2 =
            seq {
                yield ( "gg", fun countOpt -> _util.LineOrFirstToFirstNonWhitespace countOpt |> Some)
            } |> Seq.map (fun (name,func) -> 
                let kiSet = KeyNotationUtil.StringToKeyInputSet name
                SimpleMotionCommand(kiSet, func))
        Seq.append needCount needCountOpt |> Seq.append needCountOpt2 |> Seq.append needCount2
    
    let ComplexMotions = 
        let singleToKiSet = KeyNotationUtil.StringToKeyInput >> OneKeyInput
        let needCount = 
            seq {
                yield ("f", true, fun () -> CharSearch _util.ForwardChar _util.BackwardChar)
                yield ("t", true, fun () -> CharSearch _util.ForwardTillChar _util.BackwardTillChar)
                yield ("F", true, fun () -> CharSearch _util.BackwardChar _util.ForwardChar)
                yield ("T", true, fun () -> CharSearch _util.BackwardTillChar _util.ForwardTillChar)
            } |> Seq.map (fun (ki,isMovement,func) -> ComplexMotionCommand(singleToKiSet ki, isMovement,func))
        needCount
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.KeyInputSet,command)) |> Map.ofSeq

    /// Run a normal motion function
    let RunMotionFunction command func count =
        let res = func count
        match res with
        | None -> MotionResult.Error Resources.MotionCapture_InvalidMotion
        | Some(data) -> 
            let runData = {MotionCommand=command; Count=count; MotionFunction = func}
            MotionResult.Complete (data,runData)

    /// Run a complex motion operation
    let rec RunComplexMotion command func count = 
        let rec inner result =
            match result with
            | ComplexMotionResult.Cancelled -> MotionResult.Cancelled
            | ComplexMotionResult.Error(msg) -> MotionResult.Error msg
            | ComplexMotionResult.NeedMoreInput(func) -> MotionResult.NeedMoreInput (fun ki -> func ki |> inner)
            | ComplexMotionResult.Finished(func) -> RunMotionFunction command func count
        func() |> inner

    let rec WaitForCommandName count ki =
        let rec inner (previousName:KeyInputSet) (ki:KeyInput) =
            if ki.Key = VimKey.Escape then MotionResult.Cancelled 
            else
                let name = previousName.Add ki
                match Map.tryFind name MotionCommandsMap with
                | Some(command) -> 
                    match command with 
                    | SimpleMotionCommand(_,func) -> RunMotionFunction command func count
                    | ComplexMotionCommand(_,_,func) -> RunComplexMotion command func count
                | None -> 
                    let res = MotionCommandsMap |> Seq.filter (fun pair -> pair.Key.StartsWith name) 
                    if Seq.isEmpty res then MotionResult.Error Resources.MotionCapture_InvalidMotion
                    else MotionResult.NeedMoreInput (inner name)
        inner Empty ki
        
    /// Process a count prefix to the motion.  
    let ProcessCount (ki:KeyInput) (completeFunc:KeyInput -> int option -> MotionResult) startCount =
        let startCount = CommandUtil.CountOrDefault startCount
        let rec inner (processFunc: KeyInput->CountResult) (ki:KeyInput)  =               
            if ki.Key = VimKey.Escape then MotionResult.Cancelled 
            else
                match processFunc ki with 
                    | CountResult.Complete(count,nextKi) -> 
                        let fullCount = startCount * count
                        completeFunc nextKi (Some fullCount)
                    | NeedMore(nextFunc) -> MotionResult.NeedMoreInput (inner nextFunc)
        inner (CountCapture.Process) ki

    let rec GetMotion (ki:KeyInput) count =
        if ki.Key = VimKey.Escape then MotionResult.Cancelled
        elif ki.IsDigit && ki.Char <> '0' then ProcessCount ki GetMotion count
        else WaitForCommandName count ki

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionCommands = MotionCommands
        member x.GetMotion ki count = GetMotion ki count

      
    
    
