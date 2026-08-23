using System.Collections.Generic;
using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class HighlightYankTaggerSourceTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private IVimGlobalSettings _globalSettings;
        private HighlightYankTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<TextMarkerTag> _taggerSource;

        internal void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _globalSettings = _vimBuffer.LocalSettings.GlobalSettings;
            _globalSettings.HighlightYank = true;
            _globalSettings.HighlightYankDuration = 60000; // long enough that fade timing doesn't affect these tests
            var commonOperations = CommonOperationsFactory.GetCommonOperations(_vimBuffer.VimBufferData);
            _taggerSourceRaw = new HighlightYankTaggerSource(_globalSettings, _textView.TextBuffer, commonOperations);
            _taggerSource = _taggerSourceRaw;
        }

        private IEnumerable<ITagSpan<TextMarkerTag>> GetTags()
        {
            return _taggerSource.GetTags(_textView.TextSnapshot.GetExtent());
        }

        /// <summary>
        /// Need to raise tags changed when a yank occurs so the highlight actually appears
        /// </summary>
        [WpfFact]
        public void Changed_RaiseOnYank()
        {
            Create("dog cat bar");
            var didRaise = false;
            _taggerSource.Changed += delegate { didRaise = true; };
            _vimBuffer.Process("yy");
            Assert.True(didRaise);
        }

        /// <summary>
        /// Get tags should return the just-yanked span
        /// </summary>
        [WpfFact]
        public void GetTags_AfterYank()
        {
            Create("dog cat bar");
            _vimBuffer.Process("yy");
            Assert.Equal("dog cat bar", GetTags().Single().Span.GetText());
        }

        /// <summary>
        /// A motion yank should only highlight the yanked text, not the whole line
        /// </summary>
        [WpfFact]
        public void GetTags_AfterMotionYank()
        {
            Create("dog cat bar");
            _vimBuffer.Process("yw");
            Assert.Equal("dog ", GetTags().Single().Span.GetText());
        }

        /// <summary>
        /// Before any yank has occurred there should be nothing to display
        /// </summary>
        [WpfFact]
        public void GetTags_NoneBeforeAnyYank()
        {
            Create("dog cat bar");
            Assert.Empty(GetTags());
        }

        /// <summary>
        /// Don't return any tags if we're currently disabled
        /// </summary>
        [WpfFact]
        public void GetTags_NoneIfDisabled()
        {
            Create("dog cat bar");
            _globalSettings.HighlightYank = false;
            _vimBuffer.Process("yy");
            Assert.Empty(GetTags());
        }

        /// <summary>
        /// A later yank should replace the highlight from an earlier one rather than showing both
        /// </summary>
        [WpfFact]
        public void GetTags_LaterYankReplacesEarlier()
        {
            Create("dog", "cat", "bar");
            _vimBuffer.Process("yy");
            _vimBuffer.Process("jyy");
            var expected = "cat" + _textView.TextSnapshot.GetLineFromLineNumber(1).GetLineBreakText();
            Assert.Equal(expected, GetTags().Single().Span.GetText());
        }
    }
}
