#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.ComponentModel.Composition

/// TODO: Need to move the MotionUtil functions for word into this type
[<Sealed>]
type internal WordUtil 
    (
        _textBuffer : ITextBuffer,
        _textStructureNavigator : ITextStructureNavigator
    ) = 

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    ///
    /// TODO: Need to consider folded regions
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

    /// Get the SnapshotSpan for the full word span which crosses the given SanpshotPoint
    member x.GetFullWordSpan wordKind point = 
        let word = x.GetWords wordKind Path.Forward point |> SeqUtil.tryHeadOnly
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
    member x.CreateTextStructureNavigator wordKind = 
        let this = x
        { new ITextStructureNavigator with 
            member x.ContentType = _textStructureNavigator.ContentType
            member x.GetExtentOfWord point = 
                match this.GetFullWordSpan wordKind point with
                | Some span -> TextExtent(span, true)
                | None -> TextExtent(SnapshotSpan(point,1),false)
            member x.GetSpanOfEnclosing span = _textStructureNavigator.GetSpanOfEnclosing(span)
            member x.GetSpanOfFirstChild span = _textStructureNavigator.GetSpanOfFirstChild(span)
            member x.GetSpanOfNextSibling span = _textStructureNavigator.GetSpanOfNextSibling(span)
            member x.GetSpanOfPreviousSibling span = _textStructureNavigator.GetSpanOfPreviousSibling(span) }

    interface IWordUtil with 
        member x.TextBuffer = _textBuffer
        member x.GetFullWordSpan wordKind point = x.GetFullWordSpan wordKind point
        member x.GetWords wordKind path point = x.GetWords wordKind path point
        member x.CreateTextStructureNavigator wordKind = x.CreateTextStructureNavigator wordKind 

// TODO: Need to think through how we handled folded regions here.  Should we be looking an the ITextView
// snapshot or instead looking at the normal snapshot and the fold manager explicitly.  How this is done
// will have a big impact on the structure of this API
[<Export(typeof<IWordUtilFactory>)>]
type internal WordUtilFactory
    [<ImportingConstructor>]
    (
        _textStructureNavigatorSelectorService : ITextStructureNavigatorSelectorService
    ) =

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = System.Object()

    member x.CreateWordUtil (textView : ITextView) = 
        let textBuffer = textView.TextBuffer
        let textStructureNavigator = _textStructureNavigatorSelectorService.GetTextStructureNavigator textBuffer
        WordUtil(textBuffer, textStructureNavigator) :> IWordUtil

    member x.GetWordUtil (textView : ITextView) = 
        let properties = textView.Properties
        properties.GetOrCreateSingletonProperty(_key, (fun () -> x.CreateWordUtil textView))

    interface IWordUtilFactory with
        member x.GetWordUtil textView = x.GetWordUtil textView

