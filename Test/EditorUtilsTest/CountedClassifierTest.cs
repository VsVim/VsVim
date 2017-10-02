using System;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils.UnitTest
{
    public sealed class CountedClassifierTest : EditorHostTest
    {
        private readonly MockRepository _factory;
        private readonly object _key;
        private readonly PropertyCollection _propertyCollection;

        public CountedClassifierTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _key = new object();
            _propertyCollection = new PropertyCollection();
        }

        /// <summary>
        /// First create on an key should actually call the create function
        /// </summary>
        [Fact]
        public void Create_DoCreate()
        {
            var didRun = false;
            var result = new CountedClassifier(
               _propertyCollection, 
               _key, 
                () =>
                {
                    didRun = true;
                    return _factory.Create<IClassifier>().Object;
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
            Func<IClassifier> func =
                () =>
                {
                    runCount++;
                    return _factory.Create<IClassifier>().Object;
                };
            var result1 = new CountedClassifier(_propertyCollection, _key, func);
            var result2 = new CountedClassifier(_propertyCollection, _key, func);
            Assert.Equal(1, runCount);
            Assert.Same(result1.Classifier, result2.Classifier);
        }

        /// <summary>
        /// Disposing the one containing CountedTagger should dispose the underlying instance
        /// </summary>
        [Fact]
        public void Dispose_OneInstance()
        {
            var tagger = _factory.Create<IClassifier>();
            var disposable = tagger.As<IDisposable>();
            var result = new CountedClassifier(_propertyCollection, _key, () => tagger.Object);

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
            var tagger = _factory.Create<IClassifier>();
            var disposable = tagger.As<IDisposable>();
            var result1 = new CountedClassifier(_propertyCollection, _key, () => tagger.Object);
            var result2 = new CountedClassifier(_propertyCollection, _key, () => tagger.Object);

            result1.Dispose();
            disposable.Setup(x => x.Dispose()).Verifiable();
            result2.Dispose();
            disposable.Verify();
        }
    }
}
