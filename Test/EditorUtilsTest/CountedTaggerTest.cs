using System;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class CountedTaggerTest : EditorHostTest
    {
        private readonly MockRepository _factory;
        private readonly object _key;
        private readonly PropertyCollection _propertyCollection;

        public CountedTaggerTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _key = new object();
            _propertyCollection = new PropertyCollection();
        }

        private CountedTagger<TextMarkerTag> Create(object key, PropertyCollection propertyCollection, Func<ITagger<TextMarkerTag>> func)
        {
            return new CountedTagger<TextMarkerTag>(propertyCollection, key, func);
        }

        /// <summary>
        /// First create on an key should actually call the create function
        /// </summary>
        [Fact]
        public void Create_DoCreate()
        {
            var didRun = false;
            var result = Create(
               _key, 
               _propertyCollection, 
                () =>
                {
                    didRun = true;
                    return _factory.Create<ITagger<TextMarkerTag>>().Object;
                });
            Assert.True(didRun);
        }

        /// <summary>
        /// Second create should just grab the value from the property collection
        /// </summary>
        [Fact]
        public void Create_GetFromCache()
        {
            var runCount = 0;
            Func<ITagger<TextMarkerTag>> func =
                () =>
                {
                    runCount++;
                    return _factory.Create<ITagger<TextMarkerTag>>().Object;
                };
            var result1 = Create(_key, _propertyCollection, func);
            var result2 = Create(_key, _propertyCollection, func);
            Assert.Equal(1, runCount);
            Assert.NotSame(result1, result2);
            Assert.Same(result1.Tagger, result2.Tagger);
        }

        /// <summary>
        /// Disposing the one containing CountedTagger should dispose the underlying instance
        /// </summary>
        [Fact]
        public void Dispose_OneInstance()
        {
            var tagger = _factory.Create<ITagger<TextMarkerTag>>();
            var disposable = tagger.As<IDisposable>();
            var result = Create(_key, _propertyCollection, () => tagger.Object);

            disposable.Setup(x => x.Dispose()).Verifiable();
            result.Dispose();
            disposable.Verify();
        }

        /// <summary>
        /// Must dispose all of the outer CountedTagger instances before the inner ITagger is disposed
        /// </summary>
        [Fact]
        public void Dispose_ManyInstance()
        {
            var tagger = _factory.Create<ITagger<TextMarkerTag>>();
            var disposable = tagger.As<IDisposable>();
            var result1 = Create(_key, _propertyCollection, () => tagger.Object);
            var result2 = Create(_key, _propertyCollection, () => tagger.Object);

            result1.Dispose();
            disposable.Setup(x => x.Dispose()).Verifiable();
            result2.Dispose();
            disposable.Verify();
        }
    }
}
