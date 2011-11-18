using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class AsyncTaggerTest : VimTestBase
    {
        #region TestableAsyncTaggerSource

        private sealed class TestableAsyncTaggerSource : IAsyncTaggerSource<string, TextMarkerTag>
        {
            private ITextBuffer _textBuffer;
            private int _threadId;
            private event FSharpHandler<Unit> _changed;
            private List<ITagSpan<TextMarkerTag>> _promptTags;
            private List<ITagSpan<TextMarkerTag>> _backgroundTags;
            private Action<string, SnapshotSpan> _backgroundCallback;

            internal bool InMainThread
            {
                get { return _threadId == Thread.CurrentThread.ManagedThreadId; }
            }

            internal string DataForSpan { get; set; }
            internal bool IsDisposed { get; set; }

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
                    _changed(this, null);
                }
            }

            #region IAsyncTaggerSource

            string IAsyncTaggerSource<string, TextMarkerTag>.GetDataForSpan(SnapshotSpan value)
            {
                Assert.IsTrue(InMainThread);
                return DataForSpan;
            }

            FSharpList<ITagSpan<TextMarkerTag>> IAsyncTaggerSource<string, TextMarkerTag>.GetTagsInBackground(string value, SnapshotSpan span, CancellationToken cancellationToken)
            {
                Assert.IsFalse(InMainThread);

                if (_backgroundCallback != null)
                {
                    _backgroundCallback(value, span);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (_backgroundTags != null)
                {
                    return _backgroundTags.ToFSharpList();
                }

                throw new Exception("Couldn't get background tags");
            }

            FSharpOption<FSharpList<ITagSpan<TextMarkerTag>>> IAsyncTaggerSource<string, TextMarkerTag>.GetTagsPrompt(SnapshotSpan value)
            {
                Assert.IsTrue(InMainThread);
                if (_promptTags != null)
                {
                    return FSharpOption.Create(_promptTags.ToFSharpList());
                }

                return FSharpOption<FSharpList<ITagSpan<TextMarkerTag>>>.None;
            }

            event FSharpHandler<Unit> IAsyncTaggerSource<string, TextMarkerTag>.Changed
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
            get { return new NormalizedSnapshotSpanCollection(SnapshotUtil.GetExtent(_textBuffer.CurrentSnapshot)); }
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

        private TagCache<TextMarkerTag> CreateTagCache(SnapshotSpan source, params SnapshotSpan[] tagSpans)
        {
            return new TagCache<TextMarkerTag>(
                source,
                TrackingSpanUtil.Create(source, SpanTrackingMode.EdgeInclusive),
                tagSpans.Select(CreateTagSpan).ToFSharpList(),
                null);
        }

        private List<ITagSpan<TextMarkerTag>> GetTagsFull(SnapshotSpan span)
        {
            bool unused;
            return GetTagsFull(span, out unused);
        }

        private List<ITagSpan<TextMarkerTag>> GetTagsFull(SnapshotSpan span, out bool wasAsync)
        {
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequest.IsNone());
            wasAsync = false;
            var tags = _asyncTagger.GetTags(new NormalizedSnapshotSpanCollection(span)).ToList();
            if (_asyncTagger.AsyncBackgroundRequest.IsSome())
            {
                Assert.AreEqual(0, tags.Count);
                _asyncTagger.AsyncBackgroundRequest.Value.Task.Wait();
                _synchronizationContext.RunAll();
                tags = _asyncTagger.GetTags(new NormalizedSnapshotSpanCollection(span)).ToList();
                wasAsync = true;
            }

            return tags;
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
            Assert.IsTrue(_asyncTagger.TagCache.IsNone());
        }

        /// <summary>
        /// If the source can't provide prompt data then the tagger needs to go through
        /// the cache next
        /// </summary>
        [Test]
        public void GetTags_UseCache()
        {
            Create("hello world");
            _asyncTagger.TagCache = FSharpOption.Create(CreateTagCache(
                _textBuffer.GetExtent(),
                _textBuffer.GetSpan(0, 1)));
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
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequest.IsSome());
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
            _asyncTagger.AsyncBackgroundRequest = FSharpOption.Create(new AsyncBackgroundRequest(
                _textBuffer.GetExtent(),
                cancellationTokenSource,
                new Task(() => { })));

            var tags = _asyncTagger.GetTags(_textBuffer.GetLine(0).Extent).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.AreSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequest.Value.CancellationTokenSource);
        }

        /// <summary>
        /// If the existing requset is inferior to the new one then replace it
        /// </summary>
        [Test]
        public void GetTags_ReplaceWorseRequest()
        {
            Create("cat", "dog", "bear");

            var cancellationTokenSource = new CancellationTokenSource();
            _asyncTagger.AsyncBackgroundRequest = FSharpOption.Create(new AsyncBackgroundRequest(
                _textBuffer.GetLine(0).Extent,
                cancellationTokenSource,
                new Task(() => { })));

            var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.AreNotSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequest.Value.CancellationTokenSource);
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            Assert.AreEqual(_textBuffer.GetExtent(), _asyncTagger.AsyncBackgroundRequest.Value.Span);
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
            _asyncTagger.AsyncBackgroundRequest = FSharpOption.Create(new AsyncBackgroundRequest(
                _textBuffer.GetExtent(),
                cancellationTokenSource,
                new Task(() => { })));

            _textBuffer.Replace(new Span(0, 3), "bat");
            var tags = _asyncTagger.GetTags(_textBuffer.GetExtent()).ToList();
            Assert.AreEqual(0, tags.Count);
            Assert.AreNotSame(cancellationTokenSource, _asyncTagger.AsyncBackgroundRequest.Value.CancellationTokenSource);
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            Assert.AreEqual(_textBuffer.GetExtent(), _asyncTagger.AsyncBackgroundRequest.Value.Span);
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
            _asyncTagger.TagCache = FSharpOption.Create(CreateTagCache(
                _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                _textBuffer.GetSpan(0, 1)));
            var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak).ToList();
            Assert.AreEqual(1, tags.Count);
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequest.IsSome());
        }

        /// <summary>
        /// If there is a forward edit we should still return cache data as best as possible promptly
        /// and schedule a background task for the correct data
        /// </summary>
        [Test]
        public void GetTags_ForwardEdit()
        {
            Create("cat", "dog", "bat");
            _asyncTagger.TagCache = FSharpOption.Create(CreateTagCache(
                _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                _textBuffer.GetSpan(0, 1)));
            _textBuffer.Replace(new Span(0, 3), "cot");
            var tags = _asyncTagger.GetTags(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak).ToList();
            Assert.AreEqual(1, tags.Count);
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequest.IsSome());
        }

        /// <summary>
        /// If the IAsyncTaggerSource raises a TagsChanged event then the tagger must clear 
        /// out it's cache.  Anything it's stored up until this point is now invalid
        /// </summary>
        [Test]
        public void OnTagsChanged_ClearCache()
        {
            Create("hello world");
            _asyncTagger.TagCache = FSharpOption.Create(CreateTagCache(
                _textBuffer.GetExtent(),
                _textBuffer.GetSpan(0, 1)));
            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsTrue(_asyncTagger.TagCache.IsNone());
        }

        /// <summary>
        /// If the IAsyncTaggerSource raises a TagsChanged event then any existing tagger
        /// requests are invalid
        /// </summary>
        [Test]
        public void OnTagsChanged_ClearBackgroundRequest()
        {
            Create("hello world");
            var cancellationTokenSource = new CancellationTokenSource();
            _asyncTagger.AsyncBackgroundRequest = FSharpOption.Create(new AsyncBackgroundRequest(
                _textBuffer.GetExtent(),
                cancellationTokenSource,
                new Task(() => { })));
            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsTrue(_asyncTagger.AsyncBackgroundRequest.IsNone());
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
        }

        /// <summary>
        /// When the IAsyncTaggerSource raises it's event the tagger must as well
        /// </summary>
        [Test]
        public void OnTagsChanged_RaiseEvent()
        {
            Create("hello world");
            var didRun = false;
            _asyncTaggerInterface.TagsChanged += delegate
            {
                didRun = true;
            };

            _asyncTaggerSource.RaiseChanged(null);
            Assert.IsTrue(didRun);
        }
    }
}
