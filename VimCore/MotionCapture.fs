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
    let GetChar func = 
        let data = BindData<_>.CreateForSingleChar (Some KeyRemapMode.Language) func
        BindDataStorage<_>.Simple data

    /// Handles incremental searches (/ and ?).  Retrieve the BindData storage for
    /// the activation
    let IncrementalSearch path =

        // This is the function which will activate once the / or ? is bound in 
        // the IMotionCapture interface
        let activateFunc () = 

            // Store the caret point before the search begins
            let before = TextViewUtil.GetCaretPoint _textView
            let result = _incrementalSearch.Begin path

            result.Convert (fun searchResult ->
                match searchResult with
                | SearchResult.Found (searchData, _, _) -> Motion.Search searchData
                | SearchResult.NotFound (searchData, _) -> Motion.Search searchData)

        BindDataStorage.Complex activateFunc

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
                    Motion.SectionForwardOrOpenBrace)
                yield (
                    "][", 
                    MotionFlags.CursorMovement,
                    Motion.SectionForwardOrCloseBrace)
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
        |> Seq.map (fun (str, flags, motion) ->
            let name = KeyNotationUtil.StringToKeyInputSet str
            MotionBinding.Simple (name, flags, motion))
    
    let ComplexMotions = 
        let motionSeq : (string * MotionFlags * BindDataStorage<Motion>) seq = 
            seq {
                yield (
                    "f", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.ToChar, Path.Forward, c)))
                yield (
                    "t", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.TillChar, Path.Forward, c)))
                yield (
                    "F", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.ToChar, Path.Backward, c)))
                yield (
                    "T", 
                    MotionFlags.CursorMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.TillChar, Path.Backward, c)))
                yield (
                    "'",
                    MotionFlags.None,   // Cursor movement has different semantics than the motion
                    GetChar (fun c -> Motion.MarkLine c))
                yield (
                    "`",
                    MotionFlags.None,   // Cursor movement has different semantics than the motion
                    GetChar (fun c -> Motion.Mark c))
                yield (
                    "/",
                    MotionFlags.CursorMovement ||| MotionFlags.HandlesEscape,
                    IncrementalSearch Path.Forward)
                yield (
                    "?",
                    MotionFlags.CursorMovement ||| MotionFlags.HandlesEscape,
                    IncrementalSearch Path.Backward)
            } 
        motionSeq
        |> Seq.map (fun (str, flags, bindDataStorage) -> 
                let name = KeyNotationUtil.StringToKeyInputSet str 
                MotionBinding.Complex (name, flags, bindDataStorage))
    
    let AllMotionsCore =
        let simple = SimpleMotions 
        let complex = ComplexMotions 
        simple |> Seq.append complex

    let MotionBindings = AllMotionsCore

    let MotionBindingsMap = AllMotionsCore |> Seq.map (fun command ->  (command.KeyInputSet, command)) |> Map.ofSeq

    /// This continuation will run until the name of the motion is complete, 
    /// errors or is cancelled by the user
    member x.WaitForMotionName ki motionCountOpt =
        let rec inner (previousName : KeyInputSet) (ki : KeyInput) =
            if ki = KeyInputUtil.EscapeKey then 
                // User hit escape so abandon the motion
                BindResult.Cancelled 
            else
                let name = previousName.Add ki
                match Map.tryFind name MotionBindingsMap with
                | Some(command) -> 
                    match command with 
                    | MotionBinding.Simple (_, _ , motion) -> 
                        // Simple motions don't need any extra information so we can 
                        // return them directly
                        BindResult.Complete (motion, motionCountOpt)
                    | MotionBinding.Complex (_, _, bindDataStorage) -> 
                        // Complex motions need further input so delegate off
                        let bindData = bindDataStorage.CreateBindData().Convert (fun motion -> (motion, motionCountOpt))
                        BindResult.NeedMoreInput bindData
                | None -> 
                    let res = MotionBindingsMap |> Seq.filter (fun pair -> pair.Key.StartsWith name) 
                    if Seq.isEmpty res then 
                        BindResult.Error
                    else 
                        let bindData = { KeyRemapMode = None; BindFunction = inner name }
                        BindResult.NeedMoreInput bindData
        inner Empty ki

    /// Wait for the completion of the motion count
    member x.WaitforCount ki =
        let rec inner (processFunc: KeyInput->CountResult) (ki:KeyInput)  =               
            if ki = KeyInputUtil.EscapeKey then 
                BindResult.Cancelled 
            else
                match processFunc ki with 
                | CountResult.Complete(count,nextKi) -> 
                    x.WaitForMotionName nextKi (Some count)
                | NeedMore(nextFunc) -> 
                    BindResult<MotionData>.CreateNeedMoreInput None (inner nextFunc)
        inner (CountCapture.Process) ki

    member x.GetOperatorMotion (ki : KeyInput) =
        if ki = KeyInputUtil.EscapeKey then BindResult.Cancelled
        elif ki.IsDigit && ki.Char <> '0' then x.WaitforCount ki
        else x.WaitForMotionName ki None

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionBindings = MotionBindings
        member x.GetOperatorMotion ki = x.GetOperatorMotion ki

