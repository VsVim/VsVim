using System;
using System.Collections.Generic;
using System.Linq;
using Vim.EditorHost;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.Modes.Insert;
using Vim.UnitTest.Mock;
using Xunit;
using Microsoft.VisualStudio.Text.Operations;

namespace Vim.UnitTest
{
    public sealed class WordCompletionUtilTest : VimTest
    {
        private readonly ITextBuffer _textBuffer;
        private readonly WordCompletionUtil _util;

        public WordCompletionUtilTest()
        {
            _util = new WordCompletionUtil(Vim, new WordUtil(_textBuffer, new LocalSettings(Vim.GlobalSettings)));
            _textBuffer = CreateTextBuffer();
        }

        [WpfFact]
        public void SingleResult()
        {
            _textBuffer.SetText("d", "dog", "test", "cat");
            var list = _util.GetWordCompletions(_textBuffer.GetLineSpan(lineNumber: 0, length: 1));
            Assert.Equal(
                new[] { "dog" },
                list);
        }

        [WpfFact]
        public void DoubleResult()
        {
            _textBuffer.SetText("d", "dog", "test", "cat", "deck");
            var list = _util.GetWordCompletions(_textBuffer.GetLineSpan(lineNumber: 0, length: 1));
            Assert.Equal(
                new[] { "dog", "deck" },
                list);
        }

        [WpfFact]
        public void Multifile()
        {
            var vimBuffer1 = CreateVimBuffer("d", "dog", "cat");
            var vimBuffer2 = CreateVimBuffer("deck", "cat");
            var span = vimBuffer1.TextBuffer.GetLineSpan(lineNumber: 0, length: 1);
            var list = _util.GetWordCompletions(span);
            Assert.Equal(
                new[] { "dog", "deck" },
                list);
        }

        /// <summary>
        /// Within a file the order is distance from the span.
        /// </summary>
        [WpfFact]
        public void SortOrder()
        {
            _textBuffer.SetText("dog", "d", "test", "cat", "deck");
            var list = _util.GetWordCompletions(_textBuffer.GetLineSpan(lineNumber: 1, length: 1));
            Assert.Equal(
                new[] { "deck", "dog" },
                list);
        }

        [WpfFact]
        public void SortOrderMany()
        {
            _textBuffer.SetText("dog", "dish", "d", "test", "cat", "deck", "dock");
            var list = _util.GetWordCompletions(_textBuffer.GetLineSpan(lineNumber: 2, length: 1));
            Assert.Equal(
                new[] { "deck", "dock", "dog", "dish" },
                list);
        }

        /// <summary>
        /// Other files come in top to bottom order.
        /// </summary>
        [WpfFact]
        public void SortOdrerMultifile()
        {
            var vimBuffer1 = CreateVimBuffer("deck", "d", "dog", "cat");
            var vimBuffer2 = CreateVimBuffer("dash", "cat", "dim");
            var span = vimBuffer1.TextBuffer.GetLineSpan(lineNumber: 1, length: 1);
            var list = _util.GetWordCompletions(span);
            Assert.Equal(
                new[] { "dog", "deck", "dash", "dim" },
                list);
        }

    }
}
