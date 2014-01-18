using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class HighlightSearchTaggerSourceTest : VimTestBase
    {
        internal HighlightSearchTaggerSource _asyncTaggerSourceRaw;
        internal IAsyncTaggerSource<HighlightSearchData, TextMarkerTag> _asyncTaggerSource;
        protected ITextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;
        protected IVimData _vimData;

        protected void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _globalSettings = Vim.GlobalSettings;
            _globalSettings.IgnoreCase = true;
            _globalSettings.HighlightSearch = true;
            _vimData = Vim.VimData;
            _asyncTaggerSourceRaw = new HighlightSearchTaggerSource(
                _textView,
                _globalSettings,
                _vimData,
                Vim.VimHost);
            _asyncTaggerSource = _asyncTaggerSourceRaw;
        }

        protected List<ITagSpan<TextMarkerTag>> TryGetTagsPrompt(SnapshotSpan span)
        {
            IEnumerable<ITagSpan<TextMarkerTag>> tagList;
            Assert.True(_asyncTaggerSource.TryGetTagsPrompt(span, out tagList));
            return tagList.ToList();
        }

        protected List<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
        {
            return _asyncTaggerSource.GetTagsInBackground(
                _asyncTaggerSourceRaw.GetDataForSnapshot(),
                span,
                CancellationToken.None).ToList();
        }

        public sealed class GetTagsTest : HighlightSearchTaggerSourceTest
        {
            /// <summary>
            /// Do nothing if the search pattern is empty
            /// </summary>
            [Fact]
            public void PatternEmpty()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("");
                var ret = GetTags(_textBuffer.GetExtent());
                Assert.Equal(0, ret.Count());
            }

            /// <summary>
            /// Make sure the matches are returned
            /// </summary>
            [Fact]
            public void WithMatch()
            {
                Create("foo is the bar");
                _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
                var ret = GetTags(_textBuffer.GetExtent());
                Assert.Equal(1, ret.Count());
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
            }

            /// <summary>
            /// Don't return tags outside the requested span
            /// </summary>
            [Fact]
            public void OutSideSpan()
            {
                Create("foo is the bar");
                _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
                var ret = GetTags(new SnapshotSpan(_textBuffer.CurrentSnapshot, 4, 3));
                Assert.Equal(0, ret.Count());
            }

            /// <summary>
            /// It's possible for the search service to return a match of 0 length.  This is perfectly legal 
            /// and should be treated as a match of length 1.  This is how gVim does it
            ///
            /// When they are grouped thuogh return a single overarching span to avoid overloading the 
            /// editor
            /// </summary>
            [Fact]
            public void ZeroLengthResults()
            {
                Create("cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData(@"\|i\>");
                var ret = GetTags(_textBuffer.GetExtent());
                Assert.Equal(
                    new[] { "cat" },
                    ret.Select(x => x.Span.GetText()).ToList());
            }
        }

        public sealed class IsProvidingTagsTest : HighlightSearchTaggerSourceTest
        {
            public IsProvidingTagsTest()
            {
                Create("");
                _globalSettings.HighlightSearch = true;
                _vimData.LastSearchData = new SearchData("cat", Path.Forward);
            }

            [Fact]
            public void Standard()
            {
                _globalSettings.HighlightSearch = true;
                _vimData.LastSearchData = new SearchData("cat", Path.Forward);
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            [Fact]
            public void DisplayPatternSuspended()
            {
                _vimData.SuspendDisplayPattern();
                Assert.False(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            /// <summary
            /// Make sure that new instances respect the existing suppression of DisplayPattern
            /// 
            /// </summary>
            [Fact]
            public void Issue1164()
            {
                _vimData.SuspendDisplayPattern();
                var vimBuffer = CreateVimBuffer();
                var source = new HighlightSearchTaggerSource(vimBuffer.TextView, Vim.GlobalSettings, VimData, VimHost);
                Assert.False(source.IsProvidingTags);
            }
        }

        public sealed class TryGetTagsPromptTest : HighlightSearchTaggerSourceTest
        {

            /// <summary>
            /// We can promptly say nothing when highlight is disabled
            /// </summary>
            [Fact]
            public void HighlightDisabled()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
                _globalSettings.HighlightSearch = false;
                var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
                Assert.Equal(0, ret.Count);
            }

            /// <summary>
            /// We can promptly say nothing when display of tags is suspended
            /// </summary>
            [Fact]
            public void OneTimeDisabled()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
                _vimData.SuspendDisplayPattern();
                var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
                Assert.Equal(0, ret.Count);
            }
        }

        public sealed class GetTagsPromtpTest : HighlightSearchTaggerSourceTest
        {
            /// <summary>
            /// If the ITextView is not considered visible then we shouldn't be returning any
            /// tags
            /// </summary>
            [Fact]
            public void NotVisible()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
                _asyncTaggerSourceRaw._isVisible = false;
                var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
                Assert.Equal(0, ret.Count);
            }
        }

        public sealed class ChangedTest : HighlightSearchTaggerSourceTest
        {
            private bool _raised;

            public ChangedTest()
            {
                Create("");
                _vimData.LastSearchData = new SearchData("dog", Path.Forward);
                _asyncTaggerSource.Changed += delegate { _raised = true; };
            }

            /// <summary>
            /// The SuspendDisplayPattern method should cause the Changed event to be raised
            /// and stop the display of tags
            /// </summary>
            [Fact]
            public void SuspendDisplayPattern()
            {
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
                _vimData.SuspendDisplayPattern();
                Assert.True(_raised);
                Assert.False(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            /// <summary>
            /// The search ran should cause a Changed event if we were previously disabled
            /// </summary>
            [Fact]
            public void ResumeDisplayPattern()
            {
                _vimData.SuspendDisplayPattern();
                Assert.False(_asyncTaggerSourceRaw.IsProvidingTags);
                _raised = false;
                _vimData.ResumeDisplayPattern();
                Assert.True(_raised);
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            /// <summary>
            /// When the display pattern changes it should cause the Changed event to be 
            /// raised
            /// </summary>
            [Fact]
            public void DisplayPatternChanged()
            {
                _vimData.LastSearchData = new SearchData("hello", Path.Forward);
                Assert.True(_raised);
            }

            /// <summary>
            /// If the visibility of the ITextView changes it should cause a Changed event to be raised
            /// </summary>
            [Fact]
            public void IsVisibleChanged()
            {
                Assert.True(_asyncTaggerSourceRaw._isVisible);
                VimHost.IsTextViewVisible = false;
                VimHost.RaiseIsVisibleChanged(_textView);
                Assert.False(_asyncTaggerSourceRaw._isVisible);
                Assert.True(_raised);
            }

            /// <summary>
            /// The setting of the 'hlsearch' option should raise the changed event
            /// </summary>
            [Fact]
            public void RaiseChanged()
            {
                _globalSettings.HighlightSearch = false;
                _raised = false;
                _globalSettings.HighlightSearch = true;
                Assert.True(_raised);
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            /// <summary>
            /// The setting of the 'hlsearch' option should reset the one time disabled flag
            /// </summary>
            [Fact]
            public void ResetOneTimeDisabled()
            {
                _vimData.LastSearchData = new SearchData("cat", Path.Forward);
                _globalSettings.HighlightSearch = false;
                _vimData.SuspendDisplayPattern();
                _globalSettings.HighlightSearch = true;
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
            }
        }
    }
}
