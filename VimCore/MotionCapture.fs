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
        _util :ITextViewMotionUtil,
        _globalData : IMotionCaptureGlobalData ) = 

    let NeedMoreInputWithEscape func =
        let inner (ki:KeyInput) = 
            if ki.Key = VimKey.Escape then ComplexMotionResult.Cancelled
            else func(ki)
        ComplexMotionResult.NeedMoreInput inner

    let WaitCharThen func =
        let inner (ki:KeyInput) = 
            let func _ count = 
                let count = CommandUtil.CountOrDefault count
                func ki.Char count
            ComplexMotionResult.Finished func
        NeedMoreInputWithEscape inner

    let CharSearch func backwardFunc =
        let inner c count = 
            let result = func c count
            if Option.isSome result then
                let makeMotion func _ count = 
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
            if SearchKindUtil.IsForward direction then forwardFunc MotionUse.AfterOperator count
            else backwardFunc MotionUse.AfterOperator count

    let SimpleMotions =  
        let singleToKiSet = KeyNotationUtil.StringToKeyInput >> OneKeyInput
        let needCount = 
            seq { 
                yield (
                    "w", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.WordForward WordKind.NormalWord count |> Some)
                yield (
                    "W", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.WordForward  WordKind.BigWord count |> Some)
                yield (
                    "b", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.WordBackward WordKind.NormalWord count |> Some)
                yield (
                    "B", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.WordBackward WordKind.BigWord count |> Some)
                yield (
                    "$", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.EndOfLine count |> Some)
                yield (
                    "<End>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.EndOfLine count |> Some)
                yield (
                    "^", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.FirstNonWhitespaceOnLine() |> Some)
                yield (
                    "0", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.BeginingOfLine() |> Some)
                yield (
                    "e", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.EndOfWord WordKind.NormalWord count |> Some)
                yield (
                    "E", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.EndOfWord WordKind.BigWord count |> Some)
                yield (
                    "h", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharLeft count)
                yield (
                    "<Left>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharLeft count)
                yield (
                    "<Bs>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharLeft count)
                yield (
                    "<C-h>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharLeft count)
                yield (
                    "l", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharRight count)
                yield (
                    "<Right>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharRight count)
                yield (
                    "<Space>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.CharRight count)
                yield (
                    "k", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineUp count |> Some)
                yield (
                    "<Up>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineUp count |> Some)
                yield (
                    "<C-p>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineUp count |> Some)
                yield (
                    "j", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDown count |> Some)
                yield (
                    "<Down>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDown count |> Some)
                yield (
                    "<C-n>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDown count |> Some)
                yield (
                    "<C-j>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDown count |> Some)
                yield (
                    "+", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield (
                    "_", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDownToFirstNonWhitespace (count-1) |> Some)
                yield (
                    "<C-m>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield (
                    "<Enter>", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineDownToFirstNonWhitespace count |> Some)
                yield (
                    "-", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LineUpToFirstNonWhitespace count |> Some)
                yield (
                    "(", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.SentenceBackward count |> Some)
                yield (
                    ")", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.SentenceForward count |> Some)
                yield (
                    "{", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.ParagraphBackward count |> Some)
                yield (
                    "}", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.ParagraphForward count |> Some)
                yield (
                    "g_", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.LastNonWhitespaceOnLine count |> Some)
                yield (
                    "aw", 
                    MotionFlags.TextObjectSelection,
                    fun _ count -> _util.AllWord WordKind.NormalWord count |> Some)
                yield (
                    "aW", 
                    MotionFlags.TextObjectSelection,
                    fun _ count -> _util.AllWord WordKind.BigWord count |> Some)
                yield (
                    "as", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.SentenceFullForward count |> Some)
                yield (
                    "ap", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.ParagraphFullForward count |> Some)
                yield (
                    "]]", 
                    MotionFlags.CursorMovement,
                    fun motionUse count -> 
                        let arg = 
                            match motionUse with
                            | MotionUse.AfterOperator -> MotionArgument.ConsiderCloseBrace
                            | MotionUse.Movement -> MotionArgument.None
                        _util.SectionForward arg count |> Some )
                yield (
                    "][", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.SectionForward MotionArgument.None count |> Some)
                yield (
                    "[[", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.SectionBackwardOrOpenBrace count |> Some)
                yield (
                    "[]", 
                    MotionFlags.CursorMovement,
                    fun _ count -> _util.SectionBackwardOrCloseBrace count |> Some)
                yield (
                    "a\"", 
                    MotionFlags.TextObjectSelection,
                    fun _ _ -> _util.QuotedString() )
                yield (
                    "a'", 
                    MotionFlags.TextObjectSelection,
                    fun _ _ -> _util.QuotedString() )
                yield (
                    "a`", 
                    MotionFlags.TextObjectSelection,
                    fun _ _ -> _util.QuotedString() )
                yield (
                    "i\"", 
                    MotionFlags.TextObjectSelection,
                    fun _ _ -> _util.QuotedStringContents() )
                yield (
                    "i'", 
                    MotionFlags.TextObjectSelection,
                    fun _ _ -> _util.QuotedStringContents() )
                yield (
                    "i`", 
                    MotionFlags.TextObjectSelection,
                    fun _ _ -> _util.QuotedStringContents() )
            } |> Seq.map (fun (str,flags,func) ->
                    let name = KeyNotationUtil.StringToKeyInputSet str
                    let func2 motionUse count =
                        let count = CommandUtil.CountOrDefault count
                        func motionUse count
                    SimpleMotionCommand(name, flags, func2)  )
        let needCountOpt =
            seq {
                yield (
                    "G", 
                    MotionFlags.CursorMovement,
                    fun countOpt -> _util.LineOrLastToFirstNonWhitespace countOpt |> Some)
                yield (
                    "H", 
                    MotionFlags.CursorMovement,
                    fun countOpt -> _util.LineFromTopOfVisibleWindow countOpt |> Some)
                yield (
                    "L", 
                    MotionFlags.CursorMovement,
                    fun countOpt -> _util.LineFromBottomOfVisibleWindow countOpt |> Some)
                yield (
                    "M", 
                    MotionFlags.CursorMovement,
                    fun _ -> _util.LineInMiddleOfVisibleWindow () |> Some)
                yield (
                    ";", 
                    MotionFlags.CursorMovement,
                    fun count -> RepeatLastCharSearch SearchKind.Forward count )
                yield (
                    ",", 
                    MotionFlags.CursorMovement,
                    fun count -> RepeatLastCharSearch SearchKind.Backward count )
                yield ( 
                    "gg", 
                    MotionFlags.CursorMovement,
                    fun countOpt -> _util.LineOrFirstToFirstNonWhitespace countOpt |> Some)
            } |> Seq.map (fun (name,flags,func) -> 
                let kiSet = KeyNotationUtil.StringToKeyInputSet name
                let func2 motionUse count =
                    func count
                SimpleMotionCommand(kiSet, flags, func2))

        Seq.append needCount needCountOpt 
    
    let ComplexMotions = 
        let singleToKiSet = KeyNotationUtil.StringToKeyInput >> OneKeyInput
        let needCount = 
            seq {
                yield (
                    "f", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.ForwardChar _util.BackwardChar)
                yield (
                    "t", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.ForwardTillChar _util.BackwardTillChar)
                yield (
                    "F", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.BackwardChar _util.ForwardChar)
                yield (
                    "T", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.BackwardTillChar _util.ForwardTillChar)
            } |> Seq.map (fun (ki,flags,func) -> ComplexMotionCommand(singleToKiSet ki, flags,func))
        needCount
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.KeyInputSet,command)) |> Map.ofSeq

    /// Run a normal motion function
    let RunMotionFunction command func count =
        let res = func MotionUse.AfterOperator count
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
                    | SimpleMotionCommand(_,_,func) -> RunMotionFunction command func count
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

      
    
    
