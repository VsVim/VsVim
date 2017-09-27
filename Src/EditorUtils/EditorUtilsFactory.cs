
using System;
using System.Linq;
using EditorUtils.Implementation.Tagging;
using EditorUtils.Implementation.BasicUndo;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using EditorUtils.Implementation.Utilities;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils
{
    /// <summary>
    /// Importable interface which produces ITagger implementations based on sources
    /// </summary>
    public static class EditorUtilsFactory
    {
        /// <summary>
        /// Create an ITagger implementation for the IAsyncTaggerSource.
        /// </summary>
        public static ITagger<TTag> CreateTaggerRaw<TData, TTag>(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
            where TTag : ITag
        {
            return new AsyncTagger<TData, TTag>(asyncTaggerSource);
        }
        
        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource
        /// </summary>
        public static ITagger<TTag> CreateTaggerRaw<TTag>(IBasicTaggerSource<TTag> basicTaggerSource)
            where TTag : ITag
        {
            return new BasicTagger<TTag>(basicTaggerSource);
        }

        /// <summary>
        /// Create an ITagger implementation for the IAsyncTaggerSource.  This instance will be a counted 
        /// wrapper over the single IAsyncTaggerSource represented by the specified key
        /// </summary>
        public static ITagger<TTag> CreateTagger<TData, TTag>(PropertyCollection propertyCollection, object key, Func<IAsyncTaggerSource<TData, TTag>> createFunc)
            where TTag : ITag
        {
            return new CountedTagger<TTag>(
                propertyCollection,
                key,
                () => new AsyncTagger<TData, TTag>(createFunc()));
        }

        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource.  This instance will be a counted
        /// wrapper over the single IBasicTaggerSource represented by the specified key
        /// </summary>
        public static ITagger<TTag> CreateTagger<TTag>(PropertyCollection propertyCollection, object key, Func<IBasicTaggerSource<TTag>> createFunc)
            where TTag : ITag
        {
            return new CountedTagger<TTag>(
                propertyCollection,
                key,
                () => new BasicTagger<TTag>(createFunc()));
        }

        public static IClassifier CreateClassifierRaw(IBasicTaggerSource<IClassificationTag> basicTaggerSource)
        {
            return new Classifier(CreateTaggerRaw(basicTaggerSource));
        }

        public static IClassifier CreateClassifierRaw<TData>(IAsyncTaggerSource<TData, IClassificationTag> asyncTaggerSource)
        {
            return new Classifier(CreateTaggerRaw(asyncTaggerSource));
        }

        public static IClassifier CreateClassifier(PropertyCollection propertyCollection, object key, Func<IBasicTaggerSource<IClassificationTag>> createFunc)
        {
            return new CountedClassifier(
                propertyCollection,
                key,
                () => CreateClassifierRaw(createFunc()));
        }

        public static IClassifier CreateClassifier<TData>(PropertyCollection propertyCollection, object key, Func<IAsyncTaggerSource<TData, IClassificationTag>> createFunc)
        {
            return new CountedClassifier(
                propertyCollection,
                key,
                () => CreateClassifierRaw(createFunc()));
        }

        public static IBasicUndoHistoryRegistry CreateBasicUndoHistoryRegistry()
        {
            return new BasicTextUndoHistoryRegistry();
        }

        public static IProtectedOperations CreateProtectedOperations(IEnumerable<Lazy<IExtensionErrorHandler>> errorHandlers)
        {
            return new ProtectedOperations(errorHandlers);
        }

        public static IProtectedOperations CreateProtectedOperations(IEnumerable<IExtensionErrorHandler> errorHandlers)
        {
            var lazyList = errorHandlers.Select(x => new Lazy<IExtensionErrorHandler>(() => x)).ToList();
            return new ProtectedOperations(lazyList);
        }

        /// <summary>
        /// Get or create the IAdhocOutliner instance for the given ITextBuffer.  This return will be useless 
        /// unless the code which calls this method exports an ITaggerProvider which proxies the return 
        /// of GetOrCreateOutlinerTagger
        /// </summary>
        public static IAdhocOutliner GetOrCreateOutliner(ITextBuffer textBuffer)
        {
            return GetOrCreateOutlinerCore(textBuffer);
        }

        /// <summary>
        /// This is the ITagger implementation for IAdhocOutliner
        /// </summary>
        public static ITagger<OutliningRegionTag> CreateOutlinerTagger(ITextBuffer textBuffer)
        {
            return CreateTagger(
                textBuffer.Properties,
                AdhocOutliner.OutlinerTaggerKey,
                () => GetOrCreateOutlinerCore(textBuffer));
        }

        private static AdhocOutliner GetOrCreateOutlinerCore(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(AdhocOutliner.OutlinerKey, () => new AdhocOutliner(textBuffer));
        }
    }
}
