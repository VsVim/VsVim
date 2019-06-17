#light
namespace Vim
open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

/// Utility function for searching for Word values within an ITextSnapshot. This value 
/// is immutable and hence safe to use from background threads.
///
/// This uses a fixed vaule of the `iskeyword` option. This means it can get out of sync
/// with the current value. Components which are not on the background thread should 
/// prefer WordUtil as it will always be in sync but is limited to foreground thread
[<UsedInBackgroundThread>]
[<Class>]
[<Sealed>]
type SnapshotWordUtil = 

    new: keywordCharSet: VimCharSet -> SnapshotWordUtil

    /// The set of keyword chars this snapshot is using. 
    member KeywordCharSet: VimCharSet

    member IsWordChar: wordKind: WordKind -> c: char -> bool

    /// Get the full word span for the word value which crosses the given SnapshotPoint
    member GetFullWordSpan: wordKind: WordKind -> point: SnapshotPoint -> SnapshotSpan option

    /// Get the full word span for the word value which crosses the given index.
    member GetFullWordSpanInText: wordKind: WordKind -> text: string -> index: int -> Span option

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    member GetWordSpans: wordKind: WordKind -> path: SearchPath -> point: SnapshotPoint -> SnapshotSpan seq

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    member GetWordSpansInText: wordKind: WordKind -> searchPath: SearchPath -> text: string -> Span seq

[<UsedInBackgroundThread>] [<Class>] [<Sealed>] 
type SnapshotWordNavigator =
    member ContentType: IContentType
    member WordUtil: SnapshotWordUtil
    interface ITextStructureNavigator

[<Class>]
[<Sealed>]
type WordUtil = 

    new: textBuffer: ITextBuffer * localSettings: IVimLocalSettings -> WordUtil

    member KeywordCharSet: VimCharSet
    member Snapshot: SnapshotWordUtil
    member SnapshotWordNavigator: SnapshotWordNavigator
    member WordNavigator: ITextStructureNavigator
    member IsWordChar: wordKind: WordKind -> c: char -> bool

    /// <see cref="SnapshotWordUtil.GetFullWordSpan" />
    member GetFullWordSpan: wordKind: WordKind -> point: SnapshotPoint -> SnapshotSpan option

    /// <see cref="SnapshotWordUtil.GetFullWordSpanInText" />
    member GetFullWordSpanInText: wordKind: WordKind -> text: string -> index: int -> Span option

    /// <see cref="SnapshotWordUtil.GetWordSpans" />
    member GetWordSpans: wordKind: WordKind -> path: SearchPath -> point: SnapshotPoint -> SnapshotSpan seq

    /// <see cref="SnapshotWordUtil.GetWordSpansInText" />
    member GetWordSpansInText: wordKind: WordKind -> searchPath: SearchPath -> text: string -> Span seq
