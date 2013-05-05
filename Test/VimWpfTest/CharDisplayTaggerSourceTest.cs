using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UI.Wpf.Implementation.CharDisplay;
using Vim.UnitTest;
using Xunit;
using EditorUtils;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class CharDisplayTaggerSourceTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private CharDisplayTaggerSource _source;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _source = new CharDisplayTaggerSource(_textView);
        }

        public sealed class GetTagsTest : CharDisplayTaggerSourceTest
        {
            [Fact]
            public void NoTags()
            {
                Create("dog");
                var tags = _source.GetTags(_textBuffer.GetSpan(0, 3));
                Assert.Equal(0, tags.Count);
            }

            [Fact]
            public void SingleTag()
            {
                Create("d" + (char)29 + "g");
                var tags = _source.GetTags(_textBuffer.GetSpan(0, 3));
                Assert.Equal(1, tags.Count);
            }

            /// <summary>
            /// Even though the new line characters \n and \r technically register as control characters and 
            /// show up in gVim, we don't do that here.  The editor hard codes these to new line characters 
            /// and they should never be treated as anything but new lines 
            /// </summary>
            [Fact]
            public void IgnoreNewLines()
            {
                Create("dog", "cat");
                var tags = _source.GetTags(_textBuffer.GetExtent());
                Assert.Equal(0, tags.Count);
            }
        }

        /// <summary>
        /// Tests to make sure that the internal algorithm handles the moving of adornments in response to an 
        /// edit works properly
        /// </summary>
        public sealed class EditTest : CharDisplayTaggerSourceTest
        {
            [Fact]
            public void JustBefore()
            {
                Create("d" + (char)29 + "g");
                _source.GetTags(_textBuffer.GetExtent());
                _textBuffer.Insert(1, "o");
                var tags = _source.GetTags(_textBuffer.GetExtent());
                Assert.Equal(tags.Select(x => x.Span), new[] { _textBuffer.GetSpan(2, 1) });
            }

            [Fact]
            public void JustAfter()
            {
                Create("d" + (char)29 + "g");
                _source.GetTags(_textBuffer.GetExtent());
                _textBuffer.Insert(2, "o");
                var tags = _source.GetTags(_textBuffer.GetExtent());
                Assert.Equal(tags.Select(x => x.Span), new[] { _textBuffer.GetSpan(1, 1) });
            }

            [Fact]
            public void DeleteTag()
            {
                Create("d" + (char)29 + "g");
                _source.GetTags(_textBuffer.GetExtent());
                _textBuffer.Delete(new Span(1, 1));
                var tags = _source.GetTags(_textBuffer.GetExtent());
                Assert.Equal(0, tags.Count);
            }

            [Fact]
            public void InsertTag()
            {
                Create("d" + (char)29 + "g cat");
                _source.GetTags(_textBuffer.GetExtent());
                _textBuffer.Insert(3, "" + (char)29);
                var tags = _source.GetTags(_textBuffer.GetExtent());
                Assert.Equal(tags.Select(x => x.Span), new[] { _textBuffer.GetSpan(1, 1), _textBuffer.GetSpan(3, 1) });
            }
        }
    }
}
