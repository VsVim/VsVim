
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
        ITagger<TTag> CreateAsyncTagger<TData, TTag>(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
            where TTag : ITag;

        ITagger<TTag> CreateAsyncTaggerCounted<TData, TTag>(object key, PropertyCollection propertyCollection, Func<IAsyncTaggerSource<TData, TTag>> createFunc)
            where TTag : ITag;

        ITagger<TTag> CreateBasicTagger<TTag>(IBasicTaggerSource<TTag> basicTaggerSource)
            where TTag : ITag;

        ITagger<TTag> CreateBasicTaggerCounted<TTag>(object key, PropertyCollection propertyCollection, Func<IBasicTaggerSource<TTag>> createFunc)
            where TTag : ITag;
    }
}
