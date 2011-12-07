using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace EditorUtils.Implementation.Tagging
{
    [Export(typeof(ITaggerFactory))]
    internal sealed class TaggerFactory : ITaggerFactory
    {
        ITagger<TTag> ITaggerFactory.CreateAsyncTagger<TData, TTag>(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
        {
            return new AsyncTagger<TData, TTag>(asyncTaggerSource);
        }

        ITagger<TTag> ITaggerFactory.CreateAsyncTaggerCounted<TData, TTag>(object key, PropertyCollection propertyCollection, Func<IAsyncTaggerSource<TData, TTag>> createFunc)
        {
            return CountedTagger<TTag>.Create(
                key,
                propertyCollection,
                () => new AsyncTagger<TData, TTag>(createFunc()));
        }

        ITagger<TTag> ITaggerFactory.CreateBasicTagger<TTag>(IBasicTaggerSource<TTag> basicTaggerSource)
        {
            return new BasicTagger<TTag>(basicTaggerSource);
        }

        ITagger<TTag> ITaggerFactory.CreateBasicTaggerCounted<TTag>(object key, PropertyCollection propertyCollection, Func<IBasicTaggerSource<TTag>> createFunc)
        {
            return CountedTagger<TTag>.Create(
                key,
                propertyCollection,
                () => new BasicTagger<TTag>(createFunc()));
        }
    }
}
