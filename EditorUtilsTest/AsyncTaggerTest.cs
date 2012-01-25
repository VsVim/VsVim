using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EditorUtils.Implementation.Tagging;
using EditorUtils.Implementation.Utilities;
using EditorUtils.UnitTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;
using AsyncTaggerType = EditorUtils.Implementation.Tagging.AsyncTagger<string, Microsoft.VisualStudio.Text.Tagging.TextMarkerTag>;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class AsyncTaggerTest : EditorTestBase
    {
        #region TestableAsyncTaggerSource

        private sealed class TestableAsyncTaggerSource : IAsyncTaggerSource<string, TextMarkerTag>, IDisposable
        {
            private readonly ITextBuffer _textBuffer;
            private readonly int _threadId;
            private event EventHandler _changed;
            private List<ITagSpan<TextMarkerTag>> _promptTags;
            private List<ITagSpan<TextMarkerTag>> _backgroundTags;
            private Action<string, SnapshotSpan> _backgroundCallback;

            internal bool InMainThread
            {
                get { return _threadId == Thread.CurrentThread.ManagedThreadId; }
            }

            internal int? Delay { get; set; }
            internal string DataForSpan { get; set; }
            internal bool IsDisposed { get; set; }
            internal ITextView TextView { get; set; }

            internal TestableAsyncTaggerSource(ITextBuffer textBuffer)
            {
                _textBuffer = textBuffer;
                _threadId = Thread.CurrentThread.ManagedThreadId;
                DataForSpan = "";
            }

            internal void SetPromptTags(params SnapshotSpan[] tagSpans)
            {
                _promptTags = tagSpans.Select(CreateTagSpan).ToList();
            }

            internal void SetBackgroundTags(params SnapshotSpan[] tagSpans)
            {
                _backgroundTags = tagSpans.Select(CreateTagSpan).ToList();
            }

            internal void SetBackgroundCallback(Action<String, SnapshotSpan> action)
            {
                _backgroundCallback = action;
            }

            internal void RaiseChanged(SnapshotSpan? span)
            {
                if (_changed != null)
                {
                    _changed(this, EventArgs.Empty);
                }
            }

            #region IAsyncTaggerSource

            int? IAsyncTaggerSource<string, TextMarkerTag>.Delay
            {
                get { return Delay; }
            }

            ITextView IAsyncTaggerSource<string, TextMarkerTag>.TextViewOptional
            {
                get { return TextView; }
            }

            string IAsyncTaggerSource<string, TextMarkerTag>.GetDataForSpan(SnapshotSpan value)
            {
                Assert.IsTrue(InMainThread);
                return DataForSpan;
            }

            ReadOnlyCollection<ITagSpan<TextMarkerTag>> IAsyncTaggerSource<string, TextMarkerTag>.GetTagsInBackground(string value, SnapshotSpan span, CancellationToken cancellationToken)
            {
                Assert.IsFalse(InMainThread);

                if (_backgroundCallback != null)
                {
                    _backgroundCallback(value, span);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (_backgroundTags != null)
                {
                    return _backgroundTags.ToReadOnlyCollection();
                }

                throw new Exception("Couldn't get background tags");
            }

            bool IAsyncTaggerSource<string, TextMarkerTag>.TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TextMarkerTag>> tagList)
            {
                Assert.IsTrue(InMainThread);
                if (_promptTags != null)
                {
                    tagList = _promptTags;
                    return true;
                }

                tagList = null;
                return false;
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
                    Assert.IsTrue(InMainThread);
                    return _textBuffer.CurrentSnapshot;
                }
            }

            void IDisposable.Dispose()
            {
                Assert.IsTrue(InMainThread);
                IsDisposed = true;
            }

            #endregion
        }

        #endregion

        private ITextBuffer _textBuffer;
        private TestableSynchronizationContext _synchronizationContext;
        private TestableAsyncTaggerSource _asyncTaggerSource;
        private AsyncTagger<string, TextMarkerTag> _asyncTagger;
        private ITagger<TextMarkerTag> _asyncTaggerInterface;

        private NormalizedSnapshotSpanCollection EntireBufferSpan
        {
            get { return new NormalizedSnapshotSpanCollection(_textBuffer.CurrentSnapshot.GetExtent()); }
        }

        [TearDown]
        public void TearDown()
        {
            if (_synchronizationContext != null)
            {
                _synchronizationContext.Uninstall();
            }
        }

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);

            // Setup a sychronization context we can control
            _synchronizationContext = new TestableSynchronizationContext();
            _synchronizationContext.Install();

            _asyncTaggerSource = new TestableAsyncTaggerSource(_textBuffer);
            _asyncTagger = new AsyncTagger<string, TextMarkerTag>(_asyncTaggerSource);
            _asyncTaggerInterface = _asyncTagger;
        }

        private static ITagSpan<TextMarkerTag> CreateTagSpan(SnapshotSpan span)
        {
            return new TagSpan<TextMarkerTag>(span, new TextMarkerTag("my tag"));
        }

        private static ReadOnlyCollection<ITagSpan<TextMarkerTag>> CreateTagSpans(params SnapshotSpan[] tagSpans)
        {
            return tagSpans.Select(CreateTagSpan).ToReadOnlyCollection();
        }

        private AsyncTaggerType.BackgroundCacheData CreateBackgroundCacheData(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            return new AsyncTaggerType.BackgroundCacheData(
                source,
                tagSpans.Select(CreateTagSpan).ToReadOnlyCollection());
        }

        private AsyncTaggerType.TrackingCacheData CreateTrackingCacheData(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            var snapshot = source.Snapshot;
            var trackingSpan = snapshot.CreateTrackingSpan(source, SpanTrackingMode.EdgeInclusive);
            var tag = new TextMarkerTag("my tag");
            var all = tagSpans
                .Select(span => Tuple.Create(snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive), tag))
                .ToReadOnlyCollection();
            return new AsyncTaggerType.TrackingCacheData(
                trackingSpan,
                all);
        }

        private AsyncTaggerType.TagCache CreateTagCache(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            var backgroundCacheData = CreateBackgroundCacheData(source, tagSpans);
            return new AsyncTaggerType.TagCache(backgroundCacheData, null);
        }

        private List<ITagSpan<TextMarkerTag>> GetTagsFull(SnapshotSpan span)
        {
            bool unused;
            return GetTagsFull(span, out unused);
        }

        private List<ITagSpan<TextMarkerTag>> GetTagsFull(SnapshotSpan span, out bool wasAsync)
        {
            Assert.IsFalse(_asyncTagger.AsyncBackgroundRequestData.HasValue);
            wasAsync = false;
            var tags = _asyncTagger.GetTags(new NormalizedSnapshotSpanCollection(span)).ToList();
            if (_asyncTagger.AsyncBackgroundRequestData.HasValue)
            {
                _asyncTagger.AsyncBackgroundRequestData.Value.Task.Wait();
                _synchronizationContext.RunAll();
                tags = _asyncTagger.GetTags(new NormalizedSnapshotSpanCollection(span)).ToList();
                wasAsync = true;
            }

            return tags;
        }

        private AsyncTaggerType.AsyncBackgroundRequest CreateAsyncBackgroundRequest(
            SnapshotSpan span,
            CancellationTokenSource cancellationTokenSource,
            Task task = null)
        {
            task = task ?? new Task(() => { });
            return new AsyncTaggerType.AsyncBackgroundRequest(
                SnapshotLineRange.CreateForSpan(span),
                cancellationTokenSource,
                new SingleItemQueue<SnapshotLineRange>(),
                task);
        }

        private void SetTagCache(AsyncTaggerType.TrackingCacheData trackingCacheData)
        {
            _asyncTagger.TagCacheData = new AsyncTaggerType.TagCache(null, trackingCacheData);
        }

        /// <summary>
        /// If the tracking data is empty and we have now tags then it didn't chaneg
        /// </summary>
        [Test]
        public void DidTagsChange_EmptyTrackingData()
        {
            Create("cat", "dog", "bear");
            var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
            SetTagCache(CreateTrackingCacheData(span));
            Assert.IsTrue(_asyncTagger.DidTagsChange(span, CreateTagSpans(_textBuffer.GetSpan(0, 1))));
        }

        /// <summary>
        /// If the tracking data is empty and there are no new tags for the same SnapshotSpan then
        /// nothing changed
        /// </summary>
        [Test]
        public void DidTagsChange_EmptyTrackingData_NoNewTags()
        {
            Create("cat", "dog", "bear");
            var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
            SetTagCache(CreateTrackingCacheData(span));
            Assert.IsFalse(_asyncTagger.DidTagsChange(span, CreateTagSpans()));
        }

        /// <summary>
        /// They didn't change if we can map forward to the same values
        /// </summary>
        [Test]
        public void DidTagsChange_MapsToSame()
        {
            Create("cat", "dog", "bear", "tree");
            var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
            SetTagCache(CreateTrackingCacheData(span, _textBuffer.GetSpan(1, 3)));
            _textBuffer.Replace(_textBuffer.GetLine(2).Extent, "fish");
            Assert.IsFalse(_asyncTagger.DidTagsChange(_textBuffer.GetLine(0).ExtentIncludingLineBreak, CreateTagSpans(_textBuffer.GetSpan(1, 3))));
        }

        /// <summary>
        /// They changed if the edit moved them to different places
        /// </summary>
        [Test]
        public void DidTagsChange_MapsToDifferent()
        {
            Create("cat", "dog", "bear", "tree");
            var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
            SetTagCache(CreateTrackingCacheData(span, _textBuffer.GetSpan(1, 3)));
            _textBuffer.Insert(0, "big ");
            Assert.IsTrue(_asyncTagger.DidTagsChange(span, CreateTagSpans(_textBuffer.GetSpan(1, 4))));
        }

        /// <summary>
        /// They changed if they are simply different
        /// </summary>
        [Test]
        public void DidTagsChange_Different()
        {
            Create("cat", "dog", "bear", "tree");
            var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
            SetTagCache(CreateTrackingCacheData(span, _textBuffer.GetSpan(1, 3)));
            Assert.IsTrue(_asyncTagger.DidTagsChange(span, CreateTagSpans(
                _textBuffer.GetSpan(1, 3), 
                _textBuffer.GetSpan(5, 7))));
        }

        /// <summary>
        /// First choice should be to go through the prompt code.  This shouldn't create
        /// any cache
        /// </summary>
        [Test]
        public void GetTags_UsePrompt()
        {
            Create("hello world");
            _asyncTaggerSource.SetPromptTags(_textBuffer.GetSpan(0, 1));
            var tags = _asyncTagger.GetTags(EntireBufferSpan).ToList();
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual(_textBuffer.GetSpan(0, 1), tags[0].Span);
            Assert.IsTrue(_synchronizationContext.IsEmpty);
            Assert.IsTrue(_asyncTagger.TagCacheData.IsEmpty);
        }

        /// <summary>
        /// If the source can't provide prompt data then the tagger needs to go through
        /// the cache next
        /// </summary>
        [Test]
        public void GetTags_UseCache()
        {
            Create("hello world");
            _asyncTagger.TagCacheData = CreateTagCache(
                _textBuffer.GetExtent(),
                _textBuffer.GetSpan(0, 1));
            var tags = _asyncTagger.GetTags(EntireBufferSpan).ToList();
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual(_textBuffer.GetSpan(0, 1), tags[0].Span);
        }

        /// <summary>
        /// When there are no prompt sources available for tags we should schedule this
        /// for the background thread
        /// </summary>
        [Test]
        public void GetTags_UseBackground()
        {
            Create("hello world");
            var tags = _asyncTagger.GetTags(EntireBufferSpan).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequestData.HasValue);
        }

        /// <summary>
        /// Full test going through the background
        /// </summary>
        [Test]
        public void GetTags_UseBackgroundUpdateCache()
        {
            Create("cat", "dog", "bear");
            var span = _textBuffer.GetSpan(1, 2);
            _asyncTaggerSource.SetBackgroundTags(span);
            bool wasAsync;
            var tags = GetTagsFull(_textBuffer.GetExtent(), out wasAsync);
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual(span, tags[0].Span);
            Assert.IsTrue(wasAsync);
        }

        /// <summary>
        /// Make sure that we can get the tags when there is an explicit delay from
        /// the source
        /// </summary>
        [Test]
        public void GetTags_Delay()
        {
            Create("cat", "dog", "bear");
            var span = _textBuffer.GetSpan(1, 2);
            _asyncTaggerSource.SetBackgroundTags(span);
            var tags = GetTagsFull(_textBuffer.GetExtent());
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual(span, tags[0].Span);
        }

        /// <summary>
        /// The completion of the background operation should cause the TagsChanged
        /// event to be run
        /// </summary>
        [Test]
        public void GetTags_BackgroundShouldRaiseTagsChanged()
        {
            Create("cat", "dog", "bear");
            _asyncTaggerSource.SetBackgroundTags(
                _textBuffer.GetSpan(1, 2),
                _textBuffer.GetSpan(3, 4));

            var didRun = false;
            _asyncTaggerInterface.TagsChanged += delegate { didRun = true; };
            var tags = GetTagsFull(_textBuffer.GetExtent());
            Assert.IsTrue(didRun);
            Assert.AreEqual(2, tags.Count);
        }

        /// <summary>
        /// If there is a better background request in progress don't replace that 
        /// one when a call to GetTags occurs.  Better means having an encompasing
        /// span for the new request
        /// </summary>
        [Test]
        public void GetTags_DontReplaceBetterRequest()
        {
            Create("cat", "dog", "bear");

            var cancellationTokenSource = new CancellationTokenSource();
            _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequest(
                _textBuffer.GetExtent(),
                cancellationTokenSource,
                new Task(() => { }));

            var tags = _asyncTagger.GetTags(_textBuffer.GetLine(0).Extent).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.AreSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
        }

        /// <summary>
        /// If the existing requset is inferior to the new one then replace it
        /// </summary>
        [Test]
        public void GetTags_ReplaceWorseRequest()
        {
            Create("cat", "dog", "bear");

            var cancellationTokenSource = new CancellationTokenSource();
            _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequest(
                _textBuffer.GetLine(0).Extent,
                cancellationTokenSource,
                new Task(() => { }));

            var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.AreNotSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            Assert.AreEqual(_textBuffer.GetExtent(), _asyncTagger.AsyncBackgroundRequestData.Value.Span);
        }

        /// <summary>
        /// The same Span on different snapshots should cause a different request to be queued
        /// up
        /// </summary>
        [Test]
        public void GetTags_ReplaceWhenSnapshotChanges()
        {
            Create("cat", "dog", "bear");

            var cancellationTokenSource = new CancellationTokenSource();
            _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequest(
                _textBuffer.GetExtent(),
                cancellationTokenSource,
                new Task(() => { }));

            _textBuffer.Replace(new Span(0, 3), "bat");
            var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.AreNotSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequestData.Value.CancellationTokenSource);
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            Assert.AreEqual(_textBuffer.GetExtent(), _asyncTagger.AsyncBackgroundRequestData.Value.Span);
        }

        /// <summary>
        /// If the background request throws (for any reason including cancellation) then it should
        /// be handled and treated just like an empty return
        /// </summary>
        [Test]
        public void GetTags_BackgroundThrows()
        {
            Create("cat", "dog", "bat");

            var didRun = false;
            _asyncTaggerSource.SetBackgroundCallback(delegate 
            {
                didRun = true;
                throw new Exception("");
            });

            var tags = GetTagsFull(_textBuffer.GetExtent());
            Assert.AreEqual(0, tags.Count);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Even if the cache doesn't completely match the information in the cache we should at
        /// least the partial information we have and schedule the rest
        /// </summary>
        [Test]
        public void GetTags_PartialMatchInCache()
        {
            Create("cat", "dog", "bat");
            _asyncTagger.TagCacheData = CreateTagCache(
                _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                _textBuffer.GetSpan(0, 1));
            var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak).ToList();
            Assert.AreEqual(1, tags.Count);
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequestData.HasValue);
        }

        /// <summary>
        /// If there is a forward edit we should still return cache data as best as possible promptly
        /// and schedule a background task for the correct data
        /// </summary>
        [Test]
        public void GetTags_ForwardEdit()
        {
            Create("cat", "dog", "bat");
            _asyncTagger.TagCacheData = CreateTagCache(
                _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                _textBuffer.GetSpan(0, 1));
            _textBuffer.Replace(new Span(0, 3), "cot");
            var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak).ToList();
            Assert.AreEqual(1, tags.Count);
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequestData.HasValue);
        }

        /// <summary>
        /// Make sure that a prompt call updates the request span
        /// </summary>
        [Test]
        public void GetTags_PromptUpdateRequestSpan()
        {
            Create("hello world", "cat chased the dog");
            var span = _textBuffer.GetSpan(0, 6);
            _asyncTaggerSource.SetPromptTags(_textBuffer.GetSpan(0, 1));
            _asyncTagger.GetTags(span);
            Assert.AreEqual(span, _asyncTagger.CachedRequestSpan.Value);
        }

        /// <summary>
        /// If we have tags which are mixed between background and tracking we need to pull 
        /// from both sources
        /// </summary>
        [Test]
        public void GetTags_BackgroundAndTracking()
        {
            Create("cat", "dog", "bear", "pig");
            var backgroundData = CreateBackgroundCacheData(_textBuffer.GetLine(0).Extent, _textBuffer.GetLineSpan(0, 1));
            var trackingData = CreateTrackingCacheData(_textBuffer.GetLine(1).Extent, _textBuffer.GetLineSpan(1, 1));
            _asyncTagger.TagCacheData = new AsyncTaggerType.TagCache(backgroundData, trackingData);
            var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak);
            Assert.AreEqual(2, tags.Count());
        }

        /// <summary>
        /// Shrink the ITextSnapshot to 0 to ensure the GetTags call doesn't do math against the
        /// incorrect ITextSnapshot.  If we use a Span against the wrong ITextSnapshot it should 
        /// cause an exception
        /// </summary>
        [Test]
        public void GetTags_ToEmptyBuffer()
        {
            Create("cat", "dog", "bear", "pig");
            _asyncTagger.TagCacheData = CreateTagCache(
                _textBuffer.GetExtent(),
                _textBuffer.GetSpan(0, 3),
                _textBuffer.GetSpan(5, 3));
            _textBuffer.Delete(new Span(0, _textBuffer.CurrentSnapshot.Length));
            var list = GetTagsFull(_textBuffer.GetExtent());
            Assert.IsNotNull(list);
        }

        /// <summary>
        /// If the IAsyncTaggerSource raises a TagsChanged event then the tagger must clear 
        /// out it's cache.  Anything it's stored up until this point is now invalid
        /// </summary>
        [Test]
        public void OnChanged_ClearCache()
        {
            Create("hello world");
            _asyncTagger.TagCacheData = CreateTagCache(
                _textBuffer.GetExtent(),
                _textBuffer.GetSpan(0, 1));
            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsTrue(_asyncTagger.TagCacheData.IsEmpty);
        }

        /// <summary>
        /// If the IAsyncTaggerSource raises a TagsChanged event then any existing tagger
        /// requests are invalid
        /// </summary>
        [Test]
        public void OnChanged_ClearBackgroundRequest()
        {
            Create("hello world");
            var cancellationTokenSource = new CancellationTokenSource();
            _asyncTagger.AsyncBackgroundRequestData = CreateAsyncBackgroundRequest(
                _textBuffer.GetExtent(),
                cancellationTokenSource,
                new Task(() => { }));
            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsFalse(_asyncTagger.AsyncBackgroundRequestData.HasValue);
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
        }

        /// <summary>
        /// When the IAsyncTaggerSource raises it's event the tagger must as well
        /// </summary>
        [Test]
        public void OnChanged_RaiseEvent()
        {
            Create("hello world");
            _asyncTagger.CachedRequestSpan = _textBuffer.GetLine(0).Extent;
            var didRun = false;
            _asyncTaggerInterface.TagsChanged += delegate
            {
                didRun = true;
            };

            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// If we've not recieved a GetTags request then don't raise a TagsChanged event when
        /// we get a Changed event.  
        /// </summary>
        [Test]
        public void OnChanged_DontRaiseEventIfNoRequests()
        {
            Create("hello world");
            var didRun = false;
            _asyncTaggerInterface.TagsChanged += delegate
            {
                didRun = true;
            };

            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsFalse(didRun);
        }

        /// <summary>
        /// When the initial background request completes make sure that a TagsChanged is raised for the
        /// expected SnapshotSpan
        /// </summary>
        [Test]
        public void TagsChanged_BackgroundComplete()
        {
            Create("cat", "dog", "bear");
            SnapshotSpan? tagsChanged = null;
            var requestSpan = _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak;
            _asyncTaggerInterface.TagsChanged += (sender, e) => { tagsChanged = e.Span; };
            _asyncTaggerSource.SetBackgroundTags(_textBuffer.GetSpan(0, 1));
            GetTagsFull(requestSpan);
            Assert.IsTrue(tagsChanged.HasValue);
            Assert.AreEqual(requestSpan, tagsChanged);
        }

        /// <summary>
        /// During an edit we map the previous good data via ITrackingSpan instances while the next background
        /// request completes.  So by the time a background request completes we've already provided tags for 
        /// the span we are looking at.  If the simple ITrackingSpan mappings were correct then don't raise
        /// TagsChanged again for the same SnapshotSpan.  Doing so causes needless work and results in items
        /// like screen flickering
        /// </summary>
        [Test]
        public void TagsChanged_TrackingPredictedBackgroundResult()
        {
            Create("cat", "dog", "bear", "tree");

            SnapshotSpan? tagsChanged = null;
            _asyncTaggerInterface.TagsChanged += (sender, e) => { tagsChanged = e.Span; };

            // Setup the previous background cache
            _asyncTagger.TagCacheData = new AsyncTaggerType.TagCache(null, CreateTrackingCacheData(
                _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                _textBuffer.GetSpan(0, 1)));

            // Make an edit so that we are truly mapping forward then request tags for the same
            // area
            _textBuffer.Replace(_textBuffer.GetLine(2).Extent.Span, "fish");
            _asyncTaggerSource.SetBackgroundTags(_textBuffer.GetSpan(0, 1));
            GetTagsFull(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak);

            Assert.IsFalse(tagsChanged.HasValue);
        }

        /// <summary>
        /// An edit followed by different tags should raise the TagsChanged event
        /// </summary>
        [Test]
        public void TagsChanged_TrackingDidNotPredictBackgroundResult()
        {
            Create("cat", "dog", "bear", "tree");

            SnapshotSpan? tagsChanged = null;
            _asyncTaggerInterface.TagsChanged += (sender, e) => { tagsChanged = e.Span; };

            // Setup the previous background cache
            _asyncTagger.TagCacheData = new AsyncTaggerType.TagCache(null, CreateTrackingCacheData(
                _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                _textBuffer.GetSpan(0, 1)));

            // Make an edit so that we are truly mapping forward then request tags for the same
            // area
            _textBuffer.Replace(_textBuffer.GetLine(2).Extent.Span, "fish");
            _asyncTaggerSource.SetBackgroundTags(_textBuffer.GetSpan(1, 1));
            var requestSpan = _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak;
            GetTagsFull(requestSpan);

            Assert.IsTrue(tagsChanged.HasValue);
            Assert.AreEqual(requestSpan, tagsChanged.Value);
        }
    }
}
