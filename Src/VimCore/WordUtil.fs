#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

[<UsedInBackgroundThread()>]
[<Sealed>]
[<Class>]
type WordUtilSnapshot(_keywordChars: string) = 

    member x.KeywordChars = _keywordChars

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    ///
    /// This can be called from a background thread via ITextSearchService.
    [<UsedInBackgroundThread()>]
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
            | SearchPath.Forward -> span.End.Position > point.Position
            | SearchPath.Backward -> span.Start.Position < point.Position)

    /// Get the SnapshotSpan for the full word span which crosses the given SanpshotPoint
    ///
    /// This can be called from a background thread via ITextSearchService.
    [<UsedInBackgroundThread()>]
    member x.GetFullWordSpan wordKind point = 
        let word = x.GetWords wordKind SearchPath.Forward point |> SeqUtil.tryHeadOnly
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

    /// Create an ITextStructure navigator for the ITextBuffer where the GetWordExtent function 
    /// considers word values of the given WordKind.  Use the base ITextStructureNavigator of the
    /// ITextBuffer for the rest of the functions
    ///
    /// Note: This interface can be invoked from any thread via ITextSearhService
    [<UsedInBackgroundThread()>]
    member x.CreateTextStructureNavigator wordKind contentType = 

        { new ITextStructureNavigator with 
            member __.ContentType = contentType
            member __.GetExtentOfWord point = 
                match x.GetFullWordSpan wordKind point with
                | Some span -> TextExtent(span, true)
                | None -> TextExtent(SnapshotSpan(point,1),false)
            member __.GetSpanOfEnclosing span = 
                match x.GetFullWordSpan wordKind span.Start with
                | Some span -> span
                | None -> span
            member __.GetSpanOfFirstChild span = 
                SnapshotSpan(span.End, 0)
            member __.GetSpanOfNextSibling span = 
                SnapshotSpan(span.End, 0)
            member __.GetSpanOfPreviousSibling span = 
                let before = SnapshotPointUtil.SubtractOneOrCurrent span.Start
                SnapshotSpan(before, 0) }

[<Sealed>]
[<Class>]
type WordUtil() =

    // KTODO: need to take this as a constructor argument
    let mutable _snapshot = WordUtilSnapshot("")

    member x.KeywordChars
        with get() = _snapshot.KeywordChars
        and set value = _snapshot <- WordUtilSnapshot(value)

    member x.Snapshot = _snapshot

    member x.GetFullWordSpan wordKind point = x.Snapshot.GetFullWordSpan wordKind point
    member x.GetWords wordKind path point = x.Snapshot.GetWords wordKind path point



