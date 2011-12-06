using System;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Moq;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class CountedTaggerTest : EditorTestBase
    {
        private MockRepository _factory;
        private object _key;
        private PropertyCollection _propertyCollection;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _key = new object();
            _propertyCollection = new PropertyCollection();
        }

        private CountedTagger<TextMarkerTag> Create(object key, PropertyCollection propertyCollection, Func<ITagger<TextMarkerTag>> func)
        {
            var result = CountedTagger<TextMarkerTag>.Create(key, propertyCollection, func);
            return (CountedTagger<TextMarkerTag>)result;
        }

        /// <summary>
        /// First create on an key should actually call the create function
        /// </summary>
        [Test]
        public void Create_DoCreate()
        {
            var didRun = false;
            var result = CountedTagger<TextMarkerTag>.Create(
               _key, 
               _propertyCollection, 
                () =>
                {
                    didRun = true;
                    return _factory.Create<ITagger<TextMarkerTag>>().Object;
                });
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Second create should just grab the value from the property collection
        /// </summary>
        [Test]
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
            Assert.AreEqual(1, runCount);
            Assert.AreNotSame(result1, result2);
            Assert.AreSame(result1.Tagger, result2.Tagger);
        }

        /// <summary>
        /// Disposing the one containing CountedTagger should dispose the underlying instance
        /// </summary>
        [Test]
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
        [Test]
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
