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
                yield ("at", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.TagBlock TagBlockKind.All)
                yield ("ap", MotionFlags.CaretMovement, Motion.AllParagraph)
                yield ("as", MotionFlags.CaretMovement, Motion.AllSentence)
                yield ("aw", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.AllWord WordKind.NormalWord)
                yield ("aW", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.AllWord WordKind.BigWord)
                yield ("a\"", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedString '"')
                yield ("a'", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedString '\'')
                yield ("a`", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedString '`')
                yield ("a]", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Bracket)
                yield ("a[", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Bracket)
                yield ("a)", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Paren)
                yield ("a(", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.Paren)
                yield ("a<", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.AngleBracket)
                yield ("a>", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.AngleBracket)
                yield ("a{", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.CurlyBracket)
                yield ("a}", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.AllBlock BlockKind.CurlyBracket)
                yield ("b", MotionFlags.CaretMovement, Motion.WordBackward WordKind.NormalWord)
                yield ("B", MotionFlags.CaretMovement, Motion.WordBackward WordKind.BigWord)
                yield ("e", MotionFlags.CaretMovement, Motion.EndOfWord WordKind.NormalWord)
                yield ("E", MotionFlags.CaretMovement, Motion.EndOfWord WordKind.BigWord)
                yield ("ge", MotionFlags.CaretMovement, Motion.BackwardEndOfWord WordKind.NormalWord)
                yield ("gE", MotionFlags.CaretMovement, Motion.BackwardEndOfWord WordKind.BigWord)
                yield ("gg", MotionFlags.CaretMovement, Motion.LineOrFirstToFirstNonBlank)
                yield ("gj", MotionFlags.CaretMovement, Motion.DisplayLineDown)
                yield ("gk", MotionFlags.CaretMovement, Motion.DisplayLineUp)
                yield ("gm", MotionFlags.CaretMovement, Motion.DisplayLineMiddleOfScreen)
                yield ("g0", MotionFlags.CaretMovement, Motion.DisplayLineStart)
                yield ("g$", MotionFlags.CaretMovement, Motion.DisplayLineEnd)
                yield ("g_", MotionFlags.CaretMovement, Motion.LastNonBlankOnLine)
                yield ("g*", MotionFlags.CaretMovement, Motion.NextPartialWord Path.Forward)
                yield ("g#", MotionFlags.CaretMovement, Motion.NextPartialWord Path.Backward)
                yield ("g<Home>", MotionFlags.CaretMovement, Motion.DisplayLineStart)
                yield ("g<End>", MotionFlags.CaretMovement, Motion.DisplayLineEnd)
                yield ("G", MotionFlags.CaretMovement, Motion.LineOrLastToFirstNonBlank)
                yield ("h", MotionFlags.CaretMovement, Motion.CharLeft)
                yield ("H", MotionFlags.CaretMovement, Motion.LineFromTopOfVisibleWindow)
                yield ("ib", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Paren)
                yield ("iB", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.CurlyBracket)
                yield ("it", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.TagBlock TagBlockKind.Inner)
                yield ("iw", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.InnerWord WordKind.NormalWord)
                yield ("iW", MotionFlags.TextObject ||| MotionFlags.TextObjectWithLineToCharacter, Motion.InnerWord WordKind.BigWord)
                yield ("i\"", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedStringContents '"')
                yield ("i'", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedStringContents '\'')
                yield ("i`", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.QuotedStringContents '`')
                yield ("i]", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Bracket)
                yield ("i[", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Bracket)
                yield ("i)", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Paren)
                yield ("i(", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.Paren)
                yield ("i<", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.AngleBracket)
                yield ("i>", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.AngleBracket)
                yield ("i{", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.CurlyBracket)
                yield ("i}", MotionFlags.TextObject ||| MotionFlags.TextObjectWithAlwaysCharacter, Motion.InnerBlock BlockKind.CurlyBracket)
                yield ("j", MotionFlags.CaretMovement, Motion.LineDown)
                yield ("k", MotionFlags.CaretMovement, Motion.LineUp)
                yield ("l", MotionFlags.CaretMovement, Motion.CharRight)
                yield ("M", MotionFlags.CaretMovement, Motion.LineInMiddleOfVisibleWindow)
                yield ("n", MotionFlags.CaretMovement, Motion.LastSearch false)
                yield ("N", MotionFlags.CaretMovement, Motion.LastSearch true)
                yield ("L", MotionFlags.CaretMovement, Motion.LineFromBottomOfVisibleWindow)
                yield ("w", MotionFlags.CaretMovement, Motion.WordForward WordKind.NormalWord)
                yield ("W", MotionFlags.CaretMovement, Motion.WordForward WordKind.BigWord)
                yield ("<Bs>", MotionFlags.CaretMovement, Motion.SpaceLeft)
                yield ("<C-Home>", MotionFlags.CaretMovement, Motion.LineOrFirstToFirstNonBlank)
                yield ("<C-h>", MotionFlags.CaretMovement, Motion.SpaceLeft)
                yield ("<C-Right>", MotionFlags.CaretMovement, Motion.WordForward WordKind.BigWord)
                yield ("<C-Left>", MotionFlags.CaretMovement, Motion.WordBackward WordKind.BigWord)
                yield ("<C-j>", MotionFlags.CaretMovement, Motion.LineDown)
                yield ("<C-m>", MotionFlags.CaretMovement, Motion.LineDownToFirstNonBlank)
                yield ("<C-n>", MotionFlags.CaretMovement, Motion.LineDown)
                yield ("<C-p>", MotionFlags.CaretMovement, Motion.LineUp)
                yield ("<Down>", MotionFlags.CaretMovement, Motion.LineDown)
                yield ("<End>", MotionFlags.CaretMovement, Motion.EndOfLine)
                yield ("<Home>", MotionFlags.CaretMovement, Motion.BeginingOfLine)
                yield ("<Left>", MotionFlags.CaretMovement, Motion.ArrowLeft)
                yield ("<Right>", MotionFlags.CaretMovement, Motion.ArrowRight)
                yield ("<Space>", MotionFlags.CaretMovement, Motion.SpaceRight)
                yield ("<S-Left>", MotionFlags.CaretMovement, Motion.WordBackward WordKind.NormalWord)
                yield ("<S-Space>", MotionFlags.CaretMovement, Motion.WordForward WordKind.NormalWord)
                yield ("<S-Right>", MotionFlags.CaretMovement, Motion.WordForward WordKind.NormalWord)
                yield ("<Up>", MotionFlags.CaretMovement, Motion.LineUp)
                yield ("$", MotionFlags.CaretMovement, Motion.EndOfLine)
                yield ("^", MotionFlags.CaretMovement, Motion.FirstNonBlankOnCurrentLine)
                yield ("0", MotionFlags.CaretMovement, Motion.BeginingOfLine)
                yield ("|", MotionFlags.CaretMovement, Motion.ScreenColumn)
                yield ("+", MotionFlags.CaretMovement, Motion.LineDownToFirstNonBlank)
                yield ("_", MotionFlags.CaretMovement, Motion.FirstNonBlankOnLine)
                yield ("-", MotionFlags.CaretMovement, Motion.LineUpToFirstNonBlank)
                yield ("(", MotionFlags.CaretMovement, Motion.SentenceBackward)
                yield (")", MotionFlags.CaretMovement, Motion.SentenceForward)
                yield ("{", MotionFlags.CaretMovement, Motion.ParagraphBackward)
                yield ("}", MotionFlags.CaretMovement, Motion.ParagraphForward)
                yield ("[[", MotionFlags.CaretMovement, Motion.SectionBackwardOrOpenBrace)
                yield ("[]", MotionFlags.CaretMovement, Motion.SectionBackwardOrCloseBrace)
                yield ("[(", MotionFlags.CaretMovement, Motion.UnmatchedToken (Path.Backward, UnmatchedTokenKind.Paren))
                yield ("[{", MotionFlags.CaretMovement, Motion.UnmatchedToken (Path.Backward, UnmatchedTokenKind.CurlyBracket))
                yield ("]]", MotionFlags.CaretMovement, Motion.SectionForward)
                yield ("][", MotionFlags.CaretMovement, Motion.SectionForwardOrCloseBrace)
                yield ("])", MotionFlags.CaretMovement, Motion.UnmatchedToken (Path.Forward, UnmatchedTokenKind.Paren))
                yield ("]}", MotionFlags.CaretMovement, Motion.UnmatchedToken (Path.Forward, UnmatchedTokenKind.CurlyBracket))
                yield (";", MotionFlags.CaretMovement, Motion.RepeatLastCharSearch)
                yield ("%", MotionFlags.CaretMovement, Motion.MatchingToken)
                yield (",", MotionFlags.CaretMovement, Motion.RepeatLastCharSearchOpposite)
                yield ("*", MotionFlags.CaretMovement, Motion.NextWord Path.Forward)
                yield ("#", MotionFlags.CaretMovement, Motion.NextWord Path.Backward)
            } 
            
        motionSeq 
        |> Seq.map (fun (str, flags, motion) ->
            let name = KeyNotationUtil.StringToKeyInputSet str
            MotionBinding.Simple (name, flags, motion))
        |> List.ofSeq

    /// Get a char and use the provided 'func' to create a Motion value.
    let GetChar func = 
        let data = BindData<_>.CreateForSingleChar KeyRemapMode.Language func
        BindDataStorage<_>.Simple data

    /// Get a local mark and us the provided 'func' to create a Motion value
    let GetLocalMark func = 
        let bindFunc (keyInput : KeyInput) =
            match LocalMark.OfChar keyInput.Char with
            | None -> BindResult<Motion>.Error
            | Some localMark -> BindResult<_>.Complete (func localMark)
        let bindData = {
            KeyRemapMode = KeyRemapMode.Language
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
                | SearchResult.Found (searchData, _, _, _) -> Motion.Search searchData
                | SearchResult.NotFound (searchData, _) -> Motion.Search searchData)

        BindDataStorage.Complex activateFunc

    let ComplexMotions = 
        let motionSeq : (string * MotionFlags * BindDataStorage<Motion>) seq = 
            seq {
                yield (
                    "f", 
                    MotionFlags.CaretMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.ToChar, Path.Forward, c)))
                yield (
                    "t", 
                    MotionFlags.CaretMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.TillChar, Path.Forward, c)))
                yield (
                    "F", 
                    MotionFlags.CaretMovement,
                    GetChar (fun c -> Motion.CharSearch (CharSearchKind.ToChar, Path.Backward, c)))
                yield (
                    "T", 
                    MotionFlags.CaretMovement,
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
                    MotionFlags.CaretMovement ||| MotionFlags.HandlesEscape,
                    IncrementalSearch Path.Forward)
                yield (
                    "?",
                    MotionFlags.CaretMovement ||| MotionFlags.HandlesEscape,
                    IncrementalSearch Path.Backward)
            } 
        motionSeq
        |> Seq.map (fun (str, flags, bindDataStorage) -> 
                let name = KeyNotationUtil.StringToKeyInputSet str 
                MotionBinding.Complex (name, flags, bindDataStorage))
    
    let AllMotionsCore =
        let complex = ComplexMotions 
        SharedMotions 
        |> Seq.append complex
        |> List.ofSeq

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
                        let bindData = { KeyRemapMode = KeyRemapMode.None; BindFunction = inner name }
                        BindResult.NeedMoreInput bindData
        inner KeyInputSet.Empty keyInput

    /// Get the Motion value and associated count beginning with the specified KeyInput value
    member x.GetMotionAndCount keyInput =
        let result = CountCapture.GetCount KeyRemapMode.None keyInput
        result.Map (fun (count, keyInput) -> 
            let result = x.GetMotion keyInput
            result.Convert (fun motion -> (motion, count)))

    interface IMotionCapture with
        member x.TextView = _textView
        member x.MotionBindings = MotionBindings
        member x.GetMotionAndCount ki = x.GetMotionAndCount ki
        member x.GetMotion ki = x.GetMotion ki

