using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using System.Linq;
using Vim;
using Vim.UnitTest;
using System;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class WordUtilTest
    {
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private IWordUtil _wordUtil;
        private WordUtil _wordUtilRaw;

        [TearDown]
        public void TearDown()
        {
            _textBuffer = null;
        }

        private void Create(params string[] lines)
        {
            Create(0, lines);
        }

        private void Create(int caretPosition, params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _wordUtilRaw = new WordUtil(_textBuffer, EditorUtil.FactoryService.TextStructureNavigatorSelectorService.GetTextStructureNavigator(_textBuffer));
            _wordUtil = _wordUtilRaw;
        }

        /// <summary>
        /// Break up the buffer into simple words
        /// </summary>
        [Test]
        public void GetWords_Normal()
        {
            Create("dog ca$$t $b");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "ca", "$$", "t", "$", "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// A blank line should be a word 
        /// </summary>
        [Test]
        public void GetWords_BlankLine()
        {
            Create("dog cat", "", "bear");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "cat", Environment.NewLine, "bear" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the middle of a word should return the span of the entire word
        /// </summary>
        [Test]
        public void GetWords_FromMiddleOfWord()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(1));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the end of a word should return the span of the entire word
        /// </summary>
        [Test]
        public void GetWords_FromEndOfWord()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(2));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the middle of a word backward
        /// </summary>
        [Test]
        public void GetWords_BackwardFromMiddle()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetPoint(5));
            CollectionAssert.AreEquivalent(
                new[] { "cat", "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the start of a word backward should not include that particular word 
        /// </summary>
        [Test]
        public void GetWords_BackwardFromStart()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetPoint(4));
            CollectionAssert.AreEquivalent(
                new[] { "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure that blank lines are counted as words when
        /// </summary>
        [Test]
        public void GetWords_BackwardBlankLine()
        {
            Create("dog", "", "cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetLine(2).Start.Add(1));
            CollectionAssert.AreEquivalent(
                new[] { "cat", Environment.NewLine, "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

    }
}
