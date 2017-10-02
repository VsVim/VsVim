using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace EditorUtils.UnitTest
{
    /// <summary>
    /// This class is intended to encompass rules that should be true for any implementation of 
    /// ITagger (async or not).  The intent is to have many variations of the taggers defined 
    /// here and put them through the same guantlet 
    /// </summary>
    public abstract class TaggerCommonTest : EditorHostTest
    {
        #region AsyncTaggerCommonTest 

        public abstract class AsyncTaggerCommonTest : TaggerCommonTest
        {
            private AsyncTagger<string, TextMarkerTag> _asyncTagger;

            protected override ITagger<TextMarkerTag> CreateTagger(ITextView textView)
            {
                _asyncTagger = CreateAsyncTagger(textView);
                return _asyncTagger;
            }

            protected override bool IsComplete()
            {
                return !_asyncTagger.AsyncBackgroundRequestData.HasValue;
            }

            protected override void WaitUntilCompleted()
            {
                while (_asyncTagger.AsyncBackgroundRequestData.HasValue)
                {
                    Thread.Yield();
                    _synchronizationContext.RunAll();
                }
            }

            internal abstract AsyncTagger<string, TextMarkerTag> CreateAsyncTagger(ITextView textView);
        }

        #endregion

        #region PureAsyncTaggerTest

        public sealed class PureAsyncTaggerTest : AsyncTaggerCommonTest
        {
            internal sealed class Tagger : AsyncTaggerSource<string, TextMarkerTag>
            {
                internal Tagger(ITextBuffer textBuffer)
                    : base(textBuffer)
                {

                }

                protected override string GetDataForSnapshot(ITextSnapshot snapshot)
                {
                    return string.Empty;
                }

                protected override ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTagsInBackground(string data, SnapshotSpan span, CancellationToken cancellationToken)
                {
                    return TestUtils.GetDogTags(span);
                }
            }

            internal override AsyncTagger<string, TextMarkerTag> CreateAsyncTagger(ITextView textView)
            {
                return new AsyncTagger<string, TextMarkerTag>(new Tagger(textView.TextBuffer));
            }
        }

        #endregion

        #region MixedAsyncTaggerTest

        /// <summary>
        /// SnapshotSpan which start with an even number are treated as prompt, everything else is async
        /// </summary>
        public sealed class MixedAsyncTaggerTest : AsyncTaggerCommonTest
        {
            internal sealed class Tagger : AsyncTaggerSource<string, TextMarkerTag>
            {
                internal Tagger(ITextBuffer textBuffer)
                    : base(textBuffer)
                {

                }

                protected override string GetDataForSnapshot(ITextSnapshot snapshot)
                {
                    return string.Empty;
                }

                protected override ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTagsInBackground(string data, SnapshotSpan span, CancellationToken cancellationToken)
                {
                    return TestUtils.GetDogTags(span);
                }

                protected override bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TextMarkerTag>> tags)
                {
                    if (span.Start.Position % 2 == 0)
                    {
                        tags = TestUtils.GetDogTags(span);
                        return true;
                    }

                    tags = null;
                    return false;
                }
            }

            internal override AsyncTagger<string, TextMarkerTag> CreateAsyncTagger(ITextView textView)
            {
                return new AsyncTagger<string, TextMarkerTag>(new Tagger(textView.TextBuffer));
            }
        }

        #endregion

        #region PureBasicTagger

        public sealed class PureBasicTaggerTest : TaggerCommonTest
        {
            internal sealed class Tagger : IBasicTaggerSource<TextMarkerTag>
            {
                private readonly ITextBuffer _textBuffer;

                internal Tagger(ITextBuffer textBuffer)
                {
                    _textBuffer = textBuffer;
                }

                public ITextSnapshot TextSnapshot
                {
                    get { return _textBuffer.CurrentSnapshot; }
                }

                public ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
                {
                    return TestUtils.GetDogTags(span);
                }

                #pragma warning disable 67
                public event System.EventHandler Changed;
                #pragma warning restore 67
            }

            protected override ITagger<TextMarkerTag> CreateTagger(ITextView textView)
            {
                return new BasicTagger<TextMarkerTag>(new Tagger(textView.TextBuffer));
            }

            protected override bool IsComplete()
            {
                return true;
            }

            protected override void WaitUntilCompleted()
            {
                
            }
        }

        #endregion

        #region DelayedBasicTagger

        public sealed class DelayedBasicTaggerTest : TaggerCommonTest
        {
            private Tagger _basicTagger;

            internal sealed class Tagger : IBasicTaggerSource<TextMarkerTag>
            {
                private readonly ITextBuffer _textBuffer;
                private bool _enabled;

                internal bool Enabled
                {
                    get { return _enabled; }
                    set
                    {
                        var changed = _enabled != value;
                        _enabled = value;
                        if (changed)
                        {
                            Changed(this, EventArgs.Empty);
                        }
                    }
                }

                internal Tagger(ITextBuffer textBuffer)
                {
                    _textBuffer = textBuffer;
                    _enabled = false;
                }

               public ITextSnapshot TextSnapshot
                {
                    get { return _textBuffer.CurrentSnapshot; }
                }

                public ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
                {
                    if (_enabled)
                    {
                        return TestUtils.GetDogTags(span);
                    }

                    return new ReadOnlyCollection<ITagSpan<TextMarkerTag>>(new List<ITagSpan<TextMarkerTag>>());
                }

                #pragma warning disable 67
                public event System.EventHandler Changed;
                #pragma warning restore 67
            }

            protected override ITagger<TextMarkerTag> CreateTagger(ITextView textView)
            {
                _basicTagger = new Tagger(textView.TextBuffer);
                return new BasicTagger<TextMarkerTag>(_basicTagger);
            }

            protected override bool IsComplete()
            {
                return _basicTagger.Enabled;
            }

            protected override void WaitUntilCompleted()
            {
                _basicTagger.Enabled = true; 
            }
        }

        #endregion

        protected readonly TestableSynchronizationContext _synchronizationContext;
        protected ITagger<TextMarkerTag> _tagger;
        protected ITextBuffer _textBuffer;
        protected ITextView _textView;
        protected List<SnapshotSpan> _tagsChangedList;

        protected override void Dispose()
        {
            base.Dispose();
            _synchronizationContext.Uninstall();
        }

        public TaggerCommonTest()
        {
            _synchronizationContext = new TestableSynchronizationContext();
            _synchronizationContext.Install();
        }

        /// <summary>
        /// The tagger here should return all instances of the word "dog" that appear
        /// in the text
        /// </summary>
        protected abstract ITagger<TextMarkerTag> CreateTagger(ITextView textView);

        protected abstract bool IsComplete();

        protected abstract void WaitUntilCompleted();

        protected void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _tagsChangedList = new List<SnapshotSpan>();
            _tagger = CreateTagger(_textView);
            _tagger.TagsChanged += (sender, e) => _tagsChangedList.Add(e.Span);
        }

        [Fact]
        public void SimpleSingle()
        {
            Create("cat dog", "fish");
            var tags = _tagger.GetTags(_textBuffer.GetExtent()).ToList();
            var expected = new [] { _textBuffer.GetSpan(4, 3) };
            if (tags.Count == 0)
            {
                // Fine for the result to be delayed but we must see a TagsChanged event occur 
                // to signal the new tags.  The SnapshotSpan values which occur in that list
                // must include the expected span
                Assert.False(IsComplete());
                WaitUntilCompleted();
                Assert.True(_tagsChangedList.Any(x => x.Span.Contains(expected[0])));
                tags = _tagger.GetTags(_textBuffer.GetExtent()).ToList();
            }
            else
            {
                // If the tags returned promptly they shouldn't have raised any tags changed
                // events
                Assert.Equal(0, _tagsChangedList.Count);
            }

            Assert.Equal(expected, tags.Select(x => x.Span));
        }

        [Fact]
        public void SimpleMultipleSameLine()
        {
            Create("cat dog dog", "fish");
            var tags = _tagger.GetTags(_textBuffer.GetExtent()).ToList();
            var expected = new [] { _textBuffer.GetSpan(4, 3), _textBuffer.GetSpan(8, 3) };
            if (tags.Count == 0)
            {
                // Fine for the result to be delayed but we must see a TagsChanged event occur 
                // to signal the new tags.  The SnapshotSpan values which occur in that list
                // must include the expected span
                Assert.False(IsComplete());
                WaitUntilCompleted();
                foreach (var value in expected)
                {
                    Assert.True(_tagsChangedList.Any(x => x.Span.Contains(value)));
                }
                tags = _tagger.GetTags(_textBuffer.GetExtent()).ToList();
            }
            else
            {
                // If the tags returned promptly they shouldn't have raised any tags changed
                // events
                Assert.Equal(0, _tagsChangedList.Count);
            }

            Assert.Equal(expected, tags.Select(x => x.Span));
        }

        /// <summary>
        /// An ITextBuffer::Changed event should never cause the tagger itself to raise an event.  It 
        /// should let the editor drive the decision.  After all until the editor asks for a particular
        /// Span nothing has "changed" in its view
        /// </summary>
        [Fact]
        public void IgnoreTextBufferChanged()
        {
            Create("cat dog");
            _textBuffer.Replace(new Span(0, 0), "hello dog");
            Assert.Equal(0, _tagsChangedList.Count);
        }

        /// <summary>
        /// The editor will never send down an empty NormalizedSnapshotSpan collection but there is no 
        /// stopping other components from doing so.  Must protect against it
        /// </summary>
        [Fact]
        public void SourceSpansNone()
        {
            Create("cat dog");
            var col = new NormalizedSnapshotSpanCollection();
            var tags = _tagger.GetTags(col).ToList();
            Assert.Equal(0, tags.Count);
        }
    }
}
