using System;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class CountedTaggerTest : VimTestBase
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

        private FSharpFunc<Unit, ITagger<TextMarkerTag>> CreateFunc(Func<ITagger<TextMarkerTag>> func)
        {
            return func.ToFSharpFunc();
        }

        private CountedTagger<TextMarkerTag> Create(object key, PropertyCollection propertyCollection, Func<ITagger<TextMarkerTag>> func)
        {
            return Create(key, propertyCollection, func.ToFSharpFunc());
        }

        private CountedTagger<TextMarkerTag> Create(object key, PropertyCollection propertyCollection, FSharpFunc<Unit, ITagger<TextMarkerTag>> func)
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
            var func = CreateFunc(
                () =>
                {
                    didRun = true;
                    return _factory.Create<ITagger<TextMarkerTag>>().Object;
                });
            var result = CountedTagger<TextMarkerTag>.Create(_key, _propertyCollection, func);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Second create should just grab the value from the property collection
        /// </summary>
        [Test]
        public void Create_GetFromCache()
        {
            var runCount = 0;
            var func = CreateFunc(
                () =>
                {
                    runCount++;
                    return _factory.Create<ITagger<TextMarkerTag>>().Object;
                });
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
