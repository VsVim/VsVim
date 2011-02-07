#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

type internal MotionCapture 
    (
        _host : IVimHost,
        _textView : ITextView,
        _incrementalSearch : IIncrementalSearch,
        _settings : IVimLocalSettings) = 

    let _search = _incrementalSearch.SearchService

    /// Get a char and use the provided 'func' to create a Motion value.
    let GetChar func motionArgument = 
        let inner ki =
            if ki = KeyInputUtil.EscapeKey then 
                MotionResult.Cancelled
            else 
                let motion = func ki.Char
                let data = { Motion = motion; MotionArgument = motionArgument }
                MotionResult.Complete (data, None)
        MotionResult.NeedMoreInput (Some KeyRemapMode.Language, inner)

    /// Handles incremental searches (/ and ?)
    let IncrementalSearch direction motionArgument =

        let kind = 
            match direction, _settings.GlobalSettings.WrapScan with
            | Direction.Forward, true -> SearchKind.ForwardWithWrap
            | Direction.Forward, false -> SearchKind.Forward
            | Direction.Backward, true -> SearchKind.BackwardWithWrap
            | Direction.Backward, false -> SearchKind.Backward

        let before = TextViewUtil.GetCaretPoint _textView
        let rec inner (ki:KeyInput) = 
            match _incrementalSearch.Process ki with
            | SearchComplete(searchData, searchResult) ->
                let motion = Motion.Search (searchData.Text.RawText, kind)
                let data = { Motion = motion; MotionArgument = motionArgument }
                MotionResult.Complete (data, None)
            | SearchNotStarted -> MotionResult.Cancelled
            | SearchCancelled -> MotionResult.Cancelled
            | SearchNeedMore ->  MotionResult.NeedMoreInput (Some KeyRemapMode.Command, inner)
        _incrementalSearch.Begin kind
        MotionResult.NeedMoreInput (Some KeyRemapMode.Command, inner)

    let SimpleMotions =  
        let motionSeq : (string * MotionFlags * Motion) seq = 
            seq { 
                yield (
                    "w", 
                    MotionFlags.CursorMovement,
                    Motion.WordForward WordKind.NormalWord)
                yield (
                    "<S-Right>",
                    MotionFlags.CursorMovement,
                    Motion.WordForward WordKind.NormalWord)
                yield (
                    "W", 
                    MotionFlags.CursorMovement,
                    Motion.WordForward WordKind.BigWord)
                yield (
                    "<C-Right>", 
                    MotionFlags.CursorMovement,
                    Motion.WordForward WordKind.BigWord)
                yield (
                    "b", 
                    MotionFlags.CursorMovement,
                    Motion.WordBackward WordKind.NormalWord)
                yield (
                    "<S-Left>", 
                    MotionFlags.CursorMovement,
                    Motion.WordBackward WordKind.NormalWord)
                yield (
                    "B", 
                    MotionFlags.CursorMovement,
                    Motion.WordBackward WordKind.BigWord)
                yield (
                    "<C-Left>", 
                    MotionFlags.CursorMovement,
                    Motion.WordBackward WordKind.BigWord)
                yield (
                    "$", 
                    MotionFlags.CursorMovement,
                    Motion.EndOfLine)
                yield (
                    "<End>", 
                    MotionFlags.CursorMovement,
                    Motion.EndOfLine)
                yield (
                    "^", 
                    MotionFlags.CursorMovement,
                    Motion.FirstNonWhiteSpaceOnLine)
                yield (
                    "0", 
                    MotionFlags.CursorMovement,
                    Motion.BeginingOfLine)
                yield (
                    "e", 
                    MotionFlags.CursorMovement,
                    Motion.EndOfWord WordKind.NormalWord)
                yield (
                    "E", 
                    MotionFlags.CursorMovement,
                    Motion.EndOfWord WordKind.BigWord)
                yield (
                    "h", 
                    MotionFlags.CursorMovement,
                    Motion.CharLeft)
                yield (
                    "<Left>", 
                    MotionFlags.CursorMovement,
                    Motion.CharLeft)
                yield (
                    "<Bs>", 
                    MotionFlags.CursorMovement,
                    Motion.CharLeft)
                yield (
                    "<C-h>", 
                    MotionFlags.CursorMovement,
                    Motion.CharLeft)
                yield (
                    "l", 
                    MotionFlags.CursorMovement,
                    Motion.CharRight)
                yield (
                    "<Right>", 
                    MotionFlags.CursorMovement,
                    Motion.CharRight)
                yield (
                    "<Space>", 
                    MotionFlags.CursorMovement,
                    Motion.CharRight)
                yield (
                    "k", 
                    MotionFlags.CursorMovement,
                    Motion.LineUp)
                yield (
                    "<Up>", 
                    MotionFlags.CursorMovement,
                    Motion.LineUp)
                yield (
                    "<C-p>", 
                    MotionFlags.CursorMovement,
                    Motion.LineUp)
                yield (
                    "j", 
                    MotionFlags.CursorMovement,
                    Motion.LineDown)
                yield (
                    "<Down>", 
                    MotionFlags.CursorMovement,
                    Motion.LineDown)
                yield (
                    "<C-n>", 
                    MotionFlags.CursorMovement,
                    Motion.LineDown)
                yield (
                    "<C-j>", 
                    MotionFlags.CursorMovement,
                    Motion.LineDown)
                yield (
                    "+", 
                    MotionFlags.CursorMovement,
                    Motion.LineDownToFirstNonWhiteSpace)
                yield (
                    "_", 
                    MotionFlags.CursorMovement,
                    Motion.LineDownToFirstNonWhiteSpace)
                yield (
                    "<C-m>", 
                    MotionFlags.CursorMovement,
                    Motion.LineDownToFirstNonWhiteSpace)
                yield (
                    "-", 
                    MotionFlags.CursorMovement,
                    Motion.LineUpToFirstNonWhiteSpace)
                yield (
                    "(", 
                    MotionFlags.CursorMovement,
                    Motion.SentenceBackward)
                yield (
                    ")", 
                    MotionFlags.CursorMovement,
                    Motion.SentenceForward)
                yield (
                    "{", 
                    MotionFlags.CursorMovement,
                    Motion.ParagraphBackward)
                yield (
                    "}", 
                    MotionFlags.CursorMovement,
                    Motion.ParagraphForward)
                yield (
                    "g_", 
                    MotionFlags.CursorMovement,
                    Motion.LastNonWhiteSpaceOnLine)
                yield (
                    "aw", 
                    MotionFlags.TextObjectSelection,
                    Motion.AllWord WordKind.NormalWord)
                yield (
                    "aW", 
                    MotionFlags.TextObjectSelection,
                    Motion.AllWord WordKind.BigWord)
                yield (
                    "as", 
                    MotionFlags.CursorMovement,
                    Motion.AllSentence)
                yield (
                    "ap", 
                    MotionFlags.CursorMovement,
                    Motion.AllParagraph)
                yield (
                    "]]", 
                    MotionFlags.CursorMovement,
                    Motion.SectionForwardOrCloseBrace)
                yield (
                    "][", 
                    MotionFlags.CursorMovement,
                    Motion.SectionForwardOrOpenBrace)
                yield (
                    "[[", 
                    MotionFlags.CursorMovement,
                    Motion.SectionBackwardOrOpenBrace)
                yield (
                    "[]", 
                    MotionFlags.CursorMovement,
                    Motion.SectionBackwardOrCloseBrace)
                yield (
                    "a\"", 
                    MotionFlags.TextObjectSelection,
                    Motion.QuotedString)
                yield (
                    "a'", 
                    MotionFlags.TextObjectSelection,
                    Motion.QuotedString)
                yield (
                    "a`", 
                    MotionFlags.TextObjectSelection,
                    Motion.QuotedString)
                yield (
                    "i\"", 
                    MotionFlags.TextObjectSelection,
                    Motion.QuotedStringContents)
                yield (
                    "i'", 
                    MotionFlags.TextObjectSelection,
                    Motion.QuotedStringContents)
                yield (
                    "i`", 
                    MotionFlags.TextObjectSelection,
                    Motion.QuotedStringContents)
                yield (
                    "G", 
                    MotionFlags.CursorMovement,
                    Motion.LineOrLastToFirstNonWhiteSpace)
                yield (
                    "H", 
                    MotionFlags.CursorMovement,
                    Motion.LineFromTopOfVisibleWindow)
                yield (
                    "L", 
                    MotionFlags.CursorMovement,
                    Motion.LineFromBottomOfVisibleWindow)
                yield (
                    "M", 
                    MotionFlags.CursorMovement,
                    Motion.LineInMiddleOfVisibleWindow)
                yield (
                    ";", 
                    MotionFlags.CursorMovement,
                    Motion.RepeatLastCharSearch)
                yield (
                    "%",
                    MotionFlags.CursorMovement,
                    Motion.MatchingToken)
                yield (
                    ",", 
                    MotionFlags.CursorMovement,
                    Motion.RepeatLastCharSearchOpposite)
                yield ( 
                    "gg", 
                    MotionFlags.CursorMovement,
                    Motion.LineOrFirstToFirstNonWhiteSpace)
            } 
            
        motionSeq 
        |> Seq.map (fun (str,flags,func) ->
            let name = KeyNotationUtil.StringToKeyInputSet str
            SimpleMotionCommand(name, flags, func)  )
    
    let ComplexMotions = 
        let motionSeq : (string * MotionFlags * (MotionArgument -> MotionResult)) seq = 
            seq {
                yield (
                    "f", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.ToChar, Direction.Forward, c)))
                yield (
                    "t", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.TillChar, Direction.Forward, c)))
                yield (
                    "F", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.ToChar, Direction.Backward, c)))
                yield (
                    "T", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.TillChar, Direction.Backward, c)))
                yield (
                    "'",
                    MotionFlags.None,   // Cursor movement has different semantics than the motion
                    GetChar (fun c -> Motion.Mark c))
                yield (
                    "`",
                    MotionFlags.None,   // Cursor movement has different semantics than the motion
                    GetChar (fun c -> Motion.Mark c))
                yield (
                    "/",
                    MotionFlags.CursorMovement ||| MotionFlags.HandlesEscape,
                    IncrementalSearch Direction.Forward)
                yield (
                    "?",
                    MotionFlags.CursorMovement ||| MotionFlags.HandlesEscape,
                    IncrementalSearch Direction.Backward)
            } 
        motionSeq
        |> Seq.map (fun (str,flags,func) -> 
                let name = KeyNotationUtil.StringToKeyInputSet str 
                ComplexMotionCommand(name, flags, func))
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionCommands = AllMotionsCore 

    let MotionCommandsMap = AllMotionsCore |> Seq.map (fun command ->  (command.KeyInputSet, command)) |> Map.ofSeq

    /// This continuation will run until the name of the motion is complete, 
    /// errors or is cancelled by the user
    member x.WaitForMotionName arg ki =
        let rec inner (previousName : KeyInputSet) (ki : KeyInput) =
            if ki = KeyInputUtil.EscapeKey then 
                // User hit escape so abandon the motion
                MotionResult.Cancelled 
            else
                let name = previousName.Add ki
                match Map.tryFind name MotionCommandsMap with
                | Some(command) -> 
                    match command with 
                    | SimpleMotionCommand(_, _ , motion) -> 
                        // Simple motions don't need any extra information so we can 
                        // return them directly
                        let data = { Motion = motion; MotionArgument = arg }
                        MotionResult.Complete  (data, None)
                    | ComplexMotionCommand(_, _, func) -> 
                        // Complex motions need further input so delegate off
                        func arg
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
                    x.WaitForMotionName arg nextKi
                | NeedMore(nextFunc) -> MotionResult.NeedMoreInput (None, inner nextFunc)
        inner (CountCapture.Process) ki

    member x.GetOperatorMotion (ki : KeyInput) operatorCountOpt =
        let arg = { MotionContext = MotionContext.AfterOperator; OperatorCount = operatorCountOpt; MotionCount=None }
        if ki = KeyInputUtil.EscapeKey then MotionResult.Cancelled
        elif ki.IsDigit && ki.Char <> '0' then x.WaitforCount ki arg
        else x.WaitForMotionName arg ki

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionCommands = MotionCommands
        member x.GetOperatorMotion ki count = x.GetOperatorMotion ki count

