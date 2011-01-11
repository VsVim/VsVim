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
        private IncrementalSearchTagger _taggerRaw;
        private ITagger<TextMarkerTag> _tagger;

        public void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
            _search = _buffer.IncrementalSearch;
            _taggerRaw = new IncrementalSearchTagger(_buffer);
            _tagger = _taggerRaw;
        }

        private IEnumerable<ITagSpan<TextMarkerTag>> GetTags()
        {
            var span = _buffer.TextSnapshot.GetExtent();
            return _tagger.GetTags(new NormalizedSnapshotSpanCollection(span));
        }

        [Test]
        public void SwitchModeShouldRaiseTagsChanged()
        {
            Create();
            var didRaise = false;
            _tagger.TagsChanged += delegate { didRaise = true; };
            _buffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
            Assert.IsTrue(didRaise);
        }

        [Test]
        public void AfterSearchShouldReturnPreviousResult()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            Assert.AreEqual("dog", GetTags().Single().Span.GetText());
        }

        [Test]
        public void DuringSearchShouldReturnPreviousResult()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.AreEqual("dog", GetTags().Single().Span.GetText());
        }

        [Test]
        public void DontReturnAnyTagsInVisualMode()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            _buffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
            Assert.AreEqual(0, GetTags().Count());
        }

        [Test]
        public void Edit_NewLineAfterPreviousShouldNotRemoveTag()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            _textView.TextBuffer.Replace(new SnapshotSpan(_textView.GetLine(0).End, 0), "\n");
            Assert.AreEqual("dog", GetTags().Single().Span.GetText());
        }

        [Test]
        public void Edit_ChangeBeforeShouldNotRemoveTag()
        {
            Create("dog cat bar");
            _search.DoSearch("dog");
            _textView.TextBuffer.Replace(new SnapshotSpan(_textView.TextSnapshot, 0, 0), "foo");
            Assert.AreEqual("dog", GetTags().Single().Span.GetText());
        }
    }
}
