#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Formatting
open Microsoft.VisualStudio.Text.Operations
open System.Collections.Generic
open Vim.Modes
open Vim.StringBuilderExtensions
open Vim.Interpreter

type OptionBuilder() =
    member x.Bind (value, cont) = 
        match value with 
        | None -> None
        | Some value -> cont value

    member x.Return value = Some value
    member x.ReturnFrom o = o
    member x.Zero () = None

type CachedParsedItem<'T> = { 
    Version: int
    Items: List<'T>
} with

    static member GetItems (snapshot: ITextSnapshot) key doParse = 
        let textBuffer = snapshot.TextBuffer
        let propertyCollection = textBuffer.Properties

        let parseAndSave () = 
            let items = doParse snapshot
            let cachedParseItem = { Version = snapshot.Version.VersionNumber; Items = items }
            propertyCollection.[key] <- cachedParseItem
            items

        match PropertyCollectionUtil.GetValue<CachedParsedItem<'T>> key propertyCollection with
        | Some cachedParsedItem -> 
            if cachedParsedItem.Version = snapshot.Version.VersionNumber then 
                cachedParsedItem.Items
            else 
                parseAndSave ()
        | None -> parseAndSave ()

/// Used to represent xml / html tags within the file.  
type TagBlock
    (
        text: string,
        fullSpan: Span,
        innerSpan: Span,
        children: List<TagBlock>
    ) =

    let _text = text

    // This is the full span of the tag block including the opening and closing tags
    let _fullSpan = fullSpan

    // This is the span within the tag block that does not include the tags
    let _innerSpan = innerSpan

    let mutable _parent: TagBlock option = None

    let _children = children

    member x.Text = _text

    member x.FullSpan = _fullSpan

    member x.InnerSpan = _innerSpan

    member x.Children = _children

    member x.Parent 
        with get () = _parent
        and set value = _parent <- value

type TagBlockParser (snapshot: ITextSnapshot) = 

    let _snapshot = snapshot
    let _builder = OptionBuilder()

    member x.TestPosition position func = 
        if position >= snapshot.Length then
            false
        else
            func (snapshot.[position])

    member x.TestPositionChar position target = 
        x.TestPosition position (fun c -> c = target)

    member x.SkipBlanks position = 
        x.SkipWhile position CharUtil.IsBlank
    
    member x.SkipWhiteSpace position = 
        x.SkipWhile position CharUtil.IsWhiteSpace

    member x.SkipWhile position predicate = 
        let mutable position = position
        while x.TestPosition position predicate && position < _snapshot.Length do
            position <- position + 1
        position
    
    member x.ParseName startPosition = 
        let mutable position = startPosition
        while x.TestPosition position (fun c -> CharUtil.IsTagNameChar c) do
            position <- position + 1

        let length = position - startPosition
        if length = 0 then
            None
        else
            Some (Span(startPosition, length))

    /// Parse out the name of an attribute.  Returns the Span of the name if found an None if there is
    /// not a valid name 
    member x.ParseAttributeName startPosition = 
        x.ParseName startPosition

    /// Parse out the attribute value.  Return the Span of the attribute value excluding the border 
    /// quotes.  If the closing quote is not found then None is returned 
    member x.ParseAttributeValue startPosition quoteChar = 
        let mutable position = startPosition
        while not (x.TestPositionChar position quoteChar) && position <= _snapshot.Length do
            position <- position + 1

        if x.TestPositionChar position quoteChar then
            let span = Span.FromBounds(startPosition, position)
            Some span
        else
            None

    member x.ParseQuoteChar position = 
        if position < _snapshot.Length then
            let c = _snapshot.[position]
            if c = '"' || c = '\'' then 
                Some c
            else
                None
        else
            None

    member x.ParseChar position c = 
        if x.TestPositionChar position c then
            Some (position + 1)
        else
            None

    /// Parse out a single attribute name value pair 
    member x.ParseAttribute startPosition: Span option = 
        let position = x.SkipWhiteSpace startPosition
        _builder { 
            let! nameSpan = x.ParseAttributeName position
            let position = x.SkipWhiteSpace nameSpan.End
            if x.TestPositionChar position '=' then
                let quotePosition = x.SkipWhiteSpace (position + 1)
                let! quoteChar = x.ParseQuoteChar quotePosition
                let! valueSpan = x.ParseAttributeValue (quotePosition + 1) quoteChar
                let! valueEnd = x.ParseChar valueSpan.End quoteChar
                return Span.FromBounds(startPosition, valueEnd)
            else
                //The attribute has no value
                return Span.FromBounds(startPosition, nameSpan.End)
        }

    /// This will be called with the position pointing immediately after the end of the 
    /// tag name.  It should return the position immediately following all of the attribute
    /// values or None if there is an error in the attributes
    member x.ParseAttributes startPosition = 
        let mutable position = x.SkipWhiteSpace startPosition
        let mutable isError = false
        let mutable isDone = false

        while not isDone do
            if x.TestPositionChar position '>' then
                isDone <- true
            elif position >= _snapshot.Length then
                isError <- true
                isDone <- true
            else
                match x.ParseAttribute position with
                | None ->
                    isError <- true
                    isDone <- true
                | Some span -> 
                    position <- x.SkipWhiteSpace span.End

        if isError then
            None
        else
            Some position

    /// Parse out a legal start tag at the given position.  Succeeds when the point passed in 
    /// is at a '<' character of an open tag 
    ///
    /// This will ignore tags br and meta as they are specifically ignored in the documentation
    /// for :help tag-blocks
    member x.ParseStartTag startPosition = 
        if x.TestPositionChar startPosition '<' then
            _builder {
                let! nameSpan = x.ParseName (startPosition + 1) 
                let text = _snapshot.GetText(nameSpan)
                if StringUtil.IsEqualIgnoreCase "br" text || StringUtil.IsEqualIgnoreCase "meta" text then
                    return! None
                else
                    let! position = x.ParseAttributes nameSpan.End
                    let! position = x.ParseChar position '>'
                    return (text, position)
            }
        else
            None

    member x.ParseEndTag position = 
        if x.TestPositionChar position '<' && x.TestPositionChar (position + 1) '/' then
            let textStartPosition = position + 2
            let mutable position = textStartPosition
            while x.TestPosition position CharUtil.IsTagNameChar do
                position <- position + 1

            let length = position - textStartPosition
            if length > 0 && x.TestPositionChar position '>' then
                let span = Span(textStartPosition, length)
                let text = _snapshot.GetText(span)
                Some (text, position + 1)
            else
                None
        else 
            None

    /// Parse out the contents of a tag with the specified name within the given Span.  This returns
    /// a tuple of the tag contents and whether or not the ending tag was ever found.  
    member x.ParseTagContents tagName tagStartPosition contentStartPosition: TagBlock option = 
        let children = List<TagBlock>()

        // This is a tuple of values.  The first value is the end of the content 
        // portion of the text while the second is the end of the closing tag
        let mutable endPosition: (int * int) option = None
        let mutable position = contentStartPosition
        while position < _snapshot.Length && Option.isNone endPosition do
            match x.ParseStartTag position with
            | Some (startTagName, nextPosition) ->
                match x.ParseTagContents startTagName position nextPosition with
                | Some tagBlock -> 
                    children.Add(tagBlock)
                    position <- tagBlock.FullSpan.End
                | None ->
                    // Unmatched start tag, parsing is done
                    position <- _snapshot.Length
            | None ->
                match x.ParseEndTag position with 
                | Some (endTagName, nextPosition) ->
                    if StringUtil.IsEqualIgnoreCase tagName endTagName then
                        endPosition <- Some (position, nextPosition)
                    else
                        position <- position + 1
                | None -> 
                    position <- position + 1

        match endPosition with 
        | None -> None
        | Some (contentEndPosition, tagEndPosition) ->
            let fullSpan = Span.FromBounds(tagStartPosition, tagEndPosition)
            let innerSpan = Span.FromBounds(contentStartPosition, contentEndPosition)
            let tagBlock = TagBlock(tagName, fullSpan, innerSpan, children)

            // Make sure to set the parent pointer on all of the parsed children 
            children |> Seq.iter (fun child -> child.Parent <- Some tagBlock)

            Some tagBlock

    member x.ParseTagBlocks() =
        let collection = List<TagBlock>()
        let mutable position = 0
        while position < _snapshot.Length do
            match x.ParseStartTag position with
            | Some (tagName, nextPosition) ->
                match x.ParseTagContents tagName position nextPosition with
                | Some tagBlock -> 
                    collection.Add(tagBlock)
                    position <- tagBlock.FullSpan.End
                | None -> position <- position + 1
            | None -> position <- position + 1

        collection

module TagBlockUtil = 

    /// This is the key for accessing directive blocks within the ITextSnapshot.  This
    /// lets us avoid multiple parses of #if for a single ITextSnapshot
    let _tagBlockKey = obj()

    let ParseTagBlocks snapshot = 
        let parser = TagBlockParser(snapshot)
        parser.ParseTagBlocks()

    let GetTagBlocks snapshot = 
        CachedParsedItem<TagBlock>.GetItems snapshot _tagBlockKey ParseTagBlocks

    let GetTagBlockForPoint (point: SnapshotPoint) =
        let tagBlocks = GetTagBlocks point.Snapshot

        let isMatch (tagBlock: TagBlock) = 
            let span = tagBlock.FullSpan
            span.Contains point.Position

        let rec find current collection = 
            match collection |> Seq.tryFind isMatch with
            | None -> current
            | Some tagBlock -> find (Some tagBlock) tagBlock.Children

        find None tagBlocks

[<RequireQualifiedAccess>]
[<NoComparison>]
type DirectiveKind = 
    | If
    | Elif
    | Else
    | EndIf

type Directive = { 
    Kind: DirectiveKind;
    Span: Span
}
    with

    member x.AdjustStart offset = 
        let start = x.Span.Start + offset
        let span = Span(start, x.Span.Length)
        { x with Span = span }

    override x.ToString() = sprintf "%O - %O" x.Kind x.Span

type DirectiveBlock = {
    Directives: List<Directive>
    IsComplete: bool
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

type internal BlockUtil() =

    /// Get the block span for the specified char at the given context point
    member x.GetBlock (blockKind: BlockKind) contextPoint =

        // Blocks in vim are a messy combination of heuristics and
        // a lot of special cases. The main complication is the
        // handling of parentheses in string and character literals.
        // The idea is to match parentheses the same way that the
        // programming language would, that is if they are syntactic
        // elements, match them that way, and if they are string
        // elements, match them that way instead.

        // If the context point for a block is outside a string literal,
        // we don't want unbalanced parentheses in strings throwing us
        // off. Likewise, if we are inside a string literal, we don't
        // want a parenthesis inside the string to match a syntactic
        // parenthesis outside the string. So if possible we want
        // to allow valid blocks in, say, a regular expression in
        // a string, as well as valid syntactic expression blocks.

        // Likewise, unbalances parentheses or stray quote marks in
        // comments can also be problematic, but vim does very little
        // to accomodate those cases.

        // In any case, this is an inexact science because different
        // programming languages have different ways of representing
        // and encoding strings. The goal is to do the intuitive thing
        // from a programming point of view in as many cases as
        // possible.

        // Our "univeral string" (covering common cases in C, C++, C#,
        // F#, Python and Java, etc.) is text surrounded with single
        // or double quotes and using backslash to escape the quote
        // character.

        // A more robust solution would be to use an abstract syntax
        // tree supplied by a language service appropriate for the
        // current buffer but even that approach has drawbacks. The
        // current approach works as well for a programming snippet
        // in a text file as it does in an actual source file.

        let startChar, endChar = blockKind.Characters

        // Is the char at the given point escaped?
        let isEscaped point =
            match SnapshotPointUtil.TrySubtractOne point with
            | None -> false
            | Some point -> SnapshotPointUtil.GetChar point = '\\'

        // Is the char at the given point double escaped?
        let isDoubleEscaped point =
            match SnapshotPointUtil.TrySubtract point 2, SnapshotPointUtil.TrySubtract point 2 with
            | None, _
            | _, None -> false
            | Some point1, Some point2 -> SnapshotPointUtil.GetChar point1 = '\\' && SnapshotPointUtil.GetChar point2 = '\\'

        // Is the char at the given point unescaped and equal to the specified character?
        let isChar c point = SnapshotPointUtil.GetChar point = c && (not (isEscaped point) || isDoubleEscaped point)

        // Given the specified block start and end characters, return a
        // function that transform a tuple of a point and the current nesting
        // count into a tuple of final success and the next nesting count.
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

        // Compute a tuple of three quantities:
        // - Whether the target point is inside a string literal
        // - The start point of the string, as long as it there
        //   is no unmatched start character in string literals
        //   before the target
        // - The quote character used to delineate the string
        let isInStringLiteral (target: SnapshotPoint) (sequence: SnapshotPoint seq) =
            let mutable escape = false
            let mutable quote = None
            let mutable start = None
            let mutable depth = 0
            let mutable result = false, None, None

            for point in sequence do
                let c = SnapshotUtil.GetChar point.Position snapshot
                if quote <> None then
                    if escape then
                        escape <- false
                    else
                        if c = '\\' then
                            escape <- true
                        else
                            match quote with
                            | Some quoteChar when c = '\n' || c = quoteChar -> quote <- None
                            | Some _ when c = '\\' -> escape <- true
                            | _ -> ()
                    if point = target then
                        if depth <= 0 then
                            result <- true, start, quote
                        else
                            result <- true, None, quote
                    elif c = startChar then
                        depth <- depth + 1
                    elif c = endChar then
                        depth <- depth - 1
                elif c = '\'' || c = '\"' then
                    quote <- Some c
                    start <- Some point

            result

        // Choose a starting point within the block.
        let endPoint =
            if isChar startChar contextPoint then
                SnapshotPointUtil.AddOneOrCurrent contextPoint
            else
                contextPoint

        // Go to the beginning of the line and then scan forward to
        // the beginning of the containing string literal, if any.
        let endPoint, endPointIsInStringLiteral, endPointQuote =
            match
                SnapshotPointUtil.GetContainingLine endPoint
                |> SnapshotLineUtil.GetExtent
                |> SnapshotSpanUtil.GetPoints SearchPath.Forward
                |> isInStringLiteral endPoint
                with
                | true, Some adjustedEndPoint, _ -> adjustedEndPoint, false, None
                | true, None, Some quoteChar -> endPoint, true, Some quoteChar
                | _ -> endPoint, false, None

        // Parse backward skipping non-string literals
        // if the context is a string literal and skipping
        // string literals otherwise.
        let filterToContextBackward endPointQuote (sequence: SnapshotPoint seq) =
            let mutable quote: char option = endPointQuote
            seq {
                for point in sequence do
                    let c = SnapshotUtil.GetChar point.Position snapshot
                    if quote <> None then
                        match quote with
                        | Some quoteChar when c = '\n' || isChar quoteChar point -> quote <- None
                        | _ -> ()
                        if endPointIsInStringLiteral then yield point
                    elif c = '\'' || c = '\"' then
                        quote <- Some c
                    else
                        if not endPointIsInStringLiteral then yield point
            }

        // Parse forward skipping non-string literals
        // if the context is a string literal and skipping
        // string literals otherwise.
        let filterToContextForward endPointQuote (sequence: SnapshotPoint seq) =
            let mutable escape = false
            let mutable quote: char option = endPointQuote
            seq {
                for point in sequence do
                    let c = SnapshotUtil.GetChar point.Position snapshot
                    if quote <> None then
                        if escape then
                            escape <- false
                        else
                            match quote with
                            | Some quoteChar when c = '\n' || c = quoteChar -> quote <- None
                            | Some _ when c = '\\' -> escape <- true
                            | _ -> ()
                            if endPointIsInStringLiteral then yield point
                    elif c = '\'' || c = '\"' then
                        quote <- Some c
                    else
                        if not endPointIsInStringLiteral then yield point
            }

        // Search backward for the character that starts this block.
        let startPoint =
            SnapshotSpan(SnapshotPoint(snapshot, 0), endPoint)
            |> SnapshotSpanUtil.GetPoints SearchPath.Backward
            |> filterToContextBackward endPointQuote
            |> SeqUtil.tryFind 1 (findMatched endChar startChar)

        // Then search forward for the character that ends this block.
        let lastPoint =
            match startPoint with
            | None -> None
            | Some startPoint ->
                SnapshotSpan(startPoint, SnapshotUtil.GetEndPoint snapshot)
                |> SnapshotSpanUtil.GetPoints SearchPath.Forward
                |> filterToContextForward endPointQuote
                |> SeqUtil.tryFind 0 (findMatched startChar endChar)

        // Return the span from the block start to block end.
        match startPoint, lastPoint with
        | Some startPoint, Some lastPoint -> Some (startPoint, lastPoint)
        | _ -> None


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
                let func kind (token: Token) = 
                    let endPosition = token.StartIndex + token.Length
                    let span = Span.FromBounds(start, endPosition)
                    { Kind = kind; Span = span } |> Some

                // The span of the token for the #if variety blocks is the entire line
                let all kind (token: Token) = 
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
    member x.ParseDirectiveBlocks (snapshot: ITextSnapshot) = 

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
        let rec parseBlockRemainder (startDirective: Directive) lineNumber = 
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
    member x.GetDirectiveBlocks (snapshot: ITextSnapshot) = 
        CachedParsedItem<DirectiveBlock>.GetItems snapshot _directiveBlocksKey x.ParseDirectiveBlocks

    /// Find the correct MatchingTokenKind for the given line and column position on that 
    /// line.  Needs to consider all possible matching tokens and see which one is closest
    /// to the column (going forward only). 
    /// CTOOD: is this a column or position? 
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
            let result1 = StringUtil.IndexOfCharAt c1 column lineText
            let result2 = StringUtil.IndexOfCharAt c2 column lineText
            reducePair result1 result2 kind

        // Find the closest comment string to the specified column
        let findComment () = 

            // Check at the column and one before if not found at the column
            let indexOf text = 
                let result = StringUtil.IndexOfStringAt text column lineText
                match result with
                | None -> 
                    if column > 0 then StringUtil.IndexOfStringAt text (column - 1) lineText
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
        let findMatchingTokenChar target (blockKind: BlockKind) =
            let contextPoint = new SnapshotPoint(snapshot, target)
            let blockUtil = BlockUtil()
            match blockUtil.GetBlock blockKind contextPoint with
            | Some (startPoint, endPoint) ->
                let otherPoint = if contextPoint = startPoint then endPoint else startPoint
                let snapshotSpan = new SnapshotSpan(otherPoint, 1)
                Some snapshotSpan.Span
            | None -> None

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

            let mutable commentStart: int option = None
            let mutable index = 0
            let mutable found: int option = None
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
                        commentStart <- None
                        index <- index + 2
                else
                    index <- index + 1

            match found with
            | None -> None
            | Some start -> Span(start, 1) |> Some

        // Find the matching directive starting from the buffer position 'target'
        let findMatchingDirective (target: int) = 

            let blockList = x.GetDirectiveBlocks snapshot
            let mutable found: Span option = None
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

        let line, column = 
            let data = SnapshotColumn(point)
            let line = data.Line

            // The search should normalize caret's in the line break back to the last valid 
            // point of the line. 
            let column = 
                if data.IsLineBreak then
                    max 0 (line.End.Position - line.Start.Position - 1)
                else
                    data.ColumnNumber
            (line, column)

        let found = 
            let lineText = SnapshotLineUtil.GetText line
            match x.FindMatchingTokenKindCore lineText column with
            | None -> None
            | Some (column, kind) ->
                let position = line.Start.Position + column
                match kind with 
                | MatchingTokenKind.Braces -> findMatchingTokenChar position BlockKind.CurlyBracket
                | MatchingTokenKind.Brackets -> findMatchingTokenChar position BlockKind.Bracket
                | MatchingTokenKind.Parens -> findMatchingTokenChar position BlockKind.Paren
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
            | SearchPath.Forward -> 
                let span = SnapshotSpan(point, SnapshotUtil.GetEndPoint snapshot)
                span |> SnapshotSpanUtil.GetPoints SearchPath.Forward |> SeqUtil.skipMax 1
            | SearchPath.Backward ->
                let span = SnapshotSpan(SnapshotUtil.GetStartPoint snapshot, point)
                span |> SnapshotSpanUtil.GetPoints SearchPath.Backward

        // Determine the characters that will a new depth to be entered and 
        // left.  Going forward ( is up and ) is down.  
        let up, down = 
            let startChar, endChar = 
                match kind with
                | UnmatchedTokenKind.Paren -> '(', ')'
                | UnmatchedTokenKind.CurlyBracket -> '{', '}'
            match path with
            | SearchPath.Forward -> startChar, endChar
            | SearchPath.Backward -> endChar, startChar

        let mutable depth = 0
        let mutable count = count
        let mutable found: SnapshotPoint option = None
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
    LeadingWhiteSpace: SnapshotSpan
    LeadingQuote: SnapshotPoint
    Contents: SnapshotSpan
    TrailingQuote: SnapshotPoint
    TrailingWhiteSpace: SnapshotSpan 
} with
    
    member x.FullSpan = SnapshotSpanUtil.Create x.LeadingWhiteSpace.Start x.TrailingWhiteSpace.End

/// Result of trying to run a motion against the Visual Snapshot.  By default motions
/// are run against the edit snapshot but line wise motions which involve counts need
/// to be run against the visual snapshot in order to properly account for folded regions
[<RequireQualifiedAccess>]
type VisualMotionResult =

    /// The mapping succeeded
    | Succeeded of MotionResult: MotionResult

    /// The motion simply produced no data
    | FailedNoMotionResult

    /// The motion failed because the Visual SnapshotData could not be retrieved
    | FailedNoVisualSnapshotData

    /// The motion failed because the SnapshotData could not be mapped back down to the
    /// Edit snapshot
    | FailedNoMapToEditSnapshot

type internal MotionUtil 
    ( 
        _vimBufferData: IVimBufferData,
        _commonOperations: ICommonOperations
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

    /// The virtual caret point in the ITextView
    member x.CaretVirtualPoint = TextViewUtil.GetCaretVirtualPoint _textView

    member x.CaretColumn = SnapshotColumn(x.CaretPoint)

    member x.CaretVirtualColumn = TextViewUtil.GetCaretVirtualColumn _textView

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

    member x.SpanAndForwardFromLines (line1: ITextSnapshotLine) (line2: ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.EndIncludingLineBreak), true
        else SnapshotSpan(line2.Start, line1.EndIncludingLineBreak), false

    /// Apply the 'startofline' option to the given MotionResult.  This function must be 
    /// called with the MotionData mapped back to the edit snapshot
    member x.ApplyStartOfLineOption (motionData: MotionResult) =
        Contract.Assert(match motionData.MotionKind with MotionKind.LineWise _ -> true | _ -> false)
        Contract.Assert(motionData.Span.Snapshot = x.CurrentSnapshot)
        if not _globalSettings.StartOfLine then 
            motionData 
        else
            let lastLine = motionData.DirectionLastLine

            // TODO: Is GetFirstNonBlankOrStart correct here?  Should it be using the
            // End version?
            let point = SnapshotLineUtil.GetFirstNonBlankOrStart lastLine
            let column = SnapshotPointUtil.GetLineOffset point |> CaretColumn.InLastLine
            { motionData with 
                MotionKind = MotionKind.LineWise 
                CaretColumn = column }

    member x.ApplyBigDelete (result: MotionResult) =
        let flags = result.MotionResultFlags ||| MotionResultFlags.BigDelete
        { result with MotionResultFlags = flags }

    /// Linewise motions often need to deal with the VisualSnapshot of the ITextView because
    /// they see folded regions as single lines vs. the many of which are actually represented
    /// within the fold.  
    ///
    /// This function wraps the conversion of the information from the edit buffer into the 
    /// visual buffer and back again when we are done. 
    member x.MotionWithVisualSnapshotCore (action: SnapshotData -> 'T) (getMotionResult: 'T -> MotionResult option) =

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
                // Now migrate the SnapshotSpan values down to the edit buffer.  If this mapping can't 
                // be done then the motion fails.
                match motionResult.MapSpans (fun s -> BufferGraphUtil.MapSpanDownToSingle _bufferGraph s x.CurrentSnapshot) with
                | Some motionResult -> VisualMotionResult.Succeeded motionResult
                | None ->
                    _statusUtil.OnError Resources.Internal_ErrorMappingBackToEdit
                    VisualMotionResult.FailedNoMapToEditSnapshot

    /// Run the motion function against the Visual Snapshot
    member x.MotionWithVisualSnapshot (action: SnapshotData -> MotionResult) = 

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
    member x.MotionOptionWithVisualSnapshot (action: SnapshotData -> MotionResult option) = 

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

            let caretLine = textViewLines.GetTextViewLineContainingBufferPosition x.CaretPoint
            let targetLine =
                let index = textViewLines.GetIndexOfTextLine caretLine
                let index = index + count
                let index = min index (textViewLines.Count - 1)
                textViewLines.[index]
            x.DisplayLineMotion caretLine targetLine

        | _ -> x.LineDown count

    /// Motion for "count" display line upwards
    member x.DisplayLineUp count =
        match TextViewUtil.IsWordWrapEnabled _textView, TextViewUtil.GetTextViewLines _textView with
        | true, Some textViewLines ->

            let caretLine = textViewLines.GetTextViewLineContainingBufferPosition x.CaretPoint
            let targetLine =
                let index = textViewLines.GetIndexOfTextLine caretLine
                let index = index - count
                let index = max index 0
                textViewLines.[index]
            x.DisplayLineMotion caretLine targetLine

        | _ -> x.LineUp count

    /// Move from caret line to target line preserving the y coordinate
    member x.DisplayLineMotion (caretLine: ITextViewLine) (targetLine: ITextViewLine) =

        // Find the point in the target line nearest to the y coordinate of the caret.
        let caretCoordinate = caretLine.GetCharacterBounds(x.CaretPoint).Left
        let targetPoint =
            targetLine.Extent
            |> SnapshotSpanUtil.GetPoints SearchPath.Forward
            |> Seq.map (fun point -> point, targetLine.GetCharacterBounds point)
            |> Seq.map (fun (point, bounds) -> point, (bounds.Left + bounds.Right) / 2.0 - caretCoordinate)
            |> Seq.filter (fun (_, coordinate) -> coordinate >= 0.0)
            |> Seq.map (fun (point, _) -> point)
            |> SeqUtil.headOrDefault targetLine.Start

        let caretPoint = x.CaretPoint
        let span, isForward =
            if caretPoint.Position < targetPoint.Position then
                SnapshotSpan(caretPoint, targetPoint), true
            else
                SnapshotSpan(targetPoint, caretPoint), false
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward) |> Some

    member x.DisplayLineFirstNonBlank() = 
        match TextViewUtil.IsWordWrapEnabled _textView, TextViewUtil.GetTextViewLines _textView with
        | true, Some textViewLines -> 
            let line = textViewLines.GetTextViewLineContainingBufferPosition x.CaretPoint
            x.FirstNonBlankCore line.Extent
        | _ -> x.FirstNonBlankOnCurrentLine()

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
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false) |> Some

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
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true) |> Some

    member x.DisplayLineMiddleOfScreen() =
        let createForPoint (point: SnapshotPoint) = 
            let isForward = x.CaretPoint.Position <= point.Position
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint point
            let span = SnapshotSpan(startPoint, endPoint)
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward) |> Some

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

    /// Get the caret column of the specified line based on the 'startofline' option
    member x.GetCaretColumnOfLine (line: ITextSnapshotLine) =
        let offset = 
            if _globalSettings.StartOfLine then 
                line |> SnapshotLineUtil.GetFirstNonBlankOrStart |> SnapshotPointUtil.GetLineOffset
            else
                _textView |> TextViewUtil.GetCaretPoint |> SnapshotPointUtil.GetLineOffset
        CaretColumn.InLastLine offset

    /// Get the motion between the provided two lines.  The motion will be linewise
    /// and have a column of the first non-whitespace character.  If the 'startofline'
    /// option is not set it will keep the original column
    member x.LineToLineFirstNonBlankMotion (flags: MotionResultFlags) (startLine: ITextSnapshotLine) (endLine: ITextSnapshotLine) =

        // Create the range based on the provided lines.  Remember they can be in reverse
        // order
        let range, isForward = 
            let startLine, endLine, isForward = 
                if startLine.LineNumber <= endLine.LineNumber then startLine, endLine, true 
                else endLine, startLine, false
            (SnapshotLineRangeUtil.CreateForLineRange startLine endLine, isForward)
        let column = x.GetCaretColumnOfLine endLine
        MotionResult.Create(range.ExtentIncludingLineBreak, MotionKind.LineWise, isForward, flags, column)

    /// Get the block span for the specified char at the given context point
    member x.GetBlock (blockKind: BlockKind) contextPoint =
        let blockUtil = BlockUtil()
        blockUtil.GetBlock blockKind contextPoint

    member x.GetBlockWithCount (blockKind: BlockKind) contextPoint count = 

        // Need to wrap GetBlock to account for blocks being side by 
        // side.  In that case we have to detect a side by side block and 
        // keep moving right until we get the outer block or None
        let getBlockHelper closePoint =
            let mutable isDone = false
            let mutable contextPoint = SnapshotPointUtil.AddOne closePoint
            let mutable result: (SnapshotPoint * SnapshotPoint) option = None

            while not isDone do
                match x.GetBlock blockKind contextPoint with
                | None -> isDone <- true
                | Some (newOpenPoint, newClosePoint) ->
                    if closePoint.Position > newOpenPoint.Position && closePoint.Position < newClosePoint.Position then
                        result <- Some (newOpenPoint, newClosePoint)
                        isDone <- true
                    else
                        contextPoint <- SnapshotPointUtil.AddOne newClosePoint
            result

        let rec inner openPoint closePoint count = 
            if count = 0 then
                Some (openPoint, closePoint)
            else
                match getBlockHelper closePoint with 
                | None -> None
                | Some (openPoint, closePoint) -> inner openPoint closePoint (count - 1)

        match x.GetBlock blockKind contextPoint with
        | Some (openPoint, closePoint) -> inner openPoint closePoint (count - 1)
        | None -> None

    member x.GetQuotedStringData quoteChar = 

        // All of the points which represent a valid quoteChar value on the line.  This
        // will take into account escaped characters
        let quotePoints = 
            let isEscapeChar c = 
                StringUtil.ContainsChar _localSettings.QuoteEscape c 

            let list = List<SnapshotPoint>()
            let endPosition = x.CaretLine.End.Position
            let mutable currentPosition = x.CaretLine.Start.Position
            while currentPosition < endPosition do
                let currentPoint = SnapshotPoint(x.CurrentSnapshot, currentPosition)
                let currentChar = currentPoint.GetChar()
                if isEscapeChar currentChar then
                    currentPosition <- currentPosition + 2
                elif currentChar = quoteChar then
                    list.Add(SnapshotPoint(x.CurrentSnapshot, currentPosition))
                    currentPosition <- currentPosition + 1
                else
                    currentPosition <- currentPosition + 1

            list

        // When building up the pairs of quotes on a line it's important to understand that Vim 
        // doesn't care about strings in the same way as a programming language.  Consider
        //
        //  let x = 'first'second'
        //
        // A language may say that this is invalid code because it doesn't have a matched pair of
        // strings.  Vim just things there are 2 strings here.  The middle quote exists for both
        // strings
        let quoteSpans = 
            let list = List<SnapshotSpan>()
            let mutable index = 1
            while index < quotePoints.Count do
                let span = SnapshotSpan(quotePoints.[index - 1], quotePoints.[index].Add(1))
                list.Add(span)
                index <- index + 1
            list

        let getStringData (stringSpan: SnapshotSpan) = 
            let isWhiteSpace (position: int) = 
                let c = x.CurrentSnapshot.[position]
                CharUtil.IsWhiteSpace c

            let leadingSpace = 
                let mutable current = stringSpan.Start.Position
                let minimum = x.CaretLine.Start.Position
                while current > minimum && isWhiteSpace (current - 1) do
                    current <- current - 1

                SnapshotSpan(SnapshotPoint(x.CurrentSnapshot, current), stringSpan.Start)

            let trailingSpace = 
                let mutable current = stringSpan.End.Position
                let maximum = x.CaretLine.End.Position
                while current < maximum && isWhiteSpace current do
                    current <- current + 1

                SnapshotSpan(stringSpan.End, SnapshotPoint(x.CurrentSnapshot, current))

            let trailingQuote = SnapshotSpanUtil.GetLastIncludedPoint stringSpan |> Option.get

            let content = 
                let start = stringSpan.Start.Add(1)
                SnapshotSpan(start, trailingQuote)

            {
                LeadingWhiteSpace = leadingSpace
                LeadingQuote = stringSpan.Start
                Contents = content
                TrailingQuote = trailingQuote
                TrailingWhiteSpace = trailingSpace } |> Some

        // If the caret is before the first quote string on the line it is moved forward to that
        // quote
        let position = 
            let position = x.CaretPoint.Position
            if quoteSpans.Count > 0 && position < quoteSpans.[0].Start.Position then
                quoteSpans.[0].Start.Position
            else
                position

        // Now try and find the best possible string.  In general this is pretty simple, just grab
        // the string which contains the position.  The tricky case is when the position falls directly
        // on a quote.  In that case we have to determine if it is the start or end quote (just check 
        // for an even count to make that determination)
        let mutable data: QuotedStringData option = None
        let mutable index = 0 
        while index < quoteSpans.Count && Option.isNone data do
            let span = quoteSpans.[index]
            if span.End.Position - 1 = position then
                if index % 2 = 0 then
                    data <- getStringData span
            elif span.Contains(position) then
                data <- getStringData span

            index <- index + 1

        data


    member x.Mark localMark = 
        match _vimTextBuffer.GetLocalMark localMark with
        | None -> 
            None
        | Some virtualPoint ->

            // Found the motion so update the jump list
            _jumpList.Add x.CaretVirtualPoint

            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let markPoint = virtualPoint.Position
            let markPoint =
                if not _globalSettings.IsVirtualEditOneMore
                    && not (SnapshotPointUtil.IsStartOfLine markPoint)
                    && SnapshotPointUtil.IsInsideLineBreak markPoint then
                    SnapshotPointUtil.SubtractOne markPoint
                else
                    markPoint
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint markPoint
            let span = SnapshotSpan(startPoint, endPoint)
            let isForward = caretPoint = startPoint
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward, motionResultFlags = MotionResultFlags.BigDelete) |> Some

    /// Motion from the caret to the given mark within the ITextBuffer.  Because this uses
    /// absolute positions and not counts we can operate on the edit buffer and don't need
    /// to consider the visual buffer
    member x.MarkLine localMark =

        match _vimTextBuffer.GetLocalMark localMark with
        | None ->
            None
        | Some virtualPoint ->

            // Found the motion so update the jump list
            _jumpList.Add x.CaretVirtualPoint

            let startPoint, endPoint = SnapshotPointUtil.OrderAscending x.CaretPoint virtualPoint.Position
            let startLine = SnapshotPointUtil.GetContainingLine startPoint
            let endLine = SnapshotPointUtil.GetContainingLine endPoint
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            let isForward = x.CaretPoint = startPoint
            let column =
                virtualPoint.Position
                |> SnapshotPointUtil.GetContainingLine
                |> SnapshotLineUtil.GetFirstNonBlankOrStart
                |> SnapshotPointUtil.GetLineOffset
                |> CaretColumn.InLastLine
            MotionResult.Create(range.ExtentIncludingLineBreak, MotionKind.LineWise, isForward, MotionResultFlags.None, column) |> Some

    member x.MatchingTokenOrDocumentPercent numberOpt = 
        match numberOpt with
        | Some 0 -> None
        | Some x when x > 100 -> None
        | Some count -> 
            _jumpList.Add x.CaretVirtualPoint

            let lineCount = x.CurrentSnapshot.LineCount
            let line = (count * lineCount + 99) / 100

            line
            |> Util.VimLineToTssLine
            |> x.CurrentSnapshot.GetLineFromLineNumber
            |> x.LineToLineFirstNonBlankMotion MotionResultFlags.None x.CaretLine
            |> x.ApplyBigDelete
            |> Some
        | None -> x.MatchingToken() |> Option.map x.ApplyBigDelete

    /// Find the matching token for the next token on the current line 
    member x.MatchingToken() = 

        match _matchingTokenUtil.FindMatchingToken x.CaretPoint with
        | None -> None
        | Some matchingTokenSpan ->

            // Search succeeded so update the jump list before moving
            _jumpList.Add x.CaretVirtualPoint

            // Order the tokens appropriately to get the span 
            let span, isForward = 
                if x.CaretPoint.Position < matchingTokenSpan.Start.Position then
                    let endPoint = SnapshotPointUtil.AddOneOrCurrent matchingTokenSpan.Start
                    SnapshotSpan(x.CaretPoint, endPoint), true
                else
                    SnapshotSpan(matchingTokenSpan.Start, SnapshotPointUtil.AddOneOrCurrent x.CaretPoint), false

            MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward) |> Some

    member x.UnmatchedToken path kind count = 
        match _matchingTokenUtil.FindUnmatchedToken path kind x.CaretPoint count with
        | None -> None
        | Some matchingPoint ->

            // Search succeeded so update the jump list before moving
            _jumpList.Add x.CaretVirtualPoint

            // Order the tokens appropriately to get the span 
            let span, isForward = 
                if x.CaretPoint.Position < matchingPoint.Position then
                    SnapshotSpan(x.CaretPoint, matchingPoint), true
                else
                    SnapshotSpan(matchingPoint, x.CaretPoint), false

            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward) |> Some

    member x.MoveCaretToMouse () =
        match _commonOperations.MousePoint with
        | Some point ->
            let mousePoint = point.Position
            let span, isForward = 
                if x.CaretPoint.Position < mousePoint.Position then
                    SnapshotSpan(x.CaretPoint, mousePoint), true
                else
                    SnapshotSpan(mousePoint, x.CaretPoint), false

            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward) |> Some
        | None -> None

    /// Implement the all block motion
    member x.AllBlock contextPoint blockKind count =
        match x.GetBlockWithCount blockKind contextPoint count with
        | Some (openPoint, closePoint) -> 
            let span = SnapshotSpan(openPoint, closePoint.Add(1))
            MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true) |> Some
        | None -> None

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

        let all = x.GetParagraphs SearchPath.Forward x.CaretColumn |> Seq.truncate count |> List.ofSeq
        match all with
        | [] ->
            // No paragraphs forward so return nothing
            None
        | head :: tail -> 

            // Get the span of the all motion
            let span = 
                let last = List.item (all.Length - 1) all
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
                        |> SnapshotPointUtil.GetPoints SearchPath.Forward 
                        |> Seq.skipWhile isWhiteSpace
                        |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot) 

                    let line = SnapshotUtil.GetLineOrFirst span.Snapshot (endPoint.GetContainingLine().LineNumber - 1)
                    
                    SnapshotSpan(span.Start, line.End) |> Some
                else
                    None

            // Include the preceding white space in the Span
            let includePrecedingWhiteSpace () =
                let startPoint = 
                    span.Start 
                    |> SnapshotPointUtil.GetPointsIncludingLineBreak SearchPath.Backward
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
                |> SnapshotSpanUtil.GetPoints SearchPath.Forward
                |> Seq.filter (fun point -> not (SnapshotPointUtil.IsWhiteSpaceOrInsideLineBreak point))
                |> SeqUtil.isNotEmpty

            if spanHasContent then
                MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true) |> Some
            else
                None

    /// Common function for getting the 'all' version of text object motion values.  Used for
    /// sentences, paragraphs and sections.  Not used for word motions because they have too many
    /// corner cases to consider due to the line based nature
    member x.AllTextObjectCommon getObjects count = 

        let all = getObjects SearchPath.Forward x.CaretPoint
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
                        getObjects SearchPath.Backward point
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
        let searchColumn = 
            let mutable column = x.CaretColumn
            while _textObjectUtil.IsSentenceWhiteSpace sentenceKind column && not column.IsEndColumn do
                column <- column.Add 1
            column

        let sentences = x.GetSentences sentenceKind SearchPath.Forward searchColumn |> Seq.truncate count |> List.ofSeq

        let span = 
            match sentences with
            | [] ->
                // Corner case where there are simply no objects going forward.  Return
                // an empty span
                SnapshotSpan(x.CaretPoint, 0)
            | head :: tail ->

                let span = 
                    let last = List.item (sentences.Length - 1) sentences
                    SnapshotSpan(head.Start, last.End)

                // The 'as' motion considers anything between the SnapshotSpan of a sentence to
                // be white space.  So the caret is in white space if it occurs before the sentence
                let isCaretInWhiteSpace = x.CaretPoint.Position < span.Start.Position

                // The white space after the sentence is the gap between this sentence and the next
                // sentence
                let whiteSpaceAfter =
                    let mutable column = SnapshotColumn(span.End)
                    while not (_textObjectUtil.IsSentenceStart sentenceKind column) && not column.IsEndColumn do
                        column <- column.Add 1

                    if column.IsEndColumn || span.End.Position = column.StartPoint.Position then
                        None
                    else
                        Some column

                // Include the preceding white space in the Span
                let includePrecedingWhiteSpace () =
                    let mutable column = SnapshotColumn(span.Start)
                    let mutable before = 
                        if column.IsStartColumn then column
                        else column.Subtract 1
                    while column.StartPoint.Position > 0 && _textObjectUtil.IsSentenceWhiteSpace sentenceKind before do
                        column <- before
                        before <- column.Subtract 1

                    SnapshotSpan(column.StartPoint, span.End)

                // Now we need to do the standard adjustments listed at the bottom of 
                // ':help text-objects'.
                match isCaretInWhiteSpace, whiteSpaceAfter with
                | true, _ -> includePrecedingWhiteSpace()
                | false, None -> includePrecedingWhiteSpace()
                | false, Some spaceEnd -> SnapshotSpan(span.Start, spaceEnd.StartPoint)

        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true)

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
            _wordUtil.GetWords kind SearchPath.Forward contextPoint
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
                let last = List.item (all.Length - 1) all
                SnapshotSpan(firstSpan.Start, last.End)

            // Calculate the white space after the last item.  Line breaks shouldn't be included
            // in the calculation
            let whiteSpaceAfter = 
                let endPoint = 
                    span.End
                    |> SnapshotPointUtil.GetPoints SearchPath.Forward
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
                        |> SnapshotPointUtil.GetPoints SearchPath.Backward
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
                        _wordUtil.GetWords kind SearchPath.Backward span.Start
                        |> Seq.filter isOnSameLine
                        |> Seq.map SnapshotSpanUtil.GetEndPoint
                        |> SeqUtil.headOrDefault span.Start
                    SnapshotSpan(startPoint, span.End)

                | false, Some spaceSpan -> 
                    SnapshotSpan(span.Start, spaceSpan.End)

            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true) |> Some

    member x.BeginingOfLine() = 
        let start = x.CaretPoint
        let line = SnapshotPointUtil.GetContainingLine start
        let span = SnapshotSpan(line.Start, start)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false)

    /// Search for the specified char in the given direction.
    member x.CharSearch c count charSearch direction = 
        // Save the last search value
        _vimData.LastCharSearch <- Some (charSearch, direction, c)

        x.CharSearchCore c count charSearch direction false

    /// Do the actual char search motion but don't update the 'LastCharSearch' value
    member x.CharSearchCore c count charSearch direction isRepeated =

        // See vim ':help cpo-;'.
        let repeatTillAlwaysMoves = true

        let forward () = 
            let startPoint = x.CaretPoint
            let endPoint = x.CaretLine.End
            if startPoint.Position < endPoint.Position then
                let startPoint = SnapshotPointUtil.AddOne startPoint
                let startPoint =
                    if
                        repeatTillAlwaysMoves &&
                        charSearch = CharSearchKind.TillChar &&
                        isRepeated &&
                        startPoint.Position < endPoint.Position
                    then
                        SnapshotPointUtil.AddOne startPoint
                    else
                        startPoint
                SnapshotSpan(startPoint, endPoint)
                |> SnapshotSpanUtil.GetPoints SearchPath.Forward
                |> Seq.filter (SnapshotPointUtil.IsChar c)
                |> SeqUtil.skipMax (count - 1)
                |> SeqUtil.tryHeadOnly
            else
                None

        let backward () = 
            let startPoint = x.CaretLine.Start
            let endPoint = x.CaretPoint
            let endPoint =
                if
                    repeatTillAlwaysMoves &&
                    charSearch = CharSearchKind.TillChar &&
                    isRepeated &&
                    endPoint.Position > startPoint.Position
                then
                    SnapshotPointUtil.SubtractOne endPoint
                else
                    endPoint
            SnapshotSpan(startPoint, endPoint)
            |> SnapshotSpanUtil.GetPoints SearchPath.Backward
            |> Seq.filter (SnapshotPointUtil.IsChar c)
            |> SeqUtil.skipMax (count - 1)
            |> SeqUtil.tryHeadOnly

        let option = 
            match charSearch, direction with
            | CharSearchKind.ToChar, SearchPath.Forward -> 
                forward () |> Option.map (fun point ->
                    let endPoint = SnapshotPointUtil.AddOneOrCurrent point
                    let span = SnapshotSpan(x.CaretPoint, endPoint)
                    span, MotionKind.CharacterWiseInclusive)
            | CharSearchKind.TillChar, SearchPath.Forward -> 
                forward () |> Option.map (fun point ->
                    let span = SnapshotSpan(x.CaretPoint, point)
                    span, MotionKind.CharacterWiseInclusive)
            | CharSearchKind.ToChar, SearchPath.Backward ->
                backward () |> Option.map (fun point ->
                    let span = SnapshotSpan(point, x.CaretPoint)
                    span, MotionKind.CharacterWiseExclusive)
            | CharSearchKind.TillChar, SearchPath.Backward ->
                backward () |> Option.map (fun point ->
                    let point = SnapshotPointUtil.AddOne point
                    let span = SnapshotSpan(point, x.CaretPoint)
                    span, MotionKind.CharacterWiseExclusive)

        match option with 
        | None -> None
        | Some (span, motionKind) -> 
            let isForward = match direction with | SearchPath.Forward -> true | SearchPath.Backward -> false
            MotionResult.Create(span, motionKind, isForward) |> Some

    /// Repeat the last f, F, t or T search pattern.
    member x.RepeatLastCharSearch count =
        match _vimData.LastCharSearch with 
        | None -> None
        | Some (kind, direction, c) -> x.CharSearchCore c count kind direction true

    /// Repeat the last f, F, t or T search pattern in the opposite direction
    member x.RepeatLastCharSearchOpposite count =
        match _vimData.LastCharSearch with 
        | None -> None
        | Some (kind, direction, c) -> 
            let direction = 
                match direction with
                | SearchPath.Forward -> SearchPath.Backward
                | SearchPath.Backward -> SearchPath.Forward
            x.CharSearchCore c count kind direction true

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
            _wordUtil.GetWords kind SearchPath.Forward x.CaretPoint
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)

        let endPoint = 
            match motionContext with
            | MotionContext.Movement ->
                SnapshotPointUtil.GetPointOrEndPointOfLastLine endPoint
            | MotionContext.AfterOperator -> 
                // If the word motion comes after an operator and ends on the first word 
                // of a different line, then the motion is moved back to the last line containing
                // a word
                let endLine = SnapshotPointUtil.GetContainingLine endPoint
                let isFirstNonBlank = SnapshotLineUtil.GetFirstNonBlankOrStart endLine = endPoint

                if isFirstNonBlank && endLine.LineNumber > x.CaretLine.LineNumber then
                    let previousLine = 
                        SnapshotUtil.GetLines x.CurrentSnapshot (endLine.LineNumber - 1) SearchPath.Backward
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
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true, motionResultFlags = MotionResultFlags.AnyWord)

    member x.WordBackward kind count =

        let startPoint = 
            _wordUtil.GetWords kind SearchPath.Backward x.CaretPoint
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false, motionResultFlags = MotionResultFlags.AnyWord)

    /// Implements the 'ge' and 'gE' motions
    member x.BackwardEndOfWord kind count = 

        // If the caret is currently at the end of the word then we start searching one character
        // back from that point 
        let searchPoint = 
            match _wordUtil.GetWords kind SearchPath.Forward x.CaretPoint |> SeqUtil.tryHeadOnly with
            | None -> x.CaretPoint
            | Some span -> span.Start

        let words = _wordUtil.GetWords kind SearchPath.Backward searchPoint 
        let startPoint = 
            match words |> Seq.skip (count - 1) |> SeqUtil.tryHeadOnly with
            | None -> SnapshotPoint(x.CurrentSnapshot, 0)
            | Some span -> SnapshotSpanUtil.GetLastIncludedPointOrStart span

        let endPoint = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
        let span = SnapshotSpan(startPoint, endPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = false) |> Some

    /// Implements the 'e' and 'E' motions
    member x.EndOfWord kind count = 

        // The start point for an end of word motion is a little odd.  If the caret is 
        // on the last point inside a word then we calculate the next word starting at
        // the end of the first word.
        let searchPoint = 
            match _wordUtil.GetWords kind SearchPath.Forward x.CaretPoint |> SeqUtil.tryHeadOnly with
            | None -> 
                x.CaretPoint
            | Some span -> 
                if SnapshotSpanUtil.IsLastIncludedPoint span x.CaretPoint then
                    span.End
                else
                    x.CaretPoint

        let endPoint = 
            searchPoint
            |> _wordUtil.GetWords kind SearchPath.Forward
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
        MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true)

    member x.EndOfLine count = 
        let start = x.CaretPoint
        let span = SnapshotPointUtil.GetLineRangeSpan start count
        MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true, motionResultFlags = MotionResultFlags.EndOfLine)

    /// Find the first non-whitespace character on the current line.  
    member x.FirstNonBlankOnCurrentLine () =
        x.FirstNonBlankCore x.CaretLine.Extent

    member x.FirstNonBlankCore span = 
        let start = x.CaretPoint
        let target = 
            span 
            |> SnapshotSpanUtil.GetPoints SearchPath.Forward
            |> Seq.tryFind (fun x -> not (CharUtil.IsBlank (x.GetChar())) )
            |> OptionUtil.getOrDefault span.End
        let startPoint, endPoint, isForward = 
            if start.Position <= target.Position then start, target, true
            else target, start, false
        let span = SnapshotSpan(startPoint, endPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward)

    /// Create a line wise motion from the current line to (count - 1) lines
    /// downward 
    member x.FirstNonBlankOnLine count = 
        x.MotionWithVisualSnapshot (fun x -> 
            let startLine = x.CaretLine
            let endLine = 
                let number = startLine.LineNumber + (count - 1)
                SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
            let column = SnapshotLineUtil.GetFirstNonBlankOrStart endLine |> SnapshotPointUtil.GetLineOffset |> CaretColumn.InLastLine
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            MotionResult.CreateLineWise(range.ExtentIncludingLineBreak, isForward = true, caretColumn = column))

    /// Get the all tag motion
    member x.TagBlock count point kind = 

        // Get the initial tag block
        let mutable tagBlock = TagBlockUtil.GetTagBlockForPoint point 

        // The 'count' operation is implemented by just walking the parent until 
        // we hit (count - 1) parent values.  If we walk past the end of the parent
        // chain then the motion must fail 
        for i = 1 to (count - 1) do
            tagBlock <- tagBlock |> OptionUtil.map2 (fun t -> t.Parent) 

        match tagBlock with
        | None -> None
        | Some tagBlock ->
            let span = 
                match kind with
                | TagBlockKind.All -> tagBlock.FullSpan
                | TagBlockKind.Inner -> 
                    if tagBlock.InnerSpan.IsEmpty && point.Position = tagBlock.InnerSpan.Start then
                        tagBlock.FullSpan
                    else
                        tagBlock.InnerSpan
            let span = SnapshotSpan(x.CurrentSnapshot, span)
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true, motionResultFlags = MotionResultFlags.SuppressAdjustment) |> Some

    /// Get the expanded tag block based on the current kind and point
    member x.GetExpandedTagBlock startPoint (endPoint: SnapshotPoint) kind: SnapshotSpan option = 
        match TagBlockUtil.GetTagBlockForPoint startPoint with
        | None -> None
        | Some tagBlock -> 
            let span = 
                match kind with
                | TagBlockKind.All -> 
                    if tagBlock.InnerSpan.Contains startPoint.Position then
                        Some tagBlock.FullSpan
                    else
                        tagBlock.Parent |> Option.map (fun t -> t.FullSpan)
                | TagBlockKind.Inner ->
                    if tagBlock.InnerSpan.Contains startPoint.Position then
                        if tagBlock.InnerSpan = Span.FromBounds(startPoint.Position, endPoint.Position) then
                            Some tagBlock.FullSpan
                        else
                            Some tagBlock.InnerSpan
                    else
                        tagBlock.Parent |> Option.map (fun t -> if t.InnerSpan = tagBlock.FullSpan then t.FullSpan else t.InnerSpan)

            span |> Option.map (fun s -> SnapshotSpan(startPoint.Snapshot, s))


    /// An inner block motion is just the all block motion with the start and 
    /// end character removed 
    member x.InnerBlock contextPoint blockKind count =
        match x.GetBlockWithCount blockKind contextPoint count with
        | Some (openPoint, closePoint) ->
            let snapshot = openPoint.Snapshot
            let openLine = openPoint.GetContainingLine()
            let closeLine = closePoint.GetContainingLine()

            let isOpenLineWise = openPoint.Position + 1 = openLine.End.Position 
            let isCloseLineWise = 
                SnapshotSpan(closeLine.Start, closePoint)
                |> SnapshotSpanUtil.GetPoints SearchPath.Forward
                |> Seq.forall SnapshotPointUtil.IsBlank

            let span, motionKind =
                if isOpenLineWise && isCloseLineWise then
                    if openLine.LineNumber + 1 > closeLine.LineNumber - 1 then
                        let span = new SnapshotSpan(contextPoint, 0)
                        span, MotionKind.CharacterWiseInclusive
                    else
                        let range = SnapshotLineRangeUtil.CreateForLineNumberRange snapshot (openLine.LineNumber + 1) (closeLine.LineNumber - 1)
                        range.ExtentIncludingLineBreak, MotionKind.LineWise
                elif isOpenLineWise then
                    let line = SnapshotUtil.GetLine snapshot (openLine.LineNumber + 1)
                    let span = SnapshotSpan(line.Start, closePoint)
                    span, MotionKind.CharacterWiseInclusive
                elif isCloseLineWise then
                    let line = SnapshotUtil.GetLine snapshot (closeLine.LineNumber - 1)
                    let span = SnapshotSpan(openPoint.Add(1), line.End)
                    span, MotionKind.CharacterWiseInclusive
                else
                    let span = SnapshotSpan(openPoint.Add(1), closePoint)
                    span, MotionKind.CharacterWiseInclusive

            MotionResult.Create(span, motionKind, isForward = true) |> Some
        | None -> None
                
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
                |> SnapshotSpanUtil.GetPoints SearchPath.Backward
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
        let getSpan (startPoint: SnapshotPoint) point count =
            Contract.Assert(not (SnapshotPointUtil.IsInsideLineBreak point))

            let rec inner (wordSpan: SnapshotSpan) (remainingWords: SnapshotSpan list) count = 
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
                _wordUtil.GetWords wordKind SearchPath.Forward point
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
        | Some span -> MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true, motionResultFlags = MotionResultFlags.AnyWord) |> Some

    /// Implementation of the 'ip' motion. Blank and empty lines are counted together.
    member x.InnerParagraph count =
        let isWhiteSpace point =
            let line = SnapshotPointUtil.GetContainingLine point
            System.String.IsNullOrWhiteSpace(line.GetText())

        let startPoint =
            x.CaretPoint
            |> SnapshotPointUtil.GetPointsIncludingLineBreak SearchPath.Backward
            |> Seq.takeWhile (fun point -> isWhiteSpace x.CaretPoint = isWhiteSpace point)
            |> SeqUtil.lastOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)

        let endPoint =
            let rec ep pt counter =
                let lastCount = counter = count
                let isEnd point = SnapshotPointUtil.IsEndPoint point
                let point =
                    pt
                    |> SnapshotPointUtil.GetPointsIncludingLineBreak SearchPath.Forward
                    |> (fun s -> Seq.concat [s; (Seq.singleton (SnapshotUtil.GetEndPoint pt.Snapshot))] )
                    |> Seq.skipWhile (fun point -> isWhiteSpace pt = isWhiteSpace point && not (lastCount && isEnd point))
                    |> SeqUtil.tryHeadOnly
                if point.IsNone then None
                elif lastCount then Some point.Value
                else
                    let nextStartPoint = if SnapshotPointUtil.IsInsideLineBreak point.Value then point.Value
                                         else SnapshotPointUtil.AddOneOrCurrent point.Value
                    ep nextStartPoint (counter + 1)
            ep x.CaretPoint 1

        match endPoint with
        | None -> None
        | Some endPoint ->
            let snapshot = SnapshotSpan(startPoint, endPoint)
            MotionResult.Create(snapshot, MotionKind.CharacterWiseExclusive, isForward = true) |> Some

    /// Implements the '+', '<CR>', 'CTRL-M' motions. 
    ///
    /// This is a line wise motion which uses counts hence we must use the visual snapshot
    /// when calculating the value
    member x.LineDownToFirstNonBlank count =
        x.MotionWithVisualSnapshot (fun x ->
            let number = x.CaretLine.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast x.CurrentSnapshot number
            let column = SnapshotLineUtil.GetFirstNonBlankOrStart endLine |> SnapshotPointUtil.GetLineOffset |> CaretColumn.InLastLine
            let span = SnapshotSpan(x.CaretLine.Start, endLine.EndIncludingLineBreak)
            MotionResult.CreateLineWise(span, isForward = true, caretColumn = column))

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
                |> SnapshotPointUtil.GetLineOffset
                |> CaretColumn.InLastLine
            MotionResult.CreateLineWise(span, isForward = false, caretColumn = column))

    /// Implements the '|'
    ///
    /// Get the motion which is to the 'count'-th column on the current line.
    member x.LineToColumn count =
        x.MotionWithVisualSnapshot (fun x ->
            let count = count - 1
            let targetColumn = _commonOperations.GetColumnForSpacesOrEnd x.CaretLine count
            let isForward = targetColumn.StartPoint.Difference(x.CaretPoint) < 0
            let span = 
                if isForward then SnapshotSpan(x.CaretPoint, targetColumn.StartPoint)
                else SnapshotSpan(targetColumn.StartPoint, x.CaretPoint)
            let column = count |> CaretColumn.ScreenColumn
            MotionResult.CreateCharacterWise(span, isExclusive = true, isForward = isForward, caretColumn = column))

    /// Get the motion which is 'count' characters to the left of the caret on
    /// the same line
    member x.CharLeftOnSameLine count =
        let endColumn = x.CaretVirtualColumn
        if _vimTextBuffer.UseVirtualSpace && endColumn.IsInVirtualSpace then
            if count < endColumn.VirtualSpaces then

                // We are just moving in virtual space.
                let startColumn = endColumn.SubtractInLine count
                let columnNumber = startColumn.VirtualColumnNumber
                let span = VirtualSnapshotColumnSpan(startColumn, endColumn)
                MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = false, motionResultFlags = MotionResultFlags.None, caretColumn = CaretColumn.InLastLine columnNumber)
            else

                // Move from virtual space to real space.
                let rest = count - endColumn.VirtualSpaces
                let startColumn = 
                    match endColumn.Column.TrySubtractInLine rest with
                    | Some column -> column
                    | None -> SnapshotColumn(x.CaretLine)
                let span = VirtualSnapshotColumnSpan(startColumn, endColumn)
                MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = false)
        else
            let startColumn = 
                match x.CaretColumn.TrySubtractInLine count with
                | Some column -> column
                | None -> SnapshotColumn(x.CaretLine)
            let span = SnapshotSpan(startColumn.StartPoint, x.CaretPoint)
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false)

    /// Get the motion which is 'count' characters to the right of the caret 
    /// on the same line
    member x.CharRightOnSameLine count =
        if _vimTextBuffer.UseVirtualSpace then
            x.CharRightVirtual count
        else
            let endPoint =
                if SnapshotPointUtil.IsInsideLineBreak x.CaretPoint then
                    x.CaretPoint
                elif x.CaretPoint.Position + 1 = x.CaretLine.End.Position then
                    x.CaretLine.End
                else
                    match x.CaretColumn.TryAddInLine(count, includeLineBreak = true) with
                    | Some column -> column.StartPoint
                    | None -> x.CaretLine.End
            let span = SnapshotSpan(x.CaretPoint, endPoint)
            MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true)

    /// Get the motion which is 'count' characters before the caret
    /// through the buffer taking into acount 'virtualedit'
    member x.CharLeftWithLineWrap count =
        let endColumn = x.CaretVirtualColumn
        if _vimTextBuffer.UseVirtualSpace && endColumn.IsInVirtualSpace then
            if count < endColumn.VirtualSpaces then

                // We are just moving in virtual space.
                let startColumn = endColumn.SubtractInLine count
                let span = VirtualSnapshotColumnSpan(startColumn, endColumn)
                MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = false, motionResultFlags = MotionResultFlags.None, caretColumn = CaretColumn.InLastLine startColumn.VirtualColumnNumber)
            else

                // Move from virtual space to real space.
                let rest = count - endColumn.VirtualSpaces
                let skipLineBreaks = not _globalSettings.IsVirtualEditOneMore
                let startColumn = SnapshotPointUtil.GetRelativeColumn endColumn.Column -rest skipLineBreaks
                let span = VirtualSnapshotColumnSpan(startColumn, endColumn)
                MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = false)
        else
            let skipLineBreaks = not _globalSettings.IsVirtualEditOneMore
            let startColumn = SnapshotPointUtil.GetRelativeColumn x.CaretColumn -count skipLineBreaks
            let span = SnapshotColumnSpan(startColumn, x.CaretColumn)
            MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = false)

    /// Get the motion which is 'count' characters after the caret 
    /// through the buffer taking into acount 'virtualedit'
    member x.CharRightWithLineWrap count =
        if _vimTextBuffer.UseVirtualSpace then
            x.CharRightVirtual count
        else
            let skipLineBreaks = not _globalSettings.IsVirtualEditOneMore
            let endColumn = SnapshotPointUtil.GetRelativeColumn x.CaretColumn count skipLineBreaks
            let span = SnapshotColumnSpan(x.CaretColumn, endColumn)
            MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = true)

    member x.CharRightVirtual count =
        let startColumn = x.CaretVirtualColumn
        let endColumn = startColumn.AddInLine count
        let span = VirtualSnapshotColumnSpan(startColumn, endColumn)
        if endColumn.IsInVirtualSpace then
            MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive, isForward = true, motionResultFlags = MotionResultFlags.None, caretColumn = CaretColumn.InLastLine endColumn.VirtualColumnNumber)
        else
            MotionResult.Create(span.Span, MotionKind.CharacterWiseExclusive)

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
            if x.CaretLine.LineNumber = 0 then None
            else
                let startLine = SnapshotUtil.GetLineOrFirst x.CurrentSnapshot (x.CaretLine.LineNumber - count)
                let columnNumber =
                    if _vimTextBuffer.UseVirtualSpace then
                        x.CaretVirtualColumn.VirtualColumnNumber
                    else
                        x.CaretColumn.ColumnNumber
                let characterSpan =
                    let s = SnapshotColumn.GetForColumnNumberOrEnd(startLine, columnNumber)
                    let e = x.CaretColumn.AddOneOrCurrent()
                    SnapshotColumnSpan(s, e)
                let span = SnapshotSpan(startLine.Start, x.CaretLine.EndIncludingLineBreak)
                MotionResult.CreateLineWise(
                    span,
                    spanBeforeLineWise = characterSpan.Span,
                    isForward = false,
                    motionResultFlags = MotionResultFlags.MaintainCaretColumn,
                    caretColumn = CaretColumn.InLastLine columnNumber) |> Some)

    /// Move a single line down from the current line.  Should fail if we are currenly 
    /// on the last line of the ITextBuffer
    member x.LineDown count = 
        x.MotionOptionWithVisualSnapshot (fun x -> 
            if x.CaretLine.LineNumber = SnapshotUtil.GetLastLineNumber x.CurrentSnapshot then None
            else
                let lineNumber = x.CaretLine.LineNumber + count
                let lastLine = SnapshotUtil.GetLineOrLast x.CurrentSnapshot lineNumber
                let columnNumber =
                    if _vimTextBuffer.UseVirtualSpace then
                        x.CaretVirtualColumn.VirtualColumnNumber
                    else
                        x.CaretColumn.ColumnNumber
                let characterSpan = 
                    let e = SnapshotColumn.GetForColumnNumberOrEnd(lastLine, columnNumber)
                    let e = e.AddOneOrCurrent()
                    SnapshotColumnSpan(x.CaretColumn, e)
                let span = SnapshotSpan(x.CaretLine.Start, lastLine.EndIncludingLineBreak)
                MotionResult.CreateLineWise(
                    span, 
                    spanBeforeLineWise = characterSpan.Span,
                    isForward = true,
                    motionResultFlags = MotionResultFlags.MaintainCaretColumn,
                    caretColumn = CaretColumn.InLastLine columnNumber) |> Some)

    /// Get the appropriate maintain caret column flag taking into account 'startofline'
    member x.GetMaintainCaretColumnFlag () =
        if _globalSettings.StartOfLine then
            MotionResultFlags.None
        else
            MotionResultFlags.MaintainCaretColumn

    /// Implements the 'gg' motion.  
    ///
    /// Because this uses specific line numbers instead of counts we don't want to operate
    /// on the visual buffer here as vim line numbers always apply to the edit buffer. 
    member x.LineOrFirstToFirstNonBlank numberOpt = 
        _jumpList.Add x.CaretVirtualPoint

        let endLine = 
            match numberOpt with
            | Some number ->  SnapshotUtil.GetLineOrLast x.CurrentSnapshot (Util.VimLineToTssLine number)
            | None -> SnapshotUtil.GetFirstLine x.CurrentSnapshot
        let flags = x.GetMaintainCaretColumnFlag()
        x.LineToLineFirstNonBlankMotion flags x.CaretLine endLine

    /// Implements the 'G' motion
    ///
    /// Because this uses specific line numbers instead of counts we don't want to operate
    /// on the visual buffer here as vim line numbers always apply to the edit buffer. 
    member x.LineOrLastToFirstNonBlank numberOpt = 
        _jumpList.Add x.CaretVirtualPoint

        let endLine = 
            match numberOpt with
            | Some number ->  SnapshotUtil.GetLineOrLast x.CurrentSnapshot (Util.VimLineToTssLine number)
            | None -> SnapshotUtil.GetLastNormalizedLine x.CurrentSnapshot 
        let flags = x.GetMaintainCaretColumnFlag()
        x.LineToLineFirstNonBlankMotion flags x.CaretLine endLine

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
                    |> SnapshotSpanUtil.GetPoints SearchPath.Backward
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
            MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward))

    // Line from the top of the visual buffer
    member x.LineFromTopOfVisibleWindow countOpt = 
        _jumpList.Add x.CaretVirtualPoint 

        match TextViewUtil.GetVisibleVisualSnapshotLineRange _textView with 
        | NullableUtil.Null -> None
        | NullableUtil.HasValue range -> 
            if range.Count = 0 then
                None
            else

                // The minimum distance from the top of the window is zero if
                // the top of the buffer is visible and scrolloff otherwise.
                let minCount =
                    if range.StartLineNumber = 0 then 0
                    else _globalSettings.ScrollOffset
                let count = (Util.CountOrDefault countOpt) - 1
                let count = max count minCount
                let count = min count (range.Count - 1)
                let visualLine = SnapshotUtil.GetLine range.Snapshot (count + range.StartLineNumber)
                match BufferGraphUtil.MapPointDownToSnapshotStandard _textView.BufferGraph visualLine.Start x.CurrentSnapshot with
                | None -> None
                | Some point -> 
                    let line = SnapshotPointUtil.GetContainingLine point
                    let span, isForward = x.SpanAndForwardFromLines x.CaretLine line
                    let flags = x.GetMaintainCaretColumnFlag()
                    let caretColumn = x.GetCaretColumnOfLine line
                    MotionResult.Create(span, MotionKind.LineWise, isForward, flags, caretColumn)
                    |> x.ApplyStartOfLineOption
                    |> Some

    // Line from the top of the visual buffer
    member x.LineFromBottomOfVisibleWindow countOpt =
        _jumpList.Add x.CaretVirtualPoint 

        match TextViewUtil.GetVisibleVisualSnapshotLineRange _textView with
        | NullableUtil.Null -> None
        | NullableUtil.HasValue range -> 
            if range.Count = 0 then 
                None
            else

                // The minimum distance from the bottom of the window is zero if
                // the bottom of the buffer is visible and scrolloff otherwise.
                let minCount =
                    let snapshot = _textView.TextSnapshot
                    let lastLineNumber = SnapshotUtil.GetLastNormalizedLineNumber snapshot
                    if range.LastLineNumber >= lastLineNumber then 0
                    else _globalSettings.ScrollOffset
                let count = (Util.CountOrDefault countOpt) - 1
                let count = max count minCount
                let count = min count (range.Count - 1)
                let number = range.LastLineNumber - count
                let visualLine = SnapshotUtil.GetLine range.Snapshot number
                match BufferGraphUtil.MapPointDownToSnapshotStandard _textView.BufferGraph visualLine.Start x.CurrentSnapshot with
                | None -> None
                | Some point -> 
                    let line = SnapshotPointUtil.GetContainingLine point
                    let span, isForward = x.SpanAndForwardFromLines x.CaretLine line
                    let flags = x.GetMaintainCaretColumnFlag()
                    let caretColumn = x.GetCaretColumnOfLine line
                    MotionResult.Create(span, MotionKind.LineWise, isForward, flags, caretColumn)
                    |> x.ApplyStartOfLineOption
                    |> Some

    /// Motion to put the caret in the middle of the visible window.  
    member x.LineInMiddleOfVisibleWindow () =
        _jumpList.Add x.CaretVirtualPoint 

        match TextViewUtil.GetVisibleVisualSnapshotLineRange _textView with
        | NullableUtil.Null -> None
        | NullableUtil.HasValue range -> 
            if range.Count = 0 then
                None
            else
                let number = (range.Count / 2) + range.StartLineNumber
                let middleVisualLine = SnapshotUtil.GetLine range.Snapshot number
                let middleLine = 
                    match BufferGraphUtil.MapPointDownToSnapshotStandard _textView.BufferGraph middleVisualLine.Start x.CurrentSnapshot with
                    | None -> x.CaretLine
                    | Some point -> SnapshotPointUtil.GetContainingLine point

                let span, isForward = x.SpanAndForwardFromLines x.CaretLine middleLine
                let flags = x.GetMaintainCaretColumnFlag()
                let caretColumn = x.GetCaretColumnOfLine middleLine
                MotionResult.Create(span, MotionKind.LineWise, isForward, flags, caretColumn)
                |> x.ApplyStartOfLineOption
                |> Some

    /// Implements the core portion of section backward motions
    member x.SectionBackwardCore sectionKind count = 
        _jumpList.Add x.CaretVirtualPoint

        let startPoint = 
            x.CaretPoint
            |> x.GetSections sectionKind SearchPath.Backward
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)

        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false)

    /// Implements the ']]' operator
    member x.SectionForward context count = 
        let split = 
            match context with
            | MotionContext.AfterOperator -> SectionKind.OnOpenBraceOrBelowCloseBrace
            | MotionContext.Movement -> SectionKind.OnOpenBrace
        x.SectionForwardCore split context count

    /// Implements the core parts of section forward operators
    member x.SectionForwardCore sectionKind context count =
        _jumpList.Add x.CaretVirtualPoint

        let endPoint = 
            x.CaretPoint
            |> x.GetSections sectionKind SearchPath.Forward
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
                let line = SnapshotPointUtil.GetContainingLineOrLast endPoint
                if SnapshotLineUtil.IsLastNormalizedLine line then 
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

        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward)

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
        _jumpList.Add x.CaretVirtualPoint
        let endPoint =
            x.GetSentences SentenceKind.Default SearchPath.Forward x.CaretColumn
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true, motionResultFlags = MotionResultFlags.BigDelete)

    member x.SentenceBackward count = 
        _jumpList.Add x.CaretVirtualPoint
        let startPoint = 
            x.GetSentences SentenceKind.Default SearchPath.Backward x.CaretColumn
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false, motionResultFlags = MotionResultFlags.BigDelete)

    /// Implements the '}' motion
    member x.ParagraphForward count = 
        _jumpList.Add x.CaretVirtualPoint

        let endPoint = 
            x.GetParagraphs SearchPath.Forward x.CaretColumn
            |> SeqUtil.skipMax count
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetEndPoint x.CurrentSnapshot)
        let span = SnapshotSpan(x.CaretPoint, endPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = true, motionResultFlags = MotionResultFlags.BigDelete)

    /// Implements the '{' motion
    member x.ParagraphBackward count = 
        _jumpList.Add x.CaretVirtualPoint

        let startPoint = 
            x.GetParagraphs SearchPath.Backward x.CaretColumn
            |> SeqUtil.skipMax (count - 1)
            |> Seq.map SnapshotSpanUtil.GetStartPoint
            |> SeqUtil.headOrDefault (SnapshotUtil.GetStartPoint x.CurrentSnapshot)
        let span = SnapshotSpan(startPoint, x.CaretPoint)
        MotionResult.Create(span, MotionKind.CharacterWiseExclusive, isForward = false, motionResultFlags = MotionResultFlags.BigDelete)

    member x.QuotedString quoteChar = 
        match x.GetQuotedStringData quoteChar with
        | None -> None 
        | Some data -> 
            let span = 
                if not data.TrailingWhiteSpace.IsEmpty then
                    SnapshotSpanUtil.Create data.LeadingQuote data.TrailingWhiteSpace.End
                else 
                    SnapshotSpanUtil.Create data.LeadingWhiteSpace.Start data.TrailingWhiteSpace.Start
            MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true) |> Some

    member x.QuotedStringContentsWithCount quoteChar count = 
        let QuotedStringContents quoteChar = 
            match x.GetQuotedStringData quoteChar with
            | None -> None 
            | Some data ->
                let span = data.Contents
                MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true) |> Some

        if count > 1 then
          x.QuotedStringWithoutSpaces quoteChar
        else
          QuotedStringContents quoteChar

    member x.QuotedStringWithoutSpaces quoteChar = 
        match x.GetQuotedStringData quoteChar with
        | None -> None 
        | Some data -> 
            let span = SnapshotSpanUtil.Create data.LeadingQuote data.TrailingWhiteSpace.Start
            MotionResult.Create(span, MotionKind.CharacterWiseInclusive, isForward = true) |> Some

    /// Get the motion for a search command.  Used to implement the '/' and '?' motions
    member x.Search (searchData: SearchData) count = 

        // Searching as part of a motion should update the last pattern information
        // irrespective of whether or not the search completes
        _vimData.LastSearchData <- searchData.LastSearchData

        x.SearchCore searchData x.CaretPoint count

    /// Implements the core searching motion.  Will *not* update the LastPatternData 
    /// value.  Simply performs the search and returns the result
    member x.SearchCore (searchData: SearchData) searchPoint count =

        // All search operations update the jump list
        _jumpList.Add x.CaretVirtualPoint

        // The search operation should also update the search history
        _vimData.SearchHistory.Add searchData.Pattern

        let searchResult = _search.FindNextPattern searchPoint searchData _wordNavigator count

        // Raise the messages that go with this given result
        CommonUtil.RaiseSearchResultMessage _statusUtil searchResult

        let motionResult = 
            match searchResult with
            | SearchResult.Error _ -> None
            | SearchResult.NotFound _ -> None
            | SearchResult.Cancelled _ -> None
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
                        caretColumn <- CaretColumn.InLastLine (SnapshotPointUtil.GetLineOffset span.Start)
                | SearchOffsetData.End _ ->
                    endPoint <- SnapshotPointUtil.AddOneOrCurrent endPoint
                    motionKind <- MotionKind.CharacterWiseInclusive 
                | _ -> ()
                    
                if startPoint.Position = endPoint.Position then
                    None
                else
                    let span = SnapshotSpan(startPoint, endPoint)
                    MotionResult.Create(span, motionKind, isForward, MotionResultFlags.BigDelete, caretColumn) |> Some

        _vimData.ResumeDisplayPattern()
        motionResult

    /// Move the caret to the next occurrence of the last search
    member x.LastSearch isReverse count =
        let last = _vimData.LastSearchData
        if StringUtil.IsNullOrEmpty last.Pattern then
            _statusUtil.OnError Resources.NormalMode_NoPreviousSearch
            None
        else
            let path = 
                if isReverse then SearchPath.Reverse last.Path
                else last.Path
            let searchKind = SearchKind.OfPathAndWrap path _globalSettings.WrapScan
            let searchData = SearchData(last.Pattern, last.Offset, searchKind, last.Options)
            x.SearchCore searchData x.CaretPoint count

    /// Motion from the caret to the nearest lowercase mark
    member x.NextMark path count =
        match path with
        | SearchPath.Forward -> x.NearestMark count true
        | SearchPath.Backward -> x.NearestMark -count true

    /// Motion from the caret to the nearest lowercase mark line
    member x.NextMarkLine path count =
        match path with
        | SearchPath.Forward -> x.NearestMark count false
        | SearchPath.Backward -> x.NearestMark -count false

    /// Motion to the nth nearest mark, possibly exactly
    member x.NearestMark count exact =
        let isForward = count > 0

        // Choose a starting position at the beginning or end of the caret line.
        let caretLine = x.CaretLine
        let caretPoint = x.CaretPoint
        let startPoint =
            if exact then
                caretPoint
            else
                if isForward then caretLine.End else caretLine.Start
        let startPosition = startPoint.Position

        let getLocalMark localMark =
            let mark = Mark.LocalMark localMark
            _vimBufferData.Vim.MarkMap.GetMark mark _vimBufferData

        // Determine the relative offsets of all the lowercase marks
        // in the file, sorted from most negative to most positive.
        let markOffsets =
            seq {
                for letter in Letter.All do
                    yield LocalMark.Letter letter
            }
            |> Seq.map (fun localMark -> localMark, getLocalMark localMark)
            |> Seq.filter (fun (_, option) -> option.IsSome)
            |> Seq.map (fun (mark, option) -> mark, option.Value)
            |> Seq.map (fun (mark, virtualPoint) -> mark, (virtualPoint.Position.Position - startPosition))
            |> Seq.sortBy (fun (_, markOffset) -> markOffset)

        // Try to find a mark offset to jump to.
        let markOffset =
            let candidates =
                if isForward then

                    // If going foreward, look at positive offsets.
                    markOffsets
                    |> Seq.filter (fun (_, markOffset) -> markOffset > 0)
                    |> Seq.toList
                else

                    // If going backward, reverse the sequence and look at negative offsets.
                    markOffsets
                    |> Seq.rev
                    |> Seq.filter (fun (_, markOffset) -> markOffset < 0)
                    |> Seq.toList

            // Skip past the specified number of marks, or as many as possible.
            let skipCount = (min (max 0 (candidates.Length - 1)) ((abs count) - 1))
            candidates
            |> Seq.skip skipCount
            |> Seq.tryHead

        // Try to create a motion (possibly exactly) to that mark.
        match markOffset with
        | None ->
            None
        | Some (mark, _) ->
            if exact then
                x.Mark mark
            else
                x.MarkLine mark

    /// Operate on the next match for last pattern searched for
    member x.NextMatch path count =
        let last = _vimData.LastSearchData
        if StringUtil.IsNullOrEmpty last.Pattern then
            _statusUtil.OnError Resources.NormalMode_NoPreviousSearch
            None
        else
            let searchKind = SearchKind.OfPathAndWrap path _globalSettings.WrapScan
            let options = last.Options ||| SearchOptions.IncludeStartPoint
            let searchData = SearchData(last.Pattern, last.Offset, searchKind, options)

            // All search operations update the jump list.
            _jumpList.Add x.CaretVirtualPoint

            let searchResult = _search.FindNextPattern x.CaretPoint searchData _wordNavigator count

            // Raise the messages that go with this given result.
            CommonUtil.RaiseSearchResultMessage _statusUtil searchResult

            let motionResult = 
                match searchResult with
                | SearchResult.Error _ -> None
                | SearchResult.NotFound _ -> None
                | SearchResult.Cancelled _ -> None
                | SearchResult.Found (_, span, _, _) ->

                    let motionKind = MotionKind.CharacterWiseExclusive
                    let isForward = path = SearchPath.Forward
                    MotionResult.Create(span, motionKind, isForward) |> Some

            _vimData.ResumeDisplayPattern()
            motionResult

    /// Motion from the caret to the next occurrence of the partial word under the caret
    member x.NextPartialWord path count =
        x.NextWordCore path count false

    /// Motion from the caret to the next occurrence of the word under the caret
    member x.NextWord path count =
        x.NextWordCore path count true

    /// Motion for word under the caret to the next occurrence of the word
    member x.NextWordCore path count isWholeWord = 

        // Next word motions should update the jump list
        _jumpList.Add x.CaretVirtualPoint

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
            let searchKind = SearchKind.OfPathAndWrap path _globalSettings.WrapScan
            let searchData = SearchData(pattern, SearchOffsetData.None, searchKind, SearchOptions.ConsiderIgnoreCase)

            // Make sure to update the LastSearchData here.  It needs to be done 
            // whether or not the search actually succeeds
            _vimData.LastSearchData <- searchData

            // A word search always starts at the beginning of the word.  The pattern
            // based search will ensure that we don't match this word again because it
            // won't make an initial match at the provided point
            x.SearchCore searchData span.Start count

    /// Force the existing motion to be characterwise under the rules described in :help o_v
    member x.ForceCharacterWise motion motionArgument = 
        let convertMotionResult motionResult = 
            let isInLineBreakOrEnd p = SnapshotPointUtil.IsInsideLineBreak p || SnapshotPointUtil.IsEndPoint p
            match motionResult.MotionKind with
            | MotionKind.CharacterWiseExclusive ->
                // Extend the selection one character unless it goes into the line break. 
                let span = motionResult.Span
                let span = 
                    if isInLineBreakOrEnd span.End then SnapshotSpan(span.Start, 0)
                    else
                        let characterSpan = span.End |> SnapshotPointUtil.GetCharacterSpan
                        SnapshotSpan(span.Start, characterSpan.End)
                MotionResult.Create(span, MotionKind.CharacterWiseInclusive, motionResult.IsForward)
            | MotionKind.CharacterWiseInclusive ->
                // Shrink the selection a single character.
                let span = motionResult.Span
                let span = 
                    if span.IsEmpty then span
                    else
                        let characterSpan = span.End.Subtract(1) |> SnapshotPointUtil.GetCharacterSpan
                        SnapshotSpan(span.Start, characterSpan.Start)
                MotionResult.Create(span, MotionKind.CharacterWiseExclusive, motionResult.IsForward)
            | MotionKind.LineWise ->
                // Need to make this characterwise exclusive
                let span = OptionUtil.getOrDefault motionResult.Span motionResult.SpanBeforeLineWise
                let lineRange = SnapshotLineRangeUtil.CreateForSpan span
                let span = 
                    match span.Length, SnapshotSpanUtil.GetLastIncludedPoint span with
                    | 0, _ -> SnapshotSpan(span.Start, 0)
                    | 1, _ -> SnapshotSpan(span.Start, 0)
                    | _, None -> SnapshotSpan(span.Start, 0)
                    | _, Some p ->
                        if SnapshotLineUtil.IsPhantomLine lineRange.LastLine then SnapshotSpan(span.Start, p)
                        elif SnapshotPointUtil.IsInsideLineBreak p then SnapshotSpan(span.Start, lineRange.LastLine.End)
                        else SnapshotSpan(span.Start, p)
                MotionResult.Create(span, MotionKind.CharacterWiseExclusive, motionResult.IsForward)

        match x.GetMotion motion motionArgument with
        | None -> None
        | Some motionResult -> Some (convertMotionResult motionResult)

    member x.ForceLineWise motion motionArgument =
        let convertCharacter (motionResult: MotionResult) = 
            let lineRange = SnapshotLineRangeUtil.CreateForSpan motionResult.Span
            let motionResult = MotionResult.Create(lineRange.ExtentIncludingLineBreak, MotionKind.LineWise, motionResult.IsForward)
            Some motionResult

        match x.GetMotion motion motionArgument with
        | None -> None
        | Some motionResult -> 
            match motionResult.MotionKind with
            | MotionKind.LineWise -> Some motionResult
            | MotionKind.CharacterWiseExclusive -> convertCharacter motionResult
            | MotionKind.CharacterWiseInclusive -> convertCharacter motionResult

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
    member x.AdjustMotionResult motion (motionResult: MotionResult) =

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

            if Util.IsFlagSet motionResult.MotionResultFlags MotionResultFlags.SuppressAdjustment then
                motionResult
            elif endLine.LineNumber <= startLine.LineNumber then
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
                    SpanBeforeExclusivePromotion = Some originalSpan
                    MotionKind = kind
                    MotionResultFlags = flags 
                    CaretColumn = CaretColumn.AfterLastLine }
            else 
                // Rule #1. Move this back a line.
                let line = SnapshotUtil.GetLine originalSpan.Snapshot (endLine.LineNumber - 1)
                let span = SnapshotSpan(originalSpan.Start, line.End)
                let kind = MotionKind.CharacterWiseInclusive
                { motionResult with Span = span; SpanBeforeExclusivePromotion = Some originalSpan; MotionKind = kind }

        match motionResult.MotionKind with
        | MotionKind.CharacterWiseExclusive -> adjust()
        | MotionKind.CharacterWiseInclusive -> motionResult
        | MotionKind.LineWise _ -> motionResult

    /// Run the specified motion and return it's result
    member x.GetMotion motion (motionArgument: MotionArgument): MotionResult option = 

        let motionResult = 
            match motion with 
            | Motion.AllBlock blockKind -> x.AllBlock x.CaretPoint blockKind motionArgument.CountOrDefault
            | Motion.AllParagraph -> x.AllParagraph motionArgument.CountOrDefault
            | Motion.AllWord wordKind -> x.AllWord wordKind motionArgument.CountOrDefault x.CaretPoint
            | Motion.AllSentence -> x.AllSentence motionArgument.CountOrDefault |> Some
            | Motion.TagBlock kind -> x.TagBlock motionArgument.CountOrDefault x.CaretPoint kind
            | Motion.BackwardEndOfWord wordKind -> x.BackwardEndOfWord wordKind motionArgument.CountOrDefault
            | Motion.BeginingOfLine -> x.BeginingOfLine() |> Some
            | Motion.CharLeft -> x.CharLeft motionArgument.CountOrDefault |> Some
            | Motion.CharRight -> x.CharRight motionArgument.CountOrDefault |> Some
            | Motion.SpaceLeft -> x.SpaceLeft motionArgument.CountOrDefault |> Some
            | Motion.SpaceRight -> x.SpaceRight motionArgument.CountOrDefault |> Some
            | Motion.ArrowLeft -> x.ArrowLeft motionArgument.CountOrDefault |> Some
            | Motion.ArrowRight -> x.ArrowRight motionArgument.CountOrDefault |> Some
            | Motion.CharSearch (kind, direction, c) -> x.CharSearch c motionArgument.CountOrDefault kind direction
            | Motion.DisplayLineDown -> x.DisplayLineDown motionArgument.CountOrDefault
            | Motion.DisplayLineUp -> x.DisplayLineUp motionArgument.CountOrDefault
            | Motion.DisplayLineStart -> x.DisplayLineStart()
            | Motion.DisplayLineEnd -> x.DisplayLineEnd()
            | Motion.DisplayLineFirstNonBlank -> x.DisplayLineFirstNonBlank() |> Some
            | Motion.DisplayLineMiddleOfScreen -> x.DisplayLineMiddleOfScreen () 
            | Motion.EndOfLine -> x.EndOfLine motionArgument.CountOrDefault |> Some
            | Motion.EndOfWord wordKind -> x.EndOfWord wordKind motionArgument.CountOrDefault |> Some
            | Motion.FirstNonBlankOnCurrentLine -> x.FirstNonBlankOnCurrentLine() |> Some
            | Motion.FirstNonBlankOnLine -> x.FirstNonBlankOnLine motionArgument.CountOrDefault |> Some
            | Motion.ForceCharacterWise subMotion -> x.ForceCharacterWise subMotion motionArgument
            | Motion.ForceLineWise subMotion -> x.ForceLineWise subMotion motionArgument
            | Motion.InnerBlock blockKind -> x.InnerBlock x.CaretPoint blockKind motionArgument.CountOrDefault
            | Motion.InnerWord wordKind -> x.InnerWord wordKind motionArgument.CountOrDefault x.CaretPoint
            | Motion.InnerParagraph -> x.InnerParagraph motionArgument.CountOrDefault
            | Motion.LastNonBlankOnLine -> x.LastNonBlankOnLine motionArgument.CountOrDefault |> Some
            | Motion.LastSearch isReverse -> x.LastSearch isReverse motionArgument.CountOrDefault
            | Motion.LineDown -> x.LineDown motionArgument.CountOrDefault
            | Motion.LineDownToFirstNonBlank -> x.LineDownToFirstNonBlank motionArgument.CountOrDefault |> Some
            | Motion.LineFromBottomOfVisibleWindow -> x.LineFromBottomOfVisibleWindow motionArgument.Count 
            | Motion.LineFromTopOfVisibleWindow -> x.LineFromTopOfVisibleWindow motionArgument.Count 
            | Motion.LineInMiddleOfVisibleWindow -> x.LineInMiddleOfVisibleWindow() 
            | Motion.LineOrFirstToFirstNonBlank -> x.LineOrFirstToFirstNonBlank motionArgument.Count |> Some
            | Motion.LineOrLastToFirstNonBlank -> x.LineOrLastToFirstNonBlank motionArgument.Count |> Some
            | Motion.LineUp -> x.LineUp motionArgument.CountOrDefault
            | Motion.LineUpToFirstNonBlank -> x.LineUpToFirstNonBlank motionArgument.CountOrDefault |> Some
            | Motion.Mark localMark -> x.Mark localMark
            | Motion.MarkLine localMark -> x.MarkLine localMark
            | Motion.MatchingTokenOrDocumentPercent -> x.MatchingTokenOrDocumentPercent motionArgument.Count
            | Motion.MoveCaretToMouse -> x.MoveCaretToMouse()
            | Motion.NextMark path -> x.NextMark path motionArgument.CountOrDefault
            | Motion.NextMarkLine path -> x.NextMarkLine path motionArgument.CountOrDefault
            | Motion.NextPartialWord path -> x.NextPartialWord path motionArgument.CountOrDefault
            | Motion.NextMatch path -> x.NextMatch path motionArgument.CountOrDefault
            | Motion.NextWord path -> x.NextWord path motionArgument.CountOrDefault
            | Motion.ParagraphBackward -> x.ParagraphBackward motionArgument.CountOrDefault |> Some
            | Motion.ParagraphForward -> x.ParagraphForward motionArgument.CountOrDefault |> Some
            | Motion.QuotedString quoteChar -> x.QuotedString quoteChar
            | Motion.QuotedStringContents quoteChar -> x.QuotedStringContentsWithCount quoteChar motionArgument.CountOrDefault
            | Motion.RepeatLastCharSearch -> x.RepeatLastCharSearch motionArgument.CountOrDefault
            | Motion.RepeatLastCharSearchOpposite -> x.RepeatLastCharSearchOpposite motionArgument.CountOrDefault
            | Motion.Search searchData-> x.Search searchData motionArgument.CountOrDefault
            | Motion.SectionBackwardOrCloseBrace -> x.SectionBackwardOrCloseBrace motionArgument.CountOrDefault |> Some
            | Motion.SectionBackwardOrOpenBrace -> x.SectionBackwardOrOpenBrace motionArgument.CountOrDefault |> Some
            | Motion.SectionForward -> x.SectionForward motionArgument.MotionContext motionArgument.CountOrDefault |> Some
            | Motion.SectionForwardOrCloseBrace -> x.SectionForwardOrCloseBrace motionArgument.MotionContext motionArgument.CountOrDefault |> Some
            | Motion.ScreenColumn -> x.LineToColumn motionArgument.CountOrDefault |> Some
            | Motion.SentenceBackward -> x.SentenceBackward motionArgument.CountOrDefault |> Some
            | Motion.SentenceForward -> x.SentenceForward motionArgument.CountOrDefault |> Some
            | Motion.UnmatchedToken (path, kind) -> x.UnmatchedToken path kind motionArgument.CountOrDefault
            | Motion.WordBackward wordKind -> x.WordBackward wordKind motionArgument.CountOrDefault |> Some
            | Motion.WordForward wordKind -> x.WordForward wordKind motionArgument.CountOrDefault motionArgument.MotionContext |> Some

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
        | Motion.TagBlock kind -> x.TagBlock 1 point kind
        | Motion.InnerWord wordKind -> x.InnerWord wordKind 1 point 
        | Motion.InnerBlock blockKind -> x.InnerBlock point blockKind 1
        | Motion.QuotedStringContents quote -> x.QuotedStringWithoutSpaces quote
        | _ -> None

    interface IMotionUtil with
        member x.TextView = _textView
        member x.GetMotion motion motionArgument = x.GetMotion motion motionArgument
        member x.GetTextObject motion point = x.GetTextObject motion point
        member x.GetExpandedTagBlock startPoint endPoint kind = x.GetExpandedTagBlock startPoint endPoint kind