using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class BasicTaggerTest : EditorTestBase
    {
        #region TestableBasicTaggerSource 

        private sealed class TestableBasicTaggerSource : IBasicTaggerSource<TextMarkerTag>
        {
            private readonly ITextBuffer _textBuffer;
            private List<ITagSpan<TextMarkerTag>> _tags;
            private EventHandler _changed;

            internal TestableBasicTaggerSource(ITextBuffer textBuffer)
            {
                _textBuffer = textBuffer;
            }

            internal void SetTags(params SnapshotSpan[] spans)
            {
                _tags = spans
                    .Select(span => new TagSpan<TextMarkerTag>(span, new TextMarkerTag("")))
                    .Cast<ITagSpan<TextMarkerTag>>()
                    .ToList();
            }

            internal void RaiseChanged()
            {
                if (_changed != null)
                {
                    _changed(this, EventArgs.Empty);
                }
            }

            #region IBasicTaggerSource<TextMarkerTag>

            event EventHandler IBasicTaggerSource<TextMarkerTag>.Changed
            {
                add { _changed += value; }
                remove { _changed -= value; }
            }

            ReadOnlyCollection<ITagSpan<TextMarkerTag>> IBasicTaggerSource<TextMarkerTag>.GetTags(SnapshotSpan span)
            {
                return _tags.ToReadOnlyCollection();
            }

            ITextSnapshot IBasicTaggerSource<TextMarkerTag>.TextSnapshot
            {
                get { return _textBuffer.CurrentSnapshot; }
            }

            #endregion
        }

        #endregion

        private ITextBuffer _textBuffer;
        private TestableBasicTaggerSource _basicTaggerSource;
        private BasicTagger<TextMarkerTag> _basicTagger;
        private ITagger<TextMarkerTag> _basicTaggerInterface;

        public void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _basicTaggerSource = new TestableBasicTaggerSource(_textBuffer);
            _basicTagger = new BasicTagger<TextMarkerTag>(_basicTaggerSource);
            _basicTaggerInterface = _basicTagger;
        }

        /// <summary>
        /// The GetTags call should just get data from the source
        /// </summary>
        [Test]
        public void GetTags_GoToSource()
        {
            Create("cat", "dog");
            var span = _textBuffer.GetSpan(0, 1);
            _basicTaggerSource.SetTags(span);
            var tag = _basicTagger.GetTags(_textBuffer.GetExtent()).Single();
            Assert.AreEqual(span, tag.Span);
        }

        /// <summary>
        /// Make sure the GetTags call will cache the request
        /// </summary>
        [Test]
        public void GetTags_CacheRequest()
        {
            Create("cat", "dog");
            var span = _textBuffer.GetSpan(0, 1);
            _basicTaggerSource.SetTags(span);
            var requestSpan = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
            _basicTagger.GetTags(requestSpan).Single();
            Assert.AreEqual(requestSpan, _basicTagger.CachedRequestSpan.Value);
        }

        /// <summary>
        /// Make sure the GetTags call will cache the overarching request for a 
        /// series of requests
        /// </summary>
        [Test]
        public void GetTags_CacheMultipleRequest()
        {
            Create("cat", "dog", "bear");
            var span = _textBuffer.GetSpan(0, 1);
            _basicTaggerSource.SetTags(span);
            _basicTagger.GetTags(_textBuffer.GetLine(0).ExtentIncludingLineBreak).Single();
            _basicTagger.GetTags(_textBuffer.GetLine(1).ExtentIncludingLineBreak).Single();
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, _basicTagger.CachedRequestSpan.Value);
        }

        /// <summary>
        /// Make sure that after an ITextBuffer edit we cache only the new data
        /// </summary>
        [Test]
        public void GetTags_ResetCacheAfterEdit()
        {
            Create("cat", "dog", "bear");
            var span = _textBuffer.GetSpan(0, 1);
            _basicTaggerSource.SetTags(span);
            _basicTagger.GetTags(_textBuffer.GetLine(0).ExtentIncludingLineBreak).Single();
            _textBuffer.Replace(new Span(0, 1), "b");
            _basicTagger.GetTags(_textBuffer.GetLine(1).ExtentIncludingLineBreak).Single();
            Assert.AreEqual(_textBuffer.GetLine(1).ExtentIncludingLineBreak, _basicTagger.CachedRequestSpan.Value);
        }

        /// <summary>
        /// When the Changed event is raised the cached request span should be used
        /// in tags changed
        /// </summary>
        [Test]
        public void OnBasicTaggerSourceChanged_UseRequestSpan()
        {
            Create("cat", "dog");
            var span = _textBuffer.GetSpan(0, 2);
            _basicTagger.CachedRequestSpan = span;

            var didRun = false;
            _basicTaggerInterface.TagsChanged += (e, args) =>
            {
                didRun = true;
                Assert.AreEqual(span, args.Span);
            };
            _basicTaggerSource.RaiseChanged();
            Assert.IsTrue(didRun);
        }
    }
}
