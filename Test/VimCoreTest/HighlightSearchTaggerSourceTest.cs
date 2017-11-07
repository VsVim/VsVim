using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;
using Vim;
using Vim.Extensions;

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
            var tagList = _asyncTaggerSource.TryGetTagsPrompt(span);
            Assert.True(tagList.IsSome());
            return tagList.Value.ToList();
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
            [WpfFact]
            public void PatternEmpty()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("");
                var ret = GetTags(_textBuffer.GetExtent());
                Assert.Empty(ret);
            }

            /// <summary>
            /// Make sure the matches are returned
            /// </summary>
            [WpfFact]
            public void WithMatch()
            {
                Create("foo is the bar");
                _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
                var ret = GetTags(_textBuffer.GetExtent());
                Assert.Single(ret);
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
            }

            /// <summary>
            /// Don't return tags outside the requested span
            /// </summary>
            [WpfFact]
            public void OutSideSpan()
            {
                Create("foo is the bar");
                _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
                var ret = GetTags(new SnapshotSpan(_textBuffer.CurrentSnapshot, 4, 3));
                Assert.Empty(ret);
            }

            /// <summary>
            /// It's possible for the search service to return a match of 0 length.  This is perfectly legal 
            /// and should be treated as a match of length 1.  This is how gVim does it
            ///
            /// When they are grouped thuogh return a single overarching span to avoid overloading the 
            /// editor
            /// </summary>
            [WpfFact]
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
                _vimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
            }

            [WpfFact]
            public void Standard()
            {
                _globalSettings.HighlightSearch = true;
                _vimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            [WpfFact]
            public void DisplayPatternSuspended()
            {
                _vimData.SuspendDisplayPattern();
                Assert.False(_asyncTaggerSourceRaw.IsProvidingTags);
            }

            /// <summary
            /// Make sure that new instances respect the existing suppression of DisplayPattern
            /// 
            /// </summary>
            [WpfFact]
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
            [WpfFact]
            public void HighlightDisabled()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
                _globalSettings.HighlightSearch = false;
                var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
                Assert.Empty(ret);
            }

            /// <summary>
            /// We can promptly say nothing when display of tags is suspended
            /// </summary>
            [WpfFact]
            public void OneTimeDisabled()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
                _vimData.SuspendDisplayPattern();
                var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
                Assert.Empty(ret);
            }
        }

        public sealed class GetTagsPromtpTest : HighlightSearchTaggerSourceTest
        {
            /// <summary>
            /// If the ITextView is not considered visible then we shouldn't be returning any
            /// tags
            /// </summary>
            [WpfFact]
            public void NotVisible()
            {
                Create("dog cat");
                _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
                _asyncTaggerSourceRaw._isVisible = false;
                var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
                Assert.Empty(ret);
            }
        }

        public sealed class ChangedTest : HighlightSearchTaggerSourceTest
        {
            private bool _raised;

            public ChangedTest()
            {
                Create("");
                _vimData.LastSearchData = new SearchData("dog", SearchPath.Forward);
                _asyncTaggerSource.Changed += delegate { _raised = true; };
            }

            /// <summary>
            /// The SuspendDisplayPattern method should cause the Changed event to be raised
            /// and stop the display of tags
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void DisplayPatternChanged()
            {
                _vimData.LastSearchData = new SearchData("hello", SearchPath.Forward);
                Assert.True(_raised);
            }

            /// <summary>
            /// If the visibility of the ITextView changes it should cause a Changed event to be raised
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void ResetOneTimeDisabled()
            {
                _vimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                _globalSettings.HighlightSearch = false;
                _vimData.SuspendDisplayPattern();
                _globalSettings.HighlightSearch = true;
                Assert.True(_asyncTaggerSourceRaw.IsProvidingTags);
            }
        }
    }
}
