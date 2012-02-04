#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;

type internal MotionCapture 
    (
        _vimBufferData : IVimBufferData,
        _incrementalSearch : IIncrementalSearch
    ) = 

    let _textView = _vimBufferData.TextView
    let _vimHost = _vimBufferData.Vim.VimHost
    let _localSettings = _vimBufferData.LocalSettings

    static let SharedMotions =  
        let motionSeq : (string * MotionFlags * Motion) seq = 
            seq { 
                yield ("ab", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Paren)
                yield ("aB", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.CurlyBracket)
                yield ("ap", MotionFlags.CursorMovement, Motion.AllParagraph)
                yield ("as", MotionFlags.CursorMovement, Motion.AllSentence)
                yield ("aw", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.AllWord WordKind.NormalWord)
                yield ("aW", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.AllWord WordKind.BigWord)
                yield ("a\"", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedString)
                yield ("a'", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedString)
                yield ("a`", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedString)
                yield ("a]", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Bracket)
                yield ("a[", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Bracket)
                yield ("a)", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Paren)
                yield ("a(", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Paren)
                yield ("a<", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.AngleBracket)
                yield ("a>", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.AngleBracket)
                yield ("a{", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.CurlyBracket)
                yield ("a}", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.CurlyBracket)
                yield ("b", MotionFlags.CursorMovement, Motion.WordBackward WordKind.NormalWord)
                yield ("B", MotionFlags.CursorMovement, Motion.WordBackward WordKind.BigWord)
                yield ("e", MotionFlags.CursorMovement, Motion.EndOfWord WordKind.NormalWord)
                yield ("E", MotionFlags.CursorMovement, Motion.EndOfWord WordKind.BigWord)
                yield ("gg", MotionFlags.CursorMovement, Motion.LineOrFirstToFirstNonBlank)
                yield ("g_", MotionFlags.CursorMovement, Motion.LastNonBlankOnLine)
                yield ("g*", MotionFlags.CursorMovement, Motion.NextPartialWord Path.Forward)
                yield ("g#", MotionFlags.CursorMovement, Motion.NextPartialWord Path.Backward)
                yield ("G", MotionFlags.CursorMovement, Motion.LineOrLastToFirstNonBlank)
                yield ("h", MotionFlags.CursorMovement, Motion.CharLeft)
                yield ("H", MotionFlags.CursorMovement, Motion.LineFromTopOfVisibleWindow)
                yield ("ib", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Paren)
                yield ("iB", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.CurlyBracket)
                yield ("iw", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.InnerWord WordKind.NormalWord)
                yield ("iW", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.InnerWord WordKind.BigWord)
                yield ("i\"", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedStringContents)
                yield ("i'", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedStringContents)
                yield ("i`", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedStringContents)
                yield ("i]", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Bracket)
                yield ("i[", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Bracket)
                yield ("i)", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Paren)
                yield ("i(", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Paren)
                yield ("i<", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.AngleBracket)
                yield ("i>", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.AngleBracket)
                yield ("i{", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.CurlyBracket)
                yield ("i}", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.CurlyBracket)
                yield ("j", MotionFlags.CursorMovement, Motion.LineDown)
                yield ("k", MotionFlags.CursorMovement, Motion.LineUp)
                yield ("l", MotionFlags.CursorMovement, Motion.CharRight)
                yield ("M", MotionFlags.CursorMovement, Motion.LineInMiddleOfVisibleWindow)
                yield ("n", MotionFlags.CursorMovement, Motion.LastSearch false)
                yield ("N", MotionFlags.CursorMovement, Motion.LastSearch true)
                yield ("L", MotionFlags.CursorMovement, Motion.LineFromBottomOfVisibleWindow)
                yield ("w", MotionFlags.CursorMovement, Motion.WordForward WordKind.NormalWord)
                yield ("W", MotionFlags.CursorMovement, Motion.WordForward WordKind.BigWord)
                yield ("<End>", MotionFlags.CursorMovement, Motion.EndOfLine)
                yield ("<C-Home>", MotionFlags.CursorMovement, Motion.LineOrFirstToFirstNonBlank)
                yield ("<C-Right>", MotionFlags.CursorMovement, Motion.WordForward WordKind.BigWord)
                yield ("<C-Left>", MotionFlags.CursorMovement, Motion.WordBackward WordKind.BigWord)
                yield ("<C-h>", MotionFlags.CursorMovement, Motion.CharLeft)
                yield ("<C-j>", MotionFlags.CursorMovement, Motion.LineDown)
                yield ("<C-m>", MotionFlags.CursorMovement, Motion.LineDownToFirstNonBlank)
                yield ("<C-n>", MotionFlags.CursorMovement, Motion.LineDown)
                yield ("<C-p>", MotionFlags.CursorMovement, Motion.LineUp)
                yield ("<Down>", MotionFlags.CursorMovement, Motion.LineDown)
                yield ("<S-Left>", MotionFlags.CursorMovement, Motion.WordBackward WordKind.NormalWord)
                yield ("<S-Right>", MotionFlags.CursorMovement, Motion.WordForward WordKind.NormalWord)
                yield ("<Left>", MotionFlags.CursorMovement, Motion.CharLeft)
                yield ("<Bs>", MotionFlags.CursorMovement, Motion.CharLeft)
                yield ("<Right>", MotionFlags.CursorMovement, Motion.CharRight)
                yield ("<Space>", MotionFlags.CursorMovement, Motion.CharRight)
                yield ("<Up>", MotionFlags.CursorMovement, Motion.LineUp)
                yield ("$", MotionFlags.CursorMovement, Motion.EndOfLine)
                yield ("^", MotionFlags.CursorMovement, Motion.FirstNonBlankOnCurrentLine)
                yield ("0", MotionFlags.CursorMovement, Motion.BeginingOfLine)
                yield ("+", MotionFlags.CursorMovement, Motion.LineDownToFirstNonBlank)
                yield ("_", MotionFlags.CursorMovement, Motion.FirstNonBlankOnLine)
                yield ("-", MotionFlags.CursorMovement, Motion.LineUpToFirstNonBlank)
                yield ("(", MotionFlags.CursorMovement, Motion.SentenceBackward)
                yield (")", MotionFlags.CursorMovement, Motion.SentenceForward)
                yield ("{", MotionFlags.CursorMovement, Motion.ParagraphBackward)
                yield ("}", MotionFlags.CursorMovement, Motion.ParagraphForward)
                yield ("]]", MotionFlags.CursorMovement, Motion.SectionForward)
                yield ("][", MotionFlags.CursorMovement, Motion.SectionForwardOrCloseBrace)
                yield ("[[", MotionFlags.CursorMovement, Motion.SectionBackwardOrOpenBrace)
                yield ("[]", MotionFlags.CursorMovement, Motion.SectionBackwardOrCloseBrace)
                yield (";", MotionFlags.CursorMovement, Motion.RepeatLastCharSearch)
                yield ("%", MotionFlags.CursorMovement, Motion.MatchingToken)
                yield (",", MotionFlags.CursorMovement, Motion.RepeatLastCharSearchOpposite)
                yield ("*", MotionFlags.CursorMovement, Motion.NextWord Path.Forward)
                yield ("#", MotionFlags.CursorMovement, Motion.NextWord Path.Backward)
            } 
            
        motionSeq 
        |> Seq.map (fun (str, flags, motion) ->
            let name = KeyNotationUtil.StringToKeyInputSet str
            MotionBinding.Simple (name, flags, motion))
        |> List.ofSeq

    /// Get a char and use the provided 'func' to create a Motion value.
    let GetChar func = 
        let data = BindData<_>.CreateForSingleChar (Some KeyRemapMode.Language) func
        BindDataStorage<_>.Simple data

    /// Get a local mark and us the provided 'func' to create a Motion value
    let GetLocalMark func = 
        let bindFunc (keyInput : KeyInput) =
            match LocalMark.OfChar keyInput.Char with
            | None -> BindResult<Motion>.Error
            | Some localMark -> BindResult<_>.Complete (func localMark)
        let bindData = {
            KeyRemapMode = Some KeyRemapMode.Language
            BindFunction = bindFunc }
        BindDataStorage<_>.Simple bindData

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
                | SearchResult.Found (searchData, _, _) -> Motion.Search searchData.PatternData
                | SearchResult.NotFound (searchData, _) -> Motion.Search searchData.PatternData)

        BindDataStorage.Complex activateFunc

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
                    GetLocalMark (fun localMark -> Motion.MarkLine localMark))
                yield (
                    "`",
                    MotionFlags.None,   // Cursor movement has different semantics than the motion
                    GetLocalMark (fun localMark -> Motion.Mark localMark))
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
        let complex = ComplexMotions 
        SharedMotions |> Seq.append complex

    let MotionBindings = AllMotionsCore

    let MotionBindingsMap = AllMotionsCore |> Seq.map (fun command ->  (command.KeyInputSet, command)) |> Map.ofSeq

    /// Get the Motion value for the given KeyInput.  Will return a BindResult<Motion> which 
    /// digs through the values until a valid Motion result is detected 
    member x.GetMotion keyInput = 
        let rec inner (previousName : KeyInputSet) keyInput =
            if keyInput = KeyInputUtil.EscapeKey then 
                // User hit escape so abandon the motion
                BindResult.Cancelled 
            else
                let name = previousName.Add keyInput
                match Map.tryFind name MotionBindingsMap with
                | Some(command) -> 
                    match command with 
                    | MotionBinding.Simple (_, _ , motion) -> 
                        // Simple motions don't need any extra information so we can 
                        // return them directly
                        BindResult.Complete motion
                    | MotionBinding.Complex (_, _, bindDataStorage) -> 
                        // Complex motions need further input so delegate off
                        let bindData = bindDataStorage.CreateBindData()
                        BindResult.NeedMoreInput bindData
                | None -> 
                    let res = MotionBindingsMap |> Seq.filter (fun pair -> pair.Key.StartsWith name) 
                    if Seq.isEmpty res then 
                        BindResult.Error
                    else 
                        let bindData = { KeyRemapMode = None; BindFunction = inner name }
                        BindResult.NeedMoreInput bindData
        inner Empty keyInput

    /// Get the Motion value and associated count beginning with the specified KeyInput value
    member x.GetMotionAndCount keyInput =
        let result = CountCapture.GetCount None keyInput
        result.Map (fun (count, keyInput) -> 
            let result = x.GetMotion keyInput
            result.Convert (fun motion -> (motion, count)))

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionBindings = MotionBindings
        member x.GetMotionAndCount ki = x.GetMotionAndCount ki
        member x.GetMotion ki = x.GetMotion ki

