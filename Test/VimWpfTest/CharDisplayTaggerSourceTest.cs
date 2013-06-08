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
using Microsoft.VisualStudio.Text.Classification;
using Moq;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class CharDisplayTaggerSourceTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private CharDisplayTaggerSource _source;
        private IBasicTaggerSource<IntraTextAdornmentTag> _basicTaggerSource;

        protected virtual void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _source = new CharDisplayTaggerSource(_textView, new Mock<IEditorFormatMap>(MockBehavior.Loose).Object, Vim.GlobalSettings);
            _basicTaggerSource = _source;
        }

        public sealed class GetTagsTest : CharDisplayTaggerSourceTest
        {
            private void AssertCache()
            {
                for (int i = 0; i + 1 < _source.AdornmentCache.Count; i++)
                {
                    Assert.True(_source.AdornmentCache[i].Position < _source.AdornmentCache[i + 1].Position);
                }
            }

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

            /// <summary>
            /// Ask for the tags in descending order.  This will cause the cache to be populated backwards 
            /// </summary>
            [Fact]
            public void LineOrderDescending()
            {
                Create(Enumerable.Repeat("d" + (char)29 + "g", 100).ToArray());
                var snapshot = _textBuffer.CurrentSnapshot;
                for (int i = snapshot.LineCount - 1; i >= 0; i--)
                {
                    var line = snapshot.GetLine(i);
                    var tags = _source.GetTags(line.Extent);
                    Assert.Equal(tags.Select(x => x.Span), new[] { new SnapshotSpan(line.Start.Add(1), 1) });
                    AssertCache();
                }
            }

            /// <summary>
            /// Ask for the tags in ascending order.  This will cause the cache to be populated in order
            /// </summary>
            [Fact]
            public void LineOrderAscending()
            {
                Create(Enumerable.Repeat("d" + (char)29 + "g", 100).ToArray());
                var snapshot = _textBuffer.CurrentSnapshot;
                for (int i = 0; i < snapshot.LineCount; i++)
                {
                    var line = snapshot.GetLine(i);
                    var tags = _source.GetTags(line.Extent);
                    Assert.Equal(tags.Select(x => x.Span), new[] { new SnapshotSpan(line.Start.Add(1), 1) });
                    AssertCache();
                }
            }

            /// <summary>
            /// Ask for the tags in alternating order.  This will cause the cache to be populated in alternating
            /// order
            /// </summary>
            [Fact]
            public void LineOrderAlternating()
            {
                Create(Enumerable.Repeat("d" + (char)29 + "g", 100).ToArray());
                var snapshot = _textBuffer.CurrentSnapshot;
                for (int i = 1; i <= snapshot.LineCount; i++)
                {
                    int index;
                    if (i % 2 == 0)
                    {
                        index = snapshot.LineCount - (i / 2);
                    }
                    else
                    {
                        index = (i - 1) / 2;
                    }

                    var line = snapshot.GetLine(index);
                    var tags = _source.GetTags(line.Extent);
                    Assert.Equal(tags.Select(x => x.Span), new[] { new SnapshotSpan(line.Start.Add(1), 1) });
                    AssertCache();
                }

                Assert.Equal(100, _source.AdornmentCache.Count);
            }

            /// <summary>
            /// When asked for tags on an earlier snapshot we should simply ignore the request.  For non-adornment tags
            /// it's important to provide this information.  But for adornment taggers it isn't worth it.  
            /// </summary>
            [Fact]
            public void EarlierSnapshot()
            {
                Create("" + (char)29);
                var snapshot = _textBuffer.CurrentSnapshot;
                _textBuffer.Insert(0, "hello world");
                var tags = _source.GetTags(new SnapshotSpan(snapshot, 0, 1));
                Assert.Equal(0, tags.Count);
            }

            [Fact]
            public void SingleTagDisplayDisabled()
            {
                Create("d" + (char)29 + "g");
                Vim.GlobalSettings.ControlChars = false;
                var tags = _source.GetTags(_textBuffer.GetSpan(0, 3));
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

        public sealed class ChangedTest : CharDisplayTaggerSourceTest
        {
            private int _changedCount;

            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _basicTaggerSource.Changed += (x, y) => { _changedCount++; };
            }

            [Fact]
            public void OnControlCharsChanged()
            {
                Create("hello world");
                Vim.GlobalSettings.ControlChars = false;
                Assert.Equal(1, _changedCount);
            }

            [Fact]
            public void OnControlCharsNotChanged()
            {
                Create("hello world");
                Vim.GlobalSettings.ControlChars = Vim.GlobalSettings.ControlChars;
                Assert.Equal(0, _changedCount);
            }
        }
    }
}
