using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace EditorUtils.Implementation.Tagging
{
    [Export(typeof(ITaggerFactory))]
    internal sealed class TaggerFactory : ITaggerFactory
    {
        ITagger<TTag> ITaggerFactory.CreateAsyncTaggerRaw<TData, TTag>(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
        {
            return new AsyncTagger<TData, TTag>(asyncTaggerSource);
        }

        ITagger<TTag> ITaggerFactory.CreateAsyncTagger<TData, TTag>(PropertyCollection propertyCollection, object key, Func<IAsyncTaggerSource<TData, TTag>> createFunc)
        {
            return CountedTagger<TTag>.Create(
                key,
                propertyCollection,
                () => new AsyncTagger<TData, TTag>(createFunc()));
        }

        ITagger<TTag> ITaggerFactory.CreateBasicTaggerRaw<TTag>(IBasicTaggerSource<TTag> basicTaggerSource)
        {
            return new BasicTagger<TTag>(basicTaggerSource);
        }

        ITagger<TTag> ITaggerFactory.CreateBasicTagger<TTag>(PropertyCollection propertyCollection, object key, Func<IBasicTaggerSource<TTag>> createFunc)
        {
            return CountedTagger<TTag>.Create(
                key,
                propertyCollection,
                () => new BasicTagger<TTag>(createFunc()));
        }
    }
}
