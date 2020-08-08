namespace Vim

open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Text.Tagging
open System


/// Importable interface which produces ITagger implementations based on sources
module TaggerUtil =

    /// Create an ITagger implementation for the IAsyncTaggerSource.
    val CreateAsyncTaggerRaw<'TData, 'TTag when 'TTag :> ITag> : IAsyncTaggerSource<'TData, 'TTag> -> ITagger<'TTag>

    /// Create an ITagger implementation for the IAsyncTaggerSource.  This instance will be a counted
    /// wrapper over the single IAsyncTaggerSource represented by the specified key
    val CreateAsyncTagger<'TData, 'TTag when 'TTag :> ITag> : PropertyCollection
         -> obj -> (unit -> IAsyncTaggerSource<'TData, 'TTag>) -> ITagger<'TTag>

    /// Create an IClassifieer implementation for the IAsyncTaggerSource.
    val CreateAsyncClassifierRaw<'TData> : IAsyncTaggerSource<'TData, IClassificationTag> -> IClassifier

    /// Create an IClassifieer implementation for the IAsyncTaggerSource.
    val CreateAsyncClassifier<'TData> : PropertyCollection
         -> obj -> (unit -> IAsyncTaggerSource<'TData, IClassificationTag>) -> IClassifier

    /// Create an ITagger implementation for the IBasicTaggerSource.
    val CreateBasicTaggerRaw<'TTag when 'TTag :> ITag> : IBasicTaggerSource<'TTag> -> ITagger<'TTag>

    /// Create an ITagger implementation for the IBasicTaggerSource.  This instance will be a counted
    /// wrapper over the single IBasicTaggerSource represented by the specified key
    val CreateBasicTagger<'TTag when 'TTag :> ITag> : PropertyCollection
         -> obj -> (unit -> IBasicTaggerSource<'TTag>) -> ITagger<'TTag>

    /// Create an IClassifieer implementation for the IBasicTaggerSource.
    val CreateBasicClassifierRaw: IBasicTaggerSource<IClassificationTag> -> IClassifier

    /// Create an IClassifieer implementation for the IBasicTaggerSource.
    val CreateBasicClassifier: PropertyCollection
         -> obj -> (unit -> IBasicTaggerSource<IClassificationTag>) -> IClassifier

    /// Get or create the IAdhocOutliner instance for the given ITextBuffer.  This return will be useless
    /// unless the code which calls this method exports an ITaggerProvider which proxies the return
    /// of GetOrCreateOutlinerTagger
    val GetOrCreateOutliner: ITextBuffer -> IAdhocOutliner

    /// This is the ITagger implementation for IAdhocOutliner
    val CreateOutlinerTagger: ITextBuffer -> ITagger<OutliningRegionTag>
