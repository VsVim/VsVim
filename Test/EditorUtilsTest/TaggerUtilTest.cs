using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class TaggerUtilTest : EditorHostTest
    {
        public sealed class AdjustRequestSpanTest : TaggerUtilTest
        {
            private ITextBuffer _textBuffer;

            private void Create(params string[] lines)
            {
                _textBuffer = CreateTextBuffer(lines);
            }

            /// <summary>
            /// When there is a new request it becomes the entire request that we are looking at
            /// </summary>
            [Fact]
            public void NewRequest()
            {
                Create("cat dog");
                var span = _textBuffer.GetSpan(0, 3);
                Assert.Equal(span, TaggerUtil.AdjustRequestedSpan(null, span));
            }

            [Fact]
            public void BiggerAtEnd()
            {
                Create("cat dog");
                var span1 = _textBuffer.GetSpan(0, 1);
                var span2 = _textBuffer.GetSpan(3, 1);
                var overarching = span1.CreateOverarching(span2);
                Assert.Equal(overarching, TaggerUtil.AdjustRequestedSpan(span1, span2));
            }

            [Fact]
            public void BiggerAtStart()
            {
                Create("cat dog");
                var span1 = _textBuffer.GetSpan(3, 1);
                var span2 = _textBuffer.GetSpan(0, 1);
                var overarching = span1.CreateOverarching(span2);
                Assert.Equal(overarching, TaggerUtil.AdjustRequestedSpan(span1, span2));
            }

            /// <summary>
            /// For a forward edit we need to move the old span forward and then get the overarching
            /// value
            /// </summary>
            [Fact]
            public void ForwardEditSpan()
            {
                Create("cat dog");
                var span1 = _textBuffer.GetSpan(0, 1);
                _textBuffer.Insert(4, "fish ");
                var span2 = _textBuffer.GetSpan(4, 4);
                var overarching = _textBuffer.GetSpan(0, 8);
                Assert.Equal(overarching, TaggerUtil.AdjustRequestedSpan(span1, span2));
            }

            /// <summary>
            /// It is possible to request spans in the past (previous edit).  This can happen when 
            /// dealing with projection buffers (most specifically with web applications).  In this
            /// case just default back to the entire buffer.  This value isn't used for any of our caching
            /// but instead is just the value we provide to the editor when we raise a changed event
            /// </summary>
            [Fact]
            public void BackwardEditSpan()
            {
                Create("cat dog");
                var oldSpan = _textBuffer.GetSpan(0, 1);
                var oldAll = _textBuffer.CurrentSnapshot.GetExtent();
                _textBuffer.Insert(4, "fish ");
                var newSpan = _textBuffer.GetSpan(4, 4);
                Assert.Equal(oldAll, TaggerUtil.AdjustRequestedSpan(newSpan, oldSpan));
            }
        }
    }
}
