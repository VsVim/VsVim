using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using Xunit;
using Vim;
using Vim.Extensions;
using AsyncTaggerType = Vim.AsyncTagger<string, Microsoft.VisualStudio.Text.Tagging.TextMarkerTag>;
using Microsoft.FSharp.Core;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class AsyncTaggerTest : VimTestBase
    {
        #region TestableAsyncTaggerSource

        protected sealed class TestableAsyncTaggerSource : IAsyncTaggerSource<string, TextMarkerTag>, IDisposable
        {
            private readonly ITextBuffer _textBuffer;
            private readonly int _threadId;
            private event EventHandler _changed;
            private List<ITagSpan<TextMarkerTag>> _promptTags;
            private List<ITagSpan<TextMarkerTag>> _backgroundTags;
            private Action<string, SnapshotSpan> _backgroundCallback;
            private Func<SnapshotSpan, ReadOnlyCollection<ITagSpan<TextMarkerTag>>> _backgroundFunc;

            internal bool InMainThread
            {
                get { return _threadId == Thread.CurrentThread.ManagedThreadId; }
            }

            internal int? Delay { get; set; }
            internal string DataForSnapshot { get; set; }
            internal bool IsDisposed { get; set; }
            internal ITextView TextView { get; set; }

            internal TestableAsyncTaggerSource(ITextBuffer textBuffer)
            {
                _textBuffer = textBuffer;
                _threadId = Thread.CurrentThread.ManagedThreadId;
                DataForSnapshot = "";
            }

            internal void SetPromptTags(params SnapshotSpan[] tagSpans)
            {
                _promptTags = tagSpans.Select(CreateTagSpan).ToList();
            }

            internal void SetBackgroundTags(params SnapshotSpan[] tagSpans)
            {
                _backgroundTags = tagSpans.Select(CreateTagSpan).ToList();
            }

            internal void SetBackgroundCallback(Action<string, SnapshotSpan> action)
            {
                _backgroundCallback = action;
            }

            internal void SetBackgroundFunc(Func<SnapshotSpan, SnapshotSpan?> func)
            {
                _backgroundFunc =
                    span =>
                    {
                        var item = func(span);
                        if (item.HasValue)
                        {
                            var tagSpan = CreateTagSpan(item.Value);
                            return new ReadOnlyCollection<ITagSpan<TextMarkerTag>>(new[] { tagSpan });
                        }

                        return new ReadOnlyCollection<ITagSpan<TextMarkerTag>>(new List<ITagSpan<TextMarkerTag>>());
                    };
            }

            internal void SetBackgroundFunc(Func<SnapshotSpan, ReadOnlyCollection<ITagSpan<TextMarkerTag>>> func)
            {
                _backgroundFunc = func;
            }

            internal void RaiseChanged(SnapshotSpan? span)
            {
                _changed?.Invoke(this, EventArgs.Empty);
            }

            #region IAsyncTaggerSource

            FSharpOption<int> IAsyncTaggerSource<string, TextMarkerTag>.Delay
            {
                get { return FSharpOption.CreateForNullable(Delay); }
            }

            FSharpOption<ITextView> IAsyncTaggerSource<string, TextMarkerTag>.TextView
            {
                get { return FSharpOption.CreateForReference(TextView); }
            }

            string IAsyncTaggerSource<string, TextMarkerTag>.GetDataForSnapshot(ITextSnapshot snapshot)
            {
                Assert.True(InMainThread);
                return DataForSnapshot;
            }

            ReadOnlyCollection<ITagSpan<TextMarkerTag>> IAsyncTaggerSource<string, TextMarkerTag>.GetTagsInBackground(string value, SnapshotSpan span, CancellationToken cancellationToken)
            {
                Assert.False(InMainThread);

                _backgroundCallback?.Invoke(value, span);

                cancellationToken.ThrowIfCancellationRequested();

                if (_backgroundFunc != null)
                {
                    return _backgroundFunc(span);
                }

                if (_backgroundTags != null)
                {
                    return _backgroundTags.ToReadOnlyCollection();
                }

                throw new Exception("Couldn't get background tags");
            }

            FSharpOption<IEnumerable<ITagSpan<TextMarkerTag>>> IAsyncTaggerSource<string, TextMarkerTag>.TryGetTagsPrompt(SnapshotSpan span)
            {
                Assert.True(InMainThread);
                return FSharpOption.CreateForReference<IEnumerable<ITagSpan<TextMarkerTag>>>(_promptTags);
            }

            event EventHandler IAsyncTaggerSource<string, TextMarkerTag>.Changed
            {
                add { _changed += value; }
                remove { _changed -= value; }
            }

            ITextSnapshot IAsyncTaggerSource<string, TextMarkerTag>.TextSnapshot
            {
                get
                {
                    Assert.True(InMainThread);
                    return _textBuffer.CurrentSnapshot;
                }
            }

            void IDisposable.Dispose()
            {
                Assert.True(InMainThread);
                IsDisposed = true;
            }

            #endregion
        }

        #endregion

        internal readonly MockFactory _mockFactory;
        protected ITextBuffer _textBuffer;
        protected TestableAsyncTaggerSource _asyncTaggerSource;
        internal AsyncTagger<string, TextMarkerTag> _asyncTagger;
        protected ITagger<TextMarkerTag> _asyncTaggerInterface;

        public TestableSynchronizationContext TestableSynchronizationContext { get; }

        protected NormalizedSnapshotSpanCollection EntireBufferSpan
        {
            get { return new NormalizedSnapshotSpanCollection(_textBuffer.CurrentSnapshot.GetExtent()); }
        }

        internal AsyncTaggerTest()
        {
            _mockFactory = new MockFactory();
            TestableSynchronizationContext = new TestableSynchronizationContext();
        }

        public override void Dispose()
        {
            TestableSynchronizationContext.Dispose();
            base.Dispose();
        }

        protected void Create(params string[] lines)
        {
            _textBuffer = VimEditorHost.CreateTextBuffer(lines);

            _asyncTaggerSource = new TestableAsyncTaggerSource(_textBuffer);
            _asyncTagger = new AsyncTagger<string, TextMarkerTag>(_asyncTaggerSource);
            _asyncTaggerInterface = _asyncTagger;
        }

        protected static ITagSpan<TextMarkerTag> CreateTagSpan(SnapshotSpan span)
        {
            return new TagSpan<TextMarkerTag>(span, new TextMarkerTag("my tag"));
        }

        protected static ReadOnlyCollection<ITagSpan<TextMarkerTag>> CreateTagSpans(params SnapshotSpan[] tagSpans)
        {
            return tagSpans.Select(CreateTagSpan).ToReadOnlyCollection();
        }

        internal BackgroundCacheData<TextMarkerTag> CreateBackgroundCacheData(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            return new BackgroundCacheData<TextMarkerTag>(
                SnapshotLineRange.CreateForSpan(source),
                tagSpans.Select(CreateTagSpan).ToReadOnlyCollection());
        }

        internal TrackingCacheData<TextMarkerTag> CreateTrackingCacheData(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            var snapshot = source.Snapshot;
            var trackingSpan = snapshot.CreateTrackingSpan(source, SpanTrackingMode.EdgeInclusive);
            var tag = new TextMarkerTag("my tag");
            var all = tagSpans
                .Select(span => Tuple.Create(snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive), tag))
                .ToReadOnlyCollection();
            return new TrackingCacheData<TextMarkerTag>(
                trackingSpan,
                all);
        }

        internal FSharpOption<TrackingCacheData<TextMarkerTag>> CreateTrackingCacheDataSome(SnapshotSpan source, params SnapshotSpan[] tagSpans) => FSharpOption.Create(CreateTrackingCacheData(source, tagSpans));

        internal TagCache<TextMarkerTag> CreateTagCache(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            var backgroundCacheData = CreateBackgroundCacheData(source, tagSpans);
            return new TagCache<TextMarkerTag>(FSharpOption.Create(backgroundCacheData), null);
        }

        protected List<ITagSpan<TextMarkerTag>> GetTagsFull(SnapshotSpan span)
        {
            return GetTagsFull(span, out bool unused);
        }

        protected List<ITagSpan<TextMarkerTag>> GetTagsFull(NormalizedSnapshotSpanCollection col)
        {
            return GetTagsFull(col, out bool unused);
        }

        protected List<ITagSpan<TextMarkerTag>> GetTagsFull(SnapshotSpan span, out bool wasAsync)
        {
            var col = new NormalizedSnapshotSpanCollection(span);
            return GetTagsFull(col, out wasAsync);
        }

        protected List<ITagSpan<TextMarkerTag>> GetTagsFull(NormalizedSnapshotSpanCollection col, out bool wasAsync)
        {
            Assert.False(_asyncTagger.AsyncBackgroundRequestData.IsSome());
            wasAsync = false;
            var tags = _asyncTagger.GetTags(col).ToList();

            if (_asyncTagger.AsyncBackgroundRequestData.IsSome())
            {
                WaitForBackgroundToComplete();
                tags = _asyncTagger.GetTags(col).ToList();
                wasAsync = true;
            }

            return tags;
        }

        protected void WaitForBackgroundToComplete()
        {
            _asyncTagger.WaitForBackgroundToComplete(TestableSynchronizationContext);
        }

        internal AsyncBackgroundRequest CreateAsyncBackgroundRequest(
            SnapshotSpan span,
            CancellationTokenSource cancellationTokenSource,
            Task task = null)
        {
            var channel = new Channel();
            channel.WriteNormal(SnapshotLineRange.CreateForSpan(span));

            task = task ?? new Task(() => { });
            return new AsyncBackgroundRequest(
                span.Snapshot,
                channel,
                task,
                cancellationTokenSource);
        }

        internal FSharpOption<AsyncBackgroundRequest> CreateAsyncBackgroundRequestSome(
            SnapshotSpan span,
            CancellationTokenSource cancellationTokenSource,
            Task task = null) => FSharpOption.Create(CreateAsyncBackgroundRequest(span, cancellationTokenSource, task));

        internal void SetTagCache(TrackingCacheData<TextMarkerTag> trackingCacheData)
        {
            _asyncTagger.TagCacheData = new TagCache<TextMarkerTag>(null, FSharpOption.Create(trackingCacheData));
        }

        public sealed class DidTagsChangeTest : AsyncTaggerTest
        {
            /// <summary>
            /// If the tracking data is empty and we have now tags then it didn't chaneg
            /// </summary>
            [WpfFact]
            public void EmptyTrackingData()
            {
                Create("cat", "dog", "bear");
                var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                SetTagCache(CreateTrackingCacheData(span));
                Assert.True(_asyncTagger.DidTagsChange(span, CreateTagSpans(_textBuffer.GetSpan(0, 1))));
            }

            /// <summary>
            /// If the tracking data is empty and there are no new tags for the same SnapshotSpan then
            /// nothing changed
            /// </summary>
            [WpfFact]
            public void EmptyTrackingData_NoNewTags()
            {
                Create("cat", "dog", "bear");
                var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                SetTagCache(CreateTrackingCacheData(span));
                Assert.False(_asyncTagger.DidTagsChange(span, CreateTagSpans()));
            }

            /// <summary>
            /// They didn't change if we can map forward to the same values
            /// </summary>
            [WpfFact]
            public void MapsToSame()
            {
                Create("cat", "dog", "bear", "tree");
                var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                SetTagCache(CreateTrackingCacheData(span, _textBuffer.GetSpan(1, 3)));
                _textBuffer.Replace(_textBuffer.GetLine(2).Extent, "fish");
                Assert.False(_asyncTagger.DidTagsChange(_textBuffer.GetLine(0).ExtentIncludingLineBreak, CreateTagSpans(_textBuffer.GetSpan(1, 3))));
            }

            /// <summary>
            /// They changed if the edit moved them to different places
            /// </summary>
            [WpfFact]
            public void MapsToDifferent()
            {
                Create("cat", "dog", "bear", "tree");
                var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                SetTagCache(CreateTrackingCacheData(span, _textBuffer.GetSpan(1, 3)));
                _textBuffer.Insert(0, "big ");
                Assert.True(_asyncTagger.DidTagsChange(span, CreateTagSpans(_textBuffer.GetSpan(1, 4))));
            }

            /// <summary>
            /// They changed if they are simply different
            /// </summary>
            [WpfFact]
            public void Different()
            {
                Create("cat", "dog", "bear", "tree");
                var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                SetTagCache(CreateTrackingCacheData(span, _textBuffer.GetSpan(1, 3)));
                Assert.True(_asyncTagger.DidTagsChange(span, CreateTagSpans(
                    _textBuffer.GetSpan(1, 3),
                    _textBuffer.GetSpan(5, 7))));
            }
        }

        public sealed class GetTagsTest : AsyncTaggerTest
        {
            /// <summary>
            /// First choice should be to go through the prompt code.  This shouldn't create
            /// any cache
            /// </summary>
            [WpfFact]
            public void UsePrompt()
            {
                using (var TestableSynchronizationContext = new TestableSynchronizationContext())
                {
                    Create("hello world");
                    _asyncTaggerSource.SetPromptTags(_textBuffer.GetSpan(0, 1));
                    var tags = _asyncTagger.GetTags(EntireBufferSpan).ToList();
                    Assert.Single(tags);
                    Assert.Equal(_textBuffer.GetSpan(0, 1), tags[0].Span);
                    Assert.True(TestableSynchronizationContext.IsEmpty);
                    Assert.True(_asyncTagger.TagCacheData.IsEmpty);
                }
            }

            /// <summary>
            /// If the source can't provide prompt data then the tagger needs to go through
            /// the cache next
            /// </summary>
            [WpfFact]
            public void UseCache()
            {
                Create("hello world");
                _asyncTagger.TagCacheData = CreateTagCache(
                    _textBuffer.GetExtent(),
                    _textBuffer.GetSpan(0, 1));
                var tags = _asyncTagger.GetTags(EntireBufferSpan).ToList();
                Assert.Single(tags);
                Assert.Equal(_textBuffer.GetSpan(0, 1), tags[0].Span);
            }

            /// <summary>
            /// When there are no prompt sources available for tags we should schedule this
            /// for the background thread
            /// </summary>
            [WpfFact]
            public void UseBackground()
            {
                Create("hello world");
                var tags = _asyncTagger.GetTags(EntireBufferSpan).ToList();
                Assert.Empty(tags);
                Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// Full test going through the background
            /// </summary>
            [WpfFact]
            public void UseBackgroundUpdateCache()
            {
                Create("cat", "dog", "bear");
                var span = _textBuffer.GetSpan(1, 2);
                _asyncTaggerSource.SetBackgroundTags(span);
                var tags = GetTagsFull(_textBuffer.GetExtent(), out bool wasAsync);
                Assert.Single(tags);
                Assert.Equal(span, tags[0].Span);
                Assert.True(wasAsync);
            }

            /// <summary>
            /// Make sure that we can get the tags when there is an explicit delay from
            /// the source
            /// </summary>
            [WpfFact]
            public void Delay()
            {
                Create("cat", "dog", "bear");
                var span = _textBuffer.GetSpan(1, 2);
                _asyncTaggerSource.SetBackgroundTags(span);
                var tags = GetTagsFull(_textBuffer.GetExtent());
                Assert.Single(tags);
                Assert.Equal(span, tags[0].Span);
            }

            /// <summary>
            /// The completion of the background operation should cause the TagsChanged
            /// event to be run
            /// </summary>
            [WpfFact]
            public void BackgroundShouldRaiseTagsChanged()
            {
                Create("cat", "dog", "bear");
                _asyncTaggerSource.SetBackgroundTags(
                    _textBuffer.GetSpan(1, 2),
                    _textBuffer.GetSpan(3, 4));

                var didRun = false;
                _asyncTaggerInterface.TagsChanged += delegate { didRun = true; };
                var tags = GetTagsFull(_textBuffer.GetExtent());
                Assert.True(didRun);
                Assert.Equal(2, tags.Count);
            }

            /// <summary>
            /// If there is an existing request out and a new one comes in then just append to that
            /// request instead of creating a new one
            /// </summary>
            [WpfFact]
            public void AppendExistingRequest()
            {
                Create("cat", "dog", "bear");

                var cancellationTokenSource = new CancellationTokenSource();
                _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequestSome(
                    _textBuffer.GetLineRange(0).Extent,
                    cancellationTokenSource,
                    new Task(() => { }));
                var channel = _asyncTagger.AsyncBackgroundRequestData.Value.Channel;
                Assert.Equal(1, channel.CurrentStack.Length);

                var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(1).Extent).ToList();
                Assert.Equal(2, channel.CurrentStack.Length);
                Assert.Same(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
            }

            /// <summary>
            /// If the existing requset is on a different snapshot then it needs to be replaced when a new
            /// request comes in 
            /// </summary>
            [WpfFact]
            public void ReplaceWorseRequest()
            {
                Create("cat", "dog", "bear");

                var cancellationTokenSource = new CancellationTokenSource();
                _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequestSome(
                    _textBuffer.GetLine(0).Extent,
                    cancellationTokenSource,
                    new Task(() => { }));

                _textBuffer.Replace(new Span(0, 1), "b");
                var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
                Assert.Empty(tags);
                Assert.NotSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
                Assert.True(cancellationTokenSource.IsCancellationRequested);
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// The same Span on different snapshots should cause a different request to be queued
            /// up
            /// </summary>
            [WpfFact]
            public void ReplaceWhenSnapshotChanges()
            {
                Create("cat", "dog", "bear");

                var cancellationTokenSource = new CancellationTokenSource();
                _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequestSome(
                    _textBuffer.GetExtent(),
                    cancellationTokenSource,
                    new Task(() => { }));

                _textBuffer.Replace(new Span(0, 3), "bat");
                var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
                Assert.Empty(tags);
                Assert.NotSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
                Assert.True(cancellationTokenSource.IsCancellationRequested);
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// If the background request throws (for any reason including cancellation) then it should
            /// be handled and treated just like an empty return
            /// </summary>
            [WpfFact]
            public void BackgroundThrows()
            {
                Create("cat", "dog", "bat");

                var didRun = false;
                _asyncTaggerSource.SetBackgroundCallback(delegate
                {
                    didRun = true;
                    throw new Exception("");
                });

                var tags = GetTagsFull(_textBuffer.GetExtent());
                Assert.Empty(tags);
                Assert.True(didRun);
            }

            /// <summary>
            /// Even if the cache doesn't completely match the information in the cache we should at
            /// least the partial information we have and schedule the rest
            /// </summary>
            [WpfFact]
            public void PartialMatchInCache()
            {
                Create("cat", "dog", "bat");
                _asyncTagger.TagCacheData = CreateTagCache(
                    _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                    _textBuffer.GetSpan(0, 1));
                var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak).ToList();
                Assert.Single(tags);
                Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// If there is a forward edit we should still return cache data as best as possible promptly
            /// and schedule a background task for the correct data
            /// </summary>
            [WpfFact]
            public void ForwardEdit()
            {
                Create("cat", "dog", "bat");
                _asyncTagger.TagCacheData = CreateTagCache(
                    _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                    _textBuffer.GetSpan(0, 1));
                _textBuffer.Replace(new Span(0, 3), "cot");
                var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak).ToList();
                Assert.Single(tags);
                Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// Make sure that a prompt call updates the request span
            /// </summary>
            [WpfFact]
            public void PromptUpdateRequestSpan()
            {
                Create("hello world", "cat chased the dog");
                var span = _textBuffer.GetSpan(0, 6);
                _asyncTaggerSource.SetPromptTags(_textBuffer.GetSpan(0, 1));
                _asyncTagger.GetTags(span);
                Assert.Equal(span, _asyncTagger.CachedOverarchingRequestSpan.Value);
            }

            /// <summary>
            /// If we have tags which are mixed between background and tracking we need to pull 
            /// from both sources
            /// </summary>
            [WpfFact]
            public void BackgroundAndTracking()
            {
                Create("cat", "dog", "bear", "pig");
                var backgroundData = CreateBackgroundCacheData(_textBuffer.GetLine(0).Extent, _textBuffer.GetLineSpan(0, 1));
                var trackingData = CreateTrackingCacheData(_textBuffer.GetLine(1).Extent, _textBuffer.GetLineSpan(1, 1));
                _asyncTagger.TagCacheData = new TagCache<TextMarkerTag>(FSharpOption.Create(backgroundData), FSharpOption.Create(trackingData));
                var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak);
                Assert.Equal(2, tags.Count());
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// Shrink the ITextSnapshot to 0 to ensure the GetTags call doesn't do math against the
            /// incorrect ITextSnapshot.  If we use a Span against the wrong ITextSnapshot it should 
            /// cause an exception
            /// </summary>
            [WpfFact]
            public void ToEmptyBuffer()
            {
                Create("cat", "dog", "bear", "pig");
                _asyncTagger.TagCacheData = CreateTagCache(
                    _textBuffer.GetExtent(),
                    _textBuffer.GetSpan(0, 3),
                    _textBuffer.GetSpan(5, 3));
                _textBuffer.Delete(new Span(0, _textBuffer.CurrentSnapshot.Length));
                var list = GetTagsFull(_textBuffer.GetExtent());
                Assert.NotNull(list);
            }

            [WpfFact]
            public void SourceSpansMultiple_AllRelevant()
            {
                Create("cat", "dog", "fish", "tree dog");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                var col = new NormalizedSnapshotSpanCollection(new[]
                    {
                        _textBuffer.GetLineRange(1).ExtentIncludingLineBreak,
                        _textBuffer.GetLineRange(3).ExtentIncludingLineBreak
                    });
                var tags = GetTagsFull(col);
                Assert.Equal(
                    new[] { _textBuffer.GetLineSpan(1, 0, 3), _textBuffer.GetLineSpan(3, 5, 3) },
                    tags.Select(x => x.Span));
            }

            [WpfFact]
            public void SourceSpansMultiple_OneRelevant()
            {
                Create("cat", "dog", "fish", "tree dog");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                var col = new NormalizedSnapshotSpanCollection(new[]
                    {
                        _textBuffer.GetLineRange(0).ExtentIncludingLineBreak,
                        _textBuffer.GetLineRange(3).ExtentIncludingLineBreak
                    });
                var tags = GetTagsFull(col);
                Assert.Equal(
                    new[] { _textBuffer.GetLineSpan(3, 5, 3) },
                    tags.Select(x => x.Span));
            }

            /// <summary>
            /// This should never happen in the reald world but this is an API so treat it like one
            /// </summary>
            [WpfFact]
            public void SourceSpansNone()
            {
                Create("cat", "dog", "fish", "tree dog");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                var col = new NormalizedSnapshotSpanCollection();
                var tags = GetTagsFull(col);
                Assert.Empty(tags);
            }

            /// <summary>
            /// Once the cache is built for a portion of the buffer ask for a Span that is not in the 
            /// cache yet
            /// </summary>
            [WpfFact]
            public void AfterCompleteNotInCache()
            {
                Create("cat", "dog", "fish", "tree dog");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                GetTagsFull(_textBuffer.GetLineRange(0, 1).Extent);
                var tags = GetTagsFull(_textBuffer.GetLineRange(3).Extent);
                Assert.Equal(
                    new[] { _textBuffer.GetLineSpan(3, 5, 3) },
                    tags.Select(x => x.Span));
            }
        }

        public sealed class OnChangedTest : AsyncTaggerTest
        {
            /// <summary>
            /// If the IAsyncTaggerSource raises a TagsChanged event then the tagger must clear 
            /// out it's cache.  Anything it's stored up until this point is now invalid
            /// </summary>
            [WpfFact]
            public void ClearCache()
            {
                Create("hello world");
                _asyncTagger.TagCacheData = CreateTagCache(
                    _textBuffer.GetExtent(),
                    _textBuffer.GetSpan(0, 1));
                _asyncTaggerSource.RaiseChanged(null);
                Assert.True(_asyncTagger.TagCacheData.IsEmpty);
            }

            /// <summary>
            /// If the IAsyncTaggerSource raises a TagsChanged event then any existing tagger
            /// requests are invalid
            /// </summary>
            [WpfFact]
            public void ClearBackgroundRequest()
            {
                Create("hello world");
                var cancellationTokenSource = new CancellationTokenSource();
                _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequestSome(
                    _textBuffer.GetExtent(),
                    cancellationTokenSource,
                    new Task(() => { }));
                _asyncTaggerSource.RaiseChanged(null);
                Assert.False(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                Assert.True(cancellationTokenSource.IsCancellationRequested);
            }

            /// <summary>
            /// When the IAsyncTaggerSource raises it's event the tagger must as well
            /// </summary>
            [WpfFact]
            public void RaiseEvent()
            {
                Create("hello world");
                _asyncTagger.CachedOverarchingRequestSpan = FSharpOption.Create(_textBuffer.GetLine(0).Extent);
                var didRun = false;
                _asyncTaggerInterface.TagsChanged += delegate
                {
                    didRun = true;
                };

                _asyncTaggerSource.RaiseChanged(null);
                Assert.True(didRun);
            }

            /// <summary>
            /// If we've not recieved a GetTags request then don't raise a TagsChanged event when
            /// we get a Changed event.  
            /// </summary>
            [WpfFact]
            public void DontRaiseEventIfNoRequests()
            {
                Create("hello world");
                var didRun = false;
                _asyncTaggerInterface.TagsChanged += delegate
                {
                    didRun = true;
                };

                _asyncTaggerSource.RaiseChanged(null);
                Assert.False(didRun);
            }
        }

        public sealed class TagsChangedTest : AsyncTaggerTest
        {
            /// <summary>
            /// When the initial background request completes make sure that a TagsChanged is raised for the
            /// expected SnapshotSpan
            /// </summary>
            [WpfFact]
            public void BackgroundComplete()
            {
                Create("cat", "dog", "bear");
                SnapshotSpan? tagsChanged = null;
                var requestSpan = _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak;
                _asyncTaggerInterface.TagsChanged += (sender, e) => { tagsChanged = e.Span; };
                _asyncTaggerSource.SetBackgroundTags(_textBuffer.GetSpan(0, 1));
                GetTagsFull(requestSpan);
                Assert.True(tagsChanged.HasValue);
                Assert.Equal(requestSpan, tagsChanged);
            }

            /// <summary>
            /// During an edit we map the previous good data via ITrackingSpan instances while the next background
            /// request completes.  So by the time a background request completes we've already provided tags for 
            /// the span we are looking at.  If the simple ITrackingSpan mappings were correct then don't raise
            /// TagsChanged again for the same SnapshotSpan.  Doing so causes needless work and results in items
            /// like screen flickering
            /// </summary>
            [WpfFact]
            public void TrackingPredictedBackgroundResult()
            {
                Create("cat", "dog", "bear", "tree");

                SnapshotSpan? tagsChanged = null;
                _asyncTaggerInterface.TagsChanged += (sender, e) => { tagsChanged = e.Span; };

                // Setup the previous background cache
                _asyncTagger.TagCacheData = new TagCache<TextMarkerTag>(null, CreateTrackingCacheDataSome(
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetSpan(0, 1)));

                // Make an edit so that we are truly mapping forward then request tags for the same
                // area
                _textBuffer.Replace(_textBuffer.GetLine(2).Extent.Span, "fish");
                _asyncTaggerSource.SetBackgroundTags(_textBuffer.GetSpan(0, 1));
                GetTagsFull(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak);

                Assert.False(tagsChanged.HasValue);
            }

            /// <summary>
            /// An edit followed by different tags should raise the TagsChanged event
            /// </summary>
            [WpfFact]
            public void TrackingDidNotPredictBackgroundResult()
            {
                Create("cat", "dog", "bear", "tree");

                SnapshotSpan? tagsChanged = null;
                _asyncTaggerInterface.TagsChanged += (sender, e) => { tagsChanged = e.Span; };

                // Setup the previous background cache
                _asyncTagger.TagCacheData = new TagCache<TextMarkerTag>(null, CreateTrackingCacheDataSome(
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetSpan(0, 1)));

                // Make an edit so that we are truly mapping forward then request tags for the same
                // area
                _textBuffer.Replace(_textBuffer.GetLine(2).Extent.Span, "fish");
                _asyncTaggerSource.SetBackgroundTags(_textBuffer.GetSpan(1, 1));
                var requestSpan = _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak;
                GetTagsFull(requestSpan);

                Assert.True(tagsChanged.HasValue);
                Assert.Equal(requestSpan, tagsChanged.Value);
            }
        }

        public sealed class RaceConditionTests : AsyncTaggerTest
        {
            /// <summary>
            /// There is a race condition in the code base.  It occurs when a call to GetTags occurs for 
            /// uncached data while the async background request is still alive but has already completed
            /// processing.  The background thread is done but the foreground thread thinks it is processing
            /// the data.
            /// 
            /// The code works around this by seein the unfinished work after we get back to the UI thread
            /// and reschedules the request automatically
            /// </summary>
            [WpfFact]
            public void GetTags()
            {
                Create("dog", "cat", "fish", "dog");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                _asyncTagger.GetTags(_textBuffer.GetLineRange(0).Extent);
                Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());

                // Background is done.  Because we control the synchronization TestableSynchronizationContext though the foreground 
                // thread still sees it as active and hence will continue to queue data on it 
                _asyncTagger.AsyncBackgroundRequestData.Value.Task.Wait();
                Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                _asyncTagger.GetTags(_textBuffer.GetLineRange(3).Extent);

                // Clear the queue, the missing work will be seen and immedieatly requeued
                TestableSynchronizationContext.RunAll();
                WaitForBackgroundToComplete();

                var tags = _asyncTagger.GetTags(_textBuffer.GetExtent());
                Assert.Equal(
                    new[] { _textBuffer.GetLineSpan(0, 3), _textBuffer.GetLineSpan(3, 3) },
                    tags.Select(x => x.Span));
                TestableSynchronizationContext.RunAll();
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// It's possible that between the time the background thread completes it's processing 
            /// and when it can make it back to the UI thread that another request comes in.  At that
            /// point it is no loner the active request and shouldn't be updating any data 
            /// </summary>
            [WpfFact]
            public void BackgroundCompleted()
            {
                Create("dog", "cat", "fish", "dog");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                _asyncTagger.GetTags(_textBuffer.GetLineRange(0).Extent);
                _asyncTagger.AsyncBackgroundRequestData.Value.Task.Wait();

                // The background request is now complete and it's posted to the UI thread.  Create a 
                // new request on a new snapshot.  This will supercede the existing request
                _textBuffer.Replace(new Span(0, 0), "big ");
                _asyncTagger.GetTags(_textBuffer.GetLineRange(0).Extent);
                Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                var tokenSource = _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource;

                // The background will try to post twice (once for progress and the other for complete)
                for (var i = 0; i < 2; i++)
                {
                    TestableSynchronizationContext.RunOne();
                    Assert.True(_asyncTagger.AsyncBackgroundRequestData.IsSome());
                    Assert.Equal(tokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
                }

                _asyncTagger.AsyncBackgroundRequestData.Value.Task.Wait();
                TestableSynchronizationContext.RunAll();
            }
        }

        public sealed class HasTextViewTest : AsyncTaggerTest
        {
            private Mock<ITextView> _mockTextView;

            private void CreateWithView(params string[] lines)
            {
                Create(lines);
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                _mockTextView = _mockFactory.CreateTextView(_textBuffer);
                _mockTextView.SetupGet(x => x.IsClosed).Returns(false);
                _asyncTaggerSource.TextView = _mockTextView.Object;
            }

            /// <summary>
            /// When the ITextView is in a layout the ITextViewLines collection isn't accessible and will throw
            /// when accessed.  Make sure we don't access it 
            /// </summary>
            [WpfFact]
            public void InLayout()
            {
                CreateWithView("dogs cat bears");
                _mockTextView.SetupGet(x => x.InLayout).Returns(true);
                var tags = _asyncTagger.GetTags(_textBuffer.GetExtent());
                Assert.Empty(tags);
                WaitForBackgroundToComplete();
            }

            /// <summary>
            /// Make sure that visible lines are prioritized over the requested spans.  
            /// </summary>
            [WpfFact]
            public void PrioritizeVisibleLines()
            {
                CreateWithView("dog", "cat", "dog", "bear");
                _mockFactory.SetVisibleLineRange(_mockTextView, _textBuffer.GetLineRange(2));
                _asyncTagger.GetTags(_textBuffer.GetLineRange(0).Extent);
                _asyncTagger.AsyncBackgroundRequestData.Value.Task.Wait();

                // The visible lines will finish first and post.  Let only this one go through
                TestableSynchronizationContext.RunOne();
                var tags = _asyncTagger.GetTags(_textBuffer.GetExtent());
                Assert.Equal(
                    new[] { _textBuffer.GetLineSpan(2, 3) },
                    tags.Select(x => x.Span));
                WaitForBackgroundToComplete();
            }
        }

        public sealed class MiscTest : AsyncTaggerTest
        {
            /// <summary>
            /// When the editor makes consequitive requests for various spans we need to eventually fulfill
            /// all of those requests even if we prioritize the more recent ones
            /// </summary>
            [WpfFact]
            public void FulfillOutstandingRequests()
            {
                Create("cat", "dog", "fish", "tree");
                _asyncTagger.ChunkCount = 1;
                _asyncTaggerSource.SetBackgroundFunc(
                    span =>
                    {
                        var lineRange = SnapshotLineRange.CreateForSpan(span);
                        if (lineRange.LineRange.ContainsLineNumber(0))
                        {
                            return span.Snapshot.GetLineFromLineNumber(0).Extent;
                        }

                        if (lineRange.LineRange.ContainsLineNumber(1))
                        {
                            return span.Snapshot.GetLineFromLineNumber(1).Extent;
                        }
                    return null;
                    });

                for (var i = 0; i < _textBuffer.CurrentSnapshot.LineCount; i++)
                {
                    var span = _textBuffer.GetLineSpan(i, i);
                    var col = new NormalizedSnapshotSpanCollection(span);
                    _asyncTagger.GetTags(col);
                }

                _asyncTaggerSource.Delay = null;
                WaitForBackgroundToComplete();

                var tags = _asyncTagger.GetTags(new NormalizedSnapshotSpanCollection(_textBuffer.GetExtent()));
                Assert.Equal(2, tags.Count());
            }

            /// <summary>
            /// When the IAsyncTaggerSource throws it's important to make sure that we mark the
            /// tag span as having a cached value.  If we don't mark it as having such a value then 
            /// the foreground will consider it unfulfilled and will later request again for the 
            /// value
            /// </summary>
            [WpfFact]
            public void SourceThrows()
            {
                Create("cat", "dog", "fish", "tree");
                _asyncTaggerSource.Delay = null;
                _asyncTaggerSource.SetBackgroundCallback((x, y) => { throw new Exception("test"); });
                var lineRange = _textBuffer.GetLineRange(0, 0);
                _asyncTagger.GetTags(lineRange.Extent);
                WaitForBackgroundToComplete();

                Assert.True(_asyncTagger.TagCacheData.BackgroundCacheData.IsSome());
                Assert.True(_asyncTagger.TagCacheData.BackgroundCacheData.Value.VisitedCollection.Contains(lineRange.LineRange));
            }

            /// <summary>
            /// Send a request that is much bigger than the standard chunk size.  Ensure that we get all 
            /// of the values back
            /// </summary>
            [WpfFact]
            public void ChunkedData()
            {
                var list = new List<string>();
                for (var i = 0; i < 10; i++)
                {
                    list.Add("dog chases cat");
                    list.Add("fish around tree");
                    list.Add("where am i");
                    for (var j = 0; j < 7; j++)
                    {
                        list.Add("a");
                    }
                }
                Create(list.ToArray());
                _asyncTagger.ChunkCount = 10;
                _asyncTaggerSource.Delay = null;
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                _asyncTagger.GetTags(_textBuffer.GetExtent());
                WaitForBackgroundToComplete();

                var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
                Assert.Equal(10, tags.Count);
                for (var i = 0; i < tags.Count; i++)
                {
                    var span = tags[i].Span;
                    Assert.Equal("dog", span.GetText());
                    Assert.Equal(0, span.Start.GetContainingLine().LineNumber % 10);
                }
                WaitForBackgroundToComplete();
            }

            [WpfFact]
            public void AfterEditWithTrackingData()
            {
                Create("dog", "cat", "fish");
                _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                GetTagsFull(_textBuffer.GetExtent());
                _asyncTagger.TagCacheData = new TagCache<TextMarkerTag>(
                    _asyncTagger.TagCacheData.BackgroundCacheData,
                    CreateTrackingCacheDataSome(_textBuffer.GetSpan(0, 1), _textBuffer.GetSpan(0, 2)));
                _textBuffer.Replace(new Span(0, 0), "hello world ");
                var tags = GetTagsFull(_textBuffer.GetExtent());
                Assert.Equal(
                    new[] { "dog" },
                    tags.Select(x => x.Span.GetText()));
            }

            [WpfFact]
            public void ChunkCountDefault()
            {
                Create("");
                Assert.Equal(AsyncTagger<string, TextMarkerTag>.DefaultChunkCount, _asyncTagger.ChunkCount);
            }

            /// <summary>
            /// It's possible for the synchronization TestableSynchronizationContext to be null in Visual Studio.  Particularly 
            /// when the WPF designer is active.  Make sure that we handle this gracefully and don't 
            /// crash
            /// </summary>
            [WpfFact]
            public void BadSynchronizationContext()
            {
                Create("cat dog");
                var old = SynchronizationContext.Current;
                try
                {
                    _asyncTaggerSource.SetBackgroundFunc(span => TestUtils.GetDogTags(span));
                    SynchronizationContext.SetSynchronizationContext(null);
                    var tags = _asyncTagger.GetTags(_textBuffer.GetExtent());
                    Assert.Empty(tags);
                }
                finally
                {

                    SynchronizationContext.SetSynchronizationContext(old);
                }
            }
        }
    }
}
