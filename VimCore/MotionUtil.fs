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
module internal MotionUtilLegacy =

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

            use e = (SnapshotSpanUtil.GetPoints Path.Forward span).GetEnumerator()
            let builder = System.Text.StringBuilder()
            let builderStart = ref span.Start

            // Is the data in the builder a prefix match for any item in the 
            // set of possible matches
            let isPrefixMatch current = 
                StandardMatchTokenMap |> Seq.exists (fun pair -> pair.Key.StartsWith current)

            while e.MoveNext() do
                let currentPoint = e.Current
                let current = currentPoint.GetChar()

                let toYield = 
                    if builder.Length > 0 then
                        // Append the next value and check to see if we've completed the 
                        // match or need to continue looking
                        builder.Append(current) |> ignore
                        let current = builder.ToString()
                        match Map.tryFind current StandardMatchTokenMap with
                        | Some flags -> 
    
                            // Found a match.  Yield the Token
                            let token = SnapshotSpan(builderStart.Value, builder.Length), flags
                            builder.Length <- 0
                            Some token
                        | None -> 
    
                            if not (isPrefixMatch current) then 
                                // Token is not complete and we no longer have a prefix match 
                                // against a full token.  Reset our state and interpret the current
                                // point as the start of a new token
                                builder.Length <- 0
                            None
                    else
                        None

                match toYield with
                | Some token -> 
                    // Produced a token from the builder.  Yield it now
                    yield token
                | None ->
                    // No token was produced from the builder.  If the length is 0 now that means we
                    // are in a clean context and should interpret 'current' in that clean context
                    if builder.Length = 0 then
                        match Map.tryFind (current.ToString()) StandardMatchTokenMap with
                        | Some flags -> 
                            // It's a single char complete token so just return it
                            yield (SnapshotSpan(currentPoint, 1), flags)
                        | None ->
                            if Set.contains current startSet then
                                builderStart := currentPoint 
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

        // Is the text in the given Span a match for the original token?
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

            // Searching for a matching end token is straight forward.  Go forward 
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
            // Go from the start of the buffer to the start of the token.  We will keep a stack
            // of open start tokens as we descend down the list of tokens.  As we hit close tokens
            // we will pop off the top of the list.  When we hit the original token the top
            // of the list is the matching token
            let searchSpan = SnapshotSpan(SnapshotUtil.GetStartPoint span.Snapshot, span.Start)
            use e = (GetMatchTokens searchSpan).GetEnumerator()
            let rec inner startTokenList = 
                if e.MoveNext() then
                    let current, _ = e.Current 

                    if isMatch current then 
                        match startTokenList with
                        | [] ->
                            inner [current]
                        | _ -> 
                            // If we have start tokens which nest (like parens) then put the new
                            // start token at the top of the list.  Else don't even record the 
                            // token
                            if startTokenNests then inner (current :: startTokenList)
                            else inner startTokenList
                    elif current.GetText() = text then 
                        // Found another end token.  Pop off the top of the stack if there is
                        // any
                        let startTokenList = 
                            match startTokenList with
                            | [] -> List.empty
                            | _::t -> t
                        inner startTokenList
                    else 
                        // Token is uninteresting.  Just recurse
                        inner startTokenList
                else
                    // Can't move the enumerator anymore so we are at the end token.  The top 
                    // of the list is our matching token
                    ListUtil.tryHeadOnly startTokenList
            inner List.empty

[<RequireQualifiedAccess>]
type SentenceKind = 

    /// Default behavior of a sentence as defined by ':help sentence'
    | Default

    /// There is one definition of a sentence in Vim but the implementation indicates there
    /// are actually 2.  In some cases the trailing characters are not considered a part of 
    /// a sentence.
    ///
    /// http://groups.google.com/group/vim_use/browse_thread/thread/d3f28cf801dc2030
    | NoTrailingCharacters

[<RequireQualifiedAccess>]
type SectionKind =

    /// By default a section break happens on a form feed in the first 
    /// column or one of the nroff macros
    | Default

    /// Split on an open brace in addition to default settings
    | OnOpenBrace

    /// Split on a close brace in addition to default settings
    | OnCloseBrace

    /// Split on an open brace or below a close brace in addition to default
    /// settings
    | OnOpenBraceOrBelowCloseBrace

type internal MotionUtil 
    ( 
        _textView : ITextView,
        _markMap : IMarkMap,
        _localSettings : IVimLocalSettings,
        _search : ISearchService,
        _navigator : ITextStructureNavigator,
        _jumpList : IJumpList, 
        _statusUtil : IStatusUtil,
        _vimData : IVimData
    ) = 

    let _textBuffer = _textView.TextBuffer
    let _globalSettings = _localSettings.GlobalSettings

    /// Set of characters which represent the end of a sentence. 
    static let SentenceEndChars = ['.'; '!'; '?']

    /// Set of characters which can validly follow a sentence 
    static let SentenceTrailingChars = [')';']'; '"'; '\'']

    /// Caret point in the ITextView
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// Caret line in the ITextView
    member x.CaretLine = SnapshotPointUtil.GetContainingLine x.CaretPoint

    /// Current ITextSnapshot for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.SpanAndForwardFromLines (line1:ITextSnapshotLine) (line2:ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.End),true
        else SnapshotSpan(line2.Start, line1.End),false

    /// Apply the 'startofline' option to the given MotionResult
    member x.ApplyStartOfLineOption (motionData : MotionResult) =
        Contract.Assert(match motionData.MotionKind with MotionKind.LineWise _ -> true | _ -> false)
        if not _globalSettings.StartOfLine then 
            motionData 
        else
            let endLine = 
                if motionData.IsForward then SnapshotSpanUtil.GetEndLine motionData.Span
                else SnapshotSpanUtil.GetStartLine motionData.Span
            let point = TssUtil.FindFirstNonWhiteSpaceCharacter endLine
            let column = SnapshotPointUtil.GetColumn point |> CaretColumn.InLastLine
            { motionData with MotionKind = MotionKind.LineWise column }

    /// Get the motion between the provided two lines.  The motion will be linewise
    /// and have a column of the first non-whitespace character.  If the 'startofline'
    /// option is not set it will keep the original column
    member x.LineToLineFirstNonWhiteSpaceMotion (startLine : ITextSnapshotLine) (endLine : ITextSnapshotLine) = 

        // Get the column based on the 'startofline' option
        let column = 
            if _globalSettings.StartOfLine then 
                endLine |> TssUtil.FindFirstNonWhiteSpaceCharacter |> SnapshotPointUtil.GetColumn
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
        {
            Span = range.ExtentIncludingLineBreak
            IsForward = isForward
            MotionKind = MotionKind.LineWise column
            MotionResultFlags = MotionResultFlags.None }

    /// Get the SnapshotSpan values for the paragraph object starting from the given SnapshotPoint
    /// in the specified direction.  
    member x.GetParagraphs path point = 

        // Get the full span of a paragraph given a start point
        let getFullSpanFromStartPoint point =
            Contract.Assert (x.IsParagraphStart point)
            let snapshot = SnapshotPointUtil.GetSnapshot point
            let endPoint =
                point 
                |> SnapshotPointUtil.AddOne
                |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Forward
                |> Seq.skipWhile (fun p -> not (x.IsParagraphStart p))
                |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint snapshot)
            SnapshotSpan(point, endPoint)

        x.GetTextObjectsCore point path x.IsParagraphStart getFullSpanFromStartPoint

    /// Get the SnapshotLineRange values for the section values starting from the given SnapshotPoint 
    /// in the specified direction.  Note: The full span of the section will be returned if the 
    /// provided SnapshotPoint is in the middle of it
    member x.GetSectionRanges sectionKind path point =

        let isNotSectionStart line = not (x.IsSectionStart sectionKind line)

        // Get the ITextSnapshotLine which is the start of the nearest section from the given point.  
        let snapshot = SnapshotPointUtil.GetSnapshot point
        let getSectionStartBackward point = 
            let line = SnapshotPointUtil.GetContainingLine point
            SnapshotUtil.GetLines snapshot line.LineNumber Path.Backward
            |> Seq.skipWhile isNotSectionStart
            |> SeqUtil.headOrDefault (SnapshotUtil.GetFirstLine snapshot)

        // Get the SnapshotLineRange from an ITextSnapshotLine which begins a section
        let getSectionRangeFromStart (startLine: ITextSnapshotLine) =
            Contract.Assert (x.IsSectionStart sectionKind startLine)
            let nextStartLine = 
                SnapshotUtil.GetLines snapshot startLine.LineNumber Path.Forward
                |> SeqUtil.skipMax 1
                |> Seq.skipWhile isNotSectionStart
                |> SeqUtil.tryHeadOnly

            let count = 
                match nextStartLine with 
                | None -> (SnapshotUtil.GetLastLineNumber snapshot) - startLine.LineNumber + 1
                | Some nextStartLine -> nextStartLine.LineNumber - startLine.LineNumber

            SnapshotLineRangeUtil.CreateForLineAndCount startLine count |> Option.get

        match path with
        | Path.Forward ->
            // Start the search from the nearest section start backward
            let startLine = getSectionStartBackward point

            Seq.unfold (fun point -> 
                if SnapshotPointUtil.IsEndPoint point then
                    // Once we hit <end> we are done
                    None
                else
                    let startLine = SnapshotPointUtil.GetContainingLine point
                    let range = getSectionRangeFromStart startLine
                    Some (range, range.EndIncludingLineBreak)) startLine.Start
        | Path.Backward ->

            // Create the sequence by doing an unfold.  The provided point will be the 
            // start point of the following section SnapshotLineRange
            let getPrevious point =
                match SnapshotPointUtil.TrySubtractOne point with
                | None -> 
                    // Once we are at start we are done
                    None
                | Some point ->
                    let endLine = SnapshotPointUtil.GetContainingLine point
                    let startLine = getSectionStartBackward point
                    let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
                    Some (range, range.Start)

            // There is a bit of a special case with backward sections.  If the point is 
            // anywhere on the line of a section start then really it starts from the start
            // of the line
            let point = 
                let line = SnapshotPointUtil.GetContainingLine point
                if x.IsSectionStart sectionKind line then
                    line.Start
                else 
                    point

            let startLine = getSectionStartBackward point
            let sections = Seq.unfold getPrevious startLine.Start

            if startLine.Start = point then
                // The provided SnapshotPoint is the first point on a section so we don't include
                // it and the sections sequence as stands is correct
                sections
            else 
                // Need to include the section which spans this point 
                let added = startLine |> getSectionRangeFromStart |> Seq.singleton
                Seq.append added sections

    /// Get the SnapshotSpan values for the section values starting from the given SnapshotPoint 
    /// in the specified direction.  Note: The full span of the section will be returned if the 
    /// provided SnapshotPoint is in the middle of it
    member x.GetSections sectionKind path point =
        x.GetSectionRanges sectionKind path point
        |> Seq.map (fun range -> range.ExtentIncludingLineBreak)

    /// Get the SnapshotSpan values for the sentence values starting from the given SnapshotPoint 
    /// in the specified direction.  Note: The full span of the section will be returned if the 
    /// provided SnapshotPoint is in the middle of it
    member x.GetSentences sentenceKind path point = 

        // Get the full span of a sentence given the particular start point
        let getFullSpanFromStartPoint point = 
            Contract.Assert (x.IsSentenceStart sentenceKind point)

            let snapshot = SnapshotPointUtil.GetSnapshot point

            // Move forward until we hit the end point and then move one past it so the end
            // point is included in the span
            let endPoint = 
                point
                |> SnapshotPointUtil.AddOne
                |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Forward
                |> Seq.skipWhile (fun p -> not (x.IsSentenceEnd sentenceKind p))
                |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint snapshot)
            SnapshotSpan(point, endPoint)

        x.GetTextObjectsCore point path (x.IsSentenceStart sentenceKind) getFullSpanFromStartPoint

    /// Get the text objects core from the given point in the given direction
    member x.GetTextObjectsCore point path isStartPoint getSpanFromStartPoint = 

        let snapshot = SnapshotPointUtil.GetSnapshot point
        let isNotStartPoint point = not (isStartPoint point)

        // Wrap the get full span method to deal with <end>.  
        let getSpanFromStartPoint point = 
            if SnapshotPointUtil.IsEndPoint point then
                SnapshotSpan(point, 0)
            else
                getSpanFromStartPoint point

        // Get the section start going backwards from the given point.
        let getStartBackward point = 
            point 
            |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Backward
            |> Seq.skipWhile isNotStartPoint
            |> SeqUtil.headOrDefault (SnapshotPoint(snapshot, 0))

        // Get the section start going forward from the given point 
        let getStartForward point = 
            point
            |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Forward
            |> Seq.skipWhile isNotStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint snapshot)

        // Search includes the section which contains the start point so go ahead and get it
        match path, SnapshotPointUtil.IsStartPoint point with 
        | Path.Forward, _ ->

            // Get the next object.  The provided point should either be <end> or point 
            // to the start of a section
            let getNext point =
                if SnapshotPointUtil.IsEndPoint point then
                    None
                else
                    let span = getSpanFromStartPoint point
                    let startPoint = getStartForward span.End
                    Some (span, startPoint)

            // Get the point to start the sequence from.  Need to be very careful though 
            // because it's possible for the first SnapshotSpan being completely before the 
            // provided SnapshotPoint.  This can happen for text objects which have white 
            // space chunks and the provided point 
            let startPoint = 
                let startPoint = getStartBackward point
                let span = getSpanFromStartPoint startPoint
                if point.Position >= span.End.Position then
                    getStartForward span.End
                else
                    startPoint
            Seq.unfold getNext startPoint
        | Path.Backward, true ->
            // Handle the special case here
            Seq.empty
        | Path.Backward, false ->

            // Get the previous section.  The provided point should either be 0 or point
            // to the start of a section
            let getPrevious point = 
                if SnapshotPointUtil.IsStartPoint point then
                    None
                else
                    let startPoint = point |> SnapshotPointUtil.SubtractOne |> getStartBackward
                    let span = getSpanFromStartPoint startPoint
                    Some (span, startPoint)

            Seq.unfold getPrevious point

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    ///
    /// TODO: We need to account for folded regions here in the future
    member x.GetWords kind path point = 

        let snapshot = SnapshotPointUtil.GetSnapshot point
        let line = SnapshotPointUtil.GetContainingLine point
        SnapshotUtil.GetLines snapshot line.LineNumber path
        |> Seq.map (fun line ->
            if line.Length = 0 then
                // Blank lines are words.  The word covers the entire line including the line
                // break.  This can be verified by 'yw' on a blank line and pasting.  It will
                // create a new line
                line.ExtentIncludingLineBreak |> Seq.singleton
            else 
                let offset = line.Start.Position
                line.Extent
                |> SnapshotSpanUtil.GetText 
                |> TextUtil.GetWordSpans kind path 
                |> Seq.map (fun span -> SnapshotSpan(snapshot, span.Start + offset, span.Length)))
        |> Seq.concat
        |> Seq.filter (fun span -> 
            // Need to filter off items from the first line.  The point can and will often be
            // in the middle of a line and we can't return any spans which are past / before 
            // the point depending on the direction
            match path with
            | Path.Forward -> span.End.Position > point.Position
            | Path.Backward -> span.Start.Position < point.Position)

    /// Is this line a blank line with no blank lines above it 
    member x.IsBlankLineWithNoBlankAbove line = 
        if 0 = SnapshotLineUtil.GetLength line then
            let number = line.LineNumber - 1
            match SnapshotUtil.TryGetLine line.Snapshot number with
            | None -> true
            | Some line -> line.Length <> 0
        else
            false

    /// Is this point the start of a paragraph.  Considers both paragraph and section 
    /// start points
    member x.IsParagraphStart point =
        let line = SnapshotPointUtil.GetContainingLine point
        if line.Start = point then
            x.IsParagraphStartOnly line || x.IsSectionStart SectionKind.Default line
        else
            false

    /// Is this point the start of a paragraph.  This does not consider section starts but
    /// specifically items related to paragraphs.  Paragraphs begin after a blank line or
    /// at one of the specified macros
    member x.IsParagraphStartOnly line = 
        let startPoint = SnapshotLineUtil.GetStart line

        SnapshotPointUtil.IsStartPoint startPoint ||
        x.IsTextMacroMatchLine line _globalSettings.Paragraphs ||
        x.IsBlankLineWithNoBlankAbove line

    /// Is this line the start of a section.  Section boundaries can only occur at the
    /// start of a line or in a couple of scenarios around braces
    member x.IsSectionStart kind line = 

        // Is this a standard section start
        let startPoint = SnapshotLineUtil.GetStart line
        let isStandardStart = 
            if SnapshotPointUtil.IsStartPoint startPoint then
                true
            elif SnapshotPointUtil.IsChar '\f' startPoint then
                true
            else
                x.IsTextMacroMatchLine line _globalSettings.Sections

        if isStandardStart then
            true
        else
            match kind with
            | SectionKind.Default ->
                false
            | SectionKind.OnOpenBrace -> 
                SnapshotPointUtil.IsChar '{' startPoint
            | SectionKind.OnCloseBrace -> 
                SnapshotPointUtil.IsChar '}' startPoint
            | SectionKind.OnOpenBraceOrBelowCloseBrace -> 
                if SnapshotPointUtil.IsChar '{' startPoint then
                    true
                else
                    match SnapshotUtil.TryGetLine line.Snapshot (line.LineNumber - 1) with
                    | None -> false
                    | Some line -> SnapshotPointUtil.IsChar '}' line.Start

    /// Is this the end point of the span of an actual sentence.  Considers sentence, paragraph and 
    /// section semantics
    member x.IsSentenceEnd sentenceKind point = 

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

        // Is this a valid white space item to end the sentence
        let isWhiteSpaceEnd point = 
            SnapshotPointUtil.IsWhiteSpace point || 
            SnapshotPointUtil.IsInsideLineBreak point ||
            SnapshotPointUtil.IsEndPoint point

        // Is it SnapshotPoint pointing to an end char which actually ends
        // the sentence
        let isEndNoTrailing point = 
            if isCharInList SentenceEndChars point then
                point |> SnapshotPointUtil.AddOne |> isWhiteSpaceEnd
            else
                false

        // Is it a trailing character which properly ends a sentence
        let isTrailing point = 
            if isCharInList SentenceTrailingChars point then

                // Need to see if we are preceded by an end character first
                let isPrecededByEnd = 
                    if SnapshotPointUtil.IsStartPoint point then 
                        false
                    else
                        point 
                        |> SnapshotPointUtil.SubtractOne
                        |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Backward
                        |> Seq.skipWhile (isCharInList SentenceTrailingChars)
                        |> SeqUtil.tryHeadOnly
                        |> Option.map (isCharInList SentenceEndChars)
                        |> OptionUtil.getOrDefault false

                if isPrecededByEnd then
                    point |> SnapshotPointUtil.AddOne |> isWhiteSpaceEnd
                else
                    false
            else
                false

        /// The line containing the SnapshotPoint
        let line = SnapshotPointUtil.GetContainingLine point

        /// Is this line a sentence line?  A sentence line is a sentence which is caused by
        /// a paragraph, section boundary or blank line
        /// Is this point the start of a sentence line?  A sentence line is a sentence which is 
        /// caused by a paragraph, section boundary or a blank line.
        let isSentenceLine line =
            x.IsTextMacroMatchLine line _globalSettings.Paragraphs ||
            x.IsTextMacroMatchLine line _globalSettings.Sections ||
            x.IsBlankLineWithNoBlankAbove line

        // Is this the last point on a sentence line?  The last point on the sentence line is the 
        // final character of the line break.  This is key to understand: the line break on a sentence
        // line is not considered white space!!!  This can be verified by placing the caret on the 'b' in 
        // the following and doing a 'yas'
        //
        //  a
        //  
        //  b
        let isSentenceLineLast point =
            let line = SnapshotPointUtil.GetContainingLine point
            isSentenceLine line && SnapshotLineUtil.IsLastPointIncludingLineBreak line point

        if isSentenceLine line && line.Start = point then
            // If this point is the start of a sentence line then it's the end point of the
            // previous sentence span. 
            true
        else
            match SnapshotPointUtil.TrySubtractOne point with
            | None -> 
                // Start of buffer is not the end of a sentence
                false 
            | Some point -> 
                match sentenceKind with
                | SentenceKind.Default -> isEndNoTrailing point || isTrailing point || isSentenceLineLast point
                | SentenceKind.NoTrailingCharacters -> isEndNoTrailing point || isSentenceLineLast point

    /// Is this point the star of a sentence.  Considers sentences, paragraphs and section
    /// boundaries
    member x.IsSentenceStart sentenceKind point = 
        if x.IsSentenceStartOnly sentenceKind point then
            true
        else
            let line = SnapshotPointUtil.GetContainingLine point
            if line.Start = point then
                x.IsParagraphStartOnly line || x.IsSectionStart SectionKind.Default line
            else
                false

    /// Is the start of a sentence.  This doesn't consider section or paragraph boundaries
    /// but specifically items related to the start of a sentence
    member x.IsSentenceStartOnly sentenceKind point = 

        let snapshot = SnapshotPointUtil.GetSnapshot point

        // Get the point after the last sentence
        let priorEndPoint = 
            point
            |> SnapshotPointUtil.GetPointsIncludingLineBreak Path.Backward
            |> Seq.filter (x.IsSentenceEnd sentenceKind)
            |> SeqUtil.tryHeadOnly

        match priorEndPoint with 
        | None -> 
            // No prior end point so the start of this sentence is the start of the 
            // ITextBuffer
            SnapshotPointUtil.IsStartPoint point 
        | Some priorEndPoint -> 
            // Move past the white space until we get the start point.  Don't need 
            // to consider line breaks because we are only dealing with sentence 
            // specific items.  Methods like IsSentenceStart deal with line breaks
            // by including paragraphs
            let startPoint =
                priorEndPoint 
                |> SnapshotPointUtil.GetPoints Path.Forward
                |> Seq.skipWhile SnapshotPointUtil.IsWhiteSpaceOrInsideLineBreak
                |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

            startPoint.Position = point.Position
    
    /// This function is used to match nroff macros for both section and paragraph sections.  It 
    /// determines if the line starts with the proper macro string
    member x.IsTextMacroMatch point macroString =
        let line = SnapshotPointUtil.GetContainingLine point
        if line.Start <> point then
            // Match can only occur at the start of the line
            false
        else
            x.IsTextMacroMatchLine line macroString

    /// This function is used to match nroff macros for both section and paragraph sections.  It 
    /// determines if the line starts with the proper macro string
    member x.IsTextMacroMatchLine (line : ITextSnapshotLine) macroString =
        let isLengthCorrect = 0 = (String.length macroString) % 2
        if not isLengthCorrect then 
            // Match can only occur with a valid macro string length
            false
        elif line.Length = 0 || line.Start.GetChar() <> '.' then
            // Line must start with a '.' 
            false
        elif line.Length = 2 then
            // Line is in the form '.H' so can only match pairs which have a blank 
            let c = line.Start |> SnapshotPointUtil.AddOne |> SnapshotPointUtil.GetChar
            { 0 .. ((String.length macroString) - 1)}
            |> Seq.filter (fun i -> i % 2 = 0)
            |> Seq.exists (fun i -> macroString.[i] = c && macroString.[i + 1] = ' ')
        elif line.Length = 3 then
            // Line is in the form '.HH' so match both items
            let c1 = line.Start |> SnapshotPointUtil.AddOne |> SnapshotPointUtil.GetChar
            let c2 = line.Start |> SnapshotPointUtil.Add 2 |> SnapshotPointUtil.GetChar
            { 0 .. ((String.length macroString) - 1)}
            |> Seq.filter (fun i -> i % 2 = 0)
            |> Seq.exists (fun i -> macroString.[i] = c1 && macroString.[i + 1] = c2)
        else
            // Line can't match
            false

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
        | Some virtualPoint ->

            // Found the motion so update the jump list
            _jumpList.Add x.CaretPoint

            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint virtualPoint.Position
            let column = SnapshotPointUtil.GetColumn virtualPoint.Position
            let span = SnapshotSpan(startPoint, endPoint)
            {
                Span = span
                IsForward = caretPoint = startPoint
                MotionKind = MotionKind.CharacterWiseExclusive
                MotionResultFlags = MotionResultFlags.None } |> Some

    member x.MarkLine c =
        match _markMap.GetLocalMark _textBuffer c with 
        | None -> 
            None
        | Some virtualPoint ->

            // Found the motion so update the jump list
            _jumpList.Add x.CaretPoint

            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint virtualPoint.Position
            let startLine = SnapshotPointUtil.GetContainingLine startPoint
            let endLine = SnapshotPointUtil.GetContainingLine endPoint
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            {
                Span = range.ExtentIncludingLineBreak
                IsForward = x.CaretPoint = startPoint
                MotionKind =
                    virtualPoint.Position
                    |> SnapshotPointUtil.GetContainingLine
                    |> TssUtil.FindFirstNonWhiteSpaceCharacter
                    |> SnapshotPointUtil.GetColumn
                    |> CaretColumn.InLastLine
                    |> MotionKind.LineWise
                MotionResultFlags = MotionResultFlags.None } |> Some

    /// Find the matching token for the next token on the current line 
    member x.MatchingToken() = 
        // First find the next token on this line from the caret point
        let caretPoint, caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let tokens = MotionUtilLegacy.GetMatchTokens (SnapshotSpan(caretPoint, caretLine.End))
        match SeqUtil.tryHeadOnly tokens with
        | None -> 
            // No tokens on the line 
            None
        | Some (token, flags) -> 
            // Now lets look for the matching token 
            match MotionUtilLegacy.FindMatchingToken token flags with
            | None ->
                // No matching token so once again no motion data
                None
            | Some otherToken ->

                // Search succeeded so update the jump list before moving
                _jumpList.Add x.CaretPoint

                // Nice now order the tokens appropriately to get the span 
                let span, isForward = 
                    if caretPoint.Position < otherToken.Start.Position then
                        SnapshotSpan(caretPoint, otherToken.End), true
                    else
                        SnapshotSpan(otherToken.Start, caretPoint.Add(1)), false
                {
                    Span = span
                    IsForward = isForward
                    MotionKind = MotionKind.CharacterWiseInclusive
                    MotionResultFlags = MotionResultFlags.None } |> Some

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
                {
                    Span = span 
                    IsForward = true 
                    MotionKind = MotionKind.CharacterWiseExclusive
                    MotionResultFlags = MotionResultFlags.None } |> Some
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

        let kind = SentenceKind.NoTrailingCharacters
        let all = x.GetSentences kind Path.Forward x.CaretPoint |> Seq.truncate count |> List.ofSeq
        let span = 
            match all with
            | [] ->
                // Corner case where there are simply no objects going forward.  Return
                // an empty span
                SnapshotSpan(x.CaretPoint, 0)
            | head :: _ ->

                let span = 
                    let last = List.nth all (all.Length - 1)
                    SnapshotSpan(head.Start, last.End)

                // The 'as' motion considers anything between the SnapshotSpan of a sentence to
                // be white space.  So the caret is in white space if it occurs before the sentence
                let isCaretInWhiteSpace = x.CaretPoint.Position < span.Start.Position

                // The white space after the sentence is the gap between this sentence and the next
                // sentence
                let whiteSpaceAfter =
                    match x.GetSentences kind Path.Forward span.End |> SeqUtil.tryHeadOnly with
                    | None -> None
                    | Some nextSpan -> if span.End.Position < nextSpan.Start.Position then Some (SnapshotSpan(span.End, nextSpan.Start)) else None

                // Include the preceding white space in the Span
                let includePrecedingWhiteSpace () =
                    let startPoint = 
                        span.Start 
                        |> x.GetSentences kind Path.Backward
                        |> Seq.map SnapshotSpanUtil.GetEndPoint
                        |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
                    SnapshotSpan(startPoint, span.End)

                // Now we need to do the standard adjustments listed at the bottom of 
                // ':help text-objects'.
                match isCaretInWhiteSpace, whiteSpaceAfter with
                | true, _ -> includePrecedingWhiteSpace()
                | false, None -> includePrecedingWhiteSpace()
                | false, Some spaceSpan-> SnapshotSpan(span.Start, spaceSpan.End)


        {
            Span = span 
            IsForward = true 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Implements the 'aw' motion.  The 'aw' motion is limited to the current line and won't ever
    /// extend above or below it.
    member x.AllWord kind count = 

        // Is this word span on the same line as the caret?  A word won't ever span multiple lines
        // so we can be content with checking the start point
        let isOnSameLine span =
            let line = span |> SnapshotSpanUtil.GetStartPoint |> SnapshotPointUtil.GetContainingLine
            line.LineNumber = x.CaretLine.LineNumber

        // Get all of the words on this line going forward
        let all = 
            x.GetWords kind Path.Forward x.CaretPoint
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
                    |> Seq.filter (fun point -> point.Position <= x.CaretLine.End.Position)
                    |> Seq.skipWhile SnapshotPointUtil.IsWhiteSpace
                    |> SeqUtil.headOrDefault x.CaretLine.End
                let span = SnapshotSpan(span.End, endPoint)
                if span.Length > 0 then Some span else None

            let isCaretInWhiteSpace = SnapshotPointUtil.IsWhiteSpace x.CaretPoint


            // Now do the standard adjustments listed at the bottom of ':help text-objects'
            let span = 
                match isCaretInWhiteSpace, whiteSpaceAfter with
                | true , _ ->
                    // If the caret is in white space then we include all of the white space before
                    // up until the start of the line or the next non-white space character.  
                    let startPoint = 
                        span.Start
                        |> SnapshotPointUtil.GetPoints Path.Backward
                        |> SeqUtil.skipMax 1
                        |> Seq.filter (fun point -> point.Position >= x.CaretLine.Start.Position)
                        |> Seq.skipWhile (fun point -> 
                            match SnapshotPointUtil.TrySubtractOne point with
                            | None -> false
                            | Some point -> SnapshotPointUtil.IsWhiteSpace point)
                        |> SeqUtil.headOrDefault x.CaretLine.Start
                    SnapshotSpan(startPoint, span.End)

                | false, None ->
                    // There are different rules here that when the caret is in white space even 
                    // though they are not called out in the documentation.  We should only include
                    // white space here if there is a word on the same line before this one.  
                    let startPoint = 
                        x.GetWords kind Path.Backward span.Start
                        |> Seq.filter isOnSameLine
                        |> Seq.map SnapshotSpanUtil.GetEndPoint
                        |> SeqUtil.headOrDefault span.Start
                    SnapshotSpan(startPoint, span.End)

                | false, Some spaceSpan -> 
                    SnapshotSpan(span.Start, spaceSpan.End)

            {
                Span = span 
                IsForward = true 
                MotionKind = MotionKind.CharacterWiseExclusive
                MotionResultFlags = MotionResultFlags.None } |> Some

    member x.BeginingOfLine() = 
        let start = x.CaretPoint
        let line = SnapshotPointUtil.GetContainingLine start
        let span = SnapshotSpan(line.Start, start)
        {
            Span = span 
            IsForward = false 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Search for the specified char in the given direction.
    member x.CharSearch c count charSearch direction = 
        // Save the last search value
        _vimData.LastCharSearch <- Some (charSearch, direction, c)

        x.CharSearchCore c count charSearch direction

    /// Do the actual char search motion but don't update the 'LastCharSearch' value
    member x.CharSearchCore c count charSearch direction = 

        let forward () = 
            let start = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
            SnapshotSpan(start, x.CaretLine.End)
            |> SnapshotSpanUtil.GetPoints Path.Forward
            |> Seq.filter (SnapshotPointUtil.IsChar c)
            |> SeqUtil.skipMax (count - 1)
            |> SeqUtil.tryHeadOnly

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
        | None ->
            None
        | Some (span, motionKind) ->
            {
                Span = span;
                IsForward = match direction with | Path.Forward -> true | Path.Backward -> false
                MotionKind = motionKind
                MotionResultFlags = MotionResultFlags.None } |> Some

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

    member x.WordForward kind count =
        let start = x.CaretPoint
        let endPoint = TssUtil.FindNextWordStart start count kind  
        let span = SnapshotSpan(start,endPoint)
        {
            Span = span 
            IsForward = true 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.AnyWord }

    member x.WordBackward kind count =
        let start = x.CaretPoint
        let startPoint = TssUtil.FindPreviousWordStart start count kind
        let span = SnapshotSpan(startPoint,start)
        {
            Span = span 
            IsForward = false 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.AnyWord }

    /// Implements the 'e' and 'E' motions
    member x.EndOfWord kind count = 

        // The start point for an end of word motion is a little odd.  If the caret is 
        // on the last point inside a word then we calculate the next word starting at
        // the end of the first word.
        let searchPoint = 
            match x.GetWords kind Path.Forward x.CaretPoint |> SeqUtil.tryHeadOnly with
            | None -> 
                x.CaretPoint
            | Some span -> 
                if SnapshotSpanUtil.IsLastIncludedPoint span x.CaretPoint then
                    span.End
                else
                    x.CaretPoint

        let endPoint = 
            searchPoint
            |> x.GetWords kind Path.Forward
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
        let column = endPoint |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine |> Some
        { 
            Span = span
            IsForward = true
            MotionKind = MotionKind.CharacterWiseInclusive
            MotionResultFlags = MotionResultFlags.None }

    member x.EndOfLine count = 
        let start = x.CaretPoint
        let span = SnapshotPointUtil.GetLineRangeSpan start count
        {
            Span = span 
            IsForward = true 
            MotionKind = MotionKind.CharacterWiseInclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Find the first non-whitespace character on the current line.  
    member x.FirstNonWhiteSpaceOnCurrentLine () =
        let start = x.CaretPoint
        let line = start.GetContainingLine()
        let target = 
            SnapshotLineUtil.GetPoints Path.Forward line
            |> Seq.tryFind (fun x -> not (CharUtil.IsWhiteSpace (x.GetChar())) )
            |> OptionUtil.getOrDefault line.End
        let startPoint,endPoint,isForward = 
            if start.Position <= target.Position then start,target,true
            else target,start,false
        let span = SnapshotSpanUtil.CreateFromBounds startPoint endPoint
        {
            Span = span 
            IsForward = isForward 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Create a line wise motion from the current line to (count - 1) lines
    /// downward 
    member x.FirstNonWhiteSpaceOnLine count = 
        let startLine = x.CaretLine
        let endLine = 
            let number = startLine.LineNumber + (count - 1)
            SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
        let column = TssUtil.FindFirstNonWhiteSpaceCharacter endLine |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
        let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
        {
            Span = range.ExtentIncludingLineBreak
            IsForward = true
            MotionKind = MotionKind.LineWise column
            MotionResultFlags = MotionResultFlags.None }

    member x.LineDownToFirstNonWhiteSpace count =
        let line = x.CaretPoint |> SnapshotPointUtil.GetContainingLine
        let number = line.LineNumber + count
        let endLine = SnapshotUtil.GetLineOrLast line.Snapshot number
        let column = TssUtil.FindFirstNonWhiteSpaceCharacter endLine |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
        let span = SnapshotSpan(line.Start, endLine.EndIncludingLineBreak)
        {
            Span = span 
            IsForward = true 
            MotionKind = MotionKind.LineWise column
            MotionResultFlags = MotionResultFlags.None }

    member x.LineUpToFirstNonWhiteSpace count =
        let point = x.CaretPoint
        let endLine = SnapshotPointUtil.GetContainingLine point
        let startLine = SnapshotUtil.GetLineOrFirst endLine.Snapshot (endLine.LineNumber - count)
        let span = SnapshotSpan(startLine.Start, endLine.End)
        let column = 
            startLine 
            |> TssUtil.FindFirstNonWhiteSpaceCharacter
            |> SnapshotPointUtil.GetColumn
            |> CaretColumn.InLastLine
        {
            Span = span 
            IsForward = false 
            MotionKind = MotionKind.LineWise column
            MotionResultFlags = MotionResultFlags.None }

    /// Get the motion which is 'count' characters to the left of the caret on
    /// the same line
    member x.CharLeft count = 
        let startPoint = 
            SnapshotPointUtil.TryGetPreviousPointOnLine x.CaretPoint count
            |> OptionUtil.getOrDefault x.CaretLine.Start
        {
            Span = SnapshotSpan(startPoint, x.CaretPoint)
            IsForward = false 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Get the motion which is 'count' characetrs to the right of the caret 
    /// on the same line
    member x.CharRight count =
        let endPoint = 
            if SnapshotPointUtil.IsInsideLineBreak x.CaretPoint then 
                x.CaretPoint
            elif x.CaretPoint.Position + 1 = x.CaretLine.End.Position then
                x.CaretLine.End
            else
                SnapshotPointUtil.TryGetNextPointOnLine x.CaretPoint count 
                |> OptionUtil.getOrDefault x.CaretLine.End
        {
            Span = SnapshotSpan(x.CaretPoint, endPoint)
            IsForward = true 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Move a single line up from the current line.  Should fail if we are currenly
    /// on the first line of the ITextBuffer
    member x.LineUp count =
        if x.CaretLine.LineNumber = 0 then
            None
        else
            let startLine = SnapshotUtil.GetLineOrFirst x.CurrentSnapshot (x.CaretLine.LineNumber - count)
            let span = SnapshotSpan(startLine.Start, x.CaretLine.EndIncludingLineBreak)
            let column = x.CaretPoint |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
            {
                Span = span 
                IsForward = false 
                MotionKind = MotionKind.LineWise column
                MotionResultFlags = MotionResultFlags.None } |> Some

    /// Move a single line down from the current line.  Should fail if we are currenly 
    /// on the last line of the ITextBuffer
    member x.LineDown count = 
        if x.CaretLine.LineNumber = SnapshotUtil.GetLastLineNumber x.CurrentSnapshot then
            None
        else
            let endLine = SnapshotUtil.GetLineOrLast x.CurrentSnapshot (x.CaretLine.LineNumber + count)
            let span = SnapshotSpan(x.CaretLine.Start, endLine.EndIncludingLineBreak)
            let column = x.CaretPoint |> SnapshotPointUtil.GetColumn |> CaretColumn.InLastLine
            {
                Span = span 
                IsForward = true 
                MotionKind = MotionKind.LineWise column
                MotionResultFlags = MotionResultFlags.None } |> Some

    member x.LineOrFirstToFirstNonWhiteSpace numberOpt = 
        let point = x.CaretPoint
        let originLine = SnapshotPointUtil.GetContainingLine point
        let tss= originLine.Snapshot
        let endLine = 
            match numberOpt with
            | Some(number) ->  SnapshotUtil.GetLineOrFirst tss (TssUtil.VimLineToTssLine number)
            | None -> SnapshotUtil.GetFirstLine tss 
        x.LineToLineFirstNonWhiteSpaceMotion originLine endLine

    /// Implements the 'G" motion
    member x.LineOrLastToFirstNonWhiteSpace numberOpt = 
        _jumpList.Add x.CaretPoint

        let point = x.CaretPoint
        let originLine = SnapshotPointUtil.GetContainingLine point
        let tss= originLine.Snapshot
        let endLine = 
            match numberOpt with
            | Some number ->  SnapshotUtil.GetLineOrLast tss (TssUtil.VimLineToTssLine number)
            | None -> SnapshotUtil.GetLastLine tss 
        x.LineToLineFirstNonWhiteSpaceMotion originLine endLine

    member x.LastNonWhiteSpaceOnLine count = 
        let start = x.CaretPoint
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
            MotionKind = MotionKind.CharacterWiseInclusive
            MotionResultFlags = MotionResultFlags.None }

    member x.LineFromTopOfVisibleWindow countOpt = 
        _jumpList.Add x.CaretPoint 

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
            MotionKind = MotionKind.LineWise CaretColumn.None
            MotionResultFlags = MotionResultFlags.None } |> x.ApplyStartOfLineOption

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
        {
            Span = span 
            IsForward = isForward 
            MotionKind = MotionKind.LineWise CaretColumn.None
            MotionResultFlags = MotionResultFlags.None } |> x.ApplyStartOfLineOption

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
        {
            Span = span 
            IsForward = isForward 
            MotionKind = MotionKind.LineWise CaretColumn.None
            MotionResultFlags = MotionResultFlags.None } |> x.ApplyStartOfLineOption

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
        {
            Span = span 
            IsForward = false 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Implements the ']]' operator
    member x.SectionForward context count = 
        let split = 
            match context with
            | MotionContext.AfterOperator -> SectionKind.OnOpenBraceOrBelowCloseBrace
            | MotionContext.Movement -> SectionKind.OnOpenBrace
        x.SectionForwardCore split count

    /// Implements the core parts of section forward operators
    member x.SectionForwardCore sectionKind count =
        _jumpList.Add x.CaretPoint

        let endPoint = 
            x.CaretPoint
            |> x.GetSections sectionKind Path.Forward
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

        let span = SnapshotSpan(x.CaretPoint, endPoint)
        {
            Span = span 
            IsForward = true 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Implements the '][' motion
    member x.SectionForwardOrCloseBrace count =
        x.SectionForwardCore SectionKind.OnCloseBrace count

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
        {
            Span = span 
            IsForward = true 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    member x.SentenceBackward count = 
        _jumpList.Add x.CaretPoint
        let startPoint = 
            x.GetSentences SentenceKind.Default Path.Backward x.CaretPoint
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        {
            Span = span 
            IsForward = false 
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Implements the '}' motion
    member x.ParagraphForward count = 
        _jumpList.Add x.CaretPoint

        let endPoint = 
            x.GetParagraphs Path.Forward x.CaretPoint
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        {
            Span = span 
            IsForward = true
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

    /// Implements the '{' motion
    member x.ParagraphBackward count = 
        _jumpList.Add x.CaretPoint

        let startPoint = 
            x.GetParagraphs Path.Backward x.CaretPoint
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        {
            Span = span 
            IsForward = false
            MotionKind = MotionKind.CharacterWiseExclusive
            MotionResultFlags = MotionResultFlags.None }

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
                MotionKind = MotionKind.CharacterWiseInclusive
                MotionResultFlags = MotionResultFlags.None } |> Some

    member x.QuotedStringContents () = 
        match x.GetQuotedStringData() with
        | None -> None 
        | Some(data) ->
            let span = data.Contents
            {
                Span = span 
                IsForward = true 
                MotionKind = MotionKind.CharacterWiseInclusive
                MotionResultFlags = MotionResultFlags.None } |> Some

    /// Get the motion for a search command.  Used to implement the '/' and '?' motions
    member x.Search (patternData : PatternData) count = 

        // Searching as part of a motion should update the last pattern information
        // irrespective of whether or not the search completes
        _vimData.LastPatternData <- patternData

        x.SearchCore patternData x.CaretPoint count

    /// Implements the core searching motion.  Will *not* update the LastPatternData 
    /// value.  Simply performs the search and returns the result
    member x.SearchCore (patternData : PatternData) searchPoint count =

        // All search operations update the jump list
        _jumpList.Add x.CaretPoint

        // The search operation should also update the search history
        _vimData.SearchHistory.Add patternData.Pattern

        let searchResult = _search.FindNextPattern patternData searchPoint _navigator count
        match searchResult with
        | SearchResult.NotFound (searchData, isOutsidePath) ->

            // If the search is not found we need to raise the appropriate error
            // message here.  Warning messages are only produced if the caret
            // is actually moved (done elsewhere)
            let format = 
                if isOutsidePath then
                    match searchData.Kind.Path with
                    | Path.Forward -> Resources.Common_SearchHitBottomWithout
                    | Path.Backward -> Resources.Common_SearchHitTopWithout 
                else
                    Resources.Common_PatternNotFound

            _statusUtil.OnError (format searchData.Pattern)
            None

        | SearchResult.Found (_, span, _) ->

            // Create the MotionResult for the provided MotionArgument and the 
            // start and end points of the search.  Need to be careful because
            // the start and end point can be forward or reverse
            //
            // Even though the search doesn't necessarily start from the caret
            // point the resulting Span begins / ends on it
            let caretPoint = x.CaretPoint
            let endPoint = span.Start
            if caretPoint.Position = endPoint.Position then
                None
            else if caretPoint.Position < endPoint.Position then 
                {
                    Span = SnapshotSpan(caretPoint, endPoint)
                    IsForward = true
                    MotionKind = MotionKind.CharacterWiseExclusive
                    MotionResultFlags = MotionResultFlags.AnySearch } |> Some
            else 
                {
                    Span = SnapshotSpan(endPoint, caretPoint)
                    IsForward = false
                    MotionKind = MotionKind.CharacterWiseExclusive
                    MotionResultFlags = MotionResultFlags.AnySearch } |> Some

    /// Move the caret to the next occurrence of the last search
    member x.LastSearch isReverse count =
        let last = _vimData.LastPatternData
        let last = 
            if isReverse then { last with Path = Path.Reverse last.Path }
            else last

        if StringUtil.isNullOrEmpty last.Pattern then
            _statusUtil.OnError Resources.NormalMode_NoPreviousSearch
            None
        else
            x.SearchCore last x.CaretPoint count

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

        // Move forward along the line to find the first non-blank
        let point =
            x.CaretPoint
            |> SnapshotPointUtil.GetPointsOnContainingLineFrom
            |> Seq.filter (fun p -> not (SnapshotPointUtil.IsWhiteSpace p))
            |> SeqUtil.tryHeadOnly
            |> OptionUtil.getOrDefault x.CaretPoint

        match TssUtil.FindCurrentFullWordSpan point WordKind.NormalWord with
        | None -> 
            // Nothing to do if no word under the cursor
            _statusUtil.OnError Resources.NormalMode_NoWordUnderCursor
            None
        | Some span ->

            // Build up the SearchData structure
            let word = span.GetText()
            let pattern = if isWholeWord then PatternUtil.CreateWholeWord word else word
            let patternData = { Pattern = pattern; Path = path }

            // Make sure to update the LastPatternData here.  It needs to be done 
            // whether or not the search actually succeeds
            _vimData.LastPatternData <- patternData

            // A word search always starts at the beginning of the word.  The pattern
            // based search will ensure that we don't match this word again because it
            // won't make an initial match at the provided point
            x.SearchCore patternData span.Start count

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
    ///
    /// TODO: Should assert if a column is given for a character wise motion
    member x.AdjustMotionResult motion (motionResult : MotionResult) =

        // There are certain motions to which we cannot apply the 'exclusive-linewise'
        // adjustment.  They are not listed in the documentation (but it does note there
        // are exceptions to the exception).  Experimentation has shown though that 
        // it's the following
        let allowExclusiveLineWise = 
            match motion with
            | Motion.AllWord _ -> false
            | Motion.WordBackward _ -> false
            | Motion.WordForward _ -> false
            | _ -> true

        // Do the actual adjustment for exclusive motions.  The kind which should be 
        // added to the adjusted motion is provided
        let adjust () = 

            // Intentionally look at the line containing the End here as we
            // want to look to see if this is in column 0 (Vim calls it column 1). 
            let span = motionResult.Span
            let startLine = SnapshotSpanUtil.GetStartLine span
            let endLine = SnapshotPointUtil.GetContainingLine span.End
            let firstNonBlank = TssUtil.FindFirstNonWhiteSpaceCharacter startLine

            if endLine.LineNumber <= startLine.LineNumber then
                // No adjustment needed when everything is on the same line
                motionResult
            elif not (SnapshotPointUtil.IsStartOfLine span.End) then 
                // End is not the start of the line so there is no adjustment to 
                // be made.  
                motionResult
            elif span.Start.Position <= firstNonBlank.Position && allowExclusiveLineWise then
                // Rule #2. Make this a line wise motion.  Also remove the column 
                // set.  This is necessary because these set the column to 0 which
                // is redundant and confusing for line wise motions when moving the 
                // caret
                let span = SnapshotSpan(startLine.Start, span.End)
                let kind = MotionKind.LineWise CaretColumn.AfterLastLine
                { motionResult with Span = span; MotionKind = kind }
            else 
                // Rule #1. Move this back a line
                let endPoint = 
                    let line = SnapshotUtil.GetLine span.Snapshot (endLine.LineNumber - 1)
                    line.End
                let span = SnapshotSpan(span.Start, endPoint)
                let flags = motionResult.MotionResultFlags ||| MotionResultFlags.ExclusivePromotion
                let kind = MotionKind.CharacterWiseInclusive
                { motionResult with Span = span; MotionKind = kind; MotionResultFlags = flags }

        match motionResult.MotionKind with
        | MotionKind.CharacterWiseInclusive -> motionResult
        | MotionKind.CharacterWiseExclusive -> adjust ()
        | MotionKind.LineWise _ -> motionResult

    /// Run the specified motion and return it's result
    member x.GetMotion motion (motionArgument : MotionArgument) = 

        let motionResult = 
            match motion with 
            | Motion.AllParagraph -> x.AllParagraph motionArgument.Count
            | Motion.AllWord wordKind -> x.AllWord wordKind motionArgument.Count
            | Motion.AllSentence -> x.AllSentence motionArgument.Count |> Some
            | Motion.BeginingOfLine -> x.BeginingOfLine() |> Some
            | Motion.CharLeft -> x.CharLeft motionArgument.Count |> Some
            | Motion.CharRight -> x.CharRight motionArgument.Count |> Some
            | Motion.CharSearch (kind, direction, c) -> x.CharSearch c motionArgument.Count kind direction
            | Motion.EndOfLine -> x.EndOfLine motionArgument.Count |> Some
            | Motion.EndOfWord wordKind -> x.EndOfWord wordKind motionArgument.Count |> Some
            | Motion.FirstNonWhiteSpaceOnCurrentLine -> x.FirstNonWhiteSpaceOnCurrentLine() |> Some
            | Motion.FirstNonWhiteSpaceOnLine -> x.FirstNonWhiteSpaceOnLine motionArgument.Count |> Some
            | Motion.LastNonWhiteSpaceOnLine -> x.LastNonWhiteSpaceOnLine motionArgument.Count |> Some
            | Motion.LastSearch isReverse -> x.LastSearch isReverse motionArgument.Count
            | Motion.LineDown -> x.LineDown motionArgument.Count
            | Motion.LineDownToFirstNonWhiteSpace -> x.LineDownToFirstNonWhiteSpace motionArgument.Count |> Some
            | Motion.LineFromBottomOfVisibleWindow -> x.LineFromBottomOfVisibleWindow motionArgument.RawCount |> Some
            | Motion.LineFromTopOfVisibleWindow -> x.LineFromTopOfVisibleWindow motionArgument.RawCount |> Some
            | Motion.LineInMiddleOfVisibleWindow -> x.LineInMiddleOfVisibleWindow() |> Some
            | Motion.LineOrFirstToFirstNonWhiteSpace -> x.LineOrFirstToFirstNonWhiteSpace motionArgument.RawCount |> Some
            | Motion.LineOrLastToFirstNonWhiteSpace -> x.LineOrLastToFirstNonWhiteSpace motionArgument.RawCount |> Some
            | Motion.LineUp -> x.LineUp motionArgument.Count
            | Motion.LineUpToFirstNonWhiteSpace -> x.LineUpToFirstNonWhiteSpace motionArgument.Count |> Some
            | Motion.Mark c -> x.Mark c
            | Motion.MarkLine c -> x.MarkLine c 
            | Motion.MatchingToken -> x.MatchingToken()
            | Motion.NextPartialWord path -> x.NextPartialWord path motionArgument.Count
            | Motion.NextWord path -> x.NextWord path motionArgument.Count
            | Motion.ParagraphBackward -> x.ParagraphBackward motionArgument.Count |> Some
            | Motion.ParagraphForward -> x.ParagraphForward motionArgument.Count |> Some
            | Motion.QuotedString -> x.QuotedString()
            | Motion.QuotedStringContents -> x.QuotedStringContents()
            | Motion.RepeatLastCharSearch -> x.RepeatLastCharSearch()
            | Motion.RepeatLastCharSearchOpposite -> x.RepeatLastCharSearchOpposite()
            | Motion.Search patternData-> x.Search patternData motionArgument.Count
            | Motion.SectionBackwardOrCloseBrace -> x.SectionBackwardOrCloseBrace motionArgument.Count |> Some
            | Motion.SectionBackwardOrOpenBrace -> x.SectionBackwardOrOpenBrace motionArgument.Count |> Some
            | Motion.SectionForward -> x.SectionForward motionArgument.MotionContext motionArgument.Count |> Some
            | Motion.SectionForwardOrCloseBrace -> x.SectionForwardOrCloseBrace motionArgument.Count |> Some
            | Motion.SentenceBackward -> x.SentenceBackward motionArgument.Count |> Some
            | Motion.SentenceForward -> x.SentenceForward motionArgument.Count |> Some
            | Motion.WordBackward wordKind -> x.WordBackward wordKind motionArgument.Count |> Some
            | Motion.WordForward wordKind -> x.WordForward wordKind motionArgument.Count |> Some
        Option.map (x.AdjustMotionResult motion) motionResult

    interface IMotionUtil with
        member x.TextView = _textView
        member x.GetMotion motion motionArgument = x.GetMotion motion motionArgument

