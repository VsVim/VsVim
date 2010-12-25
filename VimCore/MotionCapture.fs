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

    /// Handles the f,F,t and T motions.  These are special in that they use the language 
    /// mapping mode (:help language-mapping) for their char input.  Most motions get handled
    /// via operator-pending
    let CharSearch func backwardFunc =

        let waitCharThen func =
            let inner (ki:KeyInput) =
                if ki = KeyInputUtil.EscapeKey then ComplexMotionResult.Cancelled
                else ComplexMotionResult.Finished (fun arg -> func ki.Char arg)
            ComplexMotionResult.NeedMoreInput (Some KeyRemapMode.Language, inner)

        let inner c (arg:MotionArgument) = 
            let result = func c arg.Count
            if Option.isSome result then
                let makeMotion func (arg:MotionArgument) = func c arg.Count
                _globalData.LastCharSearch <- Some (makeMotion func, makeMotion backwardFunc)
            result
        waitCharThen inner 

    /// Repeat the last f,F,t or T motion.  
    let RepeatLastCharSearch direction arg =
        match _globalData.LastCharSearch with
        | None -> 
            _host.Beep()
            None
        | Some(forwardFunc,backwardFunc) ->
            let arg = {arg with MotionContext=MotionContext.AfterOperator}
            match direction with
            | Direction.Forward -> forwardFunc arg
            | Direction.Backward -> backwardFunc arg

    let SimpleMotions =  
        let motionSeq : (string * MotionFlags * MotionFunction ) seq = 
            seq { 
                yield (
                    "w", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordForward WordKind.NormalWord arg.Count |> Some)
                yield (
                    "<S-Right>",
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordForward WordKind.NormalWord arg.Count |> Some)
                yield (
                    "W", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordForward  WordKind.BigWord arg.Count |> Some)
                yield (
                    "<C-Right>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordForward  WordKind.BigWord arg.Count |> Some)
                yield (
                    "b", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordBackward WordKind.NormalWord arg.Count |> Some)
                yield (
                    "<S-Left>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordBackward WordKind.NormalWord arg.Count |> Some)
                yield (
                    "B", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordBackward WordKind.BigWord arg.Count |> Some)
                yield (
                    "<C-Left>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.WordBackward WordKind.BigWord arg.Count |> Some)
                yield (
                    "$", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.EndOfLine arg.Count |> Some)
                yield (
                    "<End>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.EndOfLine arg.Count |> Some)
                yield (
                    "^", 
                    MotionFlags.CursorMovement,
                    fun _ -> _util.FirstNonWhitespaceOnLine() |> Some)
                yield (
                    "0", 
                    MotionFlags.CursorMovement,
                    fun _ -> _util.BeginingOfLine() |> Some)
                yield (
                    "e", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.EndOfWord WordKind.NormalWord arg.Count |> Some)
                yield (
                    "E", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.EndOfWord WordKind.BigWord arg.Count |> Some)
                yield (
                    "h", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharLeft arg.Count )
                yield (
                    "<Left>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharLeft arg.Count)
                yield (
                    "<Bs>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharLeft arg.Count)
                yield (
                    "<C-h>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharLeft arg.Count)
                yield (
                    "l", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharRight arg.Count)
                yield (
                    "<Right>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharRight arg.Count)
                yield (
                    "<Space>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.CharRight arg.Count)
                yield (
                    "k", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineUp arg.Count |> Some)
                yield (
                    "<Up>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineUp arg.Count |> Some)
                yield (
                    "<C-p>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineUp arg.Count |> Some)
                yield (
                    "j", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDown arg.Count |> Some)
                yield (
                    "<Down>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDown arg.Count |> Some)
                yield (
                    "<C-n>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDown arg.Count |> Some)
                yield (
                    "<C-j>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDown arg.Count |> Some)
                yield (
                    "+", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDownToFirstNonWhitespace arg.Count |> Some)
                yield (
                    "_", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDownToFirstNonWhitespace (arg.Count-1) |> Some)
                yield (
                    "<C-m>", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineDownToFirstNonWhitespace arg.Count |> Some)
                yield (
                    "-", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineUpToFirstNonWhitespace arg.Count |> Some)
                yield (
                    "(", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SentenceBackward arg.Count |> Some)
                yield (
                    ")", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SentenceForward arg.Count |> Some)
                yield (
                    "{", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.ParagraphBackward arg.Count |> Some)
                yield (
                    "}", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.ParagraphForward arg.Count |> Some)
                yield (
                    "g_", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LastNonWhitespaceOnLine arg.Count |> Some)
                yield (
                    "aw", 
                    MotionFlags.TextObjectSelection,
                    fun arg -> _util.AllWord WordKind.NormalWord arg.Count |> Some)
                yield (
                    "aW", 
                    MotionFlags.TextObjectSelection,
                    fun arg -> _util.AllWord WordKind.BigWord arg.Count |> Some)
                yield (
                    "as", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SentenceFullForward arg.Count |> Some)
                yield (
                    "ap", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.ParagraphFullForward arg.Count |> Some)
                yield (
                    "]]", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SectionForward arg.MotionContext arg.Count |> Some )
                yield (
                    "][", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SectionForward MotionContext.Movement arg.Count |> Some)
                yield (
                    "[[", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SectionBackwardOrOpenBrace arg.Count |> Some)
                yield (
                    "[]", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.SectionBackwardOrCloseBrace arg.Count |> Some)
                yield (
                    "a\"", 
                    MotionFlags.TextObjectSelection,
                    fun _ -> _util.QuotedString() )
                yield (
                    "a'", 
                    MotionFlags.TextObjectSelection,
                    fun _ -> _util.QuotedString() )
                yield (
                    "a`", 
                    MotionFlags.TextObjectSelection,
                    fun _ -> _util.QuotedString() )
                yield (
                    "i\"", 
                    MotionFlags.TextObjectSelection,
                    fun _ -> _util.QuotedStringContents() )
                yield (
                    "i'", 
                    MotionFlags.TextObjectSelection,
                    fun _ -> _util.QuotedStringContents() )
                yield (
                    "i`", 
                    MotionFlags.TextObjectSelection,
                    fun _ -> _util.QuotedStringContents() )
                yield (
                    "G", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineOrLastToFirstNonWhitespace arg.RawCount |> Some)
                yield (
                    "H", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineFromTopOfVisibleWindow arg.RawCount |> Some)
                yield (
                    "L", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineFromBottomOfVisibleWindow arg.RawCount |> Some)
                yield (
                    "M", 
                    MotionFlags.CursorMovement,
                    fun _ -> _util.LineInMiddleOfVisibleWindow () |> Some)
                yield (
                    ";", 
                    MotionFlags.CursorMovement,
                    fun arg -> RepeatLastCharSearch Direction.Forward arg)
                yield (
                    ",", 
                    MotionFlags.CursorMovement,
                    fun arg -> RepeatLastCharSearch Direction.Backward arg)
                yield ( 
                    "gg", 
                    MotionFlags.CursorMovement,
                    fun arg -> _util.LineOrFirstToFirstNonWhitespace arg.RawCount |> Some)
            } 
            
        motionSeq 
        |> Seq.map (fun (str,flags,func) ->
            let name = KeyNotationUtil.StringToKeyInputSet str
            SimpleMotionCommand(name, flags, func)  )
    
    let ComplexMotions = 
        let motionSeq : (string * MotionFlags * ComplexMotionFunction ) seq = 
            seq {
                yield (
                    "f", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.ForwardChar _util.BackwardChar)
                yield (
                    "t", 
                    MotionFlags.CursorMovement,
                    fun ()-> CharSearch _util.ForwardTillChar _util.BackwardTillChar)
                yield (
                    "F", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.BackwardChar _util.ForwardChar)
                yield (
                    "T", 
                    MotionFlags.CursorMovement,
                    fun () -> CharSearch _util.BackwardTillChar _util.ForwardTillChar)
            } 
        motionSeq
        |> Seq.map (fun (str,flags,func) -> 
                let name = KeyNotationUtil.StringToKeyInputSet str 
                ComplexMotionCommand(name, flags,func))
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.KeyInputSet,command)) |> Map.ofSeq

    /// Run a normal motion function
    member x.RunMotionFunction command func arg =
        match func arg with
        | None -> MotionResult.Error Resources.MotionCapture_InvalidMotion
        | Some(data) -> 
            let runData = {MotionCommand=command; MotionArgument=arg; MotionFunction = func}
            MotionResult.Complete (data,runData)

    /// Run a complex motion operation
    member x.RunComplexMotion command func arg = 
        let rec inner result =
            match result with
            | ComplexMotionResult.Cancelled -> MotionResult.Cancelled
            | ComplexMotionResult.Error(msg) -> MotionResult.Error msg
            | ComplexMotionResult.NeedMoreInput(keyRemapMode, func) -> MotionResult.NeedMoreInput (keyRemapMode,(fun ki -> func ki |> inner))
            | ComplexMotionResult.Finished(func) -> x.RunMotionFunction command func arg
        func () |> inner

    member x.WaitForCommandName arg ki =
        let rec inner (previousName:KeyInputSet) (ki:KeyInput) =
            if ki = KeyInputUtil.EscapeKey then 
                MotionResult.Cancelled 
            else
                let name = previousName.Add ki
                match Map.tryFind name MotionCommandsMap with
                | Some(command) -> 
                    match command with 
                    | SimpleMotionCommand(_,_,func) -> x.RunMotionFunction command func arg
                    | ComplexMotionCommand(_,_,func) -> x.RunComplexMotion command func arg
                | None -> 
                    let res = MotionCommandsMap |> Seq.filter (fun pair -> pair.Key.StartsWith name) 
                    if Seq.isEmpty res then MotionResult.Error Resources.MotionCapture_InvalidMotion
                    else MotionResult.NeedMoreInput (None, inner name)
        inner Empty ki
        
    /// Wait for the completion of the motion count
    member x.WaitforCount ki arg =
        let rec inner (processFunc: KeyInput->CountResult) (ki:KeyInput)  =               
            if ki = KeyInputUtil.EscapeKey then 
                MotionResult.Cancelled 
            else
                match processFunc ki with 
                | CountResult.Complete(count,nextKi) -> 
                    let arg = {arg with MotionCount=Some count}
                    x.WaitForCommandName arg nextKi
                | NeedMore(nextFunc) -> MotionResult.NeedMoreInput (None, inner nextFunc)
        inner (CountCapture.Process) ki

    member x.GetOperatorMotion (ki:KeyInput) operatorCountOpt =
        let arg = {MotionContext=MotionContext.AfterOperator; OperatorCount=operatorCountOpt; MotionCount=None}
        if ki = KeyInputUtil.EscapeKey then MotionResult.Cancelled
        elif ki.IsDigit && ki.Char <> '0' then x.WaitforCount ki arg
        else x.WaitForCommandName arg ki

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionCommands = MotionCommands
        member x.GetOperatorMotion ki count = x.GetOperatorMotion ki count

      
    
    
