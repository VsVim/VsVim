using System.Collections.Generic;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class IncrementalSearchTaggerSourceTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private IIncrementalSearch _search;
        private IVimGlobalSettings _globalSettings;
        private IncrementalSearchTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<TextMarkerTag> _taggerSource;

        public void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _globalSettings = _vimBuffer.LocalSettings.GlobalSettings;
            _globalSettings.IncrementalSearch = true;
            _globalSettings.WrapScan = true;
            _search = _vimBuffer.IncrementalSearch;
            _taggerSourceRaw = new IncrementalSearchTaggerSource(_vimBuffer);
            _taggerSource = _taggerSourceRaw;
        }

        private IEnumerable<ITagSpan<TextMarkerTag>> GetTags()
        {
            return _taggerSource.GetTags(_textView.TextSnapshot.GetExtent());
        }

        /// <summary>
        /// Need to raise tags changed when switching modes as we don't display any tags in 
        /// visual modes
        /// </summary>
        [Fact]
        public void Changed_RaiseOnSwitchMode()
        {
            Create();
            var didRaise = false;
            _taggerSource.Changed += delegate { didRaise = true; };
            _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
            Assert.True(didRaise);
        }

        /// <summary>
        /// After the search is completed we shouldn't be returning any results
        /// </summary>
        [Fact]
        public void GetTags_AfterSearchCompleted()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            Assert.Equal(0, GetTags().Count());
        }

        /// <summary>
        /// Get tags should return the current match while searching
        /// </summary>
        [Fact]
        public void GetTags_InSearchWithMatch()
        {
            Create("dog cat bar");
            _search.DoSearch("dog", enter: false);
            Assert.Equal("dog", GetTags().Single().Span.GetText());
        }

        /// <summary>
        /// Don't return any tags in Visual Mode.  We don't want to confuse these tags with the
        /// visual mode values.  
        /// </summary>
        [Fact]
        public void GetTags_NoneInVisualMode()
        {
            Create("dog cat bar");
            _vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
            _search.DoSearch("dog", enter: false);
            Assert.Equal(0, GetTags().Count());
        }

        /// <summary>
        /// Don't return any tags if we're currently disabled
        /// </summary>
        [Fact]
        public void GetTags_NoneIfDisabled()
        {
            Create("dog cat bar");
            _search.DoSearch("dog", enter: false);
            _globalSettings.IncrementalSearch = false;
            Assert.Equal(0, GetTags().Count());
        }
    }
}
