using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;
using Vim;
using Vim.Extensions;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class ChannelTest : VimTestBase
    {
        private readonly Channel _channel;
        private ITextBuffer _textBuffer;

        protected ChannelTest()
        {
            _channel = new Channel();
        }

        protected void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public sealed class WriteNormalTest : ChannelTest
        {
            /// <summary>
            /// From the point of view of the writing thread the current version should update 
            /// on every write
            /// </summary>
            [WpfFact]
            public void CurrentVersionIncrement()
            {
                Create("cat", "fish", "dog");

                for (var i = 0; i < 10; i++)
                {
                    _channel.WriteNormal(_textBuffer.GetLineRange(0));
                    Assert.Equal(i + 1, _channel.CurrentVersion);
                }
            }

            /// <summary>
            /// The underlying stack should represent the "current" state of the channel.  
            /// </summary>
            [WpfFact]
            public void CurrentStackChanges()
            {
                Create("cat", "fish", "dog");

                for (var i = 0; i < 10; i++)
                {
                    _channel.WriteNormal(_textBuffer.GetLineRange(0));
                    Assert.Equal(i + 1, _channel.CurrentStack.Length);
                }
            }

            [WpfFact]
            public void WriteThenRead()
            {
                Create("cat", "fish", "dog", "tree");

                for (var i = 0; i < 4; i++)
                {
                    var lineRange = _textBuffer.GetLineRange(i);
                    _channel.WriteNormal(lineRange);
                    var found = _channel.Read();
                    Assert.True(found.IsSome());
                    Assert.Equal(found.Value, lineRange);
                }
            }

            /// <summary>
            /// The write order to the channel for normal lines is first in last out 
            /// </summary>
            [WpfFact]
            public void FirstInLastOut()
            {
                Create("cat", "fish", "dog", "tree");

                for (var i = 0; i < 4; i++)
                {
                    _channel.WriteNormal(_textBuffer.GetLineRange(i));
                }

                var number = 3;
                var lineRange = _channel.Read();
                while (lineRange.IsSome())
                {
                    Assert.Equal(number, lineRange.Value.StartLineNumber);
                    lineRange = _channel.Read();
                    number--;
                }

                Assert.Equal(-1, number);
            }
        }

        public sealed class WriteVisibleLinesTest : ChannelTest
        {
            /// <summary>
            /// From the point of view of the writing thread the current version should update 
            /// on every write
            /// </summary>
            [WpfFact]
            public void CurrentVersionIncrement()
            {
                Create("cat", "fish", "dog");

                for (var i = 0; i < 10; i++)
                {
                    _channel.WriteVisibleLines(_textBuffer.GetLineRange(0));
                    Assert.Equal(i + 1, _channel.CurrentVersion);
                }
            }

            /// <summary>
            /// The underlying stack should represent the "current" state of normal writes.  It has nothing
            /// to do with visible line writes
            /// </summary>
            [WpfFact]
            public void CurrentStackDoesntChanges()
            {
                Create("cat", "fish", "dog");

                for (var i = 0; i < 10; i++)
                {
                    _channel.WriteVisibleLines(_textBuffer.GetLineRange(0));
                    Assert.Equal(0, _channel.CurrentStack.Length);
                }
            }

            [WpfFact]
            public void WriteThenRead()
            {
                Create("cat", "fish", "dog", "tree");

                for (var i = 0; i < 4; i++)
                {
                    var lineRange = _textBuffer.GetLineRange(i);
                    _channel.WriteVisibleLines(lineRange);
                    var found = _channel.Read();
                    Assert.True(found.IsSome());
                    Assert.Equal(found.Value, lineRange);
                }
            }
        }

        public sealed class ComplexTest : ChannelTest
        {
            [WpfFact]
            public void VisibleHasPriority_Normal()
            {
                Create("cat", "fish", "dog", "tree");
                _channel.WriteNormal(_textBuffer.GetLineRange(0));
                _channel.WriteVisibleLines(_textBuffer.GetLineRange(1));
                var lineRange = _channel.Read().Value;
                Assert.Equal(1, lineRange.StartLineNumber);
            }

            [WpfFact]
            public void VisibleHasPriority_Reverse()
            {
                Create("cat", "fish", "dog", "tree");
                _channel.WriteVisibleLines(_textBuffer.GetLineRange(1));
                _channel.WriteNormal(_textBuffer.GetLineRange(0));
                var lineRange = _channel.Read().Value;
                Assert.Equal(1, lineRange.StartLineNumber);
            }
        }
    }
}
