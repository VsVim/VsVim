#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Collections.Generic
open Vim.Modes
open Vim.StringBuilderExtensions
open Vim.Interpreter

[<RequireQualifiedAccess>]
[<NoComparison>]
type DirectiveKind = 
    | If
    | Elif
    | Else
    | EndIf

type Directive = { 
    Kind : DirectiveKind;
    Span : Span
}
    with

    member x.AdjustStart offset = 
        let start = x.Span.Start + offset
        let span = Span(start, x.Span.Length)
        { x with Span = span }

    override x.ToString() = sprintf "%O - %O" x.Kind x.Span

type DirectiveBlock = {
    Directives : List<Directive>
    IsComplete : bool
}

[<RequireQualifiedAccess>]
[<NoComparison>]
type MatchingTokenKind =

    /// A #if, #else, etc ... directive value
    | Directive

    /// A C style block comment
    | Comment

    // Parens
    | Parens

    | Brackets

    | Braces

type MatchingTokenUtil() = 

    /// This is the key for accessing directive blocks within the ITextSnapshot.  This
    /// lets us avoid multiple parses of #if for a single ITextSnapshot
    static let _directiveBlocksKey = obj()

    ///Find the directive, if any, that starts on this line
    member x.ParseDirective lineText =

        // Don't break out the tokenizer unless the line starts with a # on the first
        // non-blank character.  This is a quick optimization to avoid a lot of 
        // unnecessary allocations
        let startsWithPound = 
            let length = String.length lineText
            let mutable index = 0 
            let mutable found = false
            while index < length do
                let c = lineText.[index]
                if CharUtil.IsBlank c then
                    // Continue
                    index <- index + 1
                elif c = '#' then
                    found <- true
                    index <- length
                else
                    index <- length
            found

        if startsWithPound then 
            let tokenizer = Tokenizer(lineText, TokenizerFlags.SkipBlanks)
            match tokenizer.CurrentChar with
            | '#' ->
                let start = tokenizer.CurrentToken.StartIndex
                tokenizer.MoveNextToken()

                // Process the token kind and the token to determine the span of the 
                // directive on this line 
                let func kind (token : Token) = 
                    let endPosition = token.StartIndex + token.Length
                    let span = Span.FromBounds(start, endPosition)
                    { Kind = kind; Span = span } |> Some

                // The span of the token for the #if variety blocks is the entire line
                let all kind (token : Token) = 
                    let span = Span.FromBounds(start, lineText.Length)
                    { Kind = kind; Span = span; } |> Some

                match tokenizer.CurrentToken.TokenText with
                | "if" -> all DirectiveKind.If tokenizer.CurrentToken
                | "ifdef" -> all DirectiveKind.If tokenizer.CurrentToken
                | "ifndef" -> all DirectiveKind.If tokenizer.CurrentToken
                | "elif" -> all DirectiveKind.Elif tokenizer.CurrentToken
                | "else" -> func DirectiveKind.Else tokenizer.CurrentToken
                | "endif" -> func DirectiveKind.EndIf tokenizer.CurrentToken
                | _ -> None
            | _ -> None
        else
            None

    /// Parse out the directive blocks for the given ITextSnapshot.  Don't use
    /// this method directly.  Instead go through GetDirectiveBlocks which will
    /// cache the value
    member x.ParseDirectiveBlocks (snapshot : ITextSnapshot) = 

        let lastLineNumber = snapshot.LineCount - 1

        // Get a directive at the specified line number.  Will handle a line
        // number past the end by returning None
        let getDirective lineNumber = 
            if lineNumber > lastLineNumber then
                None
            else
                let line = SnapshotUtil.GetLine snapshot lineNumber
                let text = SnapshotLineUtil.GetText line
                match x.ParseDirective text with
                | None -> None
                | Some directive -> directive.AdjustStart line.Start.Position |> Some

        let allBlocksList = List<DirectiveBlock>()

        // Parse out the remainder of a directive given an initial directive value.  Then
        // return the line number after the completion of this block 
        let rec parseBlockRemainder (startDirective : Directive) lineNumber = 
            let list = List<Directive>()
            list.Add(startDirective)

            let rec inner lineNumber =

                // Parse the next line 
                let parseNext () = inner (lineNumber + 1)
                match getDirective lineNumber with
                | None -> 
                    if lineNumber >= lastLineNumber then
                        let block = { Directives = list; IsComplete = false }
                        allBlocksList.Add block
                        lineNumber
                    else
                        parseNext ()
                | Some directive ->
                    match directive.Kind with
                    | DirectiveKind.If -> 
                        let lineNumber = parseBlockRemainder directive (lineNumber + 1)
                        inner lineNumber
                    | DirectiveKind.Elif ->
                        list.Add directive
                        parseNext ()
                    | DirectiveKind.Else ->
                        list.Add directive
                        parseNext ()
                    | DirectiveKind.EndIf ->
                        list.Add directive
                        let block = { Directives = list; IsComplete = true }
                        allBlocksList.Add block
                        lineNumber + 1

            inner lineNumber

        // Go through every line and drive the parsing of the directives
        let rec parseAll lineNumber = 
            if lineNumber <= lastLineNumber then
                match getDirective lineNumber with
                | None -> parseAll (lineNumber + 1)
                | Some directive ->
                    match directive.Kind with
                    | DirectiveKind.If -> 
                        let nextLineNumber = parseBlockRemainder directive (lineNumber + 1)
                        parseAll nextLineNumber
                    | _ -> 
                        let list = List<Directive>(1)
                        list.Add(directive)
                        let block = { Directives = list; IsComplete = false }
                        allBlocksList.Add block

        parseAll 0
        allBlocksList

    /// Get the directive blocks for the specified ITextSnapshot
    member x.GetDirectiveBlocks (snapshot : ITextSnapshot) = 
        let textBuffer = snapshot.TextBuffer
        let propertyCollection = textBuffer.Properties

        let parseAndSave () = 
            let blocks = x.ParseDirectiveBlocks snapshot
            propertyCollection.[_directiveBlocksKey] <- (snapshot.Version, blocks)
            blocks

        match PropertyCollectionUtil.GetValue<int * List<DirectiveBlock>> _directiveBlocksKey propertyCollection with
        | Some (version, list) ->
            if version = snapshot.Version.VersionNumber then list
            else parseAndSave ()
        | None -> parseAndSave ()

    /// Find the correct MatchingTokenKind for the given line and column position on that 
    /// line.  Needs to consider all possible matching tokens and see which one is closest
    /// to the column (going forward only). 
    member x.FindMatchingTokenKindCore lineText column =
        let length = String.length lineText
        Contract.Assert(column <= length)

        // Reduce the 2 Maybe<int> values to the smaller of the int values or None if
        // both are None
        let reducePair result1 result2 kind = 
            match result1, result2 with
            | None, None -> None
            | None, Some index2 -> Some (index2, kind)
            | Some index1, None -> Some (index1, kind)
            | Some index1, Some index2 -> Some ((min index1 index2), kind)

        // Find the closest index to column for either of these characters.  Whichever is 
        // closest wins.
        let findSimplePair c1 c2 kind = 
            let result1 = StringUtil.indexOfCharAt c1 column lineText
            let result2 = StringUtil.indexOfCharAt c2 column lineText
            reducePair result1 result2 kind

        // Find the closest comment string to the specified column
        let findComment () = 

            // Check at the column and one before if not found at the column
            let indexOf text = 
                let result = StringUtil.indexOfStringAt text column lineText
                match result with
                | None -> 
                    if column > 0 then StringUtil.indexOfStringAt text (column - 1) lineText
                    else None 
                | Some index -> result

            let result1 = indexOf "/*"
            let result2 = indexOf "*/"
            reducePair result1 result2 MatchingTokenKind.Comment

        // Find the directive value on the current line 
        let directive = x.ParseDirective lineText 

        // If the directive exists exactly at the caret point then return it because
        // it should win over the other matching tokens at this point
        let findDirective () = 
            match directive with
            | None -> None
            | Some directive ->
                if directive.Span.Start = column then Some (column, MatchingTokenKind.Directive)
                else None

        // Parse out all the possibilities and find the one that is closest to the 
        // column position
        let found = 
            let list = 
                [
                    findSimplePair '(' ')' MatchingTokenKind.Parens
                    findSimplePair '[' ']' MatchingTokenKind.Brackets
                    findSimplePair '{' '}' MatchingTokenKind.Braces
                    findComment ()
                    findDirective ()
                ]
            List.minBy (fun value ->
                match value with
                | Some (column, _) -> column
                | None -> System.Int32.MaxValue) list

        match found with
        | Some _ -> found
        | None -> 
            // Lastly if there are no tokens that match on the current line but the line is a directive
            // block then it the match is the directive
            match directive with
            | None -> None
            | Some directive -> Some (directive.Span.Start, MatchingTokenKind.Directive)

    member x.FindMatchingTokenKind lineText column =
        match x.FindMatchingTokenKindCore lineText column with
        | Some (_, kind) -> Some kind
        | None -> None

    /// Find the matching token in the given ITextSnapshot for the token
    /// closest to the specified SnapshotPoint
    member x.FindMatchingToken point = 
        let snapshot = SnapshotPointUtil.GetSnapshot point

        // Find the matching character for the one which occurs at the specified
        // SnapshotPoint
        let findMatchingTokenChar target startChar endChar = 
            let stack = Stack<int>()
            let length = SnapshotUtil.GetLength snapshot
            let mutable found : int option = None
            let mutable targetDepth : int option = None
            let mutable index = 0
            while index < length do
                let c = SnapshotUtil.GetChar index snapshot
                if c = startChar then 

                    // If this is our starting character then the matching token occurs
                    // when the depth once again hits the current depth
                    if index = target then
                        targetDepth <- Some stack.Count

                    stack.Push index
                    index <- index + 1
                elif c = endChar then 

                    if index = target then
                        // We are currently at the targeted char and it's an end marker
                        // so whatever the beginning marker is is the matching token
                        if stack.Count > 0 then
                            found <- stack.Peek() |> Some

                        index <- length
                    elif stack.Count > 0 then
                        stack.Pop()  |> ignore
                        match targetDepth with
                        | None -> index <- index + 1
                        | Some size ->

                            // If we make it back down to the depth that we were targeting
                            // when we saw the start character then this is the matching token
                            if size = stack.Count then
                                found <- Some index
                                index <- length
                            else
                                index <- index + 1

                    else
                        index <- index + 1
                else
                    index <- index + 1

            match found with
            | None -> None
            | Some start -> Span(start, 1) |> Some

        // Find the comment matching the comment marker on the specified line
        let findMatchingComment target = 

            let length = SnapshotUtil.GetLength snapshot
            let isBegin, isEnd = 
                let matches index c1 c2 = 
                    if index + 1 < length then
                        let f1 = SnapshotUtil.GetChar index snapshot
                        let f2 = SnapshotUtil.GetChar (index + 1) snapshot
                        f1 = c1 && f2 = c2
                    else
                        false
                (fun index -> matches index '/' '*'), (fun index -> matches index '*' '/')

            let mutable commentStart : int option = None
            let mutable index = 0
            let mutable found : int option = None
            while index < length do 
                if Option.isNone commentStart && isBegin index then
                    commentStart <- Some index
                    index <- index + 2
                elif isEnd index then
                    if target <= index then
                        // If the target is anywhere before the end marker then this current
                        // block is where we will find our matches
                        match commentStart with
                        | None ->
                            // This is an unmatched '*/' and the target is before this token 
                            // so we are simply unmatched
                            ()
                        | Some commentStart ->
                            if target < index then found <- Some (index + 1)
                            else found <- Some commentStart

                        index <- length
                    else
                        index <- index + 2
                else
                    index <- index + 1

            match found with
            | None -> None
            | Some start -> Span(start, 1) |> Some

        // Find the matching directive starting from the buffer position 'target'
        let findMatchingDirective (target : int) = 

            let blockList = x.GetDirectiveBlocks snapshot
            let mutable found : Span option = None
            let mutable blockIndex = 0
            while blockIndex < blockList.Count do
                let block = blockList.[blockIndex]
                let index = 
                    block.Directives
                    |> Seq.tryFindIndex (fun directive -> directive.Span.Contains target)
                match index with
                | None -> blockIndex <- blockIndex + 1
                | Some index ->
                    if index + 1= block.Directives.Count then
                        if block.IsComplete then
                            found <- block.Directives.[0].Span |> Some
                    else
                        found <- block.Directives.[index + 1].Span |> Some

                    blockIndex <- blockList.Count

            found

        let line = SnapshotPointUtil.GetContainingLine point
        let lineText = SnapshotLineUtil.GetText line
        let column = point.Position - line.Start.Position
        let found = 
            match x.FindMatchingTokenKindCore lineText column with
            | None -> None
            | Some (column, kind) ->
                let position = line.Start.Position + column
                match kind with 
                | MatchingTokenKind.Braces -> findMatchingTokenChar position '{' '}'
                | MatchingTokenKind.Brackets -> findMatchingTokenChar position '[' ']'
                | MatchingTokenKind.Parens -> findMatchingTokenChar position '(' ')'
                | MatchingTokenKind.Directive -> findMatchingDirective position
                | MatchingTokenKind.Comment -> findMatchingComment position

        match found with
        | None -> None
        | Some span -> SnapshotSpan(snapshot, span) |> Some

    /// Find the 'count' unmatched token in the specified direction from the 
    /// specified point
    member x.FindUnmatchedToken path kind point count = 

        // Get the sequence of points to search for the unmatched token.  The search
        // always starts from a point that is one past the starting point even if 
        // the search is backwards
        let snapshot = SnapshotPointUtil.GetSnapshot point
        let charSeq = 
            match path with
            | Path.Forward -> 
                let span = SnapshotSpan(point, SnapshotUtil.GetEndPoint snapshot)
                span |> SnapshotSpanUtil.GetPoints Path.Forward |> SeqUtil.skipMax 1
            | Path.Backward ->
                let span = SnapshotSpan(SnapshotUtil.GetStartPoint snapshot, point)
                span |> SnapshotSpanUtil.GetPoints Path.Backward

        // Determine the characters that will a new depth to be entered and 
        // left.  Going forward ( is up and ) is down.  
        let up, down = 
            let startChar, endChar = 
                match kind with
                | UnmatchedTokenKind.Paren -> '(', ')'
                | UnmatchedTokenKind.CurlyBracket -> '{', '}'
            match path with
            | Path.Forward -> startChar, endChar
            | Path.Backward -> endChar, startChar

        let mutable depth = 0
        let mutable count = count
        let mutable found : SnapshotPoint option = None
        use e = charSeq.GetEnumerator()
        while e.MoveNext() && Option.isNone found do
            let c = e.Current.GetChar()
            if c = up then 
                depth <- depth + 1
            elif c = down && depth = 0 then 
                count <- count - 1
                if count = 0 then 
                    found <- Some e.Current 
            elif c = down then
                depth <- depth - 1
        found

type QuotedStringData =  {
    LeadingWhiteSpace : SnapshotSpan
    LeadingQuote : SnapshotPoint
    Contents : SnapshotSpan
    TrailingQuote : SnapshotPoint
    TrailingWhiteSpace : SnapshotSpan 
} with
    
    member x.FullSpan = SnapshotSpanUtil.Create x.LeadingWhiteSpace.Start x.TrailingWhiteSpace.End

/// Result of trying to run a motion against the Visual Snapshot.  By default motions
/// are run against the edit snapshot but line wise motions which involve counts need
/// to be run against the visual snapshot in order to properly account for folded regions
[<RequireQualifiedAccess>]
type VisualMotionResult =

    /// The mapping succeeded
    | Succeeded of MotionResult

    /// The motion simply produced no data
    | FailedNoMotionResult

    /// The motion failed because the Visual SnapshotData could not be retrieved
    | FailedNoVisualSnapshotData

    /// The motion failed because the SnapshotData could not be mapped back down to the
    /// Edit snapshot
    | FailedNoMapToEditSnapshot

type internal MotionUtil 
    ( 
        _vimBufferData : IVimBufferData,
        _commonOperations : ICommonOperations
    ) = 

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textView = _vimBufferData.TextView
    let _statusUtil = _vimBufferData.StatusUtil
    let _wordUtil = _vimBufferData.WordUtil
    let _localSettings = _vimTextBuffer.LocalSettings
    let _markMap = _vimTextBuffer.Vim.MarkMap
    let _jumpList = _vimBufferData.JumpList
    let _vimData = _vimTextBuffer.Vim.VimData
    let _search = _vimTextBuffer.Vim.SearchService
    let _wordNavigator = _vimTextBuffer.WordNavigator
    let _textBuffer = _textView.TextBuffer
    let _bufferGraph = _textView.BufferGraph
    let _visualBuffer = _textView.TextViewModel.VisualBuffer
    let _globalSettings = _localSettings.GlobalSettings
    let _matchingTokenUtil = MatchingTokenUtil()
    let _textObjectUtil = TextObjectUtil(_globalSettings, _textBuffer)

    /// Caret point in the ITextView
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// Caret line in the ITextView
    member x.CaretLine = SnapshotPointUtil.GetContainingLine x.CaretPoint

    /// Current ITextSnapshot for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.GetParagraphs path point = 
        _textObjectUtil.GetParagraphs path point

    member x.GetSentences sentenceKind path point =
        _textObjectUtil.GetSentences sentenceKind path point

    member x.GetSections sectionKind path point = 
        _textObjectUtil.GetSections sectionKind path point

    member x.SpanAndForwardFromLines (line1:ITextSnapshotLine) (line2:ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.End),true
        else SnapshotSpan(line2.Start, line1.End),false

    /// Apply the 'startofline' option to the given MotionResult.  This function must be 
    /// called with the MotionData mapped back to the edit snapshot
    member x.ApplyStartOfLineOption (motionData : MotionResult) =
        Contract.Assert(match motionData.MotionKind with MotionKind.LineWise _ -> true | _ -> false)
        Contract.Assert(motionData.Span.Snapshot = x.CurrentSnapshot)
        if not _globalSettings.StartOfLine then 
            motionData 
        else
            let lastLine = motionData.DirectionLastLine

            // TODO: Is GetFirstNonBlankOrStart correct here?  Should it be using the
            // End version?
            let point = SnapshotLineUtil.GetFirstNonBlankOrStart lastLine
            let column = SnapshotPointUtil.GetColumn point |> CaretColumn.InLastLine
            { motionData with 
                MotionKind = MotionKind.LineWise 
                DesiredColumn = column }

    /// Linewise motions often need to deal with the VisualSnapshot of the ITextView because
    /// they see folded regions as single lines vs. the many of which are actually represented
    /// within the fold.  
    ///
    /// This function wraps the conversion of the information from the edit buffer into the 
    /// visual buffer and back again when we are done. 
    member x.MotionWithVisualSnapshotCore (action : SnapshotData -> 'T) (getMotionResult : 'T -> MotionResult option) =

        // First get the SnapshotData for the VisualBuffer based on the current position of the
        // caret in the EditBuffer.  
        //
        // Although highly unlikely it's possible for the caret point to be unmappable into the 
        // VisualBuffer.  This would likely represent a bug in the projection mapping or the way
        // in which a projection was setup.
        match TextViewUtil.GetVisualSnapshotData _textView with
        | None -> 
            _statusUtil.OnWarning Resources.Internal_ErrorMappingToVisual
            VisualMotionResult.FailedNoVisualSnapshotData
        | Some snapshotData ->

            // Calculate the motion information based on this SnapshotData value
            let value = action snapshotData

            match getMotionResult value with
            | None ->
                VisualMotionResult.FailedNoMotionResult
            | Some motionResult ->  
                // Now migrate the SnapshotSpan down to the EditBuffer.  If we cannot map this span into
                // the EditBuffer then we must fail. 
                let span = BufferGraphUtil.MapSpanDownToSingle _bufferGraph motionResult.Span x.CurrentSnapshot
                match span with
                | None ->
                    _statusUtil.OnError Resources.Internal_ErrorMappingBackToEdit
                    VisualMotionResult.FailedNoMapToEditSnapshot
                | Some span ->
                    { motionResult with Span = span } |> VisualMotionResult.Succeeded

    /// Run the motion function against the Visual Snapshot
    member x.MotionWithVisualSnapshot (action : SnapshotData -> MotionResult) = 

        match x.MotionWithVisualSnapshotCore action (fun x -> Some x) with
        | VisualMotionResult.Succeeded motionResult ->
            motionResult
        | _ -> 
            // This can only fail due to mapping issues of the Snapshot back and
            // forth between the visual and edit snapshot.  If that happens just 
            // run against the edit snapshot
            let snapshotData = TextViewUtil.GetEditSnapshotData _textView
            action snapshotData

    /// Run the motion function against the Visual Snapshot
    member x.MotionOptionWithVisualSnapshot (action : SnapshotData -> MotionResult option) = 

        match x.MotionWithVisualSnapshotCore action (fun x -> x) with
        | VisualMotionResult.Succeeded motionResult ->
            Some motionResult
        | VisualMotionResult.FailedNoMotionResult ->
            // If the motion can't be calculated on the Visual Snapshot then there's no reason
            // to expect it to be calculated properly on the edit snapshot.
            None
        | VisualMotionResult.FailedNoMapToEditSnapshot ->
            // If the motion result couldn't be mapped back to the edit snapshot then just run
            // against the edit snapshot directly.
            let snapshotData = TextViewUtil.GetEditSnapshotData _textView
            action snapshotData
        | VisualMotionResult.FailedNoVisualSnapshotData ->
            // This should only happen in cases where the ITextView is in a very bad state as 
            // it can't map the caret from the edit buffer to the Visual Snapshot.  Can't think
            // of any reason why this could happen but as a fail safe fall back to the edit buffer
            // here
            let snapshotData = TextViewUtil.GetEditSnapshotData _textView
            action snapshotData

    /// Motion for "count" display line downwards
    member x.DisplayLineDown count = 
        match TextViewUtil.IsWordWrapEnabled _textView, TextViewUtil.GetTextViewLines _textView with
        | true, Some textViewLines ->

            let startLine = textViewLines.GetTextViewLineContainingBufferPosition x.CaretPoint
            let lastLine = 
                let index = textViewLines.GetIndexOfTextLine startLine
                let index = index + count 
                let index = min index (textViewLines.Count - 1)
                textViewLines.[index]

            // The point we use must be the same offset as the caret position in the final line.  Make
            // sure not to jump off the line here though.  Also remember the column is the offset 
            // from the display line, not the offset of the buffer line
            let endPoint = 
                let column = x.CaretPoint.Position - startLine.Start.Position
                let column = min column lastLine.Length
                match SnapshotPointUtil.TryAdd column lastLine.Start with
                | Some point -> point
                | None -> lastLine.End

            let span = SnapshotSpan(x.CaretPoint, endPoint)
            MotionResult.Create span true MotionKind.CharacterWiseExclusive |> Some

        | _ -> x.LineDown count

    /// Motion for "count" display line upwards
    member x.DisplayLineUp count = 
        match TextViewUtil.IsWordWrapEnabled _textView, TextViewUtil.GetTextViewLines _textView with
        | true, Some textViewLines ->

            let bottomLine = textViewLines.GetTextViewLineContainingBufferPosition x.CaretPoint
            let topLine = 
                let index = textViewLines.GetIndexOfTextLine bottomLine
                let index = index - count 
                let index = max index 0
                textViewLines.[index]

            // The point we use must be the same offset as the caret position in the final line.  Make
            // sure not to jump off the line here though.  Also remember the column is the offset 
            // from the display line, not the offset of the buffer line
            let topPoint = 
                let column = x.CaretPoint.Position - bottomLine.Start.Position
                let column = min column topLine.Length
                match SnapshotPointUtil.TryAdd column topLine.Start with
                | Some point -> point
                | None -> topLine.End

            let span = SnapshotSpan(topPoint, x.CaretPoint)
            MotionResult.Create span false MotionKind.CharacterWiseExclusive |> Some

        | _ -> x.LineUp count

    member x.DisplayLineStart() =
        match TextViewUtil.GetTextViewLines _textView with
        | None -> None
        | Some textViewLines ->
            let caretLine = textViewLines.GetTextViewLineContainingBufferPosition(x.CaretPoint)
            let point = 
                match caretLine.GetBufferPositionFromXCoordinate(_textView.ViewportLeft) with
                | NullableUtil.Null -> caretLine.Start
                | NullableUtil.HasValue point -> point
            let span = SnapshotSpan(point, x.CaretPoint)
            MotionResult.Create span false MotionKind.CharacterWiseExclusive |> Some

    member x.DisplayLineEnd() =
        match TextViewUtil.GetTextViewLines _textView with
        | None -> None
        | Some textViewLines ->
            let caretLine = textViewLines.GetTextViewLineContainingBufferPosition(x.CaretPoint)
            let point = 
                match caretLine.GetBufferPositionFromXCoordinate(_textView.ViewportRight) with
                | NullableUtil.Null -> SnapshotPointUtil.SubtractOneOrCurrent caretLine.End
                | NullableUtil.HasValue point -> point
            let span = SnapshotSpan(x.CaretPoint, point)
            MotionResult.Create span true MotionKind.CharacterWiseExclusive |> Some

    member x.DisplayLineMiddleOfScreen() =
        let createForPoint (point : SnapshotPoint) = 
            let isForward = x.CaretPoint.Position <= point.Position
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint point
            let span = SnapshotSpan(startPoint, endPoint)
            MotionResult.Create span isForward MotionKind.CharacterWiseExclusive |> Some

        match TextViewUtil.GetTextViewLines _textView with
        | None -> None
        | Some textViewLines ->
            let caretLine = textViewLines.GetTextViewLineContainingBufferPosition(x.CaretPoint)
            let middle = _textView.ViewportWidth / 2.0
            match caretLine.GetBufferPositionFromXCoordinate(middle) with
            | NullableUtil.Null -> 
                // If the point is beyond the width of the line then the motion should go to the 
                // end of the line 
                if middle >= caretLine.Width then
                    createForPoint caretLine.End
                else    
                    None
            | NullableUtil.HasValue point -> createForPoint point

    /// Get the motion between the provided two lines.  The motion will be linewise
    /// and have a column of the first non-whitespace character.  If the 'startofline'
    /// option is not set it will keep the original column
    member x.LineToLineFirstNonBlankMotion (startLine : ITextSnapshotLine) (endLine : ITextSnapshotLine) = 

        // Get the column based on the 'startofline' option
        let column = 
            if _globalSettings.StartOfLine then 
                endLine |> SnapshotLineUtil.GetFirstNonBlankOrStart |> SnapshotPointUtil.GetColumn
            else
                _textView |> TextViewUtil.GetCaretPoint |> SnapshotPointUtil.GetColumn
        let column = CaretColumn.InLastLine column

        // Create the range based on the provided lines.  Remember they can be in reverse
        // order
        let range, isForward = 
            let startLine, endLine, isForward = 
                if startLine.LineNumber <= endLine.LineNumber then startLine, endLine, true 
                else endLine, startLine, false
            (SnapshotLineRangeUtil.CreateForLineRange startLine endLine, isForward)
        MotionResult.CreateExEx range.ExtentIncludingLineBreak isForward MotionKind.LineWise MotionResultFlags.None column

    /// Get the block span for the specified char at the given context point
    member x.GetBlock (blockKind : BlockKind) contextPoint = 

        let startChar, endChar = blockKind.Characters

        // Is the char at the given point escaped?
        let isEscaped point = 
            match SnapshotPointUtil.TrySubtractOne point with
            | None -> false
            | Some point -> SnapshotPointUtil.GetChar point = '\\'

        let isChar c point = SnapshotPointUtil.GetChar point = c && not (isEscaped point) 

        let findMatched plusChar minusChar = 
            let inner point count = 
                let count = 
                    if isChar minusChar point then
                        count - 1
                    elif isChar plusChar point then
                        count + 1
                    else 
                        count
                count = 0, count
            inner

        let snapshot = SnapshotPointUtil.GetSnapshot contextPoint
        let startPoint = 
            SnapshotSpan(SnapshotPoint(snapshot, 0), SnapshotPointUtil.AddOneOrCurrent contextPoint)
            |> SnapshotSpanUtil.GetPoints Path.Backward
            |> SeqUtil.tryFind 1 (findMatched endChar startChar)

        let lastPoint = 
            match startPoint with
            | None -> None
            | Some startPoint ->
                SnapshotSpan(startPoint, SnapshotUtil.GetEndPoint snapshot)
                |> SnapshotSpanUtil.GetPoints Path.Forward
                |> SeqUtil.tryFind 0 (findMatched startChar endChar)

        match startPoint, lastPoint with
        | Some startPoint, Some lastPoint -> 
            let endPoint = SnapshotPointUtil.AddOneOrCurrent lastPoint
            SnapshotSpan(startPoint, endPoint) |> Some
        | _ -> None

    member x.GetQuotedStringData quoteChar = 
        let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView

        // Is the specified point a valid quote character.  This takes into account escape
        // characters which appear before the point
        let isQuotePoint point = 
            let pointChar = SnapshotPointUtil.GetChar point
            if pointChar = quoteChar then
                match SnapshotPointUtil.TrySubtractOne point with
                | None -> true 
                | Some beforePoint -> 
                    let beforeChar = SnapshotPointUtil.GetChar beforePoint
                    not (StringUtil.containsChar _localSettings.QuoteEscape beforeChar)
            else
                false

        // Get the next quote on the line from the specified point
        let getNextQuote point = 
            point
            |> SnapshotPointUtil.GetPointsOnLineForward
            |> Seq.skip 1
            |> Seq.tryFind isQuotePoint

        // Get the previous quote on the line from the specified point
        let getPreviousQuote point = 
            point
            |> SnapshotPointUtil.GetPointsOnLineBackward
            |> Seq.skip 1
            |> Seq.tryFind isQuotePoint

        // Calculate the leading whitespace span.  It includes the white space just
        // before the leading quote.  The quote is not included in the span
        let getLeadingWhiteSpace point = 
            let getPreviousPoint point = SnapshotPointUtil.TryGetPreviousPointOnLine point 1

            let rec inner current =
                match getPreviousPoint current with
                | None -> SnapshotSpanUtil.Create current point
                | Some previousPoint ->
                    if SnapshotPointUtil.IsWhiteSpace previousPoint then
                        inner previousPoint 
                    else
                        SnapshotSpanUtil.Create current point 

            inner point

        // Calculate the trailing white space from the given point.  The specified point
        // isn't included in the span
        let getTrailingSpace point = 
            let getNextPoint point = SnapshotPointUtil.TryGetNextPointOnLine point 1

            let start = SnapshotPointUtil.AddOne point
            let rec inner current = 
                if SnapshotPointUtil.IsWhiteSpace current then
                    match SnapshotPointUtil.TryAddOne current with
                    | None -> SnapshotSpanUtil.Create start current
                    | Some nextPoint -> inner nextPoint
                else
                    SnapshotSpanUtil.Create start current

            inner start

        // Find the quoted data structure from the first quote 
        let getData leadingQuote = 
            match getNextQuote leadingQuote with
            | None -> None
            | Some trailingQuote ->

                let span = 
                    let start = SnapshotPointUtil.AddOne leadingQuote
                    SnapshotSpanUtil.Create start trailingQuote

                let leadingSpan = getLeadingWhiteSpace leadingQuote 
                let trailingSpan = getTrailingSpace trailingQuote

                {
                    LeadingWhiteSpace = leadingSpan
                    LeadingQuote = leadingQuote
                    Contents = span
                    TrailingQuote = trailingQuote
                    TrailingWhiteSpace = trailingSpan } |> Some

        if isQuotePoint x.CaretPoint then
            // When starting on a quote point we have to determine if this is a start or
            // end quote of a string by examining the contents of the entire line 
            let index = 
                x.CaretLine
                |> SnapshotLineUtil.GetPoints Path.Forward 
                |> Seq.filter isQuotePoint
                |> Seq.tryFindIndex (fun point -> point = x.CaretPoint)
                |> Option.get

            let index = index + 1 
            if index % 2 = 0 then 
                // It's an end quote.  Find the start quote and use that 
                getPreviousQuote x.CaretPoint |> Option.get |> getData
            else
                getData x.CaretPoint
        else
            match getPreviousQuote x.CaretPoint with
            | Some point -> getData point
            | None -> 
                match getNextQuote x.CaretPoint with
                | Some point -> getData point
                | None -> None

    member x.Mark localMark = 
        match _vimTextBuffer.GetLocalMark localMark with
        | None -> 
            None
        | Some virtualPoint ->

            // Found the motion so update the jump list
            _jumpList.Add x.CaretPoint

            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint virtualPoint.Position
            let column = SnapshotPointUtil.GetColumn virtualPoint.Position
            let span = SnapshotSpan(startPoint, endPoint)
            let isForward = caretPoint = startPoint
            MotionResult.Create span isForward MotionKind.CharacterWiseExclusive |> Some

    /// Motion from the caret to the given mark within the ITextBuffer.  Because this uses
    /// absolute positions and not counts we can operate on the edit buffer and don't need
    /// to consider the visual buffer
    member x.MarkLine localMark =

        match _vimTextBuffer.GetLocalMark localMark with
        | None ->
            None
        | Some virtualPoint ->

            // Found the motion so update the jump list
            _jumpList.Add x.CaretPoint

            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint virtualPoint.Position
            let startLine = SnapshotPointUtil.GetContainingLine startPoint
            let endLine = SnapshotPointUtil.GetContainingLine endPoint
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            let isForward = x.CaretPoint = startPoint
            let column =
                virtualPoint.Position
                |> SnapshotPointUtil.GetContainingLine
                |> SnapshotLineUtil.GetFirstNonBlankOrStart
                |> SnapshotPointUtil.GetColumn
                |> CaretColumn.InLastLine
            MotionResult.CreateExEx range.ExtentIncludingLineBreak isForward MotionKind.LineWise MotionResultFlags.None column |> Some

    /// Find the matching token for the next token on the current line 
    member x.MatchingToken() = 

        match _matchingTokenUtil.FindMatchingToken x.CaretPoint with
        | None -> None
        | Some matchingTokenSpan ->

            // Search succeeded so update the jump list before moving
            _jumpList.Add x.CaretPoint

            // Order the tokens appropriately to get the span 
            let span, isForward = 
                if x.CaretPoint.Position < matchingTokenSpan.Start.Position then
                    let endPoint = SnapshotPointUtil.AddOneOrCurrent matchingTokenSpan.Start
                    SnapshotSpan(x.CaretPoint, endPoint), true
                else
                    SnapshotSpan(matchingTokenSpan.Start, SnapshotPointUtil.AddOneOrCurrent x.CaretPoint), false

            MotionResult.Create span isForward MotionKind.CharacterWiseInclusive |> Some

    member x.UnmatchedToken path kind count = 
        match _matchingTokenUtil.FindUnmatchedToken path kind x.CaretPoint count with
        | None -> None
        | Some matchingPoint ->

            // Search succeeded so update the jump list before moving
            _jumpList.Add x.CaretPoint

            // Order the tokens appropriately to get the span 
            let span, isForward = 
                if x.CaretPoint.Position < matchingPoint.Position then
                    SnapshotSpan(x.CaretPoint, matchingPoint), true
                else
                    SnapshotSpan(matchingPoint, x.CaretPoint), false

            MotionResult.Create span isForward MotionKind.CharacterWiseExclusive |> Some

    /// Implement the all block motion
    member x.AllBlock contextPoint blockKind count =

        if count <> 1 then
            None
        else
            let span = x.GetBlock blockKind contextPoint 
            match span with
            | None -> None
            | Some span -> MotionResult.Create span true MotionKind.CharacterWiseInclusive |> Some

    /// Implementation of the 'ap' motion.  Unfortunately this is not as simple as the documentation
    /// states it is.  While the 'ap' motion uses the same underlying definition of a paragraph 
    /// how the blank lines are treated differs greatly between 'ap' and '}'.  During an 'ap' call
    /// the blank lines are definitely considered white space while in '}' motions it's part of the
    /// next line.  This discrepancy can be easily demonstrated with the following example
    ///
    ///   a
    ///    
    ///   b
    ///
    /// Put the caret on line 0.  A 'yap' call will yank lines 1 and 2 line wise while a 'y}' will
    /// yank line 1
    member x.AllParagraph count = 

        let all = x.GetParagraphs Path.Forward x.CaretPoint |> Seq.truncate count |> List.ofSeq
        match all with
        | [] ->
            // No paragraphs forward so return nothing
            None
        | head :: tail -> 

            // Get the span of the all motion
            let span = 
                let last = List.nth all (all.Length - 1)
                SnapshotSpan (head.Start, last.End)

            // The 'ap' motion considers blank lines to be white space
            let isWhiteSpace point  = 
                let line = SnapshotPointUtil.GetContainingLine point
                line.Length = 0

            let isCaretInWhiteSpace = isWhiteSpace x.CaretPoint

            // Is there white space after is true if there is white space after this 
            // object and before the next
            let whiteSpaceAfter = 
                if isWhiteSpace span.End then
                    let endPoint = 
                        span.End
                        |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Forward 
                        |> Seq.skipWhile isWhiteSpace
                        |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)
                    SnapshotSpan(span.Start, endPoint) |> Some
                else
                    None

            // Include the preceding white space in the Span
            let includePrecedingWhiteSpace () =
                let startPoint = 
                    span.Start 
                    |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Backward
                    |> Seq.skipWhile (fun point -> 
                        // Skip while the previous point is white space
                        match SnapshotPointUtil.TrySubtractOne point with
                        | Some point -> isWhiteSpace point
                        | None -> false)
                    |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
                SnapshotSpan(startPoint, span.End)

            // Now we need to do the standard adjustments listed at the bottom of 
            // ':help text-objects'.
            let span = 
                match isCaretInWhiteSpace, whiteSpaceAfter with
                | true, _ -> includePrecedingWhiteSpace()
                | false, None -> includePrecedingWhiteSpace()
                | false, Some span -> SnapshotSpan(span.Start, span.End)

            // Special case for paragraphs.  If the entire span is white space then it 
            // doesn't count as a successful motion
            let spanHasContent = 
                span
                |> SnapshotSpanUtil.GetPoints Path.Forward
                |> Seq.filter (fun point -> not (SnapshotPointUtil.IsWhiteSpaceOrInsideLineBreak point))
                |> SeqUtil.isNotEmpty

            if spanHasContent then
                MotionResult.Create span true MotionKind.CharacterWiseExclusive |> Some
            else
                None

    /// Common function for getting the 'all' version of text object motion values.  Used for
    /// sentences, paragraphs and sections.  Not used for word motions because they have too many
    /// corner cases to consider due to the line based nature
    member x.AllTextObjectCommon getObjects count = 

        let all = getObjects Path.Forward x.CaretPoint
        let firstSpan = all |> SeqUtil.tryHeadOnly
        match firstSpan with
        | None ->
            // Corner case where there are simply no objects going forward.  Return
            // an empty span
            SnapshotSpan(x.CaretPoint, 0)
        | Some firstSpan ->

            // Get the end point of the span
            let endPoint = 
                all 
                |> SeqUtil.skipMax count
                |> Seq.map SnapshotSpanUtil.GetStartPoint
                |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

            let isCaretInWhiteSpace = x.CaretPoint.Position < firstSpan.Start.Position

            // Is there white space after is true if there is white space after this 
            // object and before the next
            let whiteSpaceAfter = 

                match all |> SeqUtil.skipMax (count + 1) |> SeqUtil.tryHeadOnly with
                | None -> 
                    None
                | Some nextPoint -> 
                    if nextPoint.Start.Position = endPoint.Position then
                        None
                    else
                        SnapshotSpan(endPoint, nextPoint.Start) |> Some

            let includePrecedingWhiteSpace () =
                // Include the leading white space in the span.  Start by looking backward
                // and using the end of the previous span as the start point
                let startPoint = 
                    if SnapshotPointUtil.IsStartPoint firstSpan.Start then
                        firstSpan.Start
                    else
                        let point = firstSpan.Start |> SnapshotPointUtil.SubtractOne
                        getObjects Path.Backward point
                        |> Seq.map SnapshotSpanUtil.GetEndPoint
                        |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
                SnapshotSpan(startPoint, endPoint)

            // Now we need to do the standard adjustments listed at the bottom of 
            // ':help text-objects'.
            match isCaretInWhiteSpace, whiteSpaceAfter with
            | true, _ -> includePrecedingWhiteSpace()
            | false, None -> includePrecedingWhiteSpace()
            | false, Some span -> SnapshotSpan(firstSpan.Start, span.End)

    /// Implements the 'as' motion.  Make sure to use the no trailing characters option here to 
    /// maintain parity with the gVim implementation.  Not sure if this is a bug or not and haven't
    /// heard back from the community
    ///   - http://groups.google.com/group/vim_use/t/d3f28cf801dc2030 
    member x.AllSentence count =

        // When the provided point is in the white space trailing a sentence the GetSentences command
        // will start by returning the sentence which exists before the white space.  In the case of 
        // AllSentence we don't want this behavior and want to start with the following sentence.  
        let sentenceKind = SentenceKind.NoTrailingCharacters
        let searchPoint = 
            let mutable column = SnapshotColumn(x.CaretPoint)
            while _textObjectUtil.IsSentenceWhiteSpace sentenceKind column && not (SnapshotPointUtil.IsEndPoint column.Point) do
                column <- column.Add 1
            column.Point

        let sentences = x.GetSentences sentenceKind Path.Forward searchPoint |> Seq.truncate count |> List.ofSeq

        let span = 
            match sentences with
            | [] ->
                // Corner case where there are simply no objects going forward.  Return
                // an empty span
                SnapshotSpan(x.CaretPoint, 0)
            | head :: tail ->

                let span = 
                    let last = List.nth sentences (sentences.Length - 1)
                    SnapshotSpan(head.Start, last.End)

                // The 'as' motion considers anything between the SnapshotSpan of a sentence to
                // be white space.  So the caret is in white space if it occurs before the sentence
                let isCaretInWhiteSpace = x.CaretPoint.Position < span.Start.Position

                // The white space after the sentence is the gap between this sentence and the next
                // sentence
                let whiteSpaceAfter =
                    let mutable column = SnapshotColumn(span.End)
                    while not (_textObjectUtil.IsSentenceStart sentenceKind column) && not (SnapshotPointUtil.IsEndPoint column.Point) do
                        column <- column.Add 1

                    if SnapshotPointUtil.IsEndPoint column.Point || span.End.Position = column.Point.Position then
                        None
                    else
                        Some column.Point

                // Include the preceding white space in the Span
                let includePrecedingWhiteSpace () =
                    let mutable column = SnapshotColumn(span.Start)
                    let mutable before = column.Subtract 1
                    while column.Point.Position > 0 && _textObjectUtil.IsSentenceWhiteSpace sentenceKind before do
                        column <- before
                        before <- column.Subtract 1

                    SnapshotSpan(column.Point, span.End)

                // Now we need to do the standard adjustments listed at the bottom of 
                // ':help text-objects'.
                match isCaretInWhiteSpace, whiteSpaceAfter with
                | true, _ -> includePrecedingWhiteSpace()
                | false, None -> includePrecedingWhiteSpace()
                | false, Some spaceEnd -> SnapshotSpan(span.Start, spaceEnd)

        MotionResult.Create span true MotionKind.CharacterWiseExclusive

    /// Implements the 'aw' motion.  The 'aw' motion is limited to the current line and won't ever
    /// extend above or below it.
    member x.AllWord kind count contextPoint = 
        let contextLine = SnapshotPointUtil.GetContainingLine contextPoint 

        // Is this word span on the same line as the context?  A word won't ever span multiple lines
        // so we can be content with checking the start point
        let isOnSameLine span =
            let line = span |> SnapshotSpanUtil.GetStartPoint |> SnapshotPointUtil.GetContainingLine
            line.LineNumber = contextLine.LineNumber

        // Get all of the words on this line going forward
        let all = 
            _wordUtil.GetWords kind Path.Forward contextPoint
            |> Seq.takeWhile isOnSameLine
            |> Seq.truncate count
            |> List.ofSeq

        match all with 
        | [] ->
            // No words going forward is an invalid span
            None

        | firstSpan :: _ -> 

            // Get the span of the text object
            let span =
                let last = List.nth all (all.Length - 1)
                SnapshotSpan(firstSpan.Start, last.End)

            // Calculate the white space after the last item.  Line breaks shouldn't be included
            // in the calculation
            let whiteSpaceAfter = 
                let endPoint = 
                    span.End
                    |> SnapshotPointUtil.GetPoints Path.Forward
                    |> Seq.filter (fun point -> point.Position <= contextLine.End.Position)
                    |> Seq.skipWhile SnapshotPointUtil.IsWhiteSpace
                    |> SeqUtil.headOrDefault contextLine.End
                let span = SnapshotSpan(span.End, endPoint)
                if span.Length > 0 then Some span else None

            let isContextInWhiteSpace = SnapshotPointUtil.IsWhiteSpace contextPoint

            // Now do the standard adjustments listed at the bottom of ':help text-objects'
            let span = 
                match isContextInWhiteSpace, whiteSpaceAfter with
                | true , _ ->
                    // If the context is in white space then we include all of the white space before
                    // up until the start of the line or the next non-white space character.  
                    let startPoint = 
                        span.Start
                        |> SnapshotPointUtil.GetPoints Path.Backward
                        |> SeqUtil.skipMax 1
                        |> Seq.filter (fun point -> point.Position >= contextLine.Start.Position)
                        |> Seq.skipWhile (fun point -> 
                            match SnapshotPointUtil.TrySubtractOne point with
                            | None -> false
                            | Some point -> SnapshotPointUtil.IsWhiteSpace point)
                        |> SeqUtil.headOrDefault contextLine.Start
                    SnapshotSpan(startPoint, span.End)

                | false, None ->
                    // There are different rules here that when the context is in white space even 
                    // though they are not called out in the documentation.  We should only include
                    // white space here if there is a word on the same line before this one.  
                    let startPoint = 
                        _wordUtil.GetWords kind Path.Backward span.Start
                        |> Seq.filter isOnSameLine
                        |> Seq.map SnapshotSpanUtil.GetEndPoint
                        |> SeqUtil.headOrDefault span.Start
                    SnapshotSpan(startPoint, span.End)

                | false, Some spaceSpan -> 
                    SnapshotSpan(span.Start, spaceSpan.End)

            MotionResult.Create span true MotionKind.CharacterWiseExclusive |> Some

    member x.BeginingOfLine() = 
        let start = x.CaretPoint
        let line = SnapshotPointUtil.GetContainingLine start
        let span = SnapshotSpan(line.Start, start)
        MotionResult.Create span false MotionKind.CharacterWiseExclusive

    /// Search for the specified char in the given direction.
    member x.CharSearch c count charSearch direction = 
        // Save the last search value
        _vimData.LastCharSearch <- Some (charSearch, direction, c)

        x.CharSearchCore c count charSearch direction

    /// Do the actual char search motion but don't update the 'LastCharSearch' value
    member x.CharSearchCore c count charSearch direction = 

        let forward () = 
            if x.CaretPoint.Position < x.CaretLine.End.Position then
                let start = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
                SnapshotSpan(start, x.CaretLine.End)
                |> SnapshotSpanUtil.GetPoints Path.Forward
                |> Seq.filter (SnapshotPointUtil.IsChar c)
                |> SeqUtil.skipMax (count - 1)
                |> SeqUtil.tryHeadOnly
            else
                None

        let backward () = 
            SnapshotSpan(x.CaretLine.Start, x.CaretPoint)
            |> SnapshotSpanUtil.GetPoints Path.Backward
            |> Seq.filter (SnapshotPointUtil.IsChar c)
            |> SeqUtil.skipMax (count - 1)
            |> SeqUtil.tryHeadOnly

        let option = 
            match charSearch, direction with
            | CharSearchKind.ToChar, Path.Forward -> 
                forward () |> Option.map (fun point ->
                    let endPoint = SnapshotPointUtil.AddOneOrCurrent point
                    let span = SnapshotSpan(x.CaretPoint, endPoint)
                    span, MotionKind.CharacterWiseInclusive)
            | CharSearchKind.TillChar, Path.Forward -> 
                forward () |> Option.map (fun point ->
                    let span = SnapshotSpan(x.CaretPoint, point)
                    span, MotionKind.CharacterWiseInclusive)
            | CharSearchKind.ToChar, Path.Backward ->
                backward () |> Option.map (fun point ->
                    let span = SnapshotSpan(point, x.CaretPoint)
                    span, MotionKind.CharacterWiseExclusive)
            | CharSearchKind.TillChar, Path.Backward ->
                backward () |> Option.map (fun point ->
                    let point = SnapshotPointUtil.AddOne point
                    let span = SnapshotSpan(point, x.CaretPoint)
                    span, MotionKind.CharacterWiseExclusive)

        match option with 
        | None -> None
        | Some (span, motionKind) -> 
            let isForward = match direction with | Path.Forward -> true | Path.Backward -> false
            MotionResult.Create span isForward motionKind |> Some

    /// Repeat the last f, F, t or T search pattern.
    member x.RepeatLastCharSearch () =
        match _vimData.LastCharSearch with 
        | None -> None
        | Some (kind, direction, c) -> x.CharSearchCore c 1 kind direction

    /// Repeat the last f, F, t or T search pattern in the opposite direction
    member x.RepeatLastCharSearchOpposite () =
        match _vimData.LastCharSearch with 
        | None -> None
        | Some (kind, direction, c) -> 
            let direction = 
                match direction with
                | Path.Forward -> Path.Backward
                | Path.Backward -> Path.Forward
            x.CharSearchCore c 1 kind direction

    member x.WordForward kind count motionContext =

        // If we are in white space in the middle of the line then we adjust the 
        // count down by 1.  From white space the 'w' motion should take us to the 
        // start of the first word not the second.
        let count = 
            if SnapshotPointUtil.IsWhiteSpace x.CaretPoint && not (SnapshotPointUtil.IsInsideLineBreak x.CaretPoint) then
                count - 1
            else
                count

        let endPoint = 
            _wordUtil.GetWords kind Path.Forward x.CaretPoint
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

        let endPoint = 
            match motionContext with
            | MotionContext.Movement -> endPoint
            | MotionContext.AfterOperator -> 
                // If the word motion comes after an operator and ends on the first word 
                // of a different line, then the motion is moved back to the last line containing
                // a word
                let endLine = SnapshotPointUtil.GetContainingLine endPoint
                let isFirstNonBlank = SnapshotLineUtil.GetFirstNonBlankOrStart endLine = endPoint

                if isFirstNonBlank && endLine.LineNumber > x.CaretLine.LineNumber then
                    let previousLine = 
                        SnapshotUtil.GetLines x.CurrentSnapshot (endLine.LineNumber - 1) Path.Backward
                        |> Seq.skipWhile (fun textLine -> SnapshotLineUtil.IsBlank textLine && textLine.LineNumber > x.CaretLine.LineNumber)
                        |> SeqUtil.tryHeadOnly
                    let previousLine = 
                        match previousLine with
                        | None -> x.CaretLine
                        | Some line -> line

                    if SnapshotLineUtil.IsEmpty previousLine then
                        previousLine.EndIncludingLineBreak
                    else
                        previousLine.End
                else
                    endPoint

        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.CreateEx span true MotionKind.CharacterWiseExclusive MotionResultFlags.AnyWord

    member x.WordBackward kind count =

        let startPoint = 
            _wordUtil.GetWords kind Path.Backward x.CaretPoint
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.CreateEx span false MotionKind.CharacterWiseExclusive MotionResultFlags.AnyWord

    /// Implements the 'e' and 'E' motions
    member x.EndOfWord kind count = 

        // The start point for an end of word motion is a little odd.  If the caret is 
        // on the last point inside a word then we calculate the next word starting at
        // the end of the first word.
        let searchPoint = 
            match _wordUtil.GetWords kind Path.Forward x.CaretPoint |> SeqUtil.tryHeadOnly with
            | None -> 
                x.CaretPoint
            | Some span -> 
                if SnapshotSpanUtil.IsLastIncludedPoint span x.CaretPoint then
                    span.End
                else
                    x.CaretPoint

        let endPoint = 
            searchPoint
            |> _wordUtil.GetWords kind Path.Forward
            |> Seq.filter (fun span ->
                // The typical word motion includes blank lines as part of the word. The one 
                // exception is end of word which doesn't count blank lines as words.  Filter
                // them out here 
                if SnapshotPointUtil.IsStartOfLine span.Start then
                    let line = SnapshotPointUtil.GetContainingLine span.Start
                    line.Length <> 0
                else
                    true)
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetEndPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create span true MotionKind.CharacterWiseInclusive

    member x.EndOfLine count = 
        let start = x.CaretPoint
        let span = SnapshotPointUtil.GetLineRangeSpan start count
        MotionResult.CreateEx span true MotionKind.CharacterWiseInclusive MotionResultFlags.EndOfLine

    /// Find the first non-whitespace character on the current line.  
    member x.FirstNonBlankOnCurrentLine () =
        let start = x.CaretPoint
        let line = start.GetContainingLine()
        let target = 
            SnapshotLineUtil.GetPoints Path.Forward line
            |> Seq.tryFind (fun x -> not (CharUtil.IsWhiteSpace (x.GetChar())) )
            |> OptionUtil.getOrDefault line.End
        let startPoint,endPoint,isForward = 
            if start.Position <= target.Position then start,target,true
            else target,start,false
        let span = SnapshotSpan(startPoint, endPoint)
        MotionResult.Create span isForward MotionKind.CharacterWiseExclusive 

    /// Create a line wise motion from the current line to (count - 1) lines
    /// downward 
    member x.FirstNonBlankOnLine count = 
        x.MotionWithVisualSnapshot (fun x -> 
            let startLine = x.CaretLine
            let endLine = 
                let number = startLine.LineNumber + (count - 1)
                SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
            let column = SnapshotLineUtil.GetFirstNonBlankOrStart endLine |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            MotionResult.CreateExEx range.ExtentIncludingLineBreak true MotionKind.LineWise MotionResultFlags.None column)

    /// An inner block motion is just the all block motion with the start and 
    /// end character removed 
    member x.InnerBlock contextPoint blockKind count =

        if count <> 1 then
            None
        else
            match x.GetBlock blockKind contextPoint with
            | None -> None
            | Some span ->
                if span.Length < 3 then
                    None
                else
                    let startPoint = SnapshotPointUtil.AddOne span.Start
                    let endPoint = SnapshotPointUtil.SubtractOne span.End
                    let span = SnapshotSpan(startPoint, endPoint)
                    MotionResult.Create span true MotionKind.CharacterWiseInclusive |> Some

    /// Implement the 'iw' motion.  Unlike the 'aw' motion it is not limited to a specific line
    /// and can exceed it
    ///
    /// Many of the text object selection motions are not documented as to whether or not they 
    /// are inclusive or exclusive including this one.  Experimentation shows though that this
    /// is inclusive.  The primary evidence being
    ///
    ///  - It doesn't go through the exclusive-linewise promotion 
    ///  - The caret position suggests inclusive when used as a caret movement in visual mode
    member x.InnerWord wordKind count contextPoint =

        let contextLine = SnapshotPointUtil.GetContainingLine contextPoint

        // Given a point which is a tab or space get the space and tab span backwards up 
        // to and including the point
        let getReverseSpaceAndTabSpaceStart point =
            Contract.Assert(SnapshotPointUtil.IsBlank point) 

            let line = SnapshotPointUtil.GetContainingLine point
            let startPoint = 
                SnapshotSpan(line.Start, point)
                |> SnapshotSpanUtil.GetPoints Path.Backward
                |> Seq.skipWhile SnapshotPointUtil.IsBlank
                |> SeqUtil.headOrDefault line.Start

            if SnapshotPointUtil.IsBlank startPoint then
                startPoint
            else
                SnapshotPointUtil.AddOne startPoint 

        // From the given point get the end point of the inner word span.  Note: This can
        // be None if the count surpasses the number of elements in the ITextBuffer.  
        //
        // The 'count' passed to inner word counts the white space and word spans equally as
        // an item.  Here we expand a word to a tuple of the word and the span of the following
        // white space
        //
        // Note: Line breaks do not factor into this calculation.  This can be demonstrated 
        // experimentally
        let getSpan (startPoint : SnapshotPoint) point count =
            Contract.Assert(not (SnapshotPointUtil.IsInsideLineBreak point))

            let rec inner (wordSpan : SnapshotSpan) (remainingWords : SnapshotSpan list) count = 
                if count = 0 then 
                    // Count gets us to this word so return it's start
                    Some wordSpan.Start
                elif count = 1 then
                    // Count gets us to the end of this word so return it's end
                    Some wordSpan.End
                elif SnapshotPointUtil.IsInsideLineBreak wordSpan.End then
                    // Line breaks don't count in this calculation.  The next start point is the
                    // start of the next line.
                    match remainingWords with
                    | [] ->
                        // Count of at least 2 and no words left.  The end is not available
                        None 
                    | nextWordSpan :: tail  ->
                        let nextLine = SnapshotPointUtil.GetContainingLine nextWordSpan.Start 
                        if nextLine.Start = nextWordSpan.Start then
                            // Subtract 1 since we needed it to get past the white space
                            inner nextWordSpan tail (count - 1)
                        else
                            // No need to subtract one to jump across the new line
                            inner nextWordSpan tail count
                else 
                    match remainingWords with
                    | [] ->
                        None
                    | nextWordSpan :: tail ->
                        // Subtract 2 because we got past the word and it's trailing white space
                        inner nextWordSpan tail (count - 2)

            // Get the set of words as a list.  To prevent excessive memory allocation cap the 
            // number of words at 'count'.  Since the 'count' includes the white space between
            // the words 'count' is a definite max on the number of words we need to consider
            let words = 
                _wordUtil.GetWords wordKind Path.Forward point
                |> Seq.truncate count
                |> List.ofSeq

            match words with 
            | [] -> 
                None
            | head :: tail -> 
                let endPoint = 
                    if point.Position < head.Start.Position then
                        // Head was in white space so it costs 1 to get over that
                        inner head tail (count - 1)
                    else
                        inner head tail count
                match endPoint with
                | None -> 
                    None
                | Some endPoint ->
                    let startPoint = 
                        if head.Start.Position < startPoint.Position then
                            head.Start
                        else
                            startPoint
                    SnapshotSpan(startPoint, endPoint) |> Some

        let span = 
            if SnapshotPointUtil.IsInsideLineBreak contextPoint && count = 1 then
                // The behavior of 'iw' is special in the case it begins in the line break and 
                // has a single count.  If there is white space before the context point we grab that 
                // else we grab a single character.  

                match SnapshotLineUtil.GetLastIncludedPoint contextLine with
                | None -> 
                    // This intentionally produces an empty span vs. None.  Doing a 'yiw' on an
                    // empty line followed by a 'put' and then 'undo' causes the no-op 'put' to 
                    // be undone
                    SnapshotSpan(contextLine.Start, 0) |> Some
                | Some point -> 
                    if SnapshotPointUtil.IsBlank point then
                        // If it's a space or tab then get the space / tab span
                        let startPoint = getReverseSpaceAndTabSpaceStart point
                        SnapshotSpan(startPoint, contextLine.End) |> Some
                    else
                        // If it's character then we get the single character.  So weird
                        SnapshotSpan(point, 1) |> Some
            elif SnapshotPointUtil.IsInsideLineBreak contextPoint then
                // With a count of greater than 1 then starting in the line break behaves
                // like a normal command.  Costs a count of 1 to get over the line break
                getSpan contextPoint contextLine.EndIncludingLineBreak (count - 1)
            else
                // Simple case.  Need to move the point backwards though if it starts in
                // a space or tab to get the full span
                let point = 
                    if SnapshotPointUtil.IsBlank contextPoint then
                        getReverseSpaceAndTabSpaceStart contextPoint
                    else
                        contextPoint
                getSpan point point count

        match span with
        | None -> None
        | Some span -> MotionResult.CreateEx span true MotionKind.CharacterWiseInclusive MotionResultFlags.AnyWord |> Some

    /// Implements the '+', '<CR>', 'CTRL-M' motions. 
    ///
    /// This is a line wise motion which uses counts hence we must use the visual snapshot
    /// when calculating the value
    member x.LineDownToFirstNonBlank count =
        x.MotionWithVisualSnapshot (fun x ->
            let number = x.CaretLine.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
            let column = SnapshotLineUtil.GetFirstNonBlankOrStart endLine |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
            let span = SnapshotSpan(x.CaretLine.Start, endLine.EndIncludingLineBreak)
            MotionResult.CreateExEx span true MotionKind.LineWise MotionResultFlags.None column)

    /// Implements the '-'
    ///
    /// This is a line wise motion which uses counts hence we must use the visual snapshot
    /// when calculating the value
    member x.LineUpToFirstNonBlank count =
        x.MotionWithVisualSnapshot (fun x ->
            let startLine = SnapshotUtil.GetLineOrFirst x.CurrentSnapshot (x.CaretLine.LineNumber - count)
            let span = SnapshotSpan(startLine.Start, x.CaretLine.EndIncludingLineBreak)
            let column = 
                startLine 
                |> SnapshotLineUtil.GetFirstNonBlankOrStart
                |> SnapshotPointUtil.GetColumn
                |> CaretColumn.InLastLine
            MotionResult.CreateExEx span false MotionKind.LineWise MotionResultFlags.None column)

    /// Implements the '|'
    ///
    /// Get the motion which is to the 'count'-th column on the current line.
    member x.LineToColumn count =
        x.MotionWithVisualSnapshot (fun x ->
            let count = count - 1
            let targetPoint = _commonOperations.GetPointForSpaces x.CaretLine count
            let forward = targetPoint.Difference(x.CaretPoint) < 0
            let span = 
                if forward then SnapshotSpan(x.CaretPoint, targetPoint)
                else            SnapshotSpan(targetPoint, x.CaretPoint)
            let column = count |> CaretColumn.ScreenColumn
            MotionResult.CreateExEx span forward MotionKind.CharacterWiseExclusive MotionResultFlags.None column)

    /// Get the motion which is 'count' characters to the left of the caret on
    /// the same line
    member x.CharLeftOnSameLine count = 
        let startPoint = 
            SnapshotPointUtil.TryGetPreviousPointOnLine x.CaretPoint count
            |> OptionUtil.getOrDefault x.CaretLine.Start
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create span false MotionKind.CharacterWiseExclusive

    /// Get the motion which is 'count' characters to the right of the caret 
    /// on the same line
    member x.CharRightOnSameLine count =
        let endPoint = 
            if SnapshotPointUtil.IsInsideLineBreak x.CaretPoint then 
                x.CaretPoint
            elif x.CaretPoint.Position + 1 = x.CaretLine.End.Position then
                x.CaretLine.End
            else
                SnapshotPointUtil.TryGetNextPointOnLine x.CaretPoint count 
                |> OptionUtil.getOrDefault x.CaretLine.End
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create span true MotionKind.CharacterWiseExclusive

    /// Get the motion which is 'count' characters before the caret
    /// through the buffer taking into acount 'virtualedit'
    member x.CharLeftWithLineWrap count =
        let skipLineBreaks = not _globalSettings.IsVirtualEditOneMore
        let startPoint = SnapshotPointUtil.GetRelativePoint x.CaretPoint -count skipLineBreaks
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create span false MotionKind.CharacterWiseExclusive

    /// Get the motion which is 'count' characters after the caret 
    /// through the buffer taking into acount 'virtualedit'
    member x.CharRightWithLineWrap count =
        let skipLineBreaks = not _globalSettings.IsVirtualEditOneMore
        let endPoint = SnapshotPointUtil.GetRelativePoint x.CaretPoint count skipLineBreaks
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create span true MotionKind.CharacterWiseExclusive

    /// Get a relative character motion backward or forward 'count' characters
    /// wrapping lines if 'withLineWrap' is specified
    member x.CharMotion count withLineWrap =
        if count < 0 then
            if withLineWrap then
                x.CharLeftWithLineWrap -count
            else
                x.CharLeftOnSameLine -count
        else
            if withLineWrap then
                x.CharRightWithLineWrap count
            else
                x.CharRightOnSameLine count

    /// Count chars left using the h key
    member x.CharLeft count =
        x.CharMotion -count _globalSettings.IsWhichWrapCharLeft

    /// Count chars right using the l key
    member x.CharRight count =
        x.CharMotion count _globalSettings.IsWhichWrapCharRight

    /// Count chars left using the backspace key
    member x.SpaceLeft count =
        x.CharMotion -count _globalSettings.IsWhichWrapSpaceLeft

    /// Count chars right using the space key
    member x.SpaceRight count =
        x.CharMotion count _globalSettings.IsWhichWrapSpaceRight

    /// Count chars left using the arrow key
    member x.ArrowLeft count =
        x.CharMotion -count _globalSettings.IsWhichWrapArrowLeft

    /// Count chars right using the arrow key
    member x.ArrowRight count =
        x.CharMotion count _globalSettings.IsWhichWrapArrowRight

    /// Move a single line up from the current line.  Should fail if we are currenly
    /// on the first line of the ITextBuffer
    member x.LineUp count =
        x.MotionOptionWithVisualSnapshot (fun x ->
            if x.CaretLine.LineNumber = 0 then
                None
            else
                let startLine = SnapshotUtil.GetLineOrFirst x.CurrentSnapshot (x.CaretLine.LineNumber - count)
                let span = SnapshotSpan(startLine.Start, x.CaretLine.EndIncludingLineBreak)
                let column = x.CaretPoint |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
                MotionResult.CreateExEx span false MotionKind.LineWise  MotionResultFlags.MaintainCaretColumn column |> Some)

    /// Move a single line down from the current line.  Should fail if we are currenly 
    /// on the last line of the ITextBuffer
    member x.LineDown count = 
        x.MotionOptionWithVisualSnapshot (fun x -> 
            if x.CaretLine.LineNumber = SnapshotUtil.GetLastLineNumber x.CurrentSnapshot then
                None
            else
                let snapshot = x.CurrentSnapshot
                let lineCount = snapshot.LineCount 
                let lineNumber = x.CaretLine.LineNumber + count
                let lastLine = SnapshotUtil.GetLineOrLast snapshot lineNumber
                let flags = 
                    let flags = MotionResultFlags.MaintainCaretColumn
                    if lastLine.LineNumber + 1 = lineCount then
                        // Make sure to note we wanted to include the last line if it's 
                        // empty
                        MotionResultFlags.IncludeEmptyLastLine ||| flags
                    else
                        flags
                let span = SnapshotSpan(x.CaretLine.Start, lastLine.EndIncludingLineBreak)
                let column = x.CaretPoint |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
                MotionResult.CreateExEx span true MotionKind.LineWise flags column |> Some)

    /// Implements the 'gg' motion.  
    ///
    /// Because this uses specific line numbers instead of counts we don't want to operate
    /// on the visual buffer here as vim line numbers always apply to the edit buffer. 
    member x.LineOrFirstToFirstNonBlank numberOpt = 
        _jumpList.Add x.CaretPoint

        let endLine = 
            match numberOpt with
            | Some number ->  SnapshotUtil.GetLineOrFirst x.CurrentSnapshot (Util.VimLineToTssLine number)
            | None -> SnapshotUtil.GetFirstLine x.CurrentSnapshot
        x.LineToLineFirstNonBlankMotion x.CaretLine endLine

    /// Implements the 'G' motion
    ///
    /// Because this uses specific line numbers instead of counts we don't want to operate
    /// on the visual buffer here as vim line numbers always apply to the edit buffer. 
    member x.LineOrLastToFirstNonBlank numberOpt = 
        _jumpList.Add x.CaretPoint

        let endLine = 
            match numberOpt with
            | Some number ->  SnapshotUtil.GetLineOrLast x.CurrentSnapshot (Util.VimLineToTssLine number)
            | None -> SnapshotUtil.GetLastLine x.CurrentSnapshot 
        x.LineToLineFirstNonBlankMotion x.CaretLine endLine

    /// Go to the last non-blank character on the 'count - 1' line
    member x.LastNonBlankOnLine count = 
        x.MotionWithVisualSnapshot (fun x -> 
            let number = x.CaretLine.LineNumber + (count - 1)
            let endLine = SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
            let endPoint = 
                // Find the last non-blank character on the line.  If the line is 
                // completely blank then the start of the line is used.  Remember we need
                // to move 1 past the character in order to include it in the line
                let endPoint = 
                    endLine.Extent
                    |> SnapshotSpanUtil.GetPoints Path.Backward
                    |> Seq.skipWhile SnapshotPointUtil.IsBlankOrEnd
                    |> SeqUtil.tryHeadOnly
                match endPoint with
                | Some point -> SnapshotPointUtil.AddOne point
                | None -> endLine.Start

            // This motion can definitely be backwards on lines which end in blanks or
            // are completely blank
            let isForward = x.CaretPoint.Position <= endPoint.Position
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint endPoint
            let span = SnapshotSpan(startPoint, endPoint)
            MotionResult.Create span isForward MotionKind.CharacterWiseInclusive)

    // TODO: Need to convert this to use the visual snapshot
    member x.LineFromTopOfVisibleWindow countOpt = 
        _jumpList.Add x.CaretPoint 

        let caretPoint, caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
        let span = 
            if lines.Length = 0 then
                caretLine.Extent
            else
                let count = Util.CountOrDefault countOpt 
                let count = min count lines.Length
                let startLine = lines.Head
                SnapshotPointUtil.GetLineRangeSpan startLine.Start count
        let isForward = caretPoint.Position <= span.End.Position
        MotionResult.Create span isForward MotionKind.LineWise
        |> x.ApplyStartOfLineOption

    // TODO: Need to convert this to use the visual snapshot
    member x.LineFromBottomOfVisibleWindow countOpt =
        _jumpList.Add x.CaretPoint 

        let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
        let span,isForward = 
            if lines.Length = 0 then caretLine.Extent,true
            else
                let endLine = 
                    match countOpt with 
                    | None -> List.nth lines (lines.Length-1)
                    | Some(count) ->    
                        let count = lines.Length - count
                        List.nth lines count
                x.SpanAndForwardFromLines caretLine endLine
        MotionResult.Create span isForward MotionKind.LineWise
        |> x.ApplyStartOfLineOption

    // TODO: Need to convert this to use the visual snapshot
    member x.LineInMiddleOfVisibleWindow () =
        _jumpList.Add x.CaretPoint 

        let caretLine = TextViewUtil.GetCaretLine _textView
        let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
        let middleLine =
            if lines.Length = 0 then caretLine
            else 
                let index = lines.Length / 2
                List.nth lines index
        let span, isForward = x.SpanAndForwardFromLines caretLine middleLine
        MotionResult.Create span isForward MotionKind.LineWise
        |> x.ApplyStartOfLineOption

    /// Implements the core portion of section backward motions
    member x.SectionBackwardCore sectionKind count = 
        _jumpList.Add x.CaretPoint

        let startPoint = 
            x.CaretPoint
            |> x.GetSections sectionKind Path.Backward
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)

        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create span false MotionKind.CharacterWiseExclusive

    /// Implements the ']]' operator
    member x.SectionForward context count = 
        let split = 
            match context with
            | MotionContext.AfterOperator -> SectionKind.OnOpenBraceOrBelowCloseBrace
            | MotionContext.Movement -> SectionKind.OnOpenBrace
        x.SectionForwardCore split context count

    /// Implements the core parts of section forward operators
    member x.SectionForwardCore sectionKind context count =
        _jumpList.Add x.CaretPoint

        let endPoint = 
            x.CaretPoint
            |> x.GetSections sectionKind Path.Forward
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

        // Modify the 'endPoint' based on the context.  When section forward is used outside
        // the context of an operator, during a movement for example, and the 'endPoint' 
        // occurs on the last line of the ITextBuffer then the first non-space / tab character
        // is chosen
        let endPoint = 
            match context with
            | MotionContext.AfterOperator ->
                endPoint
            | MotionContext.Movement -> 
                let line = SnapshotPointUtil.GetContainingLine endPoint
                if SnapshotLineUtil.IsLastLine line then 
                    match SnapshotLineUtil.GetFirstNonBlank line with
                    | Some point -> point
                    | None -> line.End
                else
                    endPoint

        // This can be backwards when a section forward movement occurs in the last line
        // of the ITextBuffer after the first non-space / tab character
        let isForward = x.CaretPoint.Position <= endPoint.Position

        let span = 
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint endPoint
            SnapshotSpan(startPoint, endPoint)

        MotionResult.Create span isForward MotionKind.CharacterWiseExclusive

    /// Implements the '][' motion
    member x.SectionForwardOrCloseBrace context count =
        x.SectionForwardCore SectionKind.OnCloseBrace context count

    /// Implements the '[[' motion
    member x.SectionBackwardOrOpenBrace count = 
        x.SectionBackwardCore SectionKind.OnOpenBrace count

    /// Implements the '[]' motion
    member x.SectionBackwardOrCloseBrace count = 
        x.SectionBackwardCore SectionKind.OnCloseBrace count

    member x.SentenceForward count = 
        _jumpList.Add x.CaretPoint
        let endPoint =
            x.GetSentences SentenceKind.Default Path.Forward x.CaretPoint
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create span true MotionKind.CharacterWiseExclusive

    member x.SentenceBackward count = 
        _jumpList.Add x.CaretPoint
        let startPoint = 
            x.GetSentences SentenceKind.Default Path.Backward x.CaretPoint
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create span false MotionKind.CharacterWiseExclusive

    /// Implements the '}' motion
    member x.ParagraphForward count = 
        _jumpList.Add x.CaretPoint

        let endPoint = 
            x.GetParagraphs Path.Forward x.CaretPoint
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create span true MotionKind.CharacterWiseExclusive 

    /// Implements the '{' motion
    member x.ParagraphBackward count = 
        _jumpList.Add x.CaretPoint

        let startPoint = 
            x.GetParagraphs Path.Backward x.CaretPoint
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create span false MotionKind.CharacterWiseExclusive

    member x.QuotedString quoteChar = 
        match x.GetQuotedStringData quoteChar with
        | None -> None 
        | Some data -> 
            let span = 
                if not data.TrailingWhiteSpace.IsEmpty then
                    SnapshotSpanUtil.Create data.LeadingQuote data.TrailingWhiteSpace.End
                else 
                    SnapshotSpanUtil.Create data.LeadingWhiteSpace.Start data.TrailingWhiteSpace.Start
            MotionResult.Create span true MotionKind.CharacterWiseInclusive |> Some

    member x.QuotedStringContents quoteChar = 
        match x.GetQuotedStringData quoteChar with
        | None -> None 
        | Some data ->
            let span = data.Contents
            MotionResult.Create span true MotionKind.CharacterWiseInclusive |> Some

    /// Get the motion for a search command.  Used to implement the '/' and '?' motions
    member x.Search (searchData : SearchData) count = 

        // Searching as part of a motion should update the last pattern information
        // irrespective of whether or not the search completes
        _vimData.LastSearchData <- searchData.LastSearchData

        x.SearchCore searchData x.CaretPoint count

    /// Implements the core searching motion.  Will *not* update the LastPatternData 
    /// value.  Simply performs the search and returns the result
    member x.SearchCore (searchData : SearchData) searchPoint count =

        // All search operations update the jump list
        _jumpList.Add x.CaretPoint

        // The search operation should also update the search history
        _vimData.SearchHistory.Add searchData.Pattern

        let searchResult = _search.FindNextPattern searchPoint searchData _wordNavigator count

        // Raise the messages that go with this given result
        CommonUtil.RaiseSearchResultMessage _statusUtil searchResult

        let motionResult = 
            match searchResult with
            | SearchResult.NotFound _ ->
                // Nothing to return here. 
                None
            | SearchResult.Found (_, span, _, _) ->
                // Create the MotionResult for the provided MotionArgument and the 
                // start and end points of the search.  Need to be careful because
                // the start and end point can be forward or reverse
                //
                // Even though the search doesn't necessarily start from the caret
                // point the resulting Span begins / ends on it
                let mutable startPoint = x.CaretPoint
                let mutable endPoint = span.Start
                let mutable motionKind = MotionKind.CharacterWiseExclusive
                let mutable isForward = true
                let mutable caretColumn = CaretColumn.None

                if startPoint.Position > endPoint.Position then
                    isForward <- false
                    endPoint <- x.CaretPoint
                    startPoint <- span.Start

                // Update the span and motion kind based on the offset information.  This
                // can make the span inclusive or linewise in some cases
                match searchData.Offset with
                | SearchOffsetData.Line _ -> 
                    let startLine = SnapshotPointUtil.GetContainingLine startPoint
                    startPoint <- startLine.Start

                    let endLine = SnapshotPointUtil.GetContainingLine endPoint
                    endPoint <- endLine.EndIncludingLineBreak
                    motionKind <- MotionKind.LineWise
                    if isForward then
                        caretColumn <- CaretColumn.InLastLine (SnapshotPointUtil.GetColumn span.Start)
                | SearchOffsetData.End _ ->
                    endPoint <- SnapshotPointUtil.AddOneOrCurrent endPoint
                    motionKind <- MotionKind.CharacterWiseInclusive 
                | _ -> ()
                    
                if startPoint.Position = endPoint.Position then
                    None
                else
                    let span = SnapshotSpan(startPoint, endPoint)
                    MotionResult.CreateExEx span isForward motionKind MotionResultFlags.None caretColumn |> Some

        _vimData.ResumeDisplayPattern()
        motionResult

    /// Move the caret to the next occurrence of the last search
    member x.LastSearch isReverse count =
        let last = _vimData.LastSearchData
        if StringUtil.isNullOrEmpty last.Pattern then
            _statusUtil.OnError Resources.NormalMode_NoPreviousSearch
            None
        else
            let path = 
                if isReverse then Path.Reverse last.Path
                else last.Path
            let searchKind = SearchKind.OfPathAndWrap path _globalSettings.WrapScan
            let searchData = SearchData(last.Pattern, last.Offset, searchKind, last.Options)
            x.SearchCore searchData x.CaretPoint count

    /// Motion from the caret to the next occurrence of the partial word under the caret
    member x.NextPartialWord path count =
        x.NextWordCore path count false

    /// Motion from the caret to the next occurrence of the word under the caret
    member x.NextWord path count =
        x.NextWordCore path count true

    /// Motion for word under the caret to the next occurrence of the word
    member x.NextWordCore path count isWholeWord = 

        // Next word motions should update the jump list
        _jumpList.Add x.CaretPoint

        // Pick the start point of the word.  If there are any words on this line after
        // the caret then we choose them.  Else we stick with the first non-blank.
        // 
        // Note: The search for the word start is forward even if we are doing a 
        // backward search
        let point =
            let points = SnapshotPointUtil.GetPointsOnLineForward x.CaretPoint
            let isWordPoint point = 
                let c = SnapshotPointUtil.GetChar point 
                TextUtil.IsWordChar WordKind.NormalWord c

            match points |> Seq.filter isWordPoint |> SeqUtil.tryHeadOnly with
            | Some point -> point
            | None -> points |> Seq.filter SnapshotPointUtil.IsNotBlank |> SeqUtil.headOrDefault x.CaretPoint

        match _wordUtil.GetFullWordSpan WordKind.NormalWord point with
        | None -> 
            // Nothing to do if no word under the cursor
            _statusUtil.OnError Resources.NormalMode_NoWordUnderCursor
            None
        | Some span ->

            // Build up the SearchData structure
            let word = span.GetText()

            // Can only do whole words on actual words.  If it's not an actual word then
            // we revert back to non-whole word match
            let isWholeWord = 
                let isWord = word |> Seq.forall (TextUtil.IsWordChar WordKind.NormalWord)
                isWholeWord && isWord

            let pattern = if isWholeWord then PatternUtil.CreateWholeWord word else word
            let searchData = SearchData(pattern, path, _globalSettings.WrapScan)

            // Make sure to update the LastSearchData here.  It needs to be done 
            // whether or not the search actually succeeds
            _vimData.LastSearchData <- searchData

            // A word search always starts at the beginning of the word.  The pattern
            // based search will ensure that we don't match this word again because it
            // won't make an initial match at the provided point
            x.SearchCore searchData span.Start count

    /// Adjust the MotionResult value based on the rules detailed in ':help exclusive'.  The
    /// rules in summary are
    ///
    ///  1. If exclusive, ends in column 0 the motion is moved to the end of the previous
    ///     line and motion becomes inclusive
    ///  2. If exclusive, ends in column 0, and starts before or at the first non-blank
    ///     in the line then it becomes line wise
    ///
    /// The documentation around 'exclusive-linewise' says it applies to any 
    /// exclusive motion.  However in practice it doesn't appear to apply to 
    /// word motions
    member x.AdjustMotionResult motion (motionResult : MotionResult) =

        // Do the actual adjustment for exclusive motions.  The kind which should be 
        // added to the adjusted motion is provided
        let adjust () = 

            // Intentionally look at the line containing the End here as we
            // want to look to see if this is in column 0 (Vim calls it column 1). 
            let originalSpan = motionResult.Span
            let startLine = SnapshotSpanUtil.GetStartLine originalSpan
            let endLine = SnapshotPointUtil.GetContainingLine originalSpan.End
            let snapshot = startLine.Snapshot
            let firstNonBlank = SnapshotLineUtil.GetFirstNonBlankOrStart startLine
            let endsInColumnZero = SnapshotPointUtil.IsStartOfLine originalSpan.End

            if endLine.LineNumber <= startLine.LineNumber then
                // No adjustment needed when everything is on the same line
                motionResult
            elif not endsInColumnZero then
                // End is not the start of the line so there is no adjustment to 
                // be made.  
                motionResult
            elif originalSpan.Start.Position <= firstNonBlank.Position then
                // Rule #2. Make this a line wise motion.  Also remove the column 
                // set.  This is necessary because these set the column to 0 which
                // is redundant and confusing for line wise motions when moving the 
                // caret
                let span = SnapshotSpan(startLine.Start, endLine.Start)
                let kind = MotionKind.LineWise 
                let flags = motionResult.MotionResultFlags ||| MotionResultFlags.ExclusiveLineWise
                { motionResult with 
                    Span = span
                    OriginalSpan = originalSpan
                    MotionKind = kind
                    MotionResultFlags = flags 
                    DesiredColumn = CaretColumn.AfterLastLine }
            else 
                // Rule #1. Move this back a line.
                let line = SnapshotUtil.GetLine originalSpan.Snapshot (endLine.LineNumber - 1)
                let span = SnapshotSpan(originalSpan.Start, line.End)
                let kind = MotionKind.CharacterWiseInclusive
                { motionResult with Span = span; OriginalSpan = originalSpan; MotionKind = kind }

        match motionResult.MotionKind with
        | MotionKind.CharacterWiseExclusive -> adjust()
        | MotionKind.CharacterWiseInclusive -> motionResult
        | MotionKind.LineWise _ -> motionResult

    /// Run the specified motion and return it's result
    member x.GetMotion motion (motionArgument : MotionArgument) = 

        let motionResult = 
            match motion with 
            | Motion.AllBlock blockKind -> x.AllBlock x.CaretPoint blockKind motionArgument.Count
            | Motion.AllParagraph -> x.AllParagraph motionArgument.Count
            | Motion.AllWord wordKind -> x.AllWord wordKind motionArgument.Count x.CaretPoint
            | Motion.AllSentence -> x.AllSentence motionArgument.Count |> Some
            | Motion.BeginingOfLine -> x.BeginingOfLine() |> Some
            | Motion.CharLeft -> x.CharLeft motionArgument.Count |> Some
            | Motion.CharRight -> x.CharRight motionArgument.Count |> Some
            | Motion.SpaceLeft -> x.SpaceLeft motionArgument.Count |> Some
            | Motion.SpaceRight -> x.SpaceRight motionArgument.Count |> Some
            | Motion.ArrowLeft -> x.ArrowLeft motionArgument.Count |> Some
            | Motion.ArrowRight -> x.ArrowRight motionArgument.Count |> Some
            | Motion.CharSearch (kind, direction, c) -> x.CharSearch c motionArgument.Count kind direction
            | Motion.DisplayLineDown -> x.DisplayLineDown motionArgument.Count
            | Motion.DisplayLineUp -> x.DisplayLineUp motionArgument.Count
            | Motion.DisplayLineStart -> x.DisplayLineStart()
            | Motion.DisplayLineEnd -> x.DisplayLineEnd()
            | Motion.DisplayLineMiddleOfScreen -> x.DisplayLineMiddleOfScreen () 
            | Motion.EndOfLine -> x.EndOfLine motionArgument.Count |> Some
            | Motion.EndOfWord wordKind -> x.EndOfWord wordKind motionArgument.Count |> Some
            | Motion.FirstNonBlankOnCurrentLine -> x.FirstNonBlankOnCurrentLine() |> Some
            | Motion.FirstNonBlankOnLine -> x.FirstNonBlankOnLine motionArgument.Count |> Some
            | Motion.InnerBlock blockKind -> x.InnerBlock x.CaretPoint blockKind motionArgument.Count
            | Motion.InnerWord wordKind -> x.InnerWord wordKind motionArgument.Count x.CaretPoint
            | Motion.LastNonBlankOnLine -> x.LastNonBlankOnLine motionArgument.Count |> Some
            | Motion.LastSearch isReverse -> x.LastSearch isReverse motionArgument.Count
            | Motion.LineDown -> x.LineDown motionArgument.Count
            | Motion.LineDownToFirstNonBlank -> x.LineDownToFirstNonBlank motionArgument.Count |> Some
            | Motion.LineFromBottomOfVisibleWindow -> x.LineFromBottomOfVisibleWindow motionArgument.RawCount |> Some
            | Motion.LineFromTopOfVisibleWindow -> x.LineFromTopOfVisibleWindow motionArgument.RawCount |> Some
            | Motion.LineInMiddleOfVisibleWindow -> x.LineInMiddleOfVisibleWindow() |> Some
            | Motion.LineOrFirstToFirstNonBlank -> x.LineOrFirstToFirstNonBlank motionArgument.RawCount |> Some
            | Motion.LineOrLastToFirstNonBlank -> x.LineOrLastToFirstNonBlank motionArgument.RawCount |> Some
            | Motion.LineUp -> x.LineUp motionArgument.Count
            | Motion.LineUpToFirstNonBlank -> x.LineUpToFirstNonBlank motionArgument.Count |> Some
            | Motion.Mark localMark -> x.Mark localMark
            | Motion.MarkLine localMark -> x.MarkLine localMark
            | Motion.MatchingToken -> x.MatchingToken()
            | Motion.NextPartialWord path -> x.NextPartialWord path motionArgument.Count
            | Motion.NextWord path -> x.NextWord path motionArgument.Count
            | Motion.ParagraphBackward -> x.ParagraphBackward motionArgument.Count |> Some
            | Motion.ParagraphForward -> x.ParagraphForward motionArgument.Count |> Some
            | Motion.QuotedString quoteChar -> x.QuotedString quoteChar
            | Motion.QuotedStringContents quoteChar -> x.QuotedStringContents quoteChar
            | Motion.RepeatLastCharSearch -> x.RepeatLastCharSearch()
            | Motion.RepeatLastCharSearchOpposite -> x.RepeatLastCharSearchOpposite()
            | Motion.Search searchData-> x.Search searchData motionArgument.Count
            | Motion.SectionBackwardOrCloseBrace -> x.SectionBackwardOrCloseBrace motionArgument.Count |> Some
            | Motion.SectionBackwardOrOpenBrace -> x.SectionBackwardOrOpenBrace motionArgument.Count |> Some
            | Motion.SectionForward -> x.SectionForward motionArgument.MotionContext motionArgument.Count |> Some
            | Motion.SectionForwardOrCloseBrace -> x.SectionForwardOrCloseBrace motionArgument.MotionContext motionArgument.Count |> Some
            | Motion.ScreenColumn -> x.LineToColumn motionArgument.Count |> Some
            | Motion.SentenceBackward -> x.SentenceBackward motionArgument.Count |> Some
            | Motion.SentenceForward -> x.SentenceForward motionArgument.Count |> Some
            | Motion.UnmatchedToken (path, kind) -> x.UnmatchedToken path kind motionArgument.Count
            | Motion.WordBackward wordKind -> x.WordBackward wordKind motionArgument.Count |> Some
            | Motion.WordForward wordKind -> x.WordForward wordKind motionArgument.Count motionArgument.MotionContext |> Some

        // If this motion is being used for an operator we need to consider the exclusive
        // promotions
        motionResult 
        |> Option.map (fun motionResult -> 
            match motionArgument.MotionContext with
            | MotionContext.AfterOperator -> x.AdjustMotionResult motion motionResult
            | MotionContext.Movement -> motionResult) 

    member x.GetTextObject motion point = 
        // TODO: Need to expand for all text objects

        match motion with 
        | Motion.AllBlock blockKind -> x.AllBlock point blockKind 1
        | Motion.AllWord wordKind -> x.AllWord wordKind 1 point
        | Motion.InnerWord wordKind -> x.InnerWord wordKind 1 point 
        | Motion.InnerBlock blockKind -> x.InnerBlock point blockKind 1
        | _ -> None

    interface IMotionUtil with
        member x.TextView = _textView
        member x.GetMotion motion motionArgument = x.GetMotion motion motionArgument
        member x.GetTextObject motion point = x.GetTextObject motion point

