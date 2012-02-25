
using System;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
namespace EditorUtils
{
    /// <summary>
    /// Importable interface which produces ITagger implementations based on sources
    /// </summary>
    public interface ITaggerFactory
    {
        /// <summary>
        /// Create an ITagger implementation for the IAsyncTaggerSource.
        /// </summary>
        ITagger<TTag> CreateAsyncTaggerRaw<TData, TTag>(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
            where TTag : ITag;

        /// <summary>
        /// Create an ITagger implementation for the IAsyncTaggerSource.  This instance will be a counted 
        /// wrapper over the single IAsyncTaggerSource represented by the specified key
        /// </summary>
        ITagger<TTag> CreateAsyncTagger<TData, TTag>(PropertyCollection propertyCollection, object key, Func<IAsyncTaggerSource<TData, TTag>> createFunc)
            where TTag : ITag;

        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource
        /// </summary>
        ITagger<TTag> CreateBasicTaggerRaw<TTag>(IBasicTaggerSource<TTag> basicTaggerSource)
            where TTag : ITag;

        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource.  This instance will be a counted
        /// wrapper over the single IBasicTaggerSource represented by the specified key
        /// </summary>
        ITagger<TTag> CreateBasicTagger<TTag>(PropertyCollection propertyCollection, object key, Func<IBasicTaggerSource<TTag>> createFunc)
            where TTag : ITag;
    }
}
