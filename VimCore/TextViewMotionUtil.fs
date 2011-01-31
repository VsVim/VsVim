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

/// Helper function for getting motions on an ITextView / ITextBuffer instance
module internal TextViewMotionUtilHelper =

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
                TssUtil.FindFirstNonWhitespaceCharacter line = span.Start
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


type internal TextViewMotionUtil 
    ( 
        _textView : ITextView,
        _markMap : IMarkMap,
        _localSettings : IVimLocalSettings ) = 

    let _textBuffer = _textView.TextBuffer
    let _settings = _localSettings.GlobalSettings

    /// Caret point in the view
    member x.StartPoint = TextViewUtil.GetCaretPoint _textView

    member x.SpanAndForwardFromLines (line1:ITextSnapshotLine) (line2:ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.End),true
        else SnapshotSpan(line2.Start, line1.End),false

    /// Apply the startofline option to the given MotionData
    member x.ApplyStartOfLineOption (motionData:MotionData) =
        if not _settings.StartOfLine then motionData 
        else
            let endLine = 
                if motionData.IsForward then SnapshotSpanUtil.GetEndLine motionData.Span
                else SnapshotSpanUtil.GetStartLine motionData.Span
            let point = TssUtil.FindFirstNonWhitespaceCharacter endLine
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
    member x.LineToLineFirstNonWhitespaceMotion (startLine : ITextSnapshotLine) (endLine : ITextSnapshotLine) = 

        // Get the column based on the 'startofline' option
        let column = 
            if _settings.StartOfLine then 
                endLine |> TssUtil.FindFirstNonWhitespaceCharacter |> SnapshotPointUtil.GetColumn
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

    member x.GetParagraphs point direction count = 
        let span = 
            MotionUtil.GetParagraphs point direction
            |> Seq.truncate count
            |> Seq.map (fun p -> p.Span)
            |> SnapshotSpanUtil.CreateCombined
        let span = 
            match span with 
            | Some(span) -> span
            | None -> 
                // Can have no paragraphs at either end
                if Direction.Backward = direction || point.Snapshot.Length = 0 then 
                    point.Snapshot 
                    |> SnapshotUtil.GetStartPoint 
                    |> SnapshotSpanUtil.CreateEmpty
                else 
                    point.Snapshot 
                    |> SnapshotUtil.GetEndPoint 
                    |> SnapshotPointUtil.SubtractOne 
                    |> SnapshotSpanUtil.CreateEmpty
        let isForward = Direction.Forward = direction
        {
            Span = span 
            IsForward = isForward 
            IsAnyWordMotion = false
            MotionKind = MotionKind.Exclusive 
            OperationKind = OperationKind.LineWise 
            Column = None}

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
                    |> TssUtil.FindFirstNonWhitespaceCharacter
                    |> SnapshotPointUtil.GetColumn
                    |> Some } |> Some

    /// Find the matching token for the next token on the current line 
    member x.MatchingToken() = 
        // First find the next token on this line from the caret point
        let caretPoint, caretLine = TextViewUtil.GetCaretPointAndLine _textView
        let tokens = TextViewMotionUtilHelper.GetMatchTokens (SnapshotSpan(caretPoint, caretLine.End))
        match SeqUtil.tryHeadOnly tokens with
        | None -> 
            // No tokens on the line 
            None
        | Some (token, flags) -> 
            // Now lets look for the matching token 
            match TextViewMotionUtilHelper.FindMatchingToken token flags with
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

    interface ITextViewMotionUtil with
        member x.TextView = _textView
        member x.CharSearch c count charSearch direction = 
            match charSearch, direction with
            | CharSearch.ToChar, Direction.Forward -> x.ForwardCharMotionCore c count TssUtil.FindNextOccurranceOfCharOnLine
            | CharSearch.TillChar, Direction.Forward -> x.ForwardCharMotionCore c count TssUtil.FindTillNextOccurranceOfCharOnLine
            | CharSearch.ToChar, Direction.Backward -> x.BackwardCharMotionCore c count TssUtil.FindPreviousOccurranceOfCharOnLine 
            | CharSearch.TillChar, Direction.Backward -> x.BackwardCharMotionCore c count TssUtil.FindTillPreviousOccurranceOfCharOnLine
        member x.Mark c = x.Mark c
        member x.MarkLine c = x.MarkLine c
        member x.MatchingToken() = x.MatchingToken()
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
        member x.AllWord kind count = 
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
                Column = None}
        member x.EndOfWord kind count = 

            // Create the appropriate MotionData structure with the provided SnapshotSpan
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
        member x.FirstNonWhitespaceOnLine () =
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
        member x.BeginingOfLine () =
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
        member x.LineDownToFirstNonWhitespace count =
            let line = x.StartPoint |> SnapshotPointUtil.GetContainingLine
            let number = line.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast line.Snapshot number
            let column = TssUtil.FindFirstNonWhitespaceCharacter endLine |> SnapshotPointUtil.GetColumn |> Some
            let span = SnapshotSpan(line.Start, endLine.EndIncludingLineBreak)
            {
                Span = span 
                IsForward = true 
                IsAnyWordMotion = false
                MotionKind = MotionKind.Inclusive 
                OperationKind = OperationKind.LineWise 
                Column = column}
        member x.LineUpToFirstNonWhitespace count =
            let point = x.StartPoint
            let endLine = SnapshotPointUtil.GetContainingLine point
            let startLine = SnapshotUtil.GetLineOrFirst endLine.Snapshot (endLine.LineNumber - count)
            let span = SnapshotSpan(startLine.Start, endLine.End)
            let column = 
                startLine 
                |> TssUtil.FindFirstNonWhitespaceCharacter
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
        member x.LineOrFirstToFirstNonWhitespace numberOpt = 
            let point = x.StartPoint
            let originLine = SnapshotPointUtil.GetContainingLine point
            let tss= originLine.Snapshot
            let endLine = 
                match numberOpt with
                | Some(number) ->  SnapshotUtil.GetLineOrFirst tss (TssUtil.VimLineToTssLine number)
                | None -> SnapshotUtil.GetFirstLine tss 
            x.LineToLineFirstNonWhitespaceMotion originLine endLine
        member x.LineOrLastToFirstNonWhitespace numberOpt = 
            let point = x.StartPoint
            let originLine = SnapshotPointUtil.GetContainingLine point
            let tss= originLine.Snapshot
            let endLine = 
                match numberOpt with
                | Some(number) ->  SnapshotUtil.GetLineOrLast tss (TssUtil.VimLineToTssLine number)
                | None -> SnapshotUtil.GetLastLine tss 
            x.LineToLineFirstNonWhitespaceMotion originLine endLine
        member x.LastNonWhitespaceOnLine count = 
            let start = x.StartPoint
            let startLine = SnapshotPointUtil.GetContainingLine start
            let snapshot = startLine.Snapshot
            let number = startLine.LineNumber + (count-1)
            let endLine = SnapshotUtil.GetLineOrLast snapshot number
            let endPoint = TssUtil.FindLastNonWhitespaceCharacter endLine
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
                    let count = (CommandUtil.CountOrDefault countOpt) 
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
            | PointKind.EndPoint(_,point) -> MotionData.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
            | PointKind.ZeroLength(point) -> MotionData.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
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
        member x.SentenceFullForward count =
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
                Column = None}
        member x.ParagraphForward count =
            match TextViewUtil.GetCaretPointKind _textView with
            | PointKind.Normal(point) -> x.GetParagraphs point Direction.Forward count
            | PointKind.EndPoint(point,_) -> x.GetParagraphs point Direction.Forward count
            | PointKind.ZeroLength(point) -> MotionData.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
        member x.ParagraphBackward count =
            x.GetParagraphs (TextViewUtil.GetCaretPoint _textView) Direction.Backward count
        member x.ParagraphFullForward count =
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let span = MotionUtil.GetFullParagraph caretPoint
            x.GetParagraphs span.Start Direction.Forward count 
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

    