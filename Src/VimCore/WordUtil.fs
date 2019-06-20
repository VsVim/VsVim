#light
namespace Vim
open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open System
open System.Diagnostics

[<UsedInBackgroundThread()>]
[<Sealed>]
[<Class>]
type SnapshotWordUtil(_keywordCharSet: VimCharSet) = 

    member x.KeywordCharSet = _keywordCharSet
    member x.IsBigWordChar c = not (Char.IsWhiteSpace(c))
    member x.IsNormalWordChar c = _keywordCharSet.Contains c
    member x.IsBigWordOnlyChar c = (not (x.IsNormalWordChar c)) && (not (Char.IsWhiteSpace(c)))

    member x.IsWordChar wordKind c =
        match wordKind with
        | WordKind.BigWord -> x.IsBigWordChar c
        | WordKind.NormalWord -> x.IsNormalWordChar c 

    member x.GetFullWordSpanInText wordKind (text: string) index =
        match EditUtil.GetFullLineBreakSpanAtIndex text index with
        | Some span ->
            if span.Start > 0 && Option.isSome (EditUtil.GetFullLineBreakSpanAtIndex text (span.Start - 1)) then Some span
            else None
        | None -> 
            let c = text.[index]
            if CharUtil.IsWhiteSpace c then
                None
            else
                let predicate = 
                    match wordKind with
                    | WordKind.NormalWord -> 
                        if x.IsNormalWordChar c then x.IsNormalWordChar
                        else x.IsBigWordOnlyChar
                    | WordKind.BigWord -> x.IsBigWordChar
                let mutable startIndex = index
                let mutable index = index
                while startIndex > 0 && predicate text.[startIndex - 1] do
                    startIndex <- startIndex - 1
                while index < text.Length && predicate text.[index] do
                    index <- index + 1
                Some (Span.FromBounds(startIndex, index))

    /// Get the word spans on the text in the given direction
    member x.GetWordSpansInText wordKind searchPath (text: string) =
        match searchPath with 
        | SearchPath.Forward ->
            0
            |> Seq.unfold (fun index ->
                let mutable index = index
                let mutable span: Span Option = None
                while index < text.Length && Option.isNone span do
                    span <- x.GetFullWordSpanInText wordKind text index
                    index <- index + 1
                match span with 
                | Some span -> Some (span, span.End)
                | None -> None)
        | SearchPath.Backward ->
            text.Length - 1
            |> Seq.unfold (fun index ->
                let mutable index = index
                let mutable span: Span Option = None
                while index >= 0 && Option.isNone span do
                    span <- x.GetFullWordSpanInText wordKind text index
                    index <- index - 1
                match span with 
                | Some span -> Some (span, span.Start - 1)
                | None -> None)

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    ///
    /// This can be called from a background thread via ITextSearchService.
    member x.GetWordSpans kind path point = 

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
                |> x.GetWordSpansInText kind path 
                |> Seq.map (fun span -> SnapshotSpan(snapshot, span.Start + offset, span.Length)))
        |> Seq.concat
        |> Seq.filter (fun span -> 
            // Need to filter off items from the first line.  The point can and will often be
            // in the middle of a line and we can't return any spans which are past / before 
            // the point depending on the direction
            match path with
            | SearchPath.Forward -> span.End.Position > point.Position
            | SearchPath.Backward -> span.Start.Position < point.Position)

    /// Get the SnapshotSpan for the full word span which crosses the given SanpshotPoint
    ///
    /// This can be called from a background thread via ITextSearchService.
    member x.GetFullWordSpan wordKind point = 
        let word = x.GetWordSpans wordKind SearchPath.Forward point |> SeqUtil.tryHeadOnly
        match word with 
        | None -> 
            // No more words forward then no word at the given SnapshotPoint
            None
        | Some span ->
            // Need to account for the first word not being on the given point.  Blanks 
            // for instance
            if span.Contains(point) then
                Some span
            else 
                None

/// ITextStructure navigator where the GetWordExtent function uses a fixed definition
/// of word
[<UsedInBackgroundThread>]
[<Class>]
[<Sealed>]
type SnapshotWordNavigator
    (
        _wordUtil: SnapshotWordUtil,
        _contentType: IContentType
    ) =

    member x.WordUtil = _wordUtil
    member x.ContentType = _contentType

    interface ITextStructureNavigator with 
        member __.ContentType = _contentType
        member __.GetExtentOfWord point = 
            match _wordUtil.GetFullWordSpan WordKind.NormalWord point with
            | Some span -> TextExtent(span, true)
            | None -> TextExtent(SnapshotSpan(point, 1), false)
        member __.GetSpanOfEnclosing span = 
            match _wordUtil.GetFullWordSpan WordKind.NormalWord span.Start with
            | Some span -> span
            | None -> span
        member __.GetSpanOfFirstChild span = 
            SnapshotSpan(span.End, 0)
        member __.GetSpanOfNextSibling span = 
            SnapshotSpan(span.End, 0)
        member __.GetSpanOfPreviousSibling span = 
            let before = SnapshotPointUtil.SubtractOneOrCurrent span.Start
            SnapshotSpan(before, 0) 

[<Sealed>]
[<Class>]
type WordUtil(_textBuffer: ITextBuffer, _localSettings: IVimLocalSettings) as this =

    let mutable _snapshotWordUtil = SnapshotWordUtil(_localSettings.IsKeywordCharSet)
    let mutable _snapshotWordNavigator = Unchecked.defaultof<SnapshotWordNavigator>
    let mutable _wordNavigator = Unchecked.defaultof<ITextStructureNavigator>

    do
        let createNavigators() =
            _snapshotWordNavigator <- SnapshotWordNavigator(_snapshotWordUtil, _textBuffer.ContentType) 
            _wordNavigator <- this.CreateTextStructureNavigator()
        _textBuffer.ContentTypeChanged
        |> Observable.add (fun _ -> createNavigators())

        _localSettings.SettingChanged
        |> Observable.add (fun e ->
            if e.Setting.Name = LocalSettingNames.IsKeywordName then
                _snapshotWordUtil <- SnapshotWordUtil(_localSettings.IsKeywordCharSet)
                createNavigators())

        createNavigators()

    member x.KeywordCharSet = _snapshotWordUtil.KeywordCharSet
    member x.Snapshot = _snapshotWordUtil
    member x.SnapshotWordNavigator = _snapshotWordNavigator
    member x.WordNavigator = _wordNavigator
    member x.IsWordChar wordKind c = x.Snapshot.IsWordChar wordKind c
    member x.GetFullWordSpan wordKind point = x.Snapshot.GetFullWordSpan wordKind point
    member x.GetFullWordSpanInText wordKind text index = x.Snapshot.GetFullWordSpanInText wordKind text index
    member x.GetWordSpans wordKind path point = x.Snapshot.GetWordSpans wordKind path point
    member x.GetWordSpansInText wordKind searchPath text = x.Snapshot.GetWordSpansInText wordKind searchPath text

    member x.CreateTextStructureNavigator() =
        { new ITextStructureNavigator with 
            member __.ContentType = _textBuffer.ContentType
            member __.GetExtentOfWord point = 
                match x.GetFullWordSpan WordKind.NormalWord point with
                | Some span -> TextExtent(span, true)
                | None -> TextExtent(SnapshotSpan(point,1),false)
            member __.GetSpanOfEnclosing span = 
                match x.GetFullWordSpan WordKind.NormalWord span.Start with
                | Some span -> span
                | None -> span
            member __.GetSpanOfFirstChild span = 
                SnapshotSpan(span.End, 0)
            member __.GetSpanOfNextSibling span = 
                SnapshotSpan(span.End, 0)
            member __.GetSpanOfPreviousSibling span = 
                let before = SnapshotPointUtil.SubtractOneOrCurrent span.Start
                SnapshotSpan(before, 0) }



