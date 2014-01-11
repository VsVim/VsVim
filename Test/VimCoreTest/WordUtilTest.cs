using System;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class WordUtilTest : VimTestBase
    {
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private IWordUtil _wordUtil;
        private WordUtil _wordUtilRaw;

        private void Create(params string[] lines)
        {
            Create(0, lines);
        }

        private void Create(int caretPosition, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _wordUtilRaw = new WordUtil();
            _wordUtil = _wordUtilRaw;
        }

        /// <summary>
        /// Break up the buffer into simple words
        /// </summary>
        [Fact]
        public void GetWords_Normal()
        {
            Create("dog ca$$t $b");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(0));
            Assert.Equal(
                new[] { "dog", "ca", "$$", "t", "$", "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// A blank line should be a word 
        /// </summary>
        [Fact]
        public void GetWords_BlankLine()
        {
            Create("dog cat", "", "bear");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(0));
            Assert.Equal(
                new[] { "dog", "cat", Environment.NewLine, "bear" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the middle of a word should return the span of the entire word
        /// </summary>
        [Fact]
        public void GetWords_FromMiddleOfWord()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(1));
            Assert.Equal(
                new[] { "dog", "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the end of a word should return the span of the entire word
        /// </summary>
        [Fact]
        public void GetWords_FromEndOfWord()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(2));
            Assert.Equal(
                new[] { "dog", "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the middle of a word backward
        /// </summary>
        [Fact]
        public void GetWords_BackwardFromMiddle()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetPoint(5));
            Assert.Equal(
                new[] { "cat", "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the start of a word backward should not include that particular word 
        /// </summary>
        [Fact]
        public void GetWords_BackwardFromStart()
        {
            Create("dog cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetPoint(4));
            Assert.Equal(
                new[] { "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure that blank lines are counted as words when
        /// </summary>
        [Fact]
        public void GetWords_BackwardBlankLine()
        {
            Create("dog", "", "cat");
            var ret = _wordUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetLine(2).Start.Add(1));
            Assert.Equal(
                new[] { "cat", Environment.NewLine, "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

    }
}
