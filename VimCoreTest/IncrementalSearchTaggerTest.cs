using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class IncrementalSearchTaggerTest
    {
        private IVimBuffer _buffer;
        private ITextView _textView;
        private IIncrementalSearch _search;
        private IVimGlobalSettings _globalSettings;
        private IncrementalSearchTagger _taggerRaw;
        private ITagger<TextMarkerTag> _tagger;

        public void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
            _globalSettings = _buffer.LocalSettings.GlobalSettings;
            _globalSettings.IncrementalSearch = true;
            _globalSettings.WrapScan = true;
            _search = _buffer.IncrementalSearch;
            _taggerRaw = new IncrementalSearchTagger(_buffer);
            _tagger = _taggerRaw;
        }

        private IEnumerable<ITagSpan<TextMarkerTag>> GetTags()
        {
            var span = _buffer.TextSnapshot.GetExtent();
            return _tagger.GetTags(new NormalizedSnapshotSpanCollection(span));
        }

        /// <summary>
        /// Need to raise tags changed when switching modes as we don't display any tags in 
        /// visual modes
        /// </summary>
        [Test]
        public void SwitchModeShouldRaiseTagsChanged()
        {
            Create();
            var didRaise = false;
            _tagger.TagsChanged += delegate { didRaise = true; };
            _buffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
            Assert.IsTrue(didRaise);
        }

        /// <summary>
        /// After the search is completed we shouldn't be returning any results
        /// </summary>
        [Test]
        public void GetTags_AfterSearchCompleted()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            Assert.AreEqual(0, GetTags().Count());
        }

        /// <summary>
        /// Get tags should return the current match while searching
        /// </summary>
        [Test]
        public void GetTags_InSearchWithMatch()
        {
            Create("dog cat bar");
            _search.DoSearch("dog", enter: false);
            Assert.AreEqual("dog", GetTags().Single().Span.GetText());
        }

        /// <summary>
        /// Don't return any tags in Visual Mode.  We don't want to confuse these tags with the
        /// visual mode values.  
        /// </summary>
        [Test]
        public void GetTags_NoneInVisualMode()
        {
            Create("dog cat bar");
            _buffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
            _search.DoSearch("dog", enter: false);
            Assert.AreEqual(0, GetTags().Count());
        }

        /// <summary>
        /// Don't return any tags if we're currently disabled
        /// </summary>
        [Test]
        public void GetTags_NoneIfDisabled()
        {
            Create("dog cat bar");
            _search.DoSearch("dog", enter: false);
            _globalSettings.IncrementalSearch = false;
            Assert.AreEqual(0, GetTags().Count());
        }
    }
}
