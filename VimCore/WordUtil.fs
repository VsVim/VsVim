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

    /// Get the SnapshotSpan for the full word span which crosses the given SanpshotPoint
    member x.GetFullWordSpan wordKind point = 
        let line = SnapshotPointUtil.GetContainingLine point
        let text = line.GetText()
        let pos = point.Position - line.Start.Position
        match pos >= text.Length with
        | true -> 
            // It's in the line break.  There is no word in the line break
            None
        | false ->
            match TextUtil.FindFullWordSpan wordKind text pos with
            | Some s ->
                let adjusted = new Span(line.Start.Position+s.Start, s.Length)
                Some (new SnapshotSpan(point.Snapshot, adjusted))
            | None -> 
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
        member x.CreateTextStructureNavigator wordKind = x.CreateTextStructureNavigator wordKind 

// TODO: Need to think through how we handled folded regions here.  Should we be looking an the ITextView
// snapshot or instead looking at the normal snapshot and the fold manager explicitly.  How this is done
// will have a big impact on the structure of this API
[<Export(typeof<IWordUtil>)>]
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

