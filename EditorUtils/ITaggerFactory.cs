
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
        /// Create an ITagger implementation for the IAsyncTaggerSource
        /// </summary>
        ITagger<TTag> CreateAsyncTagger<TData, TTag>(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
            where TTag : ITag;

        /// <summary>
        /// Create a counted ITagger implementation for IAsyncTaggerSource.  This will cause a single
        /// IAsyncTaggerSource to be used for ITagger requests for the provided key
        /// </summary>
        ITagger<TTag> CreateAsyncTaggerCounted<TData, TTag>(object key, PropertyCollection propertyCollection, Func<IAsyncTaggerSource<TData, TTag>> createFunc)
            where TTag : ITag;

        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource
        /// </summary>
        ITagger<TTag> CreateBasicTagger<TTag>(IBasicTaggerSource<TTag> basicTaggerSource)
            where TTag : ITag;

        /// <summary>
        /// Create a counted ITagger implementation for IBasicTaggerSource.  This will cause a single
        /// IBasicTaggerSource to be used for ITagger requests for the provided key
        /// </summary>
        ITagger<TTag> CreateBasicTaggerCounted<TTag>(object key, PropertyCollection propertyCollection, Func<IBasicTaggerSource<TTag>> createFunc)
            where TTag : ITag;
    }
}
