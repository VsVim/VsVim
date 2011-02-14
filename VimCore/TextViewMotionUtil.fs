#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type QuotedStringData =  {
    LeadingWhiteSpace : SnapshotSpan
    LeadingQuote : SnapshotPoint
    Contents : SnapshotSpan
    TrailingQuote : SnapshotPoint
    TrailingWhiteSpace : SnapshotSpan 
} with
    
    member x.FullSpan = SnapshotSpanUtil.Create x.LeadingWhiteSpace.Start x.TrailingWhiteSpace.End

/// Flags which can occur on a matching token
type TokenFlags = 

    /// No flags and hence no restrictions
    | None = 0

    /// Specified when a match must occur on a separate line
    | MatchOnSeparateLine = 0x1

    /// Specified when a token is only valid at the start of the line
    | ValidOnlyAtStartOfLine = 0x2 

    /// Start token does not create a new nesting.  Used for C style 
    /// block comments because multiple / * doesn't create a nesting
    | StartTokenDoesNotNest = 0x4

type Token = SnapshotSpan * TokenFlags

/// Motion utility class for parsing out constructs from the text buffer
module internal MotionUtil =

    /// The Standard tokens which are used when getting matches in an ITextBuffer.  The syntax
    /// is (start, end matches, flags)
    let StandardMatchTokens = 
        [
            ("(", [")"], TokenFlags.None)
            ("[", ["]"], TokenFlags.None)
            ("{", ["}"], TokenFlags.None)
            ("/*", ["*/"], TokenFlags.StartTokenDoesNotNest)
            ("#if", ["#else"; "#elif"; "#endif"], TokenFlags.MatchOnSeparateLine ||| TokenFlags.ValidOnlyAtStartOfLine)
            ("#else", ["#else"; "#elif"; "#endif"], TokenFlags.MatchOnSeparateLine ||| TokenFlags.ValidOnlyAtStartOfLine)
            ("#elif", ["#else"; "#elif"; "#endif"], TokenFlags.MatchOnSeparateLine ||| TokenFlags.ValidOnlyAtStartOfLine) 
        ]

    /// Set of all of the tokens which need to be considered
    let StandardMatchTokenMap = 
        StandardMatchTokens
        |> Seq.map (fun (start, endList, flags) -> (start :: endList) |> Seq.map (fun token -> (token, flags)))
        |> Seq.concat
        |> Map.ofSeq

    /// Get the Token in the given SnapshotSpan 
    let GetMatchTokens span = 

        // Build up a set of tokens which start words that we care about
        let startSet = 
            StandardMatchTokenMap
            |> Seq.map (fun pair -> pair.Key.[0])
            |> Set.ofSeq

        seq {

            use e = (SnapshotSpanUtil.GetPoints span).GetEnumerator()
            let builder = System.Text.StringBuilder()
            let builderStart = ref span.Start

            // Is the data in the builder a prefix match for any item in the 
            // set of possible matches
            let isPrefixMatch current = 
                StandardMatchTokenMap |> Seq.exists (fun pair -> pair.Key.StartsWith current)

            let inToken = ref false
            while e.MoveNext() do
                let currentPoint = e.Current
                let current = currentPoint.GetChar()
                if inToken.Value then
                    // Append the next value and check to see if we've completed the 
                    // match or need to continue looking
                    builder.Append(current) |> ignore
                    let current = builder.ToString()
                    match Map.tryFind current StandardMatchTokenMap with
                    | Some flags -> 

                        // Found a match.  Yield the Token
                        yield (SnapshotSpan(builderStart.Value, builder.Length), flags)
                        inToken := false

                    | None -> 

                        // If we don't still have a prefix match then reset the search
                        if not (isPrefixMatch current) then 
                            inToken := false
                else 
                    match Map.tryFind (current.ToString()) StandardMatchTokenMap with
                    | Some flags -> 
                        yield (SnapshotSpan(currentPoint, 1), flags)
                    | None ->
                        if Set.contains current startSet then
                            builderStart := currentPoint 
                            inToken := true
                            builder.Length <- 0
                            builder.Append(current) |> ignore

        } |> Seq.filter (fun (span, flags) -> 

            if Util.IsFlagSet flags TokenFlags.ValidOnlyAtStartOfLine then 
                // Filter out tokens which must begin at the start of the line
                // and don't
                let line = SnapshotSpanUtil.GetStartLine span
                TssUtil.FindFirstNonWhiteSpaceCharacter line = span.Start
            else
                true)

    /// Find the matching token within the buffer 
    let FindMatchingToken span flags = 

        // Is this a start token
        let startTokenNests = not (Util.IsFlagSet flags TokenFlags.StartTokenDoesNotNest)
        let text = SnapshotSpanUtil.GetText span
        let isStart, possibleMatches = 
            match StandardMatchTokens |> Seq.tryFind (fun (start, _, _) -> start = text) with
            | None -> 
                // Not a start token.  Matches are all start tokens which have this as an
                // end token
                let possibleMatches = 
                    StandardMatchTokens
                    |> Seq.filter (fun (_, endTokens, _) -> Seq.exists (fun t -> t = text) endTokens)
                    |> Seq.map (fun (start, _ , _) -> start)
                false, possibleMatches
            | Some (_, endTokens, _) ->
                // A start token, match the end tokens
                true, (endTokens |> Seq.ofList)

        let isMatch span = 
            let text = SnapshotSpanUtil.GetText span
            Seq.exists (fun t -> t = text) possibleMatches
        if isStart then
            // Starting from this token.  Start the next line if that is one of the options
            // for this token 
            let startPoint = 
                if Util.IsFlagSet flags TokenFlags.MatchOnSeparateLine then
                    span |> SnapshotSpanUtil.GetStartLine |> SnapshotLineUtil.GetEndIncludingLineBreak
                else
                    span.End
            let endPoint = SnapshotUtil.GetEndPoint startPoint.Snapshot
            let searchSpan = SnapshotSpan(startPoint, endPoint)

            // Searching for a matching end token is straigh forward.  Go forward 
            // until we find the first matching item 
            use e = (GetMatchTokens searchSpan).GetEnumerator()
            let rec inner depth = 
                if e.MoveNext() then
                    let current, _ = e.Current 
                    if isMatch current then 
                        if depth = 1 then Some current
                        else inner (depth - 1)
                    elif startTokenNests && current.GetText() = text then 
                        inner (depth + 1)
                    else 
                        inner depth
                else
                    None
            inner 1 
        else
            // Go from the start of the buffer to the start of the token
            let searchSpan = SnapshotSpan(SnapshotUtil.GetStartPoint span.Snapshot, span.Start)
            use e = (GetMatchTokens searchSpan).GetEnumerator()
            let rec inner startToken depth = 
                if e.MoveNext() then
                    let current, _ = e.Current 
                    if isMatch current then 
                        match startToken with
                        | None -> 
                            inner (Some current) 0
                        | Some _ ->
                            if startTokenNests then inner startToken (depth + 1)
                            else inner startToken 0
                    elif current.GetText() = text then 
                        if depth > 0 then inner startToken (depth - 1)
                        else inner None 0 
                    else 
                        inner startToken depth
                else
                    startToken
            inner None 0

    /// Does this point represent a section boundary.  This occurs after a form feed 
    /// in the first column in a line.  
    ///
    /// It should also occur on a section macro boundary but at this time those are
    /// not supported
    let IsSectionBoundary point = 
        let line = SnapshotPointUtil.GetContainingLine point
        line.Start = point && line.Length > 0 && point.GetChar() = '\f'

    /// Is the provided point a Paragraph only boundary?  A paragraph boundary will occur 
    /// on an empty line.  Note: Empty means empty, not a blank line.
    ///
    /// It should also occur on a paragraph macro boundary but at this time those are
    /// not supported
    let IsParagraphBoundaryOnly point = 
        let line = SnapshotPointUtil.GetContainingLine point
        line.Start = point && line.Length = 0 

    /// Is this a functional paragraph boundary.  That is a section or paragraph boundary
    let IsParagraphBoundary point = IsParagraphBoundaryOnly point || IsSectionBoundary point

    let GetParagraphs point direction = 

        let snapshot = SnapshotPointUtil.GetSnapshot point

        // Get the paragraphs from the given point to the end of the buffer in a forward
        // motion 
        let forSpanForward () = 
            seq {

                // This is the start of the next span returned from this sequence
                let startPoint = ref point

                // Does this search begin on a paragraph only boundary
                let searchStartOnParagraphOnlyBoundary = ref (IsParagraphBoundaryOnly point)

                // Current line number being examined
                let lineNumber = 
                    let line = SnapshotPointUtil.GetContainingLine point
                    if line.Start = point then ref line.LineNumber
                    else ref (line.LineNumber + 1)

                while SnapshotUtil.IsLineNumberValid snapshot lineNumber.Value do

                    // Skip the lines until the specified func returns true
                    let rec skip func number = 
                        match SnapshotUtil.TryGetLine snapshot number with
                        | None ->
                            number + 1
                        | Some(line) -> 
                            if func line.Start then
                                skip func (number + 1)
                            else
                                number

                    // When a span starts on a paragraph only boundary we will skip until we are 
                    // past all of the consequetive paragraph only boundaries.
                    lineNumber := 
                        if searchStartOnParagraphOnlyBoundary.Value then
                            skip IsParagraphBoundary lineNumber.Value
                        else 
                            lineNumber.Value

                    // Now skip until we hit a paragraph boundary
                    lineNumber := skip (fun point -> not (IsParagraphBoundary point)) lineNumber.Value

                    match SnapshotUtil.TryGetLine snapshot lineNumber.Value with
                    | None ->
                        // Do nothing here.  We'll fall out of the loop and yield at the
                        // catch all at the bottom
                        ()
                    | Some (line) -> 
                        yield SnapshotSpan(startPoint.Value, line.Start)
                        lineNumber := line.LineNumber + 1
                        startPoint := line.Start
                        searchStartOnParagraphOnlyBoundary := IsParagraphBoundaryOnly line.Start

                // Catch the remaining values in the final paragraph 
                yield SnapshotSpan(startPoint.Value, SnapshotUtil.GetEndPoint snapshot)
            }

        // Get the paragraph from the given point to the start of the buffer in a 
        // backward motion.
        let forSpanBackward() =
            seq { 

                // This is the end of the next span which is returned
                let endPoint = ref point

                // The line number we are currently processing 
                let lineNumber =
                    let line = SnapshotPointUtil.GetContainingLine point
                    ref line.LineNumber

                while SnapshotUtil.IsLineNumberValid snapshot lineNumber.Value do 

                    // Skip the lines until the specified func returns true
                    let rec skip func number = 
                        match SnapshotUtil.TryGetLine snapshot number with
                        | None ->
                            number - 1
                        | Some(line) -> 
                            if func line.Start then
                                skip func (number - 1)
                            else
                                number

                    // When a span starts on a paragraph only boundary we will skip until we are 
                    // past all of the consequetive paragraph only boundaries.  Then skip while 
                    // it's not any paragraph boundary
                    lineNumber := 
                        lineNumber.Value
                        |> skip IsParagraphBoundaryOnly
                        |> skip (fun point -> not (IsParagraphBoundary point))

                    match SnapshotUtil.TryGetLine snapshot lineNumber.Value with
                    | None ->
                        // Do nothing here.  We'll fall out of the loop and yield at the
                        // catch all at the bottom
                        ()
                    | Some (line) -> 
                        yield SnapshotSpan(line.Start, endPoint.Value)
                        lineNumber := line.LineNumber - 1
                        endPoint := line.Start

                yield SnapshotSpan(SnapshotUtil.GetStartPoint snapshot, endPoint.Value)
            }

        match direction with 
        | Direction.Forward -> forSpanForward()
        | Direction.Backward -> forSpanBackward()

    /// Set of characters which represent the end of a sentence. 
    let SentenceEndChars = ['.'; '!'; '?']

    /// Set of characters which can validly follow a sentence 
    let SentenceTrailingChars = [')';']'; '"'; '\'']

    let GetSentences point direction = 

        let snapshot = SnapshotPointUtil.GetSnapshot point

        // Is the char for the provided point in the given list.  Make sure to 
        // account for the snapshot end point here as it makes the remaining 
        // logic easier 
        let isCharInList list point = 
            match SnapshotPointUtil.TryGetChar point with
            | None -> 
                false
            | Some c ->
                let c = SnapshotPointUtil.GetChar point
                ListUtil.contains c list 

        // Functions for moving the point forward or backward while the provided 
        // lambda returns true for the current point
        // Skip forward while the provided func is returning true for the point
        let moveForwardWhile, moveBackwardWhile =
            let func movePointFunc testPointFunc point = 
                let rec inner point = 
                    if testPointFunc point then
                        match movePointFunc point with
                        | None -> point
                        | Some point -> inner point
                    else
                        point
                inner point
            (func SnapshotPointUtil.TryAddOne), (func SnapshotPointUtil.TrySubtractOne)

        // Is this a sentence boundary.  Only checks for actual sentence boundaries
        // and not paragraph or section boundaries.
        let isSentenceBoundaryOnly point = 
            if isCharInList SentenceEndChars point then

                // Need to verify the end point is followed by a valid terminating
                // character: whitespace or end of line
                let line = SnapshotPointUtil.GetContainingLine point

                // Move past the possible legal trailing chars which can occur
                // between the end and terminating char
                let point = 
                    let point = SnapshotPointUtil.AddOne point
                    moveForwardWhile (isCharInList SentenceTrailingChars) point

                SnapshotPointUtil.IsWhiteSpace point ||
                SnapshotPointUtil.IsEndPoint point ||
                line.End = point 

            else
                false

        // Get the end of the boundary provided a SnapshotPoint which points to 
        // a sentence boundary end character
        let getBoundaryEnd boundaryPoint = 

            if SnapshotPointUtil.IsEndPoint boundaryPoint then
                boundaryPoint
            else
                let point = SnapshotPointUtil.AddOne boundaryPoint

                if isCharInList SentenceTrailingChars point then
                    // Definitely at the end of a sentence.  First step is to skip
                    // all of the after end chars
                    let point = 
                        let point = SnapshotPointUtil.AddOne point 
                        moveForwardWhile (isCharInList SentenceTrailingChars) point

                    // Now we can skip as many white spaces and tabs as we'd like
                    let point = 
                        let isWhiteSpace point = 
                            match SnapshotPointUtil.TryGetChar point with
                            | None -> false
                            | Some c -> CharUtil.IsWhiteSpace c 
                        moveForwardWhile isWhiteSpace point

                    // Skip again if we're at the end of a line 
                    let line = SnapshotPointUtil.GetContainingLine point
                    let span = SnapshotLineUtil.GetLineBreakSpan line
                    if span.Contains point then line.EndIncludingLineBreak
                    else point
    
                else
                    // No trailing characters so the end point is one after
                    // the actual end character 
                    point

        // Get the sentences for the given span in a forward fashion
        let forSpanForward span = 
            seq {

                // This is the start point of the next SnapshotSpan we will
                // be yielding
                let startPoint = ref (SnapshotSpanUtil.GetStartPoint span)

                let currentPoint = ref (SnapshotSpanUtil.GetStartPoint span)

                while currentPoint.Value.Position < span.End.Position do

                    // Move forward until we have a sentence boundary
                    let point = moveForwardWhile (fun point -> not (isSentenceBoundaryOnly point)) currentPoint.Value
                    let point = getBoundaryEnd point

                    if span.Contains(point) then
                        // End is within the span so create the span
                        yield SnapshotSpan(startPoint.Value, point)
                        startPoint := point
                        currentPoint := point
                    else
                        // Set currentPoint to be the end of the span so we break out
                        // of the loop
                        currentPoint := span.End

                if startPoint.Value <> span.End then
                    yield SnapshotSpan(startPoint.Value, span.End)
            }

        // Get the sentences for the given span in a backward fashion
        let forSpanBackward span = 
            seq {

                // Represents the end point of the next SnapshotSpan value yielded
                // from this sequence
                let endPoint = ref (SnapshotSpanUtil.GetEndPoint span)

                // Current point which is used to iterate through the buffer 
                let currentPoint = ref (SnapshotPointUtil.SubtractOneOrCurrent span.End)

                while currentPoint.Value.Position > span.Start.Position do

                    // Move the point backwards until we hit an actual sentence break
                    let point = moveBackwardWhile (fun point -> not (isSentenceBoundaryOnly point)) currentPoint.Value
                    if point.Position <= span.Start.Position then
                        // Break out of the loop 
                        currentPoint := span.Start
                    else
                        // Get the end of the current boundary as that will be the start of the next
                        // SnapshotSpan
                        let startPoint = getBoundaryEnd point

                        // Watch out for the case where we start at the end of the buffer and the 
                        // last character is a sentenc boundary.  Don't return the empty span
                        if startPoint <> endPoint.Value then
                            yield SnapshotSpan(startPoint, endPoint.Value)

                        endPoint := startPoint
                        currentPoint := SnapshotPointUtil.SubtractOneOrCurrent point

                yield SnapshotSpan(span.Start, endPoint.Value)
            }

        let func = 
            match direction with
            | Direction.Forward -> forSpanForward
            | Direction.Backward -> forSpanBackward

        GetParagraphs point direction
        |> Seq.map func
        |> Seq.concat

    let GetSentenceFull point = 
        GetSentences point Direction.Backward
        |> Seq.tryFind (fun x -> x.Contains(point))
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateFromStartToProvidedEnd point)

type internal TextViewMotionUtil 
    ( 
        _textView : ITextView,
        _markMap : IMarkMap,
        _localSettings : IVimLocalSettings,
        _search : ISearchService,
        _navigator : ITextStructureNavigator,
        _vimData : IVimData ) = 

    let _textBuffer = _textView.TextBuffer
    let _settings = _localSettings.GlobalSettings

    /// Caret point in the view
    member x.StartPoint = TextViewUtil.GetCaretPoint _textView

    member x.SpanAndForwardFromLines (line1:ITextSnapshotLine) (line2:ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.End),true
        else SnapshotSpan(line2.Start, line1.End),false

    /// Apply the startofline option to the given MotionResult
    member x.ApplyStartOfLineOption (motionData:MotionResult) =
        if not _settings.StartOfLine then motionData 
        else
            let endLine = 
                if motionData.IsForward then SnapshotSpanUtil.GetEndLine motionData.Span
                else SnapshotSpanUtil.GetStartLine motionData.Span
            let point = TssUtil.FindFirstNonWhiteSpaceCharacter endLine
            let _,column = SnapshotPointUtil.GetLineColumn point
            { motionData with Column=Some column }

    member x.ForwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None 
        | Some(point:SnapshotPoint) -> 
            let span = SnapshotSpan(start, point.Add(1))
            {
                Span = span 
                IsForward = true 
                IsAnyWordMotion = false
                OperationKind = OperationKind.CharacterWise 
                MotionKind = MotionKind.Inclusive 
                Column = None} |> Some

    member x.BackwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None
        | Some(point:SnapshotPoint) ->
            let span = SnapshotSpan(point, start)
            {
                Span = span 
                IsForward = false 
                IsAnyWordMotion = false
                OperationKind = OperationKind.CharacterWise 
                MotionKind = MotionKind.Exclusive 
                Column = None} |> Some

    /// Get the motion between the provided two lines.  The motion will be linewise
    /// and have a column of the first non-whitespace character.  If the 'startofline'
    /// option is not set it will keep the original column
    member x.LineToLineFirstNonWhiteSpaceMotion (startLine : ITextSnapshotLine) (endLine : ITextSnapshotLine) = 

        // Get the column based on the 'startofline' option
        let column = 
            if _settings.StartOfLine then 
                endLine |> TssUtil.FindFirstNonWhiteSpaceCharacter |> SnapshotPointUtil.GetColumn
            else
                _textView |> TextViewUtil.GetCaretPoint |> SnapshotPointUtil.GetColumn

        // Create the range based on the provided lines.  Remember they can be in reverse
        // order
        let range, isForward = 
            let startLine, endLine, isForward = 
                if startLine.LineNumber <= endLine.LineNumber then startLine, endLine, true 
                else endLine, startLine, false
            (SnapshotLineRangeUtil.CreateForLineRange startLine endLine, isForward)
        {
            Span = range.ExtentIncludingLineBreak
            IsForward = isForward
            IsAnyWordMotion = false
            OperationKind = OperationKind.LineWise 
            MotionKind = MotionKind.Inclusive 
            Column = Some column }

    member x.GetParagraphs direction count = 
        let point = TextViewUtil.GetCaretPoint _textView
        let span = 
            MotionUtil.GetParagraphs point direction
            |> Seq.truncate count
            |> SnapshotSpanUtil.CreateCombined
        let span = 
            match span with 
            | Some(span) -> span
            | None -> SnapshotSpan(point, 0)
        let isForward = Direction.Forward = direction
        {
            Span = span 
            IsForward = isForward 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive
            OperationKind = OperationKind.CharacterWise
            Column = None }

    member x.SectionBackwardOrOther count otherChar = 
        let startPoint = SnapshotUtil.GetStartPoint _textView.TextSnapshot
        let endPoint = SnapshotUtil.GetEndPoint _textView.TextSnapshot

        // Get the previous point is the chain given the "current" point.  Must move
        // backwards to avoid infinite looping on the control chars.  Has the pleasant
        // side effect of implementing the motion as exclusive
        let rec getPrev point =
            let rec inner point = 
                if point = startPoint then startPoint
                elif point = endPoint then inner (endPoint.Subtract(1))
                elif SnapshotPointUtil.IsStartOfLine point then 
                    let c = SnapshotPointUtil.GetChar point 
                    if c = '\f' then point
                    elif c = otherChar then point
                    else inner (point.Subtract(1))
                else inner (point.Subtract(1))
            if point = startPoint then point
            else point.Subtract(1) |> inner

        let caretPoint = TextViewUtil.GetCaretPoint _textView 
        let beginPoint = 
            let rec inner count point = 
                if count = 0 then point
                else inner (count-1) (getPrev point)
            inner count caretPoint
        let span = SnapshotSpan(beginPoint,caretPoint)
        {
            Span = span 
            IsForward = false 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = Some 0}

    member x.GetQuotedStringData () = 
        let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView

        // Find the quoted data structure from a given point on the line 
        let getData startPoint = 

            // Find a quote from the given point.  This takes into account escaped quotes
            // and will only return a non-escaped one
            let findQuote point =
                let rec inner point inEscape = 
                    if point = caretLine.End then None
                    else
                        let c = SnapshotPointUtil.GetChar point
                        if c = '\"' then
                            if inEscape then inner (point.Add(1)) false 
                            else Some point
                        elif StringUtil.containsChar _localSettings.QuoteEscape c then 
                            inner (point.Add(1)) true
                        else
                            inner (point.Add(1)) false
                inner point false
    
            match findQuote startPoint with
            | None -> None
            | Some(firstQuote) ->
                match findQuote (firstQuote.Add(1)) with
                | None -> None
                | Some(secondQuote) ->
    
                    let span = SnapshotSpanUtil.Create (SnapshotPointUtil.AddOne firstQuote) secondQuote
                    let isWhiteSpace = SnapshotPointUtil.GetChar >> CharUtil.IsWhiteSpace

                    // Calculate the leading whitespace span.  It includes the white space just
                    // before the leading quote.  The quote is not included in the span
                    let leadingSpan = 
    
                        let isPreviousWhiteSpace point = 
                            if point = caretLine.Start then false
                            else 
                                match SnapshotPointUtil.TrySubtractOne point with
                                | None -> false
                                | Some(prev) -> prev |> isWhiteSpace
    
                        let rec inner start = 
                            if isPreviousWhiteSpace start then start |> SnapshotPointUtil.SubtractOne |> inner
                            else SnapshotSpanUtil.Create start firstQuote
                        inner firstQuote

                    // Calculate the trailing whitespace span
                    let trailingSpan =
                        
                        let isNextWhiteSpace point = 
                            match SnapshotPointUtil.TryAddOne point with
                            | None -> false
                            | Some(next) ->
                                if next.Position >= caretLine.End.Position then false
                                else isWhiteSpace next
    
                        let start = SnapshotPointUtil.AddOne secondQuote
                        let rec inner endPoint =
                            if isNextWhiteSpace endPoint then endPoint |> SnapshotPointUtil.AddOne |> inner
                            elif endPoint = start then SnapshotSpanUtil.CreateEmpty start
                            else 
                                let endPoint = SnapshotPointUtil.AddOne endPoint
                                SnapshotSpanUtil.Create start endPoint
                        inner start

                    {
                        LeadingWhiteSpace = leadingSpan
                        LeadingQuote = firstQuote
                        Contents = span
                        TrailingQuote = secondQuote
                        TrailingWhiteSpace = trailingSpan } |> Some

        // Holds all of the QuotedStringData for the given line.  This doesn't really 
        // understand string infrastructure and will see two quoted strings as three.  
        let all = 
            seq {
                let more = ref true
                let cur = ref caretLine.Start
                while more.Value do
                    match getData cur.Value with
                    | None -> more := false
                    | Some(data) ->
                        yield data
                        cur := data.TrailingQuote 
            }

        // First search for the QuotedStringData who's FullSpan includes the caret 
        // point 
        match all |> Seq.tryFind (fun data -> data.FullSpan.Contains caretPoint) with
        | Some(data) -> Some data
        | None -> SeqUtil.tryHeadOnly all

    member x.Mark c = 
        match _markMap.GetLocalMark _textBuffer c with 
        | None -> 
            None
        | Some (virtualPoint) ->
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint virtualPoint.Position
            let span = SnapshotSpan(startPoint, endPoint)
            {
                Span = span
                IsForward = caretPoint = startPoint
                IsAnyWordMotion = false
                MotionKind = MotionKind.Exclusive
                OperationKind = OperationKind.CharacterWise
                Column = SnapshotPointUtil.GetColumn virtualPoint.Position |> Some } |> Some

    member x.MarkLine c =
        match _markMap.GetLocalMark _textBuffer c with 
        | None -> 
            None
        | Some (virtualPoint) ->
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint virtualPoint.Position
            let startLine = SnapshotPointUtil.GetContainingLine startPoint
            let endLine = SnapshotPointUtil.GetContainingLine endPoint
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            {
                Span = range.ExtentIncludingLineBreak
                IsForward = caretPoint = startPoint
                IsAnyWordMotion = false
                MotionKind = MotionKind.Inclusive
                OperationKind = OperationKind.LineWise
                Column = 
                    virtualPoint.Position
                    |> SnapshotPointUtil.GetContainingLine
                    |> TssUtil.FindFirstNonWhiteSpaceCharacter
                    |> SnapshotPointUtil.GetColumn
                    |> Some } |> Some

    /// Find the matching token for the next token on the current line 
    member x.MatchingToken() = 
        // First find the next token on this line from the caret point
        let caretPoint, caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let tokens = MotionUtil.GetMatchTokens (SnapshotSpan(caretPoint, caretLine.End))
        match SeqUtil.tryHeadOnly tokens with
        | None -> 
            // No tokens on the line 
            None
        | Some (token, flags) -> 
            // Now lets look for the matching token 
            match MotionUtil.FindMatchingToken token flags with
            | None ->
                // No matching token so once again no motion data
                None
            | Some otherToken ->
                // Nice now order the tokens appropriately to get the span 
                let span, isForward = 
                    if caretPoint.Position < otherToken.Start.Position then
                        SnapshotSpan(caretPoint, otherToken.End), true
                    else
                        SnapshotSpan(otherToken.Start, caretPoint.Add(1)), false
                let column = otherToken.Start |> SnapshotPointUtil.GetColumn |> Some
                {
                    Span = span
                    IsForward = isForward
                    IsAnyWordMotion = false
                    MotionKind = MotionKind.Inclusive
                    OperationKind = OperationKind.CharacterWise
                    Column = column} |> Some

    /// This method mainly used as an implementation of the 'ap' motion.  In many 
    /// ways this is the odd-ball out 'a' motion inside of the "Text Object Selection"
    /// (:help object-select) set of motions.  The documentation for 'a' motions states
    /// if there is no trailing whitespace or the caret started in whitespace then
    /// the leading whitespace will be included.  The 'p' motion is the only one
    /// which appears to go backwards line wise
    member x.GetAllParagraph count = 

        let caretPoint = TextViewUtil.GetCaretPoint _textView 
        let span = 
            let startPoint = SnapshotUtil.GetStartPoint caretPoint.Snapshot
            MotionUtil.GetParagraphs startPoint Direction.Forward 
            |> Seq.skipWhile (fun span -> not (span.Contains caretPoint))
            |> Seq.truncate count
            |> SnapshotSpanUtil.CreateCombined
            |> OptionUtil.getOrDefault (SnapshotSpan(caretPoint, 0))

        let span = 

            // Does the span end in whitespace
            let doesSpanEndInWhiteSpace = 
                let line = SnapshotPointUtil.GetContainingLine span.End
                SnapshotLineUtil.IsWhiteSpace line

            if SnapshotPointUtil.IsWhiteSpace caretPoint || not (SnapshotPointUtil.IsEndPoint span.End || doesSpanEndInWhiteSpace) then
                // Need to include the whitespace in front of the motion 
                let startLine = 
                    let snapshot = caretPoint.Snapshot
                    let rec inner (line : ITextSnapshotLine) =
                        let prevNumber = line.LineNumber - 1
                        match SnapshotUtil.TryGetLine snapshot prevNumber with
                        | None ->
                            line 
                        | Some(prevLine) ->
                            if SnapshotLineUtil.IsWhiteSpace prevLine then
                                inner prevLine
                            else
                                line
                    span.Start |> SnapshotPointUtil.GetContainingLine |> inner
                SnapshotSpan(startLine.Start, span.End)
            elif doesSpanEndInWhiteSpace then
                // Include the trailing whitespace
                let endLine = 
                    let snapshot = caretPoint.Snapshot
                    let rec inner (line : ITextSnapshotLine) = 
                        let nextNumber = line.LineNumber + 1
                        match SnapshotUtil.TryGetLine snapshot nextNumber with
                        | None ->
                            line
                        | Some nextLine ->
                            if SnapshotLineUtil.IsWhiteSpace nextLine then inner nextLine
                            else line
                    span.End |> SnapshotPointUtil.GetContainingLine |> inner
                SnapshotSpan(span.Start, endLine.EndIncludingLineBreak)
            else
                span

        {
            Span = span
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive
            OperationKind = OperationKind.LineWise
            Column = None }

    /// Implements the 'aw' motion. 
    member x.GetAllWord kind count = 
        let start = x.StartPoint
        let start = match TssUtil.FindCurrentFullWordSpan start kind with 
                        | Some (s) -> s.Start
                        | None -> start
        let endPoint = TssUtil.FindNextWordStart start count kind  
        let span = SnapshotSpan(start,endPoint)
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None }

    member x.BeginingOfLine() = 
        let start = x.StartPoint
        let line = SnapshotPointUtil.GetContainingLine start
        let span = SnapshotSpan(line.Start, start)
        {
            Span = span 
            IsForward = false 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None}

    member x.CharSearch c count charSearch direction = 
        match charSearch, direction with
        | CharSearchKind.ToChar, Direction.Forward -> x.ForwardCharMotionCore c count TssUtil.FindNextOccurranceOfCharOnLine
        | CharSearchKind.TillChar, Direction.Forward -> x.ForwardCharMotionCore c count TssUtil.FindTillNextOccurranceOfCharOnLine
        | CharSearchKind.ToChar, Direction.Backward -> x.BackwardCharMotionCore c count TssUtil.FindPreviousOccurranceOfCharOnLine 
        | CharSearchKind.TillChar, Direction.Backward -> x.BackwardCharMotionCore c count TssUtil.FindTillPreviousOccurranceOfCharOnLine

    /// Repeat the last f, F, t or T search pattern.
    member x.RepeatLastCharSearch () =
        match _vimData.LastCharSearch with 
        | None -> None
        | Some (kind, direction, c) -> x.CharSearch c 1 kind direction

    /// Repeat the last f, F, t or T search pattern in the opposite direction
    member x.RepeatLastCharSearchOpposite () =
        match _vimData.LastCharSearch with 
        | None -> None
        | Some (kind, direction, c) -> 
            let direction = 
                match direction with
                | Direction.Forward -> Direction.Backward
                | Direction.Backward -> Direction.Forward
            x.CharSearch c 1 kind direction

    member x.WordForward kind count =
        let start = x.StartPoint
        let endPoint = TssUtil.FindNextWordStart start count kind  
        let span = SnapshotSpan(start,endPoint)
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = true
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None}
    member x.WordBackward kind count =
        let start = x.StartPoint
        let startPoint = TssUtil.FindPreviousWordStart start count kind
        let span = SnapshotSpan(startPoint,start)
        {
            Span = span 
            IsForward = false 
            IsAnyWordMotion = true
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None}
    member x.AllWord kind count = x.GetAllWord kind count
    member x.EndOfWord kind count = 

        // Create the appropriate MotionResult structure with the provided SnapshotSpan
        let withSpan span = {
            Span = span 
            IsForward = true 
            IsAnyWordMotion  =  false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None} 

        // Move forward until we find the first non-blank and hence a word character 
        let point =
            SnapshotPointUtil.GetPoints x.StartPoint SearchKind.Forward
            |> Seq.skipWhile (fun point -> point.GetChar() |> CharUtil.IsWhiteSpace)
            |> SeqUtil.tryHeadOnly
        match point with 
        | None -> 
            SnapshotSpanUtil.CreateFromBounds x.StartPoint (SnapshotUtil.GetEndPoint _textView.TextSnapshot) |> withSpan
        | Some(point) -> 

            // Have a point and we are on a word.  There is a special case to consider where
            // we started on the last character of a word.  In that case we start searching from
            // the next point 
            let searchPoint, count = 
                match TssUtil.FindCurrentWordSpan point kind with 
                | None -> point, count
                | Some(span) -> 
                    if SnapshotSpanUtil.IsLastIncludedPoint span x.StartPoint then (span.End, count)
                    else (span.End, count - 1)

            if count = 0 then 
                // Getting the search point moved the 1 word count we had.  Done
                SnapshotSpanUtil.CreateFromBounds x.StartPoint searchPoint |> withSpan
            else 
                // Need to skip (count - 1) remaining words to get to the one we're looking for
                let wordSpan = 
                    TssUtil.GetWordSpans searchPoint kind SearchKind.Forward
                    |> SeqUtil.skipMax (count - 1)
                    |> SeqUtil.tryHeadOnly

                match wordSpan with
                | Some(span) -> 
                    SnapshotSpanUtil.Create x.StartPoint span.End |> withSpan
                | None -> 
                    let endPoint = SnapshotUtil.GetEndPoint _textView.TextSnapshot
                    SnapshotSpanUtil.Create x.StartPoint endPoint |> withSpan

    member x.EndOfLine count = 
        let start = x.StartPoint
        let span = SnapshotPointUtil.GetLineRangeSpan start count
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None}
    member x.FirstNonWhiteSpaceOnLine () =
        let start = x.StartPoint
        let line = start.GetContainingLine()
        let target = 
            SnapshotLineUtil.GetPoints line
            |> Seq.tryFind (fun x -> not (CharUtil.IsWhiteSpace (x.GetChar())) )
            |> OptionUtil.getOrDefault line.End
        let startPoint,endPoint,isForward = 
            if start.Position <= target.Position then start,target,true
            else target,start,false
        let span = SnapshotSpanUtil.CreateFromBounds startPoint endPoint
        {
            Span = span 
            IsForward = isForward 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None} 

    member x.LineDownToFirstNonWhiteSpace count =
        let line = x.StartPoint |> SnapshotPointUtil.GetContainingLine
        let number = line.LineNumber + count
        let endLine = SnapshotUtil.GetLineOrLast line.Snapshot number
        let column = TssUtil.FindFirstNonWhiteSpaceCharacter endLine |> SnapshotPointUtil.GetColumn |> Some
        let span = SnapshotSpan(line.Start, endLine.EndIncludingLineBreak)
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = column}

    member x.LineUpToFirstNonWhiteSpace count =
        let point = x.StartPoint
        let endLine = SnapshotPointUtil.GetContainingLine point
        let startLine = SnapshotUtil.GetLineOrFirst endLine.Snapshot (endLine.LineNumber - count)
        let span = SnapshotSpan(startLine.Start, endLine.End)
        let column = 
            startLine 
            |> TssUtil.FindFirstNonWhiteSpaceCharacter
            |> SnapshotPointUtil.GetColumn
        {
            Span = span 
            IsForward = false 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = Some column}

    member x.CharLeft count = 
        let start = x.StartPoint
        let prev = SnapshotPointUtil.GetPreviousPointOnLine start count 
        if prev = start then None
        else {
            Span = SnapshotSpan(prev,start) 
            IsForward = false 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None} |> Some

    member x.CharRight count =
        let start = x.StartPoint
        let next = SnapshotPointUtil.GetNextPointOnLine start count 
        if next = start then None
        else {
            Span = SnapshotSpan(start,next) 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None } |> Some

    member x.LineUp count =     
        let point = x.StartPoint
        let endLine = SnapshotPointUtil.GetContainingLine point
        let startLineNumber = max 0 (endLine.LineNumber - count)
        let startLine = SnapshotUtil.GetLine endLine.Snapshot startLineNumber
        let span = SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)
        {
            Span = span 
            IsForward = false 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = None } 

    member x.LineDown count = 
        let point = x.StartPoint
        let startLine = SnapshotPointUtil.GetContainingLine point
        let endLineNumber = startLine.LineNumber + count
        let endLine = SnapshotUtil.GetLineOrLast startLine.Snapshot endLineNumber
        let span = SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)            
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = None } 

    member x.LineOrFirstToFirstNonWhiteSpace numberOpt = 
        let point = x.StartPoint
        let originLine = SnapshotPointUtil.GetContainingLine point
        let tss= originLine.Snapshot
        let endLine = 
            match numberOpt with
            | Some(number) ->  SnapshotUtil.GetLineOrFirst tss (TssUtil.VimLineToTssLine number)
            | None -> SnapshotUtil.GetFirstLine tss 
        x.LineToLineFirstNonWhiteSpaceMotion originLine endLine

    member x.LineOrLastToFirstNonWhiteSpace numberOpt = 
        let point = x.StartPoint
        let originLine = SnapshotPointUtil.GetContainingLine point
        let tss= originLine.Snapshot
        let endLine = 
            match numberOpt with
            | Some(number) ->  SnapshotUtil.GetLineOrLast tss (TssUtil.VimLineToTssLine number)
            | None -> SnapshotUtil.GetLastLine tss 
        x.LineToLineFirstNonWhiteSpaceMotion originLine endLine

    member x.LastNonWhiteSpaceOnLine count = 
        let start = x.StartPoint
        let startLine = SnapshotPointUtil.GetContainingLine start
        let snapshot = startLine.Snapshot
        let number = startLine.LineNumber + (count-1)
        let endLine = SnapshotUtil.GetLineOrLast snapshot number
        let endPoint = TssUtil.FindLastNonWhiteSpaceCharacter endLine
        let endPoint = if SnapshotUtil.GetEndPoint snapshot = endPoint then endPoint else endPoint.Add(1)
        let span = SnapshotSpan(start,endPoint)
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None}

    member x.LineFromTopOfVisibleWindow countOpt = 
        let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
        let span = 
            if lines.Length = 0 then caretLine.Extent
            else  
                let count = (CommandUtil2.CountOrDefault countOpt) 
                let count = min count lines.Length
                let startLine = lines.Head
                SnapshotPointUtil.GetLineRangeSpan startLine.Start count
        let isForward = caretPoint.Position <= span.End.Position
        {
            Span = span 
            IsForward = isForward 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = None} |> x.ApplyStartOfLineOption

    member x.LineFromBottomOfVisibleWindow countOpt =
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
        {
            Span = span 
            IsForward = isForward 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = None} |> x.ApplyStartOfLineOption

    member x.LineInMiddleOfVisibleWindow () =
        let caretLine = TextViewUtil.GetCaretLine _textView
        let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
        let middleLine =
            if lines.Length = 0 then caretLine
            else 
                let index = lines.Length / 2
                List.nth lines index
        let span,isForward = x.SpanAndForwardFromLines caretLine middleLine
        {
            Span = span 
            IsForward = isForward 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Inclusive 
            OperationKind = OperationKind.LineWise 
            Column = None} |> x.ApplyStartOfLineOption

    member x.SentenceForward count = 
        match TextViewUtil.GetCaretPointKind _textView with 
        | PointKind.Normal (point) -> 
            let span = 
                MotionUtil.GetSentences point Direction.Forward
                |> Seq.truncate count
                |> SnapshotSpanUtil.CreateCombined 
                |> Option.get   // GetSentences must return at least one
            {
                Span = span 
                IsForward = true 
                IsAnyWordMotion = false
                MotionKind = MotionKind.Exclusive 
                OperationKind = OperationKind.CharacterWise 
                Column = None}
        | PointKind.EndPoint(_,point) -> MotionResult.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
        | PointKind.ZeroLength(point) -> MotionResult.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise

    member x.SentenceBackward count = 
        let caretPoint = TextViewUtil.GetCaretPoint _textView
        let span = 
            MotionUtil.GetSentences caretPoint Direction.Backward
            |> Seq.truncate count
            |> SnapshotSpanUtil.CreateCombined 
            |> Option.get   // GetSentences must return at least one
        {
            Span = span 
            IsForward = false 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None}

    member x.GetAllSentence count =
        let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let span = 
            if SnapshotLineUtil.GetLength caretLine = 0 then 
                // This behavior is not specified in the standard but if the caret line has
                // 0 length then only it is included in the span 
                SnapshotLineUtil.GetExtentIncludingLineBreak caretLine
            else 
                let startPoint = 
                    MotionUtil.GetSentenceFull caretPoint
                    |> SnapshotSpanUtil.GetStartPoint
                MotionUtil.GetSentences startPoint Direction.Forward 
                |> Seq.truncate count
                |> SnapshotSpanUtil.CreateCombinedOrEmpty caretPoint.Snapshot
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = None }

    member x.ParagraphForward count = 
        let startPoint = TextViewUtil.GetCaretPoint _textView
        let endPoint = 
            let next = MotionUtil.GetParagraphs startPoint Direction.Forward |> Seq.skip count
            match SeqUtil.tryHeadOnly next with
            | None -> SnapshotUtil.GetEndPoint startPoint.Snapshot
            | Some span -> span.Start
        let span = SnapshotSpan(startPoint, endPoint)
        {
            Span = span
            IsForward = true
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive
            OperationKind = OperationKind.CharacterWise
            Column = None }

    member x.SectionForward context count =
        let endPoint = SnapshotUtil.GetEndPoint _textView.TextSnapshot

        // Move a single count forward from the given point
        let rec getNext point = 
            if point = endPoint then point
            else
                let nextPoint = point.Add(1)
                if nextPoint = endPoint then endPoint
                elif SnapshotPointUtil.IsStartOfLine nextPoint then
                    match nextPoint.GetChar() with
                    | '\f' -> nextPoint
                    | '{' -> nextPoint 
                    | '}' -> 
                        match context with
                        | MotionContext.AfterOperator ->
                            let line = SnapshotPointUtil.GetContainingLine nextPoint
                            line.EndIncludingLineBreak
                        | MotionContext.Movement -> getNext nextPoint
                    | _ -> getNext nextPoint
                else getNext nextPoint

        let startPoint = TextViewUtil.GetCaretPoint _textView 
        let endPoint = 
            let rec inner count point = 
                if count = 0 then point
                else inner (count-1) (getNext point)
            inner count startPoint 

        let span = SnapshotSpan(startPoint,endPoint)
        {
            Span = span 
            IsForward = true 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.CharacterWise 
            Column = Some 0}

    member x.SectionBackwardOrOpenBrace count = x.SectionBackwardOrOther count '{'

    member x.SectionBackwardOrCloseBrace count = x.SectionBackwardOrOther count '}'

    member x.QuotedString () = 
        match x.GetQuotedStringData() with
        | None -> None 
        | Some(data) -> 
            let span = 
                if not data.TrailingWhiteSpace.IsEmpty then SnapshotSpanUtil.Create data.LeadingQuote data.TrailingWhiteSpace.End
                else SnapshotSpanUtil.Create data.LeadingWhiteSpace.Start data.TrailingWhiteSpace.Start
            {
                Span = span 
                IsForward = true 
                IsAnyWordMotion = false
                MotionKind = MotionKind.Inclusive 
                OperationKind = OperationKind.CharacterWise 
                Column = None} |> Some

    member x.QuotedStringContents () = 
        match x.GetQuotedStringData() with
        | None -> None 
        | Some(data) ->
            let span = data.Contents
            {
                Span = span 
                IsForward = true 
                IsAnyWordMotion = false
                MotionKind = MotionKind.Inclusive 
                OperationKind = OperationKind.CharacterWise 
                Column = None} |> Some

    /// Get the motion for a search command.  Used to implement the '/' and '?' motions
    member x.Search (searchData : SearchData) =

        // Searching as part of a motion should update the last search information
        // irrespective of whether or not the search completes
        _vimData.LastSearchData <- searchData

        let caretPoint = TextViewUtil.GetCaretPoint _textView
        let searchPoint = Util.GetSearchPoint searchData.Kind caretPoint
        match _search.FindNext searchData searchPoint _navigator with
        | None -> 
            None
        | Some span ->
            // Create the MotionResult for the provided MotionArgument and the 
            // start and end points of the search.  Need to be careful because
            // the start and end point can be forward or reverse
            let endPoint = span.Start
            if caretPoint.Position = endPoint.Position then
                None
            else if caretPoint.Position < endPoint.Position then 
                {
                    Span = SnapshotSpan(caretPoint, endPoint)
                    IsForward = true
                    IsAnyWordMotion = false
                    MotionKind = MotionKind.Exclusive
                    OperationKind = OperationKind.CharacterWise
                    Column = SnapshotPointUtil.GetColumn endPoint |> Some } |> Some
            else 
                {
                    Span = SnapshotSpan(endPoint, caretPoint)
                    IsForward = false
                    IsAnyWordMotion = false
                    MotionKind = MotionKind.Exclusive
                    OperationKind = OperationKind.CharacterWise
                    Column = SnapshotPointUtil.GetColumn endPoint |> Some } |> Some

    /// Run the specified motion and return it's result
    member x.GetMotion motion (motionArgument : MotionArgument) = 
        match motion with 
        | Motion.AllParagraph -> x.GetAllParagraph motionArgument.Count |> Some
        | Motion.AllWord wordKind -> x.GetAllWord wordKind motionArgument.Count |> Some
        | Motion.AllSentence -> x.GetAllSentence motionArgument.Count |> Some
        | Motion.BeginingOfLine -> x.BeginingOfLine() |> Some
        | Motion.CharLeft -> x.CharLeft motionArgument.Count
        | Motion.CharRight -> x.CharRight motionArgument.Count
        | Motion.CharSearch (kind, direction, c) -> x.CharSearch c motionArgument.Count kind direction
        | Motion.EndOfLine -> x.EndOfLine motionArgument.Count |> Some
        | Motion.EndOfWord wordKind -> x.EndOfWord wordKind motionArgument.Count |> Some
        | Motion.FirstNonWhiteSpaceOnLine -> x.FirstNonWhiteSpaceOnLine() |> Some
        | Motion.LastNonWhiteSpaceOnLine -> x.LastNonWhiteSpaceOnLine motionArgument.Count |> Some
        | Motion.LineDown -> x.LineDown motionArgument.Count |> Some
        | Motion.LineDownToFirstNonWhiteSpace -> x.LineDownToFirstNonWhiteSpace motionArgument.Count |> Some
        | Motion.LineFromBottomOfVisibleWindow -> x.LineFromBottomOfVisibleWindow motionArgument.RawCount |> Some
        | Motion.LineFromTopOfVisibleWindow -> x.LineFromTopOfVisibleWindow motionArgument.RawCount |> Some
        | Motion.LineInMiddleOfVisibleWindow -> x.LineInMiddleOfVisibleWindow() |> Some
        | Motion.LineOrFirstToFirstNonWhiteSpace -> x.LineOrFirstToFirstNonWhiteSpace motionArgument.RawCount |> Some
        | Motion.LineOrLastToFirstNonWhiteSpace -> x.LineOrLastToFirstNonWhiteSpace motionArgument.RawCount |> Some
        | Motion.LineUp -> x.LineUp motionArgument.Count |> Some
        | Motion.LineUpToFirstNonWhiteSpace -> x.LineUpToFirstNonWhiteSpace motionArgument.Count |> Some
        | Motion.Mark c -> x.Mark c
        | Motion.MarkLine c -> x.MarkLine c 
        | Motion.MatchingToken -> x.MatchingToken()
        | Motion.ParagraphBackward -> x.GetParagraphs Direction.Backward motionArgument.Count |> Some
        | Motion.ParagraphForward -> x.GetParagraphs Direction.Forward motionArgument.Count |> Some
        | Motion.QuotedString -> x.QuotedString()
        | Motion.QuotedStringContents -> x.QuotedStringContents()
        | Motion.RepeatLastCharSearch -> x.RepeatLastCharSearch()
        | Motion.RepeatLastCharSearchOpposite -> x.RepeatLastCharSearchOpposite()
        | Motion.Search searchData -> x.Search searchData
        | Motion.SectionBackwardOrCloseBrace -> x.SectionBackwardOrCloseBrace motionArgument.Count |> Some
        | Motion.SectionBackwardOrOpenBrace -> x.SectionBackwardOrOpenBrace motionArgument.Count |> Some
        | Motion.SectionForwardOrOpenBrace -> x.SectionForward motionArgument.MotionContext motionArgument.Count |> Some
        | Motion.SectionForwardOrCloseBrace -> x.SectionForward motionArgument.MotionContext motionArgument.Count |> Some
        | Motion.SentenceBackward -> x.SentenceBackward motionArgument.Count |> Some
        | Motion.SentenceForward -> x.SentenceForward motionArgument.Count |> Some
        | Motion.WordBackward wordKind -> x.WordBackward wordKind motionArgument.Count |> Some
        | Motion.WordForward wordKind -> x.WordForward wordKind motionArgument.Count |> Some

    interface ITextViewMotionUtil with
        member x.TextView = _textView
        member x.GetMotion motion motionArgument = x.GetMotion motion motionArgument

