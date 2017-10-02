using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class BasicTaggerTest : EditorHostTest
    {
        #region TestableBasicTaggerSource 

        protected sealed class TestableBasicTaggerSource : IBasicTaggerSource<TextMarkerTag>
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

            #endregion
        }

        #endregion

        protected ITextBuffer _textBuffer;
        protected TestableBasicTaggerSource _basicTaggerSource;
        internal BasicTagger<TextMarkerTag> _basicTagger;
        protected ITagger<TextMarkerTag> _basicTaggerInterface;

        protected void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _basicTaggerSource = new TestableBasicTaggerSource(_textBuffer);
            _basicTagger = new BasicTagger<TextMarkerTag>(_basicTaggerSource);
            _basicTaggerInterface = _basicTagger;
        }

        public sealed class GetTagsTest : BasicTaggerTest
        {
            /// <summary>
            /// The GetTags call should just get data from the source
            /// </summary>
            [Fact]
            public void GoToSource()
            {
                Create("cat", "dog");
                var span = _textBuffer.GetSpan(0, 1);
                _basicTaggerSource.SetTags(span);
                var tag = _basicTagger.GetTags(_textBuffer.GetExtent()).Single();
                Assert.Equal(span, tag.Span);
            }

            /// <summary>
            /// Make sure the GetTags call will cache the request
            /// </summary>
            [Fact]
            public void CacheRequest()
            {
                Create("cat", "dog");
                var span = _textBuffer.GetSpan(0, 1);
                _basicTaggerSource.SetTags(span);
                var requestSpan = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                _basicTagger.GetTags(requestSpan).Single();
                Assert.Equal(requestSpan, _basicTagger.CachedRequestSpan.Value);
            }

            /// <summary>
            /// Make sure the GetTags call will cache the overarching request for a 
            /// series of requests
            /// </summary>
            [Fact]
            public void CacheMultipleRequest()
            {
                Create("cat", "dog", "bear");
                var span = _textBuffer.GetSpan(0, 1);
                _basicTaggerSource.SetTags(span);
                _basicTagger.GetTags(_textBuffer.GetLine(0).ExtentIncludingLineBreak).Single();
                _basicTagger.GetTags(_textBuffer.GetLine(1).ExtentIncludingLineBreak).Single();
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, _basicTagger.CachedRequestSpan.Value);
            }

            /// <summary>
            /// When an edit occurs the CachedSpan should include the requests that existed before
            /// the edit.  The editor does tagging ideally on a line by line basis.  When an edit occurs
            /// in an ideal scenario it will only ask for tag information for that particular line and
            /// will maintain the cached values for the other lines.
            /// 
            /// It will do this unless it is explicitly told that those lines changed.  It is not
            /// the job of the AsyncTagger base to do this.  Instead it is the job of the actually
            /// IAsyncTaggerSource to raise the Changed event if this is necessary.  Hence we must
            /// maintain the CachedSpan over the previously produced tags because we are still 
            /// responsible for them
            /// </summary>
            [Fact]
            public void ExtendCacheAfterEdit()
            {
                Create("cat", "dog", "bear");
                var span = _textBuffer.GetSpan(0, 1);
                _basicTaggerSource.SetTags(span);
                _basicTagger.GetTags(_textBuffer.GetLine(0).ExtentIncludingLineBreak).Single();
                _textBuffer.Replace(new Span(0, 1), "b");
                _basicTagger.GetTags(_textBuffer.GetLine(1).ExtentIncludingLineBreak).Single();
                var lineRange = _textBuffer.GetLineRange(0, 1);
                Assert.Equal(lineRange.ExtentIncludingLineBreak, _basicTagger.CachedRequestSpan.Value);
            }
        }

        public sealed class ChangedTest : BasicTaggerTest
        {
            /// <summary>
            /// When the Changed event is raised the cached request span should be used
            /// in tags changed
            /// </summary>
            [Fact]
            public void UseRequestSpan()
            {
                Create("cat", "dog");
                var span = _textBuffer.GetSpan(0, 2);
                _basicTagger.CachedRequestSpan = span;

                var didRun = false;
                _basicTaggerInterface.TagsChanged += (e, args) =>
                {
                    didRun = true;
                    Assert.Equal(span, args.Span);
                };
                _basicTaggerSource.RaiseChanged();
                Assert.True(didRun);
            }
        }
    }
}
